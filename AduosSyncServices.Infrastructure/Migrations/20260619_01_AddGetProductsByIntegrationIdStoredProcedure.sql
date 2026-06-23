CREATE OR ALTER PROCEDURE dbo.Products_GetForDetailUpdate
    @Limit INT,
    @IntegrationCompany INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT DISTINCT p.IntegrationId
    FROM Products p
    WHERE NOT EXISTS (SELECT 1 FROM ProductCategories pc WHERE pc.ProductId = p.Id)
      AND IntegrationCompany = @IntegrationCompany
    ORDER BY p.IntegrationId
    OFFSET 0 ROWS FETCH NEXT @Limit ROWS ONLY;
END
GO

CREATE OR ALTER PROCEDURE dbo.Products_GetByIntegrationId
    @IntegrationId INT,
    @IntegrationCompany INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP 1 *
    FROM Products p
    WHERE IntegrationId = @IntegrationId AND IntegrationCompany = @IntegrationCompany 
    ORDER BY p.CreatedDate DESC;
END
GO


CREATE OR ALTER PROCEDURE [dbo].[Products_UpsertBatch]
    @Products dbo.ProductUpsertType READONLY
AS
BEGIN
    SET NOCOUNT ON;

    -------------------------------------------------------------------
    -- STEP 1: Mark conflicting products as OUT OF STOCK
    -- Same IntegrationCompany + IntegrationId but DIFFERENT Code
    -------------------------------------------------------------------
    UPDATE p
    SET p.InStock = 0,
        p.UpdatedDate = SYSUTCDATETIME()
    FROM dbo.Products p
    INNER JOIN @Products s
        ON p.IntegrationCompany = s.IntegrationCompany
       AND p.IntegrationId = s.IntegrationId
       AND p.Code <> s.Code;
    

    -------------------------------------------------------------------
    -- STEP 2: MERGE normal upsert (by Code + IntegrationCompany)
    -------------------------------------------------------------------
    MERGE dbo.Products AS target
    USING
    (
        SELECT
            p.Code,
            LEFT(p.Name,
                CASE
                    WHEN LEN(p.Name) <= 75 THEN LEN(p.Name)
                    ELSE 75 - CHARINDEX(' ', REVERSE(LEFT(p.Name, 75))) + 1
                END
            ) AS Name,
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
    ON target.Code = source.Code
   AND target.IntegrationCompany = source.IntegrationCompany

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
        ISNULL(target.Package, 0) <> ISNULL(source.Package, 0)
    )
    THEN UPDATE SET
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
        UpdatedDate = SYSUTCDATETIME()

    WHEN NOT MATCHED THEN
        INSERT (
            Code, Name, SupplierLogo, SupplierName, Description, CustomerCode, Ean,
            InStock, Weight, Fits, Unit, CurrencyPrice, Substitutes,
            IntegrationCompany, IntegrationId, DeliveryType,
            PriceNet, PriceGross, Package, CreatedDate, UpdatedDate
        )
        VALUES (
            source.Code, source.Name, source.SupplierLogo, source.SupplierName, source.Description, source.CustomerCode, source.Ean,
            source.InStock, source.Weight, source.Fits, source.Unit, source.CurrencyPrice, source.Substitutes,
            source.IntegrationCompany, source.IntegrationId, source.DeliveryType,
            source.PriceNet, source.PriceGross, source.Package,
            SYSUTCDATETIME(), SYSUTCDATETIME()
        );
END
GO