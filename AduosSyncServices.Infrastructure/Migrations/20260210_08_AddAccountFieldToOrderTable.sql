IF NOT EXISTS (
    SELECT 1 
    FROM sys.columns 
    WHERE Name = N'Account' 
      AND Object_ID = Object_ID(N'dbo.AllegroOrders')
)
BEGIN
    ALTER TABLE dbo.AllegroOrders
    ADD Account INT NOT NULL DEFAULT(1);
END

IF NOT EXISTS (
    SELECT 1 
    FROM sys.columns 
    WHERE Name = N'IntegrationCompany' 
      AND Object_ID = Object_ID(N'dbo.AllegroOrders')
)
BEGIN
    ALTER TABLE dbo.AllegroOrders
    ADD IntegrationCompany INT NOT NULL DEFAULT(2);
END