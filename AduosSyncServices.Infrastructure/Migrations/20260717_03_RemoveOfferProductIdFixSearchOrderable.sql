-- AllegroOffers.ProductId is always NULL in production (upstream sync never populated it - see
-- OfferRepository.UpsertOffers, which always writes DBNull.Value for it). Products_SearchOrderable's
-- INNER JOIN on it therefore never matches, so the manual-order product search has been returning zero
-- rows outright. Every other offer<->product lookup in this codebase already joins on
-- ExternalId = Code instead (see AllegroOffers_GetOffersToUpdate) - switch this proc to match, and
-- remove the dead ProductId column entirely per explicit request.

-- AllegroOffers_Upsert must be dropped before the AllegroOfferType TVP it references can be replaced.
DROP PROCEDURE IF EXISTS dbo.AllegroOffers_Upsert;
GO

DROP TYPE IF EXISTS dbo.AllegroOfferType;
GO

CREATE TYPE dbo.AllegroOfferType AS TABLE
(
    [Id] NVARCHAR(255),
    [Account] INT,
    [Name] NVARCHAR(255),
    [CategoryId] INT,
    [Price] DECIMAL(18, 2),
    [Stock] INT,
    [WatchersCount] INT,
    [VisitsCount] INT,
    [Status] NVARCHAR(50),
    [DeliveryName] NVARCHAR(255),
    [StartingAt] DATETIME2,
    [ExternalId] NVARCHAR(255)
);
GO

CREATE PROCEDURE dbo.AllegroOffers_Upsert
    @Offers dbo.AllegroOfferType READONLY
AS
BEGIN
    SET NOCOUNT ON;

    MERGE AllegroOffers WITH (UPDLOCK, HOLDLOCK) AS target
    USING @Offers AS source
    ON target.Id = source.Id
    WHEN MATCHED AND (
        target.Name <> source.Name OR
        target.Price <> source.Price OR
        target.Stock <> source.Stock OR
        target.Status <> source.Status OR
        target.WatchersCount <> source.WatchersCount OR
        target.VisitsCount <> source.VisitsCount OR
        target.StartingAt <> source.StartingAt OR
        target.Account <> source.Account OR
        target.CategoryId <> source.CategoryId OR
        ISNULL(target.DeliveryName, '') <> ISNULL(source.DeliveryName, '') OR
        ISNULL(target.ExternalId, '') <> ISNULL(source.ExternalId, '')
    ) THEN
        UPDATE SET
            Name = source.Name,
            Account = source.Account,
            CategoryId = source.CategoryId,
            Price = source.Price,
            Stock = source.Stock,
            WatchersCount = source.WatchersCount,
            VisitsCount = source.VisitsCount,
            Status = source.Status,
            DeliveryName = source.DeliveryName,
            StartingAt = source.StartingAt,
            ExternalId = source.ExternalId
    WHEN NOT MATCHED THEN
        INSERT (Id, Account, Name, CategoryId, Price, Stock, WatchersCount, VisitsCount, Status, DeliveryName, StartingAt, ExternalId)
        VALUES (source.Id, source.Account, source.Name, source.CategoryId, source.Price, source.Stock, source.WatchersCount, source.VisitsCount, source.Status, source.DeliveryName, source.StartingAt, source.ExternalId);
END
GO

IF EXISTS (
    SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_dbo.AllegroOffers_dbo.Products_ProductId'
)
BEGIN
    ALTER TABLE dbo.AllegroOffers DROP CONSTRAINT [FK_dbo.AllegroOffers_dbo.Products_ProductId];
END
GO

IF EXISTS (
    SELECT 1 FROM sys.columns WHERE Name = N'ProductId' AND Object_ID = OBJECT_ID(N'dbo.AllegroOffers')
)
BEGIN
    ALTER TABLE dbo.AllegroOffers DROP COLUMN ProductId;
END
GO

-- Products eligible for a manually-placed order: not archived, and currently listed with an active
-- Allegro offer, joined by ExternalId = Code (ProductId no longer exists on AllegroOffers).
-- Also surfaces PackQty (the "Opakowanie" column) - the required package's quantity when one exists,
-- else 1 - mirroring OfferFactory.GetPackageQuantity's PackRequired == 1 convention.
CREATE OR ALTER PROCEDURE dbo.Products_SearchOrderable
    @SearchTerm NVARCHAR(200),
    @IntegrationCompany INT,
    @Account INT,
    @MaxResults INT = 50,
    @Offset INT = 0
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        p.Id AS ProductId,
        p.Code,
        p.Name,
        p.InStock,
        p.Unit,
        p.PriceNet AS PurchasePriceNet,
        p.PriceGross AS PurchasePriceGross,
        o.Price AS SalePrice,
        JSON_VALUE(o.Images, '$[0]') AS ImageUrl,
        ISNULL(
            (SELECT TOP 1 pack.PackQty FROM dbo.ProductPackages pack WHERE pack.ProductId = p.Id AND pack.PackRequired = 1),
            1
        ) AS PackQty
    FROM dbo.Products p
    INNER JOIN dbo.AllegroOffers o ON o.ExternalId = p.Code AND o.Account = @Account
    WHERE p.IntegrationCompany = @IntegrationCompany
      AND p.IsArchived = 0
      AND o.Status = 'ACTIVE'
      AND (p.Name LIKE '%' + @SearchTerm + '%' OR p.Code LIKE '%' + @SearchTerm + '%')
    ORDER BY p.Name
    OFFSET @Offset ROWS FETCH NEXT @MaxResults ROWS ONLY;
END
GO

ALTER PROCEDURE [dbo].[Products_GetByIntegrationId]
    @IntegrationId INT,
    @IntegrationCompany INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP 1 *
    FROM Products p
    WHERE IntegrationId = @IntegrationId AND IntegrationCompany = @IntegrationCompany
    ORDER BY CreatedDate DESC;
END
GO
