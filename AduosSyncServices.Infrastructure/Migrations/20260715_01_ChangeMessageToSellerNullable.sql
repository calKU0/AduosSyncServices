IF EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.AllegroOrders')
      AND name = 'MessageToSeller'
      AND is_nullable = 0
)
BEGIN
    ALTER TABLE dbo.AllegroOrders
    ALTER COLUMN MessageToSeller VARCHAR(MAX) NULL;
END