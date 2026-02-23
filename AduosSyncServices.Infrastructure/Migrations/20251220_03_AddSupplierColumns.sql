IF NOT EXISTS (
    SELECT 1 
    FROM sys.columns 
    WHERE Name = N'IntegrationId' 
      AND Object_ID = Object_ID(N'dbo.Products')
)
BEGIN
    ALTER TABLE dbo.Products
    ADD IntegrationId INT NULL;
END


IF NOT EXISTS (
    SELECT 1 
    FROM sys.columns 
    WHERE Name = N'IntegrationCompany' 
      AND Object_ID = Object_ID(N'dbo.Products')
)
BEGIN
    ALTER TABLE dbo.Products
    ADD IntegrationCompany NVARCHAR(50) NULL;
END
