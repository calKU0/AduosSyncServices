using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ShopGold.Aduos.Gaska.ProductsService.Helpers
{
    /// <summary>
    /// Skrót zawartości zdjęć produktu. Serwis Allegro pobiera zdjęcia na nowo przy każdej aktualizacji
    /// szczegółów, więc zmianę poznajemy po zawartości, nie po nazwie czy dacie pliku. Skróty plików
    /// pamiętamy razem z rozmiarem i datą zapisu, także w pliku na dysku - żeby po restarcie serwisu
    /// nie czytać od nowa zdjęć wszystkich produktów, tylko te nowe lub zmienione.
    /// </summary>
    public sealed class ImageHashCache
    {
        private readonly string? _filePath;
        private readonly Dictionary<string, Entry> _files = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _used = new(StringComparer.OrdinalIgnoreCase);
        private bool _loaded;
        private bool _dirty;

        /// <param name="filePath">Plik ze skrótami; null = tylko w pamięci.</param>
        public ImageHashCache(string? filePath = null)
        {
            _filePath = filePath;
        }

        /// <summary>Wspólny skrót listy zdjęć (kolejność ma znaczenie - pierwsze zdjęcie jest główne). Null dla pustej listy.</summary>
        public string? ComputeHash(IReadOnlyList<string> files)
        {
            if (files.Count == 0)
                return null;

            EnsureLoaded();
            using var combined = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

            foreach (var file in files)
            {
                combined.AppendData(Encoding.ASCII.GetBytes(FileHash(file)));
                combined.AppendData("|"u8);
            }

            return Convert.ToHexString(combined.GetHashAndReset()).ToLowerInvariant();
        }

        /// <summary>
        /// Zapisuje skróty na dysk. Z <paramref name="removeUnused"/> usuwa skróty plików nieużytych od ostatniego
        /// takiego zapisu (zdjęcia produktów, których już nie wysyłamy). Zapis przez plik tymczasowy,
        /// żeby przerwany zapis nie zostawił uszkodzonego pliku.
        /// </summary>
        public void Save(bool removeUnused = true)
        {
            if (_filePath == null || !_loaded)
                return;

            var stale = removeUnused ? _files.Keys.Where(k => !_used.Contains(k)).ToList() : new List<string>();
            foreach (var key in stale)
                _files.Remove(key);

            if (_dirty || stale.Count > 0)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
                var temp = _filePath + ".tmp";
                using (var writer = new StreamWriter(temp, false, new UTF8Encoding(false)))
                {
                    foreach (var (path, entry) in _files)
                        writer.WriteLine(string.Join('\t', path, entry.Length.ToString(CultureInfo.InvariantCulture),
                            entry.WriteTimeTicks.ToString(CultureInfo.InvariantCulture), entry.Hash));
                }

                File.Move(temp, _filePath, overwrite: true);
            }

            if (removeUnused)
                _used.Clear();
            _dirty = false;
        }

        private string FileHash(string path)
        {
            _used.Add(path);
            var info = new FileInfo(path);
            var ticks = info.LastWriteTimeUtc.Ticks;
            if (_files.TryGetValue(path, out var cached) && cached.Length == info.Length && cached.WriteTimeTicks == ticks)
                return cached.Hash;

            using var stream = File.OpenRead(path);
            var hash = Convert.ToHexString(SHA256.HashData(stream));
            _files[path] = new Entry(info.Length, ticks, hash);
            _dirty = true;
            return hash;
        }

        /// <summary>Uszkodzony albo nieczytelny plik nie blokuje synchronizacji - skróty policzą się od nowa.</summary>
        private void EnsureLoaded()
        {
            if (_loaded)
                return;

            _loaded = true;
            if (_filePath == null || !File.Exists(_filePath))
                return;

            try
            {
                foreach (var line in File.ReadLines(_filePath))
                {
                    var parts = line.Split('\t');
                    if (parts.Length == 4
                        && long.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var length)
                        && long.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var ticks)
                        && parts[3].Length == 64)
                    {
                        _files[parts[0]] = new Entry(length, ticks, parts[3]);
                    }
                }
            }
            catch (IOException)
            {
                _files.Clear();
            }
            catch (UnauthorizedAccessException)
            {
                _files.Clear();
            }
        }

        private readonly record struct Entry(long Length, long WriteTimeTicks, string Hash);
    }
}
