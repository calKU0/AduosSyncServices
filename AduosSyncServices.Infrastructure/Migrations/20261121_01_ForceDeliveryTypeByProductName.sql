-- Wymuszony typ dostawy na podstawie nazwy produktu (tylko gdy Gąska przysłała DeliveryType = 0):
--   nazwa zawiera "Spirala", "Klepisko" lub "Sito" -> DeliveryType = 2 (dostawa niestandardowa)
--   nazwa zawiera "Tłumik"                          -> DeliveryType = 1 (gabaryt)
-- Porównanie bez rozróżniania wielkości liter (collation bazy). Reguła działa w obu procedurach upsertu,
-- a poniższy UPDATE od razu poprawia produkty, które już są w bazie.

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
            CASE
                WHEN ISNULL(p.DeliveryType, 0) = 0 AND (p.Name LIKE N'%spirala%' OR p.Name LIKE N'%klepisko%' OR p.Name LIKE N'%sito%') THEN 2
                WHEN ISNULL(p.DeliveryType, 0) = 0 AND p.Name LIKE N'%t' + NCHAR(322) + N'umik%' THEN 1
                ELSE p.DeliveryType
            END AS DeliveryType,
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
            CASE
                WHEN ISNULL(@DeliveryType, 0) = 0 AND (@Name LIKE N'%spirala%' OR @Name LIKE N'%klepisko%' OR @Name LIKE N'%sito%') THEN 2
                WHEN ISNULL(@DeliveryType, 0) = 0 AND @Name LIKE N'%t' + NCHAR(322) + N'umik%' THEN 1
                ELSE @DeliveryType
            END AS DeliveryType,
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

UPDATE dbo.Products
SET DeliveryType = CASE
        WHEN Name LIKE N'%spirala%' OR Name LIKE N'%klepisko%' OR Name LIKE N'%sito%' THEN 2
        ELSE 1
    END,
    UpdatedDate = SYSUTCDATETIME()
WHERE ISNULL(DeliveryType, 0) = 0
  AND (Name LIKE N'%spirala%' OR Name LIKE N'%klepisko%' OR Name LIKE N'%sito%' OR Name LIKE N'%t' + NCHAR(322) + N'umik%');
