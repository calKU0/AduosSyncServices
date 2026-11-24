namespace ShopGold.Aduos.Gaska.ProductsService.Helpers
{
    /// <summary>
    /// Drzewo kategorii sklepu między cyklami (singleton). Odczyt z panelu to jedno zapytanie na każdą
    /// kategorię z podkategoriami, więc czytamy je ponownie tylko wtedy, gdy import mógł dodać kategorie
    /// albo minęła doba (ktoś mógł zmienić kategorie ręcznie w panelu).
    /// </summary>
    public sealed class ShopCategoryCache
    {
        private static readonly TimeSpan MaxAge = TimeSpan.FromHours(24);

        private readonly object _lock = new();
        private ShopCategoryMap? _map;
        private DateTime _loadedAt;
        private readonly HashSet<string> _creationAttempted = new(StringComparer.Ordinal);

        public ShopCategoryMap? Current
        {
            get
            {
                lock (_lock)
                    return _map != null && DateTime.UtcNow - _loadedAt < MaxAge ? _map : null;
            }
        }

        public void Set(ShopCategoryMap map)
        {
            lock (_lock)
            {
                _map = map;
                _loadedAt = DateTime.UtcNow;
            }
        }

        public void Invalidate()
        {
            lock (_lock)
                _map = null;
        }

        /// <summary>
        /// Ścieżki, które już próbowaliśmy założyć. Gdy po imporcie kategorii nadal jej nie ma (np. sklep
        /// zmienił nazwę), nie ponawiamy importu w każdym cyklu - do restartu usługi.
        /// </summary>
        public void MarkCreationAttempted(string pathKey)
        {
            lock (_lock)
                _creationAttempted.Add(pathKey);
        }

        public bool WasCreationAttempted(string pathKey)
        {
            lock (_lock)
                return _creationAttempted.Contains(pathKey);
        }
    }
}
