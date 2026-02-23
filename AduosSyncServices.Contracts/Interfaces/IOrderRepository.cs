using AduosSyncServices.Contracts.Models;

namespace AduosSyncServices.Contracts.Interfaces
{
    public interface IOrderRepository
    {
        public Task SaveAllegroOrder(AllegroOrder order);

        public Task MarkAsOrderedInExternalCompany(int orderId, int externalOrderId);

        public Task<List<AllegroOrder>> GetOrdersToUpdateExternalInfo(List<string> shippingRates);

        public Task<List<AllegroOrder>> GetPendingOrdersForExternalCompany(int delayMinutes);

        public Task UpdateOrderExternalInfo(AllegroOrder order);

        public Task<List<AllegroOrder>> GetOrdersToUpdateInAllegro();

        public Task SetEmailSent(int orderId);
    }
}