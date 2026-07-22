-- Allows deleting a manually-created order (Source = 1) that hasn't been placed with the supplier
-- yet. Orders synced from Allegro (Source = 0) and any order already sent to Gąska are protected -
-- the WHERE guards enforce this server-side regardless of what the UI allows. AllegroOrderItems has
-- a plain (non-cascading) FK to AllegroOrders, so the child rows must be removed first.
-- Returns the number of orders actually deleted (0 = guard blocked it, 1 = removed).
CREATE OR ALTER PROCEDURE dbo.AllegroOrders_DeleteManual
    @Id INT,
    @IntegrationCompany INT,
    @Account INT
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (
        SELECT 1 FROM dbo.AllegroOrders
        WHERE Id = @Id
          AND IntegrationCompany = @IntegrationCompany
          AND Account = @Account
          AND Source = 1
          AND SentToExternalCompany = 0
    )
    BEGIN
        SELECT 0;
        RETURN;
    END

    BEGIN TRAN;

    DELETE FROM dbo.AllegroOrderItems
    WHERE AllegroOrderId = @Id;

    DELETE FROM dbo.AllegroOrders
    WHERE Id = @Id
      AND IntegrationCompany = @IntegrationCompany
      AND Account = @Account
      AND Source = 1
      AND SentToExternalCompany = 0;

    DECLARE @deleted INT = @@ROWCOUNT;

    COMMIT;

    SELECT @deleted;
END
