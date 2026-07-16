-- Products missing from a Gąska sync are now archived (IsArchived = 1) instead of deleted outright.
-- A product only gets hard-deleted once it's archived AND its Allegro offer (if any) has Status = 'ENDED'.

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.Products') AND name = 'IsArchived'
)
BEGIN
    ALTER TABLE dbo.Products ADD IsArchived BIT NOT NULL CONSTRAINT DF_Products_IsArchived DEFAULT (0);
END
GO

-- Same batching pattern as Products_DeleteMissingBySyncRun, but archives instead of deleting.
-- Filters on IsArchived = 0 so already-archived rows drop out of the candidate set on the next
-- batch iteration - without that guard, TOP(@BatchSize) would keep re-selecting the same rows
-- forever since UPDATE (unlike DELETE) doesn't shrink the pool.
CREATE OR ALTER PROCEDURE dbo.Products_ArchiveMissingBySyncRun
    @SyncRunId UNIQUEIDENTIFIER,
    @IntegrationCompany INT,
    @BatchSize INT = 10000
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (
        SELECT 1
        FROM dbo.ProductSyncStaging s
        WHERE s.SyncRunId = @SyncRunId
          AND s.IntegrationCompany = @IntegrationCompany
    )
    BEGIN
        SELECT 0;
        RETURN;
    END

    ;WITH ToArchive AS
    (
        SELECT TOP (@BatchSize) p.Id
        FROM dbo.Products p
        LEFT JOIN dbo.ProductSyncStaging s
            ON s.SyncRunId = @SyncRunId
           AND s.IntegrationCompany = @IntegrationCompany
           AND s.IntegrationId = p.IntegrationId
        WHERE p.IntegrationCompany = @IntegrationCompany
          AND p.IsArchived = 0
          AND (p.IntegrationId IS NULL OR s.IntegrationId IS NULL)
        ORDER BY p.Id
    )
    UPDATE p
    SET p.IsArchived = 1,
        p.UpdatedDate = SYSUTCDATETIME()
    FROM dbo.Products p
    INNER JOIN ToArchive a ON a.Id = p.Id;

    SELECT @@ROWCOUNT;
END
GO

-- Final cleanup step: an archived product is only actually removed once its Allegro offer (if it
-- ever had one) has reached Status = 'ENDED'. Products with no offer at all are deleted too, since
-- there's nothing left blocking removal.
CREATE OR ALTER PROCEDURE dbo.Products_DeleteArchivedProductsWithEndedOffers
    @IntegrationCompany INT,
    @BatchSize INT = 5000
AS
BEGIN
    SET NOCOUNT ON;

    ;WITH ToDelete AS
    (
        SELECT TOP (@BatchSize) p.Id
        FROM dbo.Products p
        LEFT JOIN dbo.AllegroOffers o ON o.ExternalId = p.Code
        WHERE p.IntegrationCompany = @IntegrationCompany
          AND p.IsArchived = 1
          AND (o.Id IS NULL OR o.Status = 'ENDED')
        ORDER BY p.Id
    )
    DELETE p
    FROM dbo.Products p
    INNER JOIN ToDelete d ON d.Id = p.Id;

    SELECT @@ROWCOUNT;
END
GO

-- Reset IsArchived = 0 whenever a product reappears in a Gąska sync, so a previously-archived
-- product that comes back doesn't stay stuck archived forever.
CREATE OR ALTER PROCEDURE dbo.Products_UpsertBatch
    @Products dbo.ProductUpsertType READONLY
AS
BEGIN
    SET NOCOUNT ON;

    MERGE dbo.Products AS target
    USING
    (
        SELECT
            p.Code,
            LEFT(p.Name,
                CASE
                    WHEN LEN(p.Name) <= 75 THEN LEN(p.Name)
                    ELSE 75 - CHARINDEX(' ', REVERSE(LEFT(p.Name, 75))) + 1
                END) AS Name,
            p.SupplierLogo,
            p.SupplierName,
            p.Description,
            p.CustomerCode,
            p.Ean,
            p.InStock,
            p.Weight,
            NULLIF(p.Fits, '') AS Fits,
            p.Unit,
            p.CurrencyPrice,
            NULLIF(p.Substitutes, '') AS Substitutes,
            p.IntegrationCompany,
            p.IntegrationId,
            p.DeliveryType,
            p.PriceNet,
            p.PriceGross,
            p.Package
        FROM @Products p
    ) AS source
    ON target.Code = source.Code AND target.IntegrationCompany = source.IntegrationCompany
    WHEN MATCHED AND
    (
        ISNULL(target.Name, '') <> ISNULL(source.Name, '') OR
        ISNULL(target.SupplierLogo, '') <> ISNULL(source.SupplierLogo, '') OR
        ISNULL(target.SupplierName, '') <> ISNULL(source.SupplierName, '') OR
        ISNULL(target.Description, '') <> ISNULL(source.Description, '') OR
        ISNULL(target.CustomerCode, '') <> ISNULL(source.CustomerCode, '') OR
        ISNULL(target.Ean, '') <> ISNULL(source.Ean, '') OR
        ISNULL(target.InStock, 0) <> ISNULL(source.InStock, 0) OR
        ISNULL(target.Weight, 0) <> ISNULL(source.Weight, 0) OR
        ISNULL(target.Fits, '') <> ISNULL(source.Fits, '') OR
        ISNULL(target.Unit, '') <> ISNULL(source.Unit, '') OR
        ISNULL(target.CurrencyPrice, '') <> ISNULL(source.CurrencyPrice, '') OR
        ISNULL(target.Substitutes, '') <> ISNULL(source.Substitutes, '') OR
        ISNULL(target.IntegrationId, 0) <> ISNULL(source.IntegrationId, 0) OR
        ISNULL(target.DeliveryType, 0) <> ISNULL(source.DeliveryType, 0) OR
        ISNULL(target.PriceNet, 0) <> ISNULL(source.PriceNet, 0) OR
        ISNULL(target.PriceGross, 0) <> ISNULL(source.PriceGross, 0) OR
        ISNULL(target.Package, 0) <> ISNULL(source.Package, 0) OR
        target.IsArchived <> 0
    ) THEN
        UPDATE SET
            Name = source.Name,
            SupplierLogo = source.SupplierLogo,
            SupplierName = source.SupplierName,
            Description = source.Description,
            CustomerCode = source.CustomerCode,
            Ean = source.Ean,
            InStock = source.InStock,
            Weight = source.Weight,
            Fits = source.Fits,
            Unit = source.Unit,
            CurrencyPrice = source.CurrencyPrice,
            Substitutes = source.Substitutes,
            IntegrationId = source.IntegrationId,
            DeliveryType = source.DeliveryType,
            PriceNet = source.PriceNet,
            PriceGross = source.PriceGross,
            Package = source.Package,
            IsArchived = 0,
            UpdatedDate = SYSUTCDATETIME()
    WHEN NOT MATCHED THEN
        INSERT
        (
            Code, Name, SupplierLogo, SupplierName, Description, CustomerCode, Ean,
            InStock, Weight, Fits, Unit, CurrencyPrice, Substitutes,
            IntegrationCompany, IntegrationId, DeliveryType,
            PriceNet, PriceGross, Package, CreatedDate, UpdatedDate
        )
        VALUES
        (
            source.Code, source.Name, source.SupplierLogo, source.SupplierName, source.Description, source.CustomerCode, source.Ean,
            source.InStock, source.Weight, source.Fits, source.Unit, source.CurrencyPrice, source.Substitutes,
            source.IntegrationCompany, source.IntegrationId, source.DeliveryType,
            source.PriceNet, source.PriceGross, source.Package, SYSUTCDATETIME(), SYSUTCDATETIME()
        );
END
GO

CREATE OR ALTER PROCEDURE dbo.Products_Upsert
    @Code NVARCHAR(255),
    @Name NVARCHAR(255),
    @SupplierLogo NVARCHAR(255) = NULL,
    @SupplierName NVARCHAR(255) = NULL,
    @Description NVARCHAR(MAX) = NULL,
    @CustomerCode NVARCHAR(255) = NULL,
    @Ean NVARCHAR(50) = NULL,
    @InStock FLOAT = 0,
    @Weight FLOAT,
    @Fits NVARCHAR(MAX) = NULL,
    @Unit NVARCHAR(50),
    @Currency NVARCHAR(50) = NULL,
    @Substitutes NVARCHAR(MAX) = NULL,
    @IntegrationCompany INT,
    @IntegrationId INT = NULL,
    @DeliveryType INT = 0,
    @PriceNet DECIMAL(18, 2),
    @PriceGross DECIMAL(18, 2),
    @Package DECIMAL(18, 2)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Result TABLE (Id INT NOT NULL);

    MERGE dbo.Products AS target
    USING
    (
        SELECT
            @Code AS Code,
            LEFT(@Name,
                CASE
                    WHEN LEN(@Name) <= 75 THEN LEN(@Name)
                    ELSE 75 - CHARINDEX(' ', REVERSE(LEFT(@Name, 75))) + 1
                END) AS Name,
            @SupplierLogo AS SupplierLogo,
            @SupplierName AS SupplierName,
            @Description AS Description,
            @CustomerCode AS CustomerCode,
            @Ean AS Ean,
            @InStock AS InStock,
            @Weight AS Weight,
            NULLIF(@Fits, '') AS Fits,
            @Unit AS Unit,
            @Currency AS CurrencyPrice,
            NULLIF(@Substitutes, '') AS Substitutes,
            @IntegrationCompany AS IntegrationCompany,
            @IntegrationId AS IntegrationId,
            @DeliveryType AS DeliveryType,
            @PriceNet AS PriceNet,
            @PriceGross AS PriceGross,
            @Package AS Package
    ) AS source
    ON target.Code = source.Code AND target.IntegrationCompany = source.IntegrationCompany
    WHEN MATCHED AND
    (
        ISNULL(target.Name, '') <> ISNULL(source.Name, '') OR
        ISNULL(target.SupplierLogo, '') <> ISNULL(source.SupplierLogo, '') OR
        ISNULL(target.SupplierName, '') <> ISNULL(source.SupplierName, '') OR
        ISNULL(target.Description, '') <> ISNULL(source.Description, '') OR
        ISNULL(target.CustomerCode, '') <> ISNULL(source.CustomerCode, '') OR
        ISNULL(target.Ean, '') <> ISNULL(source.Ean, '') OR
        ISNULL(target.InStock, 0) <> ISNULL(source.InStock, 0) OR
        ISNULL(target.Weight, 0) <> ISNULL(source.Weight, 0) OR
        ISNULL(target.Fits, '') <> ISNULL(source.Fits, '') OR
        ISNULL(target.Unit, '') <> ISNULL(source.Unit, '') OR
        ISNULL(target.CurrencyPrice, '') <> ISNULL(source.CurrencyPrice, '') OR
        ISNULL(target.Substitutes, '') <> ISNULL(source.Substitutes, '') OR
        ISNULL(target.IntegrationId, 0) <> ISNULL(source.IntegrationId, 0) OR
        ISNULL(target.DeliveryType, 0) <> ISNULL(source.DeliveryType, 0) OR
        ISNULL(target.PriceNet, 0) <> ISNULL(source.PriceNet, 0) OR
        ISNULL(target.PriceGross, 0) <> ISNULL(source.PriceGross, 0) OR
        ISNULL(target.Package, 0) <> ISNULL(source.Package, 0) OR
        target.IsArchived <> 0
    ) THEN
        UPDATE SET
            Name = source.Name,
            SupplierLogo = source.SupplierLogo,
            SupplierName = source.SupplierName,
            Description = source.Description,
            CustomerCode = source.CustomerCode,
            Ean = source.Ean,
            InStock = source.InStock,
            Weight = source.Weight,
            Fits = source.Fits,
            Unit = source.Unit,
            CurrencyPrice = source.CurrencyPrice,
            Substitutes = source.Substitutes,
            IntegrationId = source.IntegrationId,
            DeliveryType = source.DeliveryType,
            PriceNet = source.PriceNet,
            PriceGross = source.PriceGross,
            Package = source.Package,
            IsArchived = 0,
            UpdatedDate = SYSUTCDATETIME()
    WHEN NOT MATCHED THEN
        INSERT
        (
            Code,
            Name,
            SupplierLogo,
            SupplierName,
            Description,
            CustomerCode,
            Ean,
            InStock,
            Weight,
            Fits,
            Unit,
            CurrencyPrice,
            Substitutes,
            IntegrationCompany,
            IntegrationId,
            DeliveryType,
            PriceNet,
            PriceGross,
            Package,
            CreatedDate,
            UpdatedDate
        )
        VALUES
        (
            source.Code,
            source.Name,
            source.SupplierLogo,
            source.SupplierName,
            source.Description,
            source.CustomerCode,
            source.Ean,
            source.InStock,
            source.Weight,
            source.Fits,
            source.Unit,
            source.CurrencyPrice,
            source.Substitutes,
            source.IntegrationCompany,
            source.IntegrationId,
            source.DeliveryType,
            source.PriceNet,
            source.PriceGross,
            source.Package,
            SYSUTCDATETIME(),
            SYSUTCDATETIME()
        )
    OUTPUT inserted.Id INTO @Result(Id);

    IF NOT EXISTS (SELECT 1 FROM @Result)
    BEGIN
        INSERT INTO @Result(Id)
        SELECT TOP (1) Id
        FROM dbo.Products
        WHERE Code = @Code
          AND IntegrationCompany = @IntegrationCompany
        ORDER BY Id DESC;
    END

    SELECT TOP (1) Id FROM @Result;
END
GO

-- Never consider archived products for new Allegro offer creation.
CREATE OR ALTER PROCEDURE dbo.Products_GetToUpload
    @MinProductStock INT,
    @MinProductPrice DECIMAL(15,4),
    @IntegrationCompany INT,
    @Account INT
AS
BEGIN
    SET NOCOUNT ON;

    ;WITH EligibleProducts AS
    (
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
            p.DeliveryType
        FROM dbo.Products p
        WHERE p.IntegrationCompany = @IntegrationCompany
          AND p.IsArchived = 0
          AND p.InStock >= @MinProductStock
          AND p.PriceNet >= @MinProductPrice
          AND NULLIF(p.DefaultAllegroCategory, 0) IS NOT NULL
          AND EXISTS (SELECT 1 FROM dbo.ProductCategories rc WHERE rc.ProductId = p.Id)
          AND NOT EXISTS
          (
              SELECT 1
              FROM dbo.AllegroOffers ao
              WHERE ao.ExternalId = p.Code
                AND ao.Account = @Account
          )
    )
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
    FROM EligibleProducts p
    LEFT JOIN dbo.ProductSpecifications ps ON ps.ProductId = p.Id
    JOIN dbo.ProductParameters pp ON pp.ProductId = p.Id
    JOIN dbo.CategoryParameters cp ON cp.Id = pp.CategoryParameterId
    LEFT JOIN dbo.ProductApplications ap ON ap.ProductId = p.Id
    LEFT JOIN dbo.ProductPackages pack ON pack.ProductId = p.Id
    ORDER BY p.Id;
END
GO

-- Carry IsArchived through to AllegroOfferService.UpdateOffers so it can decide to end the offer
-- instead of patching it normally.
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
        IsArchived BIT NOT NULL,
        CreatedDate DATETIME2 NOT NULL,
        UpdatedDate DATETIME2 NOT NULL
    );

    INSERT INTO #OffersWithProducts
    (
        OfferId, ExternalId, OfferName, CategoryId, Status, StartingAt, DeliveryName,
        ProductId, AllegroId, Code, ProductName, Description, Ean, Weight, Fits, SupplierName,
        Substitutes, InStock, Unit, CurrencyPrice, PriceNet, PriceGross, DefaultAllegroCategory,
        Package, DeliveryType, IsArchived, CreatedDate, UpdatedDate
    )
    SELECT
        ao.Id, ao.ExternalId, ao.Name, ao.CategoryId, ao.Status, ao.StartingAt, ao.DeliveryName,
        p.Id, p.AllegroId, p.Code, p.Name, p.Description,
        p.Ean, p.Weight, p.Fits, p.SupplierName, p.Substitutes, p.InStock, p.Unit,
        p.CurrencyPrice, p.PriceNet, p.PriceGross, p.DefaultAllegroCategory, p.Package, p.DeliveryType,
        p.IsArchived, p.CreatedDate, p.UpdatedDate
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
        IsArchived,
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
GO
