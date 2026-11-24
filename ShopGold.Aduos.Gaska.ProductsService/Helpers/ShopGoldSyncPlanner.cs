using ShopGold.Aduos.Gaska.ProductsService.Models;
using System.Text.Json;

namespace ShopGold.Aduos.Gaska.ProductsService.Helpers
{
    /// <summary>Plik do zaimportowania: wiersze i pola (kolumny), które mają w nim być.</summary>
    public sealed record ShopGoldImportBatch(IReadOnlyList<string> Fields, List<ShopGoldRow> Rows);

    public sealed class ShopGoldSyncPlan
    {
        public List<ShopGoldRow> Adds { get; } = new();

        /// <summary>
        /// Pliki aktualizacji: zbiorczy (wszystkie zmienione kolumny), zdjęcia i wyłączenia - zwykle 1-3 pliki.
        /// </summary>
        public List<ShopGoldImportBatch> Updates { get; } = new();

        public int SkippedWithoutImages { get; set; }
        public int SkippedInactiveNew { get; set; }
        public int DeferredNew { get; set; }
        public int Deactivations { get; set; }
        public int Unchanged { get; set; }
    }

    /// <summary>
    /// Porównuje produkty z ostatnio wysłanym stanem. Nowe produkty idą do importu dodającego ze wszystkimi
    /// polami (i zdjęciami), zmienione - do jednego zbiorczego importu aktualizującego (patrz
    /// <see cref="BuildUpdateBatches"/>), niezmienione nie są wysyłane wcale.
    /// </summary>
    public static class ShopGoldSyncPlanner
    {
        public static ShopGoldSyncPlan Plan(
            IReadOnlyCollection<ShopGoldRow> rows,
            IReadOnlyDictionary<string, ShopGoldProductState> states,
            int maxNewProducts,
            bool skipProductsWithoutImages,
            bool allowDeactivation)
        {
            var plan = new ShopGoldSyncPlan();
            var changedRows = new List<(ShopGoldRow Row, List<string> Changed)>();
            var currentCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var row in rows)
            {
                currentCodes.Add(row.CatalogNumber);

                if (!states.TryGetValue(row.CatalogNumber, out var state))
                {
                    if (!row.IsActive)
                    {
                        // Produkt bez stanu dodamy, kiedy będzie go można kupić - import dodający jest najdroższy.
                        plan.SkippedInactiveNew++;
                        continue;
                    }

                    if (skipProductsWithoutImages && row.Images.Count == 0)
                    {
                        plan.SkippedWithoutImages++;
                        continue;
                    }

                    if (plan.Adds.Count >= maxNewProducts)
                    {
                        plan.DeferredNew++;
                        continue;
                    }

                    plan.Adds.Add(row);
                    continue;
                }

                var changed = ChangedFields(row, state);
                if (changed.Count == 0)
                {
                    plan.Unchanged++;
                    continue;
                }

                changedRows.Add((row, changed));
            }

            plan.Updates.AddRange(BuildUpdateBatches(changedRows));

            // Wyłączenia osobnym plikiem - te wiersze mają tylko pole Status.
            if (allowDeactivation)
            {
                var deactivations = states.Values
                    .Where(s => s.IsActive && !currentCodes.Contains(s.Code))
                    .Select(s => ShopGoldRowFactory.BuildDeactivation(s.Code, s.ProductId, s.ImagesHash))
                    .ToList();

                if (deactivations.Count > 0)
                {
                    plan.Updates.Add(new ShopGoldImportBatch(new[] { ShopGoldFields.Status }, deactivations));
                    plan.Deactivations = deactivations.Count;
                }
            }

            return plan;
        }

        /// <summary>
        /// Jeden zbiorczy plik aktualizacji: kolumny wszystkich pól, które zmieniły się w którymkolwiek produkcie,
        /// a produkty, w których dane pole się nie zmieniło, wysyłają jego obecną wartość z bazy.
        /// Wyjątki (osobne pliki):
        /// - zdjęcia - tylko produkty ze zmienionymi zdjęciami; inaczej sklep przetwarzałby zdjęcia każdego
        ///   produktu w pliku zbiorczym przy każdej zmianie ceny czy stanu,
        /// - produkty, które nie mają wartości dla jakiejś kolumny (np. Kategorie_id, gdy kategorii nie ma
        ///   jeszcze w sklepie) - pusta wartość w pliku aktualizacji mogłaby wyczyścić dane w sklepie, więc
        ///   idą plikiem bez tych kolumn.
        /// </summary>
        public static List<ShopGoldImportBatch> BuildUpdateBatches(IReadOnlyList<(ShopGoldRow Row, List<string> Changed)> changedRows)
        {
            var batches = new List<ShopGoldImportBatch>();

            var unionFields = ShopGoldFields.Order
                .Where(f => f != ShopGoldFields.Images && changedRows.Any(c => c.Changed.Contains(f)))
                .ToList();

            var dataGroups = changedRows
                .Where(c => c.Changed.Any(f => f != ShopGoldFields.Images))
                .Select(c => c.Row)
                .GroupBy(row => string.Join("|", unionFields.Where(f => CanSend(row, f))))
                .OrderByDescending(g => g.Count());

            foreach (var group in dataGroups)
            {
                var fields = unionFields.Where(f => CanSend(group.First(), f)).ToList();
                batches.Add(new ShopGoldImportBatch(fields, group.ToList()));
            }

            var imageRows = changedRows
                .Where(c => c.Changed.Contains(ShopGoldFields.Images))
                .Select(c => c.Row)
                .ToList();

            if (imageRows.Count > 0)
                batches.Add(new ShopGoldImportBatch(new[] { ShopGoldFields.Images }, imageRows));

            return batches;
        }

        /// <summary>Czy wiersz ma wartość pola (np. Kategorie_id jest tylko, gdy sklep zna wszystkie kategorie).</summary>
        private static bool CanSend(ShopGoldRow row, string field) => row.Fields.ContainsKey(field);

        /// <summary>Pola, których odcisk różni się od zapisanego stanu.</summary>
        public static List<string> ChangedFields(ShopGoldRow row, ShopGoldProductState state)
        {
            var stored = ParseFingerprints(state.FieldsJson);

            return ShopGoldFields.Order
                .Where(row.Fields.ContainsKey)
                .Where(name => !ShopGoldFields.AddOnly.Contains(name))
                .Where(name =>
                {
                    var current = row.Fields[name].Fingerprint;

                    // Brak zdjęć lokalnie (np. w trakcie ponownego pobierania) nie może skasować zdjęć w sklepie.
                    if (name == ShopGoldFields.Images && string.IsNullOrEmpty(current))
                        return false;

                    return !stored.TryGetValue(name, out var previous) || previous != current;
                })
                .ToList();
        }

        /// <summary>Stan po udanym imporcie: zapisane odciski uzupełnione o wysłane pola.</summary>
        public static ShopGoldProductState StateAfterImport(ShopGoldRow row, IReadOnlyCollection<string> sentFields, ShopGoldProductState? previous)
        {
            var fingerprints = ParseFingerprints(previous?.FieldsJson);

            foreach (var field in sentFields)
            {
                if (row.Fields.TryGetValue(field, out var f))
                    fingerprints[field] = f.Fingerprint;
            }

            var imagesSent = sentFields.Contains(ShopGoldFields.Images);
            var statusSent = sentFields.Contains(ShopGoldFields.Status);

            return new ShopGoldProductState
            {
                Code = row.CatalogNumber,
                ProductId = row.ProductId ?? previous?.ProductId,
                FieldsJson = JsonSerializer.Serialize(fingerprints.OrderBy(kv => kv.Key, StringComparer.Ordinal).ToDictionary(kv => kv.Key, kv => kv.Value)),
                ImagesHash = imagesSent ? row.ImagesHash : previous?.ImagesHash,
                IsActive = statusSent ? row.IsActive : previous?.IsActive ?? row.IsActive
            };
        }

        /// <summary>
        /// Wiersze z pliku "dodawanie", których sklep nie dodał - ich numer katalogowy już był w sklepie
        /// (np. przejmowane produkty dodane wcześniej ręcznie), więc import je pominął. Sklep zwraca
        /// nazwy dodanych produktów; wiersze dopasowujemy po nazwie (z uwzględnieniem powtórzeń).
        /// Gdy nie da się dopasować, wiersz trafia do aktualizacji - powtórna aktualizacja właśnie
        /// dodanego produktu niczego nie psuje, a pominięcie oznaczałoby produkt, który się nie aktualizuje.
        /// </summary>
        public static List<ShopGoldRow> RowsNotAdded(IReadOnlyList<ShopGoldRow> rows, int addedCount, IReadOnlyCollection<string> addedNames)
        {
            if (addedCount >= rows.Count)
                return new List<ShopGoldRow>();

            var remaining = addedNames
                .GroupBy(NormalizeName)
                .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

            var notAdded = new List<ShopGoldRow>();

            foreach (var row in rows)
            {
                var key = NormalizeName(RowName(row));

                if (remaining.TryGetValue(key, out var count) && count > 0)
                    remaining[key] = count - 1;
                else
                    notAdded.Add(row);
            }

            return notAdded;
        }

        private static string RowName(ShopGoldRow row) =>
            row.Fields.TryGetValue(ShopGoldFields.Name, out var field)
                ? field.Columns.FirstOrDefault().Value ?? string.Empty
                : string.Empty;

        private static string NormalizeName(string name) =>
            System.Text.RegularExpressions.Regex.Replace(name ?? string.Empty, @"\s+", " ").Trim().ToLowerInvariant();

        /// <summary>Kopia stanu bez odcisku pola - pole zostanie wysłane ponownie.</summary>
        public static ShopGoldProductState WithoutField(ShopGoldProductState state, string field)
        {
            var fingerprints = ParseFingerprints(state.FieldsJson);
            fingerprints.Remove(field);

            return new ShopGoldProductState
            {
                Code = state.Code,
                ProductId = state.ProductId,
                FieldsJson = JsonSerializer.Serialize(fingerprints),
                ImagesHash = state.ImagesHash,
                IsActive = state.IsActive
            };
        }

        public static Dictionary<string, string> ParseFingerprints(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return new Dictionary<string, string>(StringComparer.Ordinal);

            try
            {
                return JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                    ?? new Dictionary<string, string>(StringComparer.Ordinal);
            }
            catch (JsonException)
            {
                // Uszkodzony stan = wysyłamy wszystkie pola ponownie.
                return new Dictionary<string, string>(StringComparer.Ordinal);
            }
        }
    }
}
