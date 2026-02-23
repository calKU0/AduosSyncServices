using AduosSyncServices.Contracts.DTOs.Allegro;
using AduosSyncServices.Contracts.Interfaces;
using AduosSyncServices.Contracts.Models;
using AduosSyncServices.Contracts.Settings;
using AduosSyncServices.Infrastructure.Data;
using Allegro.Aduos.Gaska.ProductsService.Constants;
using Allegro.Aduos.Gaska.ProductsService.Settings;
using Dapper;
using Microsoft.Extensions.Options;
using System.Data;
using System.Globalization;

namespace Allegro.Aduos.Gaska.ProductsService.Repositories
{
    public class OfferRepository : IOfferRepository
    {
        private readonly DapperContext _context;
        private readonly List<DeliverySettings> _deliveries;
        private readonly ILogger<OfferRepository> _logger;

        public OfferRepository(ILogger<OfferRepository> logger, DapperContext context, IOptions<AppSettings> options)
        {
            _logger = logger;
            _context = context;
            _deliveries = options.Value.Deliveries;
        }

        public async Task UpsertOffers(List<Offer> offers, CancellationToken ct)
        {
            if (offers == null || !offers.Any()) return;

            _logger.LogInformation("Starting upsert of {Count} offers", offers.Count);

            using var connection = _context.CreateConnection();
            connection.Open();
            using var transaction = connection.BeginTransaction();

            try
            {
                const int batchSize = 1000;
                // 1️ Map AllegroOffer entities
                var allegroOffers = offers.Select(o =>
                {
                    decimal.TryParse(o.SellingMode?.Price?.Amount, NumberStyles.Any, CultureInfo.InvariantCulture, out var price);
                    int.TryParse(o.Category?.Id, out var categoryId);

                    return new
                    {
                        Id = o.Id,
                        Account = ServiceConstants.Account,
                        Name = o.Name ?? string.Empty,
                        ProductId = (int?)null,
                        CategoryId = categoryId,
                        Price = price,
                        Stock = o.Stock?.Available ?? 0,
                        WatchersCount = o.Stats?.WatchersCount ?? 0,
                        VisitsCount = o.Stats?.VisitsCount ?? 0,
                        Status = o.Publication?.Status ?? string.Empty,
                        DeliveryName = o.Delivery?.ShippingRates?.Name,
                        StartingAt = o.Publication?.StartingAt ?? new DateTime(1753, 1, 1),
                        ExternalId = o.External?.Id
                    };
                }).ToList();

                foreach (var batch in allegroOffers.Chunk(batchSize))
                {
                    await connection.ExecuteAsync(
                        "AllegroOffers_Upsert",
                        batch,
                        transaction,
                        commandType: CommandType.StoredProcedure,
                        commandTimeout: 900);
                }

                transaction.Commit();
                _logger.LogInformation("Upsert of offers completed: {Count} processed", allegroOffers.Count);
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                _logger.LogError(ex, "Failed upsert of offers");
                throw;
            }
        }

        public async Task UpsertOfferDetails(List<AllegroOfferDetails.Root> offers, CancellationToken ct)
        {
            if (offers == null || !offers.Any()) return;

            _logger.LogInformation("Starting upsert of {Count} offer details", offers.Count);

            using var connection = _context.CreateConnection();
            connection.Open();
            using var transaction = connection.BeginTransaction();

            try
            {
                var allegroOffers = offers.Select(o =>
                {
                    decimal price = 0;
                    decimal.TryParse(o?.SellingMode?.Price?.Amount, NumberStyles.Any, CultureInfo.InvariantCulture, out price);

                    int categoryId = 0;
                    int.TryParse(o?.Category?.Id, out categoryId);

                    return new
                    {
                        Id = o.Id,
                        Account = ServiceConstants.Account,
                        Name = o.Name ?? string.Empty,
                        CategoryId = categoryId,
                        Price = price,
                        Stock = o?.Stock?.Available ?? 0,
                        Status = o?.Publication?.Status ?? "UNKNOWN",
                        DeliveryName = o?.Delivery?.ShippingRates?.Id,
                        ExternalId = o?.External?.Id,
                        Weight = 0,
                        Images = o?.Images != null ? System.Text.Json.JsonSerializer.Serialize(o.Images) : null,
                        StartingAt = o?.Publication?.StartingAt ?? new DateTime(1753, 1, 1),
                        HandlingTime = o?.Delivery?.HandlingTime,
                        ResponsiblePerson = o?.ProductSet?.FirstOrDefault()?.ResponsiblePerson?.Id ?? string.Empty,
                        ResponsibleProducer = o?.ProductSet?.FirstOrDefault()?.ResponsibleProducer?.Id ?? string.Empty
                    };
                }).ToList();

                await connection.ExecuteAsync(
                    "AllegroOffers_UpsertDetails",
                    allegroOffers,
                    transaction,
                    commandType: CommandType.StoredProcedure,
                    commandTimeout: 900);

                // ---- Descriptions ----
                var descriptions = new List<object>();
                foreach (var o in offers)
                {
                    int sectionIndex = 1;
                    if (o.Description?.Sections != null)
                    {
                        foreach (var section in o.Description.Sections)
                        {
                            foreach (var item in section.Items)
                            {
                                descriptions.Add(new
                                {
                                    OfferId = o.Id,
                                    Type = item.Type,
                                    Content = item.Type == "TEXT" ? item.Content : item.Url,
                                    SectionId = sectionIndex
                                });
                            }
                            sectionIndex++;
                        }
                    }
                }

                if (descriptions.Any())
                {
                    await connection.ExecuteAsync(
                        "AllegroOfferDescriptions_Insert",
                        descriptions,
                        transaction,
                        commandType: CommandType.StoredProcedure,
                        commandTimeout: 900);
                }

                // ---- Attributes ----
                var attributes = new List<object>();
                foreach (var o in offers)
                {
                    if (o.Parameters != null)
                    {
                        foreach (var param in o.Parameters)
                        {
                            attributes.Add(new
                            {
                                OfferId = o.Id,
                                AttributeId = param.Id,
                                Type = param.ValuesIds?.Any() == true ? "dictionary" : "string",
                                ValuesJson = System.Text.Json.JsonSerializer.Serialize(param.Values ?? new List<string>()),
                                ValuesIdsJson = System.Text.Json.JsonSerializer.Serialize(param.ValuesIds ?? new List<string>())
                            });
                        }
                    }
                }

                if (attributes.Any())
                {
                    await connection.ExecuteAsync(
                        "AllegroOfferAttributes_Insert",
                        attributes,
                        transaction,
                        commandType: CommandType.StoredProcedure);
                }

                transaction.Commit();
                _logger.LogInformation("Upsert of offer details completed");
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                _logger.LogError(ex, "Failed upsert of offer details");
                throw;
            }
        }

        public async Task<List<AllegroOffer>> GetAllOffers(CancellationToken ct)
        {
            using var connection = _context.CreateConnection();
            connection.Open();
            return (await connection.QueryAsync<AllegroOffer>(
                "AllegroOffers_GetAll",
                new { Account = ServiceConstants.Account },
                commandType: CommandType.StoredProcedure)).ToList();
        }

        public async Task<List<AllegroOffer>> GetOffersWithoutDetails(CancellationToken ct)
        {
            using var connection = _context.CreateConnection();
            return (await connection.QueryAsync<AllegroOffer>(
                "AllegroOffers_GetWithoutDetails",
                commandType: CommandType.StoredProcedure)).ToList();
        }

        public async Task<List<AllegroOffer>> GetOffersToUpdate(CancellationToken ct)
        {
            var deliveryNames = _deliveries?
                .Select(d => d.DeliveryName)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList() ?? new List<string>();

            if (!deliveryNames.Any())
            {
                _logger.LogInformation("Brak skonfigurowanych dostaw — pomijam pobieranie ofert do aktualizacji.");
                return new List<AllegroOffer>();
            }

            using var connection = _context.CreateConnection();
            connection.Open();

            // Step 1: Get offers, images, and specs in one call
            var command = new CommandDefinition(
                "AllegroOffers_GetOffersToUpdate",
                new { DeliveryNames = string.Join(",", deliveryNames), IntegrationCompany = ServiceConstants.Company, Account = ServiceConstants.Account },
                commandTimeout: 900,
                cancellationToken: ct,
                commandType: CommandType.StoredProcedure);

            using var grid = await connection.QueryMultipleAsync(command);

            var offers = grid.Read<AllegroOffer, Product, AllegroOffer>(
                (offer, product) =>
                {
                    offer.Product = product;
                    return offer;
                },
                splitOn: "Id").ToList();

            if (!offers.Any())
                return offers;

            offers = offers
                .GroupBy(o => o.Product.Id)
                .Select(g => g.OrderByDescending(o => o.StartingAt).First())
                .ToList();

            var allImages = grid.Read<AllegroImages>().ToList();
            var allSpecs = grid.Read<ProductSpecification>().ToList();
            var allApplications = grid.Read<ProductApplication>().ToList();
            var allPackages = grid.Read<ProductPackage>().ToList();
            var allParameters = grid.Read<ProductParameter>().ToList();

            // Step 2: Aggregate into product collections
            var imagesLookup = allImages.ToLookup(i => i.ProductId);
            var specsLookup = allSpecs.ToLookup(s => s.ProductId);
            var applicationsLookup = allApplications.ToLookup(a => a.ProductId);
            var packagesLookup = allPackages.ToLookup(p => p.ProductId);
            var parametersLookup = allParameters.ToLookup(p => p.ProductId);

            foreach (var offer in offers)
            {
                var product = offer.Product;
                product.AllegroImages = imagesLookup[product.Id].ToList();
                product.Specifications = specsLookup[product.Id].ToList();
                product.Applications = applicationsLookup[product.Id].ToList();
                product.Packages = packagesLookup[product.Id].ToList();
                product.Parameters = parametersLookup[product.Id].ToList();
            }

            return offers;
        }

        public async Task DeleteOffer(int productId, CancellationToken ct)
        {
            using var connection = _context.CreateConnection();
            connection.Open();

            var code = await connection.ExecuteScalarAsync<string>(
                "AllegroOffers_DeleteByProductId",
                new { ProductId = productId },
                commandType: CommandType.StoredProcedure);

            if (string.IsNullOrWhiteSpace(code))
            {
                _logger.LogWarning("Product with Id {ProductId} not found. Cannot delete offer.", productId);
                return;
            }

            _logger.LogInformation("Deleted Allegro offer for product {Code}.", code);
        }
    }
}