using AduosSyncServices.Contracts.Data.Enums;
using AduosSyncServices.Contracts.Models;

namespace AduosSyncServices.Contracts.Interfaces
{
    public interface IOrderRepository
    {
        public Task SaveAllegroOrder(AllegroOrder order);

        public Task MarkAsOrderedInExternalCompany(int orderId, int externalOrderId, bool isDropshipping);

        // Deletes a manual order that hasn't been placed with the supplier yet. Returns false (no-op)
        // when the order isn't manual or has already been sent to Gąska - the guard lives in the SP.
        public Task<bool> DeleteManualOrder(int orderId);

        // Assigns (or clears, when internalStatusId is null) the internal status for the given orders.
        public Task SetOrdersInternalStatus(IReadOnlyCollection<int> orderIds, int? internalStatusId);

        public Task<List<AllegroOrder>> GetOrdersToUpdateExternalInfo();

        public Task<List<AllegroOrder>> GetAllOrdersForExternalCompany();

        public Task UpdateOrderExternalInfo(AllegroOrder order);

        public Task<List<AllegroOrder>> GetOrdersToUpdateInAllegro();

        public Task SetEmailSent(int orderId);

        /// <summary>Numery przesyłek, o których wiemy, że są już w Allegro.</summary>
        public Task<List<AllegroShipment>> GetSentShipments(int orderId);

        /// <summary>Zapamiętuje numer przesyłki, który jest już w Allegro (idempotentne).</summary>
        public Task AddSentShipment(int orderId, string carrierId, string waybill);

        public Task MarkShipmentsChecked(int orderId);

        /// <summary>Zapisuje status, który Allegro właśnie przyjęło - żeby nie wysyłać go ponownie.</summary>
        public Task UpdateRealizeStatus(int orderId, AllegroOrderStatus status);
    }
}