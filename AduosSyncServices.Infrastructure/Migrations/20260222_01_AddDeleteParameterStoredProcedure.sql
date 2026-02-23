CREATE OR ALTER PROCEDURE [dbo].[ProductParameters_DeleteByParameterName]
    @ProductId INT,
    @ParameterName VARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;

    DELETE FROM pp
    FROM dbo.ProductParameters pp
    JOIN dbo.CategoryParameters cp on cp.Id = pp.Id

    WHERE pp.ProductId = @ProductId AND cp.Name = @ParameterName

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
        Substitutes, InStock, Unit, CurrencyPrice, PriceNet, PriceGross, DefaultAllegroCategory,
        Package, DeliveryType, CreatedDate, UpdatedDate
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

    SELECT DISTINCT ai.*
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