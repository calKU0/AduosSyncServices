namespace AduosSyncServices.Contracts.Interfaces
{
    public interface IAllegroCategoryService
    {
        Task UpdateAllegroCategories(CancellationToken ct = default);

        Task FetchAndSaveCategoryParameters(CancellationToken ct = default);
    }
}