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
