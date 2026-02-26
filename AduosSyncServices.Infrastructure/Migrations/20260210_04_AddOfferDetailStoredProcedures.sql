CREATE OR ALTER PROCEDURE dbo.AllegroOffers_UpsertDetails
    @Id NVARCHAR(255),
    @Account INT,
    @Name NVARCHAR(255),
    @CategoryId INT,
    @Price DECIMAL(18, 2),
    @Stock INT,
    @Status NVARCHAR(50),
    @DeliveryName NVARCHAR(255) = NULL,
    @ExternalId NVARCHAR(255) = NULL,
    @Weight DECIMAL(18, 2),
    @Images NVARCHAR(MAX) = NULL,
    @StartingAt DATETIME2,
    @HandlingTime NVARCHAR(255) = NULL,
    @ResponsiblePerson NVARCHAR(255) = NULL,
    @ResponsibleProducer NVARCHAR(255) = NULL
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
            Status = @Status,
            DeliveryName = @DeliveryName,
            ExternalId = @ExternalId,
            Weight = @Weight,
            Images = @Images,
            StartingAt = @StartingAt,
            HandlingTime = @HandlingTime,
            ResponsiblePerson = @ResponsiblePerson,
            ResponsibleProducer = @ResponsibleProducer
    WHEN NOT MATCHED THEN
        INSERT (Id, Account, Name, CategoryId, Price, Stock, Status, DeliveryName, ExternalId, Weight, Images, StartingAt, HandlingTime, ResponsiblePerson, ResponsibleProducer)
        VALUES (@Id, @Account, @Name, @CategoryId, @Price, @Stock, @Status, @DeliveryName, @ExternalId, @Weight, @Images, @StartingAt, @HandlingTime, @ResponsiblePerson, @ResponsibleProducer);
END
GO

CREATE OR ALTER PROCEDURE dbo.AllegroOfferDescriptions_Insert
    @OfferId NVARCHAR(255),
    @Type NVARCHAR(50),
    @Content NVARCHAR(MAX),
    @SectionId INT
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO AllegroOfferDescriptions (OfferId, Type, Content, SectionId)
    VALUES (@OfferId, @Type, @Content, @SectionId);
END
GO

CREATE OR ALTER PROCEDURE dbo.AllegroOfferAttributes_Insert
    @OfferId NVARCHAR(255),
    @AttributeId NVARCHAR(255),
    @Type NVARCHAR(50),
    @ValuesJson NVARCHAR(MAX),
    @ValuesIdsJson NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO AllegroOfferAttributes (OfferId, AttributeId, Type, ValuesJson, ValuesIdsJson)
    VALUES (@OfferId, @AttributeId, @Type, @ValuesJson, @ValuesIdsJson);
END
GO

CREATE OR ALTER PROCEDURE dbo.AllegroOffers_GetWithoutDetails
AS
BEGIN
    SET NOCOUNT ON;

    SELECT * FROM AllegroOffers o
    WHERE NOT EXISTS (SELECT 1 FROM AllegroOfferDescriptions d WHERE d.OfferId = o.Id)
      AND Status = 'ACTIVE'
    ORDER BY StartingAt DESC;
END
GO


CREATE OR ALTER PROCEDURE dbo.Products_GetToUpload
    @MinProductStock INT,
    @MinProductPrice DECIMAL(15,4),
    @IntegrationCompany INT,
    @Account INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        p.Id,
        p.Code,
        p.Name,
        p.Description,
        p.Ean,
        p.Weight,
        p.Fits,
        p.SupplierName,
        p.InStock,
        p.Unit,
        p.CurrencyPrice,
        p.PriceNet,
        p.PriceGross,
        p.DefaultAllegroCategory,
        p.Package,
        p.CreatedDate,
        p.UpdatedDate,
        p.Substitutes,
        p.AllegroId,
        p.DeliveryType,
        ps.Id,
        ps.ProductId,
        ps.Name,
        ps.Value,
        ps.UnitName,
        pp.Id,
        pp.ProductId,
        pp.CategoryParameterId,
        cp.Name,
        pp.Value,
        pp.IsForProduct,
        ap.Id,
        ap.ApplicationId,
        ap.Name,
        ap.ParentID,
        ap.ProductId,
        pack.Id,
        pack.PackEan,
        pack.PackGrossWeight,
        pack.PackNettWeight,
        pack.PackQty,
        pack.PackRequired,
        pack.PackUnit,
        pack.ProductId
    FROM Products p
    LEFT JOIN ProductSpecifications ps ON ps.ProductId = p.Id
    JOIN ProductParameters pp ON pp.ProductId = p.Id
    JOIN CategoryParameters cp ON cp.Id = pp.CategoryParameterId
    JOIN ProductCategories rc ON rc.ProductId = p.Id
    LEFT JOIN ProductApplications ap ON ap.ProductId = p.Id
    LEFT JOIN ProductPackages pack ON pack.ProductId = p.Id
    LEFT JOIN AllegroOffers ao ON ao.ExternalId = p.Code AND ao.Account = @Account
    WHERE p.InStock >= @MinProductStock AND p.PriceNet >= @MinProductPrice
      AND NULLIF(p.DefaultAllegroCategory, 0) IS NOT NULL
      AND ao.Id IS NULL
      AND IntegrationCompany = @IntegrationCompany
    ORDER BY p.Id;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[AllegroOffers_GetOffersToUpdate]
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
        DeliveryType INT NULL,
        CreatedDate DATETIME2 NOT NULL,
        UpdatedDate DATETIME2 NOT NULL
    );

    INSERT INTO #OffersWithProducts
    (
        OfferId, ExternalId, OfferName, CategoryId, Status, StartingAt, DeliveryName,
        ProductId, AllegroId, Code, ProductName, Description, Ean, Weight, Fits, SupplierName,
        Substitutes, InStock, Unit, CurrencyPrice, PriceNet, PriceGross, DefaultAllegroCategory, DeliveryType,
        Package, CreatedDate, UpdatedDate
    )
    SELECT
        ao.Id, ao.ExternalId, ao.Name, ao.CategoryId, ao.Status, ao.StartingAt, ao.DeliveryName,
        p.Id, p.AllegroId, p.Code, p.Name, p.Description,
        p.Ean, p.Weight, p.Fits, p.SupplierName, p.Substitutes, p.InStock, p.Unit,
        p.CurrencyPrice, p.PriceNet, p.PriceGross, p.DefaultAllegroCategory, p.Package, p.DeliveryType,
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
        DeliveryType,
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

    SELECT pa.*
    FROM ProductApplications pa
    WHERE pa.ProductId IN (SELECT DISTINCT ProductId FROM #OffersWithProducts);

    SELECT pack.*
    FROM ProductPackages pack
    WHERE pack.ProductId IN (SELECT DISTINCT ProductId FROM #OffersWithProducts);

    SELECT param.Id,
    param.ProductId,
    param.CategoryParameterId,
    param.Value,
    param.IsForProduct,
    catParam.Name
    FROM ProductParameters param
    join dbo.CategoryParameters catParam on param.CategoryParameterId = catParam.Id
    WHERE param.ProductId IN (SELECT DISTINCT ProductId FROM #OffersWithProducts);
END
GO