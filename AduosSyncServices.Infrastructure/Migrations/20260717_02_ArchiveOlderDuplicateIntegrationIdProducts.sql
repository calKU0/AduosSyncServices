-- When Gąska changes a product's code, the sync (which matches on Code + IntegrationCompany)
-- inserts a NEW Products row and the old-code row goes stale. Products_ArchiveMissingBySyncRun
-- never catches it, because its IntegrationId is still present in the feed - so both rows stay
-- active with the same IntegrationId forever. This procedure archives every older duplicate
-- (by CreatedDate, tie-broken by Id), keeping only the newest active row per IntegrationId.
-- The archived row then follows the normal lifecycle: its Allegro offer gets ENDED on the next
-- offer update, after which Products_DeleteArchivedProductsWithEndedOffers can remove it.
CREATE OR ALTER PROCEDURE dbo.Products_ArchiveOlderDuplicates
    @IntegrationCompany INT
AS
BEGIN
    SET NOCOUNT ON;

    ;WITH Ranked AS
    (
        SELECT Id,
               ROW_NUMBER() OVER (
                   PARTITION BY IntegrationId
                   ORDER BY CreatedDate DESC, Id DESC
               ) AS rn
        FROM dbo.Products
        WHERE IntegrationCompany = @IntegrationCompany
          AND IsArchived = 0
          AND IntegrationId IS NOT NULL
          AND IntegrationId <> 0
    )
    UPDATE p
    SET p.IsArchived = 1,
        p.UpdatedDate = SYSUTCDATETIME()
    FROM dbo.Products p
    INNER JOIN Ranked r ON r.Id = p.Id
    WHERE r.rn > 1;

    SELECT @@ROWCOUNT;
END
GO
