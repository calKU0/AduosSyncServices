using AduosSyncServices.Contracts.Models;

namespace AduosSyncServices.Contracts.Interfaces
{
    public interface IOrderRepository
    {
        public Task SaveAllegroOrder(AllegroOrder order);

        public Task MarkAsOrderedInExternalCompany(int orderId, int externalOrderId, bool isDropshipping);

        public Task<List<AllegroOrder>> GetOrdersToUpdateExternalInfo();

        public Task<List<AllegroOrder>> GetAllOrdersForExternalCompany();

        public Task UpdateOrderExternalInfo(AllegroOrder order);

        public Task<List<AllegroOrder>> GetOrdersToUpdateInAllegro();

        public Task SetEmailSent(int orderId);
    }
}