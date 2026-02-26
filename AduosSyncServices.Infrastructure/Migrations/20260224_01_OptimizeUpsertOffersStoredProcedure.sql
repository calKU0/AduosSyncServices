CREATE TYPE [dbo].[AllegroOfferType] AS TABLE (
    [Id] NVARCHAR(255),
    [Account] INT,
    [Name] NVARCHAR(255),
    [ProductId] INT,
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

ALTER PROCEDURE [dbo].[AllegroOffers_Upsert]
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
        INSERT (Id, Account, Name, ProductId, CategoryId, Price, Stock, WatchersCount, VisitsCount, Status, DeliveryName, StartingAt, ExternalId)
        VALUES (source.Id, source.Account, source.Name, source.ProductId, source.CategoryId, source.Price, source.Stock, source.WatchersCount, source.VisitsCount, source.Status, source.DeliveryName, source.StartingAt, source.ExternalId);
END
GO