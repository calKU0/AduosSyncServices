IF EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'Weight' AND Object_ID = Object_ID(N'dbo.Products'))
BEGIN
    -- Weight
    ALTER TABLE dbo.Products
    DROP CONSTRAINT IF EXISTS DF_Products_Weight;

    ALTER TABLE dbo.Products
    ADD CONSTRAINT DF_Products_Weight
        DEFAULT 0 FOR Weight;
END

IF EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'PriceNet' AND Object_ID = Object_ID(N'dbo.Products'))
BEGIN
    -- PriceNet
    ALTER TABLE dbo.Products
    DROP CONSTRAINT IF EXISTS DF_Products_PriceNet;

    ALTER TABLE dbo.Products
    ADD CONSTRAINT DF_Products_PriceNet
        DEFAULT 0 FOR PriceNet;
END

IF EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'PriceGross' AND Object_ID = Object_ID(N'dbo.Products'))
BEGIN
    -- PriceGross
    ALTER TABLE dbo.Products
    DROP CONSTRAINT IF EXISTS DF_Products_PriceGross;

    ALTER TABLE dbo.Products
    ADD CONSTRAINT DF_Products_PriceGross
        DEFAULT 0 FOR PriceGross;
END

IF EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'Package' AND Object_ID = Object_ID(N'dbo.Products'))
BEGIN
    -- Package
    ALTER TABLE dbo.Products
    DROP CONSTRAINT IF EXISTS DF_Products_Package;

    ALTER TABLE dbo.Products
    ADD CONSTRAINT DF_Products_Package
        DEFAULT 1 FOR Package;
END
GO

IF EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'DefaultAllegroCategory' AND Object_ID = Object_ID(N'dbo.Products'))
BEGIN
    -- Package
    ALTER TABLE dbo.Products
    DROP CONSTRAINT IF EXISTS DF_Products_DefaultAllegroCategory;

    ALTER TABLE dbo.Products
    ADD CONSTRAINT DF_Products_DefaultAllegroCategory
        DEFAULT 0 FOR DefaultAllegroCategory;
END
GO