using AduosSyncServices.Contracts.Data.Enums;
using AduosSyncServices.Contracts.Interfaces;
using AduosSyncServices.Contracts.Models;
using AduosSyncServices.Contracts.Settings;
using AduosSyncServices.Infrastructure.Data;
using AduosSyncServices.Infrastructure.Settings;
using Dapper;
using Microsoft.Extensions.Options;
using System.Data;

namespace AduosSyncServices.Infrastructure.Repositories
{
    public class OrderRepository : IOrderRepository
    {
        private readonly DapperContext _context;
        private readonly int _company;
        private readonly int _account;
        public OrderRepository(DapperContext context, IOptions<RepositorySettings> options)
        {
            _context = context;
            _company = (int)options.Value.Company;
            _account = (int)options.Value.Account;
        }
        public async Task<List<AllegroOrder>> GetOrdersToUpdateExternalInfo()
        {
            using var conn = _context.CreateConnection();
            conn.Open();
            var storedProcedure = "dbo.AllegroOrders_GetToUpdateExternalInfo";

            var orderDict = new Dictionary<int, AllegroOrder>();

            var orders = await conn.QueryAsync<AllegroOrder, AllegroOrderItem, AllegroOrder>(
                storedProcedure,
                (order, item) =>
                {
                    if (!orderDict.TryGetValue(order.Id, out var currentOrder))
                    {
                        currentOrder = order;
                        currentOrder.Items = new List<AllegroOrderItem>();
                        orderDict.Add(order.Id, currentOrder);
                    }

                    if (item != null)
                        currentOrder.Items.Add(item);

                    return currentOrder;
                },
                new
                {
                    IntegrationCompany = _company,
                    Account = _account,
                },
                splitOn: "Id",
                commandType: CommandType.StoredProcedure
            );

            return orderDict.Values.ToList();
        }

        public async Task<List<AllegroOrder>> GetOrdersToUpdateInAllegro()
        {
            using var conn = _context.CreateConnection();
            conn.Open();
            var storedProcedure = "dbo.AllegroOrders_GetToUpdateInAllegro";

            var orderDict = new Dictionary<int, AllegroOrder>();

            var orders = await conn.QueryAsync<AllegroOrder, AllegroOrderItem, AllegroOrder>(
                storedProcedure,
                (order, item) =>
                {
                    if (!orderDict.TryGetValue(order.Id, out var currentOrder))
                    {
                        currentOrder = order;
                        currentOrder.Items = new List<AllegroOrderItem>();
                        orderDict.Add(order.Id, currentOrder);
                    }

                    if (item != null)
                        currentOrder.Items.Add(item);

                    return currentOrder;
                },
                param: new
                {
                    Account = _account,
                    IntegrationCompany = _company
                },
                splitOn: "Id",
                commandType: CommandType.StoredProcedure
            );

            return orderDict.Values.ToList();
        }

        public async Task<List<AllegroOrder>> GetAllOrdersForExtranalCompany()
        {
            using var conn = _context.CreateConnection();
            conn.Open();
            var storedProcedure = "dbo.AllegroOrders_GetAllOrdersForExtranalCompany";

            var orderDict = new Dictionary<int, AllegroOrder>();

            var orders = await conn.QueryAsync<AllegroOrder, AllegroOrderItem, AllegroOrder>(
                storedProcedure,
                (order, item) =>
                {
                    if (!orderDict.TryGetValue(order.Id, out var currentOrder))
                    {
                        currentOrder = order;
                        currentOrder.Items = new List<AllegroOrderItem>();
                        orderDict.Add(order.Id, currentOrder);
                    }

                    if (item != null)
                        currentOrder.Items.Add(item);

                    return currentOrder;
                },
                param: new
                {
                    Account = _account,
                    IntegrationCompany = _company
                },
                splitOn: "Id",
                commandType: CommandType.StoredProcedure
            );

            return orderDict.Values.ToList();
        }

        public async Task MarkAsOrderedInExternalCompany(int orderId, int externalOrderId, bool isDropshipping)
        {
            using var conn = _context.CreateConnection();
            conn.Open();
            await conn.ExecuteAsync(
                "dbo.AllegroOrders_MarkAsOrderedInExternalCompany",
                new { OrderId = orderId, ExternalOrderId = externalOrderId, IsDropshipping = isDropshipping },
                commandType: CommandType.StoredProcedure
            );
        }

        public async Task SaveAllegroOrder(AllegroOrder order)
        {
            using var conn = _context.CreateConnection();
            conn.Open();
            using var transaction = conn.BeginTransaction();

            try
            {
                var orderParams = new DynamicParameters();
                orderParams.Add("@AllegroId", order.AllegroId);
                orderParams.Add("@MessageToSeller", order.MessageToSeller);
                orderParams.Add("@Note", order.Note);
                orderParams.Add("@Status", order.Status);
                orderParams.Add("@RealizeStatus", order.RealizeStatus);
                orderParams.Add("@Amount", order.Amount);
                orderParams.Add("@ClientNickname", order.ClientNickname);
                orderParams.Add("@RecipientFirstName", order.RecipientFirstName);
                orderParams.Add("@RecipientLastName", order.RecipientLastName);
                orderParams.Add("@RecipientStreet", order.RecipientStreet);
                orderParams.Add("@RecipientCity", order.RecipientCity);
                orderParams.Add("@RecipientPostalCode", order.RecipientPostalCode);
                orderParams.Add("@RecipientCountry", order.RecipientCountry);
                orderParams.Add("@RecipientCompanyName", order.RecipientCompanyName);
                orderParams.Add("@RecipientEmail", order.RecipientEmail);
                orderParams.Add("@RecipientPhoneNumber", order.RecipientPhoneNumber);
                orderParams.Add("@DeliveryMethodId", order.DeliveryMethodId);
                orderParams.Add("@DeliveryMethodName", order.DeliveryMethodName);
                orderParams.Add("@CancellationDate", order.CancellationDate);
                orderParams.Add("@CreatedAt", order.CreatedAt);
                orderParams.Add("@Revision", order.Revision);
                orderParams.Add("@SentToExternalCompany", order.SentToExternalCompany);
                orderParams.Add("@ExternalOrderId", order.ExternalOrderId);
                orderParams.Add("@PaymentType", order.PaymentType);
                orderParams.Add("@ExternalOrderStatus", order.ExternalOrderStatus);
                orderParams.Add("@ExternalOrderNumber", order.ExternalOrderNumber);
                orderParams.Add("@ExternalDeliveryName", order.ExternalDeliveryName);
                orderParams.Add("@Account", order.Account);
                orderParams.Add("@IntegrationCompany", order.IntegrationCompany);
                orderParams.Add("@Id", dbType: DbType.Int32, direction: ParameterDirection.Output);

                await conn.ExecuteAsync(
                    "dbo.AllegroOrders_Save",
                    orderParams,
                    transaction,
                    commandType: CommandType.StoredProcedure
                );

                if (order.Id == 0)
                    order.Id = orderParams.Get<int>("@Id");

                foreach (var item in order.Items)
                {
                    var product = await conn.QueryFirstOrDefaultAsync<(int ProductId, string Code)>(
                        "dbo.Products_GetByCode",
                        new { Code = item.ExternalId, IntegrationCompany = _company },
                        transaction,
                        commandType: CommandType.StoredProcedure
                    );

                    if (product == default)
                        throw new Exception($"Product with Id {item.ProductId} not found.");

                    var itemParams = new
                    {
                        AllegroOrderId = order.Id,
                        OrderItemId = item.OrderItemId,
                        ProductId = product.ProductId,
                        OfferId = item.OfferId,
                        OfferName = item.OfferName,
                        ExternalId = product.Code,
                        PriceGross = item.PriceGross,
                        Currency = item.Currency,
                        Quantity = item.Quantity,
                        ExternalCourier = item.ExternalCourier,
                        ExternalTrackingNumber = item.ExternalTrackingNumber,
                        ShippingRate = item.ShippingRate,
                        BoughtAt = item.BoughtAt
                    };

                    await conn.ExecuteAsync(
                        "dbo.AllegroOrderItems_Upsert",
                        itemParams,
                        transaction,
                        commandType: CommandType.StoredProcedure
                    );
                }

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        public async Task SetEmailSent(int orderId)
        {
            using var conn = _context.CreateConnection();
            conn.Open();

            await conn.ExecuteAsync(
                "dbo.AllegroOrders_SetEmailSent",
                new { OrderId = orderId },
                commandType: CommandType.StoredProcedure
            );
        }

        public async Task UpdateOrderExternalInfo(AllegroOrder order)
        {
            using var conn = _context.CreateConnection();
            conn.Open();
            using var transaction = conn.BeginTransaction();

            try
            {
                await conn.ExecuteAsync(
                    "dbo.AllegroOrders_UpdateExternalInfo",
                    new
                    {
                        order.Id,
                        order.ExternalOrderStatus,
                        order.ExternalOrderNumber,
                        order.ExternalDeliveryName
                    },
                    transaction,
                    commandType: CommandType.StoredProcedure
                );

                foreach (var item in order.Items)
                {
                    await conn.ExecuteAsync(
                        "dbo.AllegroOrderItems_UpdateExternalInfo",
                        new
                        {
                            AllegroOrderId = order.Id,
                            item.OrderItemId,
                            item.ProductId,
                            item.ExternalCourier,
                            item.ExternalTrackingNumber
                        },
                        transaction,
                        commandType: CommandType.StoredProcedure
                    );
                }

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }
    }
}

