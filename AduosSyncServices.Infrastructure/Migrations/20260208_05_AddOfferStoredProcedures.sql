CREATE OR ALTER PROCEDURE dbo.AllegroOffers_Upsert
    @Id NVARCHAR(255),
    @Account INT,
    @Name NVARCHAR(255),
    @ProductId INT = NULL,
    @CategoryId INT,
    @Price DECIMAL(18, 2),
    @Stock INT,
    @WatchersCount INT,
    @VisitsCount INT,
    @Status NVARCHAR(50),
    @DeliveryName NVARCHAR(255) = NULL,
    @StartingAt DATETIME2,
    @ExternalId NVARCHAR(255) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    MERGE AllegroOffers AS target
    USING (SELECT @Id AS Id) AS source
    ON target.Id = source.Id
    WHEN MATCHED THEN
        UPDATE SET
            Name = @Name,
            Account = @Account,
            CategoryId = @CategoryId,
            Price = @Price,
            Stock = @Stock,
            WatchersCount = @WatchersCount,
            VisitsCount = @VisitsCount,
            Status = @Status,
            DeliveryName = @DeliveryName,
            StartingAt = @StartingAt,
            ExternalId = @ExternalId
    WHEN NOT MATCHED THEN
        INSERT (Id, Account, Name, ProductId, CategoryId, Price, Stock, WatchersCount, VisitsCount, Status, DeliveryName, StartingAt, ExternalId)
        VALUES (@Id, @Account, @Name, @ProductId, @CategoryId, @Price, @Stock, @WatchersCount, @VisitsCount, @Status, @DeliveryName, @StartingAt, @ExternalId);
END
GO

CREATE OR ALTER PROCEDURE dbo.AllegroOffers_GetAll
@Account INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT * FROM AllegroOffers WHERE Account = @Account;
END
GO

CREATE OR ALTER PROCEDURE dbo.AllegroOffers_GetOffersToUpdate
    @DeliveryNames NVARCHAR(MAX),
    @IntegrationCompany INT,
    @Account INT
AS
BEGIN
    SET NOCOUNT ON;

    CREATE TABLE #OffersWithProducts
    (
        OfferId NVARCHAR(255) NOT NULL,
        ExternalId NVARCHAR(255) NULL,
        OfferName NVARCHAR(255) NOT NULL,
        CategoryId INT NOT NULL,
        Status NVARCHAR(50) NOT NULL,
        StartingAt DATETIME2 NOT NULL,
        DeliveryName NVARCHAR(255) NULL,
        ProductId INT NOT NULL,
        AllegroId NVARCHAR(255) NULL,
        Code NVARCHAR(255) NOT NULL,
        ProductName NVARCHAR(255) NOT NULL,
        Description NVARCHAR(MAX) NULL,
        Ean NVARCHAR(50) NULL,
        Weight FLOAT NOT NULL,
        Fits NVARCHAR(MAX) NULL,
        SupplierName NVARCHAR(255) NULL,
        Substitutes NVARCHAR(MAX) NULL,
        InStock FLOAT NOT NULL,
        Unit NVARCHAR(50) NULL,
        CurrencyPrice NVARCHAR(50) NULL,
        PriceNet DECIMAL(18, 2) NOT NULL,
        PriceGross DECIMAL(18, 2) NOT NULL,
        DefaultAllegroCategory INT NOT NULL,
        Package DECIMAL(18, 2) NOT NULL,
        CreatedDate DATETIME2 NOT NULL,
        UpdatedDate DATETIME2 NOT NULL
    );

    INSERT INTO #OffersWithProducts
    (
        OfferId, ExternalId, OfferName, CategoryId, Status, StartingAt, DeliveryName,
        ProductId, AllegroId, Code, ProductName, Description, Ean, Weight, Fits, SupplierName,
        Substitutes, InStock, Unit, CurrencyPrice, PriceNet, PriceGross, DefaultAllegroCategory,
        Package, CreatedDate, UpdatedDate
    )
    SELECT
        ao.Id, ao.ExternalId, ao.Name, ao.CategoryId, ao.Status, ao.StartingAt, ao.DeliveryName,
        p.Id, p.AllegroId, p.Code, p.Name, p.Description,
        p.Ean, p.Weight, p.Fits, p.SupplierName, p.Substitutes, p.InStock, p.Unit,
        p.CurrencyPrice, p.PriceNet, p.PriceGross, p.DefaultAllegroCategory, p.Package,
        p.CreatedDate, p.UpdatedDate
    FROM AllegroOffers ao
    INNER JOIN Products p ON p.Code = ao.ExternalId AND p.IntegrationCompany = @IntegrationCompany
    WHERE ao.Status IN ('ACTIVE', 'ENDED') and Account = @Account 
        AND ao.DeliveryName IN (SELECT value FROM STRING_SPLIT(@DeliveryNames, ','));

    SELECT
        OfferId AS Id,
        ExternalId,
        OfferName AS Name,
        CategoryId,
        Status,
        StartingAt,
        DeliveryName,
        ProductId AS Id,
        AllegroId,
        Code,
        ProductName AS Name,
        Description,
        Ean,
        Weight,
        Fits,
        SupplierName,
        Substitutes,
        InStock,
        Unit,
        CurrencyPrice,
        PriceNet,
        PriceGross,
        DefaultAllegroCategory,
        Package,
        CreatedDate,
        UpdatedDate
    FROM #OffersWithProducts;

    SELECT ai.*
    FROM AllegroImages ai
    WHERE ai.ProductId IN (SELECT DISTINCT ProductId FROM #OffersWithProducts)
      AND ai.Connected = 1 AND Account = @Account;

    SELECT ps.*
    FROM ProductSpecifications ps
    WHERE ps.ProductId IN (SELECT DISTINCT ProductId FROM #OffersWithProducts);
END
GO

CREATE OR ALTER PROCEDURE dbo.AllegroOffers_DeleteByProductId
    @ProductId INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Code NVARCHAR(255);
    DECLARE @OfferId NVARCHAR(255);

    SELECT @Code = Code
    FROM Products
    WHERE Id = @ProductId;

    IF @Code IS NULL
    BEGIN
        SELECT CAST(NULL AS NVARCHAR(255)) AS Code;
        RETURN;
    END

    SELECT @OfferId = Id
    FROM AllegroOffers
    WHERE ExternalId = @Code;

    IF @OfferId IS NOT NULL
    BEGIN
        DELETE FROM AllegroOfferAttributes
        WHERE OfferId = @OfferId;

        DELETE FROM AllegroOfferDescriptions
        WHERE OfferId = @OfferId;

        DELETE FROM AllegroOffers
        WHERE Id = @OfferId;
    END

    SELECT @Code AS Code;
END
GO
