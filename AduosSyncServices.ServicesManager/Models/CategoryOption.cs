using AduosSyncServices.ServicesManager.Helpers;

namespace AduosSyncServices.ServicesManager.Models
{
    /// <summary>
    /// Kategoria Gąski do wyboru w konfiguratorze. <see cref="Key"/> (id kategorii) trafia do appsettings,
    /// <see cref="Path"/> widzi użytkownik.
    /// </summary>
    public sealed record CategoryOption(string Key, string Path, int Depth)
    {
        /// <summary>Tekst do wyszukiwania - bez polskich znaków i wielkich liter.</summary>
        public string SearchText { get; } = TolerantSearch.Normalize($"{Path} {Key}");
    }
}
