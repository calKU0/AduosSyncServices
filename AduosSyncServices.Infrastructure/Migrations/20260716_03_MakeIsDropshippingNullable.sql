-- IsDropshipping used to default to 0, which incorrectly implied "not dropshipping" for orders
-- that simply hadn't been placed with the supplier yet. It is now nullable: NULL means "not yet
-- known" (order not placed), and only PlaceHeadquartersOrderAsync/PlaceCustomerOrdersAsync ever
-- set it to an explicit true/false, at the moment the order is actually placed.
IF EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE Name = N'IsDropshipping'
      AND Object_ID = OBJECT_ID(N'dbo.AllegroOrders')
      AND is_nullable = 0
)
BEGIN
    DECLARE @ConstraintName NVARCHAR(200);
    SELECT @ConstraintName = dc.name
    FROM sys.default_constraints dc
    JOIN sys.columns c ON c.default_object_id = dc.object_id
    WHERE c.object_id = OBJECT_ID(N'dbo.AllegroOrders') AND c.name = N'IsDropshipping';

    IF @ConstraintName IS NOT NULL
        EXEC('ALTER TABLE dbo.AllegroOrders DROP CONSTRAINT [' + @ConstraintName + ']');

    ALTER TABLE dbo.AllegroOrders ALTER COLUMN IsDropshipping BIT NULL;
END
GO
