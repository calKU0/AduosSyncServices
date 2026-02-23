IF EXISTS (
    SELECT 1 
    FROM sys.columns 
    WHERE Name = N'InStock' 
      AND Object_ID = Object_ID(N'dbo.Products')
)
BEGIN
    -- Set existing NULLs to 0 to avoid constraint issues
    UPDATE dbo.Products
    SET InStock = 0
    WHERE InStock IS NULL;

    -- Alter column to allow NULL and set default
    ALTER TABLE dbo.Products
    DROP CONSTRAINT IF EXISTS DF_Products_InStock;

    ALTER TABLE dbo.Products
    ADD CONSTRAINT DF_Products_InStock
        DEFAULT 0 FOR InStock;

    ALTER TABLE dbo.Products
    ALTER COLUMN InStock FLOAT NULL;
END
GO