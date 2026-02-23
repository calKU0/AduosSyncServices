IF NOT EXISTS (
    SELECT 1 
    FROM sys.columns 
    WHERE Name = N'Substitutes' 
      AND Object_ID = Object_ID(N'dbo.Products')
)
BEGIN
    ALTER TABLE dbo.Products
    ADD Substitutes NVARCHAR(MAX) NULL;
END
