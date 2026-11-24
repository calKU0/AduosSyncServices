using Microsoft.Extensions.Options;
using ShopGold.Aduos.Gaska.ProductsService.Settings;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace ShopGold.Aduos.Gaska.ProductsService.Services
{
    public enum ShopGoldImportMode
    {
        /// <summary>
        /// Dodaje nowe produkty. Produkt, którego numer katalogowy już jest w sklepie, jest POMIJANY
        /// (bez aktualizacji) - takie wiersze trzeba wysłać jeszcze raz w trybie aktualizacji.
        /// </summary>
        Add,

        /// <summary>Tylko aktualizacja - produkty spoza sklepu są pomijane.</summary>
        Update
    }

    /// <param name="AddedNames">Nazwy produktów faktycznie dodanych (pole "nazwy" odpowiedzi importu).</param>
    public sealed record ShopGoldImportResult(int Processed, int Added, int Updated, IReadOnlyList<string> AddedNames);

    public interface IShopGoldPanelClient
    {
        /// <summary>Uruchamia import pliku CSV z katalogu /import sklepu i czeka na jego zakończenie.</summary>
        Task<ShopGoldImportResult> ImportCsvAsync(string fileName, ShopGoldImportMode mode, CancellationToken ct);

        /// <summary>Drzewo kategorii sklepu z panelu (Asortyment / Kategorie): id i pełna ścieżka nazw.</summary>
        Task<List<ShopCategory>> GetCategoriesAsync(CancellationToken ct);

        /// <summary>Czyści cache sklepu - to samo co przycisk na stronie głównej panelu (?resetCache).</summary>
        Task ResetCacheAsync(CancellationToken ct);
    }

    /// <summary>Kategoria sklepu: id i ścieżka nazw od kategorii głównej.</summary>
    public sealed record ShopCategory(int Id, IReadOnlyList<string> Path);

    /// <summary>
    /// Import CSV przez panel zarządzania ShopGold - te same zapytania co przeglądarka:
    /// obsluga_csv_import.php zwraca token importu i liczbę linii, a import.php?tok=... przetwarza
    /// plik paczkami (po 40 linii) i zwraca w JSON, ile linii już przetworzył.
    /// Sklep nie ma API do importu, a import wymaga zalogowanej sesji administratora.
    /// </summary>
    public sealed class ShopGoldPanelClient : IShopGoldPanelClient
    {
        private const int MaxStalledRequests = 3;

        private static readonly Regex TokenRegex = new(@"import\.php\?tok=([A-Za-z0-9]+)", RegexOptions.Compiled);
        private static readonly Regex LineCountRegex = new(@"var\s+ilosc_linii\s*=\s*(\d+)", RegexOptions.Compiled);
        private static readonly Regex FormRegex = new(@"<form\b[^>]*>.*?</form>", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
        private static readonly Regex InputRegex = new(@"<input\b[^>]*>", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex PasswordInputRegex = new(@"<input\b[^>]*type\s*=\s*[""']?password", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private readonly HttpClient _http;
        private readonly ShopGoldSettings _settings;
        private readonly ILogger<ShopGoldPanelClient> _logger;
        private bool _loggedIn;

        public ShopGoldPanelClient(HttpClient http, IOptions<ShopGoldSettings> settings, ILogger<ShopGoldPanelClient> logger)
        {
            _http = http;
            _settings = settings.Value;
            _logger = logger;
        }

        public Task<ShopGoldImportResult> ImportCsvAsync(string fileName, ShopGoldImportMode mode, CancellationToken ct) =>
            ImportCsvAsync(fileName, mode, retryAfterLogin: true, ct);

        private async Task<ShopGoldImportResult> ImportCsvAsync(string fileName, ShopGoldImportMode mode, bool retryAfterLogin, CancellationToken ct)
        {
            await EnsureLoggedInAsync(ct);

            var rodzajImportu = mode == ShopGoldImportMode.Add ? "dodawanie" : "aktualizacja";

            var startHtml = await PostFormAsync("import_danych/obsluga_csv_import.php", new Dictionary<string, string>
            {
                ["akcja"] = "import",
                ["plik"] = fileName,
                ["sep"] = ";",
                ["typ"] = "wszystkie",
                ["import_zdjec"] = _settings.ImportImages,
                ["rodzaj_import"] = rodzajImportu
            }, ct);

            if (IsLoginPage(startHtml))
            {
                if (!retryAfterLogin)
                    throw new InvalidOperationException("ShopGold session is not accepted by the import page.");

                // Sesja wygasła między zapytaniami - logujemy się ponownie i powtarzamy start (raz).
                _loggedIn = false;
                return await ImportCsvAsync(fileName, mode, retryAfterLogin: false, ct);
            }

            var tokenMatch = TokenRegex.Match(startHtml);
            var linesMatch = LineCountRegex.Match(startHtml);

            if (!tokenMatch.Success || !linesMatch.Success)
                throw new InvalidOperationException($"ShopGold did not start the import of {fileName} (no import token in the response). Check that the file is in the shop's /import directory.");

            var token = tokenMatch.Groups[1].Value;
            var lineCount = int.Parse(linesMatch.Groups[1].Value);

            if (lineCount <= 1)
                throw new InvalidOperationException($"ShopGold could not read {fileName} (empty file or wrong format).");

            int limit = 1, added = 0, updated = 0, processed = 0, stalled = 0;
            var addedNames = new List<string>();

            while (true)
            {
                ct.ThrowIfCancellationRequested();

                var json = await PostFormAsync($"import_danych/import.php?tok={token}", new Dictionary<string, string>
                {
                    ["format_importu"] = "csv",
                    ["struktura"] = "csv",
                    ["plik"] = fileName,
                    ["separator"] = ";",
                    ["rodzaj_import"] = rodzajImportu,
                    ["typ"] = "wszystkie",
                    ["import_zdjec"] = _settings.ImportImages,
                    ["ilosc_linii"] = lineCount.ToString(),
                    ["limit"] = limit.ToString()
                }, ct, ajax: true);

                var step = ParseStep(json, fileName);
                // Liczniki i nazwy dotyczą bieżącej paczki (40 linii), nie całego pliku.
                added += step.Added;
                updated += step.Updated;
                addedNames.AddRange(ParseNames(step.Names));

                if (step.Total > processed)
                {
                    processed = step.Total;
                    stalled = 0;
                }
                else if (++stalled >= MaxStalledRequests)
                {
                    throw new InvalidOperationException($"ShopGold import of {fileName} stopped progressing at line {processed} of {lineCount}.");
                }

                // Ta sama pętla co w panelu: kolejna paczka od linii zwróconej w "suma".
                if (lineCount - 1 > limit)
                    limit = step.Total;
                else
                    break;
            }

            return new ShopGoldImportResult(Math.Min(processed, lineCount - 1), added, updated, addedNames);
        }

        /// <summary>"&lt;li&gt;Nazwa 1&lt;/li&gt;&lt;li&gt;Nazwa 2&lt;/li&gt;" -> nazwy produktów.</summary>
        internal static IEnumerable<string> ParseNames(string? html) =>
            string.IsNullOrWhiteSpace(html)
                ? Enumerable.Empty<string>()
                : Regex.Matches(html, @"<li[^>]*>(.*?)</li>", RegexOptions.Singleline | RegexOptions.IgnoreCase)
                    .Select(m => WebUtility.HtmlDecode(Regex.Replace(m.Groups[1].Value, "<[^>]+>", string.Empty)).Trim())
                    .Where(n => n.Length > 0);

        private static ImportStep ParseStep(string json, string fileName)
        {
            try
            {
                return JsonSerializer.Deserialize<ImportStep>(json, new JsonSerializerOptions { NumberHandling = JsonNumberHandling.AllowReadingFromString })
                    ?? throw new InvalidOperationException("empty response");
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException)
            {
                var preview = json.Length > 300 ? json[..300] : json;
                throw new InvalidOperationException($"Unexpected ShopGold import response for {fileName}: {preview}", ex);
            }
        }

        private static readonly Regex CategoryRowRegex = new(@"<tr\b[^>]*\bid=""p_(\d+)""[^>]*>(.*?)</tr>", RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase);
        private static readonly Regex CategoryNameRegex = new(@"<span class=""(?:KatGl|Kateg)"">(.*?)</span>", RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase);

        public async Task<List<ShopCategory>> GetCategoriesAsync(CancellationToken ct)
        {
            await EnsureLoggedInAsync(ct);

            // Strona kategorii podaje w skrypcie parametry "tok" zapytań listy i podkategorii.
            var page = await GetAsync("kategorie/kategorie.php", ct);
            if (IsLoginPage(page))
            {
                _loggedIn = false;
                await EnsureLoggedInAsync(ct);
                page = await GetAsync("kategorie/kategorie.php", ct);
            }

            var listToken = Regex.Match(page, @"kategorie_lista\.php""\s*,\s*\{[^}]*tok:\s*'([^']*)'").Groups[1].Value;
            var subToken = Regex.Match(page, @"podkategorie\.php""\s*,\s*\{[^}]*tok:\s*'([^']*)'").Groups[1].Value;

            var result = new List<ShopCategory>();
            var seen = new HashSet<int>();
            var pending = new Queue<(int Id, List<string> Path, List<int> IdPath)>();

            var top = await GetAjaxAsync($"kategorie/kategorie_lista.php?id_poz=0&tok={Uri.EscapeDataString(listToken)}", ct);
            foreach (var (id, name, hasChildren) in ParseCategoryRows(top))
                Visit(id, name, hasChildren, new List<string>(), new List<int>());

            while (pending.Count > 0)
            {
                ct.ThrowIfCancellationRequested();
                var (parentId, parentPath, parentIds) = pending.Dequeue();

                // Jak w panelu: sciezka = id przodków zakończone "_" (dla kategorii głównej pusta).
                var sciezka = string.Concat(parentIds.Take(parentIds.Count - 1).Select(i => i + "_"));
                var html = await GetAjaxAsync(
                    $"kategorie/podkategorie.php?pole={parentId}&sciezka={Uri.EscapeDataString(sciezka)}&id_poz=0&rozwin=false&tok={Uri.EscapeDataString(subToken)}", ct);

                foreach (var (id, name, hasChildren) in ParseCategoryRows(html))
                    Visit(id, name, hasChildren, parentPath, parentIds);
            }

            return result;

            void Visit(int id, string name, bool hasChildren, List<string> parentPath, List<int> parentIds)
            {
                if (!seen.Add(id))
                    return;

                var path = new List<string>(parentPath) { name };
                var ids = new List<int>(parentIds) { id };
                result.Add(new ShopCategory(id, path));

                if (hasChildren)
                    pending.Enqueue((id, path, ids));
            }
        }

        /// <summary>Wiersze listingu kategorii: id, nazwa i czy ma podkategorie (ikona "Rozwiń").</summary>
        internal static IEnumerable<(int Id, string Name, bool HasChildren)> ParseCategoryRows(string html)
        {
            foreach (Match row in CategoryRowRegex.Matches(html))
            {
                var nameMatch = CategoryNameRegex.Match(row.Groups[2].Value);
                if (!nameMatch.Success)
                    continue;

                var name = WebUtility.HtmlDecode(Regex.Replace(nameMatch.Groups[1].Value, "<[^>]+>", string.Empty))
                    .Replace((char)0xA0, ' ')
                    .Trim();

                var hasChildren = row.Groups[2].Value.Contains("podkategorie(", StringComparison.Ordinal);
                yield return (int.Parse(row.Groups[1].Value), name, hasChildren);
            }
        }

        public async Task ResetCacheAsync(CancellationToken ct)
        {
            await EnsureLoggedInAsync(ct);

            var html = await GetAsync("?resetCache", ct);
            if (IsLoginPage(html))
            {
                _loggedIn = false;
                await EnsureLoggedInAsync(ct);
                await GetAsync("?resetCache", ct);
            }
        }

        private async Task<string> GetAjaxAsync(string relative, CancellationToken ct)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(AdminBase(), relative));
            request.Headers.Referrer = new Uri(AdminBase(), "kategorie/kategorie.php");
            request.Headers.Add("X-Requested-With", "XMLHttpRequest");

            using var response = await _http.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync(ct);
        }

        private async Task EnsureLoggedInAsync(CancellationToken ct)
        {
            if (_loggedIn)
                return;

            var check = await GetAsync("import_danych/obsluga_csv.php", ct);
            if (!IsLoginPage(check))
            {
                _loggedIn = true;
                return;
            }

            if (string.IsNullOrWhiteSpace(_settings.AdminLogin) || string.IsNullOrWhiteSpace(_settings.AdminPassword))
                throw new InvalidOperationException("ShopGold admin login/password is not configured.");

            var loginPageHtml = string.IsNullOrWhiteSpace(_settings.LoginPage)
                ? check
                : await GetAsync(_settings.LoginPage, ct);

            var form = FormRegex.Matches(loginPageHtml).Select(m => m.Value).FirstOrDefault(f => PasswordInputRegex.IsMatch(f))
                ?? throw new InvalidOperationException("ShopGold login form not found.");

            var fields = new Dictionary<string, string>();
            string? loginField = NullIfEmpty(_settings.LoginFieldName);
            string? passwordField = NullIfEmpty(_settings.PasswordFieldName);

            foreach (Match input in InputRegex.Matches(form))
            {
                var name = Attribute(input.Value, "name");
                if (string.IsNullOrEmpty(name))
                    continue;

                var type = (Attribute(input.Value, "type") ?? "text").ToLowerInvariant();

                switch (type)
                {
                    case "password":
                        passwordField ??= name;
                        break;
                    case "text" or "email":
                        loginField ??= name;
                        break;
                    case "checkbox" or "radio":
                        if (input.Value.Contains("checked", StringComparison.OrdinalIgnoreCase))
                            fields[name] = WebUtility.HtmlDecode(Attribute(input.Value, "value") ?? "on");
                        break;
                    case "submit" or "image" or "button" or "reset" or "file":
                        break;
                    default:
                        fields[name] = WebUtility.HtmlDecode(Attribute(input.Value, "value") ?? string.Empty);
                        break;
                }
            }

            if (loginField == null || passwordField == null)
                throw new InvalidOperationException("ShopGold login form fields not recognized - set LoginFieldName and PasswordFieldName.");

            fields[loginField] = _settings.AdminLogin;
            fields[passwordField] = _settings.AdminPassword;

            var action = WebUtility.HtmlDecode(Attribute(Regex.Match(form, @"<form\b[^>]*>", RegexOptions.IgnoreCase).Value, "action") ?? string.Empty);
            var loginUrl = string.IsNullOrWhiteSpace(_settings.LoginPage) ? "import_danych/obsluga_csv.php" : _settings.LoginPage;
            var actionUri = string.IsNullOrWhiteSpace(action)
                ? new Uri(AdminBase(), loginUrl)
                : new Uri(AdminBase(), action);

            using var response = await _http.PostAsync(actionUri, new FormUrlEncodedContent(fields), ct);
            response.EnsureSuccessStatusCode();

            var after = await GetAsync("import_danych/obsluga_csv.php", ct);
            if (IsLoginPage(after))
                throw new InvalidOperationException("ShopGold admin login failed - check login and password (or IP restrictions/captcha on the panel).");

            _loggedIn = true;
            _logger.LogInformation("Logged in to the ShopGold admin panel.");
        }

        private async Task<string> GetAsync(string relative, CancellationToken ct)
        {
            using var response = await _http.GetAsync(new Uri(AdminBase(), relative), ct);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync(ct);
        }

        private async Task<string> PostFormAsync(string relative, Dictionary<string, string> form, CancellationToken ct, bool ajax = false)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(AdminBase(), relative))
            {
                Content = new FormUrlEncodedContent(form)
            };

            request.Headers.Referrer = new Uri(AdminBase(), "import_danych/obsluga_csv_import.php");
            if (ajax)
                request.Headers.Add("X-Requested-With", "XMLHttpRequest");

            using var response = await _http.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync(ct);
        }

        private Uri AdminBase()
        {
            var baseUrl = _settings.BaseUrl.TrimEnd('/');
            var adminPath = "/" + (_settings.AdminPath ?? string.Empty).Trim('/');
            return new Uri(baseUrl + adminPath + "/");
        }

        private static bool IsLoginPage(string html) => PasswordInputRegex.IsMatch(html);

        private static string? Attribute(string tag, string name)
        {
            var match = Regex.Match(tag, $@"\b{name}\s*=\s*(?:""([^""]*)""|'([^']*)'|([^\s>]+))", RegexOptions.IgnoreCase);
            if (!match.Success)
                return null;

            return match.Groups[1].Success ? match.Groups[1].Value
                : match.Groups[2].Success ? match.Groups[2].Value
                : match.Groups[3].Value;
        }

        private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

        private sealed class ImportStep
        {
            [JsonPropertyName("suma")]
            public int Total { get; set; }

            [JsonPropertyName("dodane")]
            public int Added { get; set; }

            [JsonPropertyName("aktualizacja")]
            public int Updated { get; set; }

            [JsonPropertyName("nazwy")]
            public string? Names { get; set; }
        }
    }
}
