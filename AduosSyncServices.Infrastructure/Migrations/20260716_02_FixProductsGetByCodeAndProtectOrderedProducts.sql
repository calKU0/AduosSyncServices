-- Products_GetByCode returned Products.IntegrationId aliased as "ProductId" instead of the actual
-- Products.Id primary key. AllegroOrderItems.ProductId has an FK to Products.Id (NOT NULL), so every
-- order-item upsert was writing a Gąska IntegrationId into that FK column - virtually guaranteed to
-- violate the constraint, since Id and IntegrationId never coincide (confirmed against live data:
-- 0 of 45 existing products have Id = IntegrationId). This is what SaveAllegroOrder was hitting.
CREATE OR ALTER PROCEDURE dbo.Products_GetByCode
    @Code NVARCHAR(255),
    @IntegrationCompany INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT Id AS ProductId, Code
    FROM Products
    WHERE Code = @Code AND IntegrationCompany = @IntegrationCompany;
END
GO

-- Defensive follow-up to the fix above: AllegroOrderItems.ProductId is a NOT NULL FK to Products.Id,
-- so a product that any historical order line still references must never be hard-deleted - doing so
-- would permanently break re-syncing that order (SaveAllegroOrder throws "Product not found" the
-- moment Products_GetByCode can't resolve the order item's product code) and/or reintroduce the same
-- dangling-FK failure on the next AllegroOrderItems_Upsert touching that row.
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
          AND NOT EXISTS (SELECT 1 FROM dbo.AllegroOrderItems aoi WHERE aoi.ProductId = p.Id)
        ORDER BY p.Id
    )
    DELETE p
    FROM dbo.Products p
    INNER JOIN ToDelete d ON d.Id = p.Id;

    SELECT @@ROWCOUNT;
END
GO
