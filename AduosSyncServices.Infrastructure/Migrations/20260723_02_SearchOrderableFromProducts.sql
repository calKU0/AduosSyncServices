-- Manual-order product search now runs against the Products table alone - a product no longer needs
-- an active Allegro offer to be orderable. The sale price is no longer returned either (the UI
-- computes it as net price x the products-service margin), and thumbnails come straight from the
-- products-service image folder on disk, not the database. DeliveryType is returned for display
-- (0 standard / 1 bulky / 2 custom).
CREATE OR ALTER PROCEDURE dbo.Products_SearchOrderable
    @SearchTerm NVARCHAR(200),
    @IntegrationCompany INT,
    @Account INT,
    @MaxResults INT = 50,
    @Offset INT = 0
AS
BEGIN
    SET NOCOUNT ON;

    -- Treat LIKE wildcards typed by the user literally ("%", "_", "[").
    DECLARE @EscapedTerm NVARCHAR(220) = REPLACE(REPLACE(REPLACE(REPLACE(@SearchTerm,
        '\', '\\'),
        '%', '\%'),
        '_', '\_'),
        '[', '\[');

    SELECT
        p.Id AS ProductId,
        p.Code,
        p.Name,
        p.InStock,
        p.Unit,
        p.PriceNet AS PurchasePriceNet,
        p.PriceGross AS PurchasePriceGross,
        p.DeliveryType,
        ISNULL(
            (SELECT TOP 1 pack.PackQty FROM dbo.ProductPackages pack WHERE pack.ProductId = p.Id AND pack.PackRequired = 1),
            1
        ) AS PackQty
    FROM dbo.Products p
    WHERE p.IntegrationCompany = @IntegrationCompany
      AND p.IsArchived = 0
      AND (p.Name LIKE '%' + @EscapedTerm + '%' ESCAPE '\' OR p.Code LIKE '%' + @EscapedTerm + '%' ESCAPE '\')
    ORDER BY p.Name
    OFFSET @Offset ROWS FETCH NEXT @MaxResults ROWS ONLY;
END
