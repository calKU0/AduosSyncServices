IF NOT EXISTS (
    SELECT 1 FROM sys.tables WHERE name = 'Products' AND schema_id = SCHEMA_ID('dbo')
)
BEGIN
    CREATE TABLE [dbo].[Products]
    (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [Code] NVARCHAR(255) NOT NULL,
        [Name] NVARCHAR(255) NOT NULL,
        [Description] NVARCHAR(MAX) NULL,
        [Ean] NVARCHAR(50) NULL,
        [Weight] FLOAT NOT NULL,
        [Fits] NVARCHAR(MAX) NULL,
        [SupplierName] NVARCHAR(255) NULL,
        [InStock] FLOAT NOT NULL,
        [Unit] NVARCHAR(50) NULL,
        [CurrencyPrice] NVARCHAR(50) NULL,
        [PriceNet] DECIMAL(18,2) NOT NULL,
        [PriceGross] DECIMAL(18,2) NOT NULL,
        [DefaultAllegroCategory] INT NOT NULL,
        [Package] DECIMAL(18,2) NOT NULL,
        [CreatedDate] DATETIME2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedDate] DATETIME2 NOT NULL DEFAULT (SYSUTCDATETIME())
    );
END
GO


IF NOT EXISTS (
    SELECT 1 FROM sys.tables WHERE name = 'ProductSpecifications' AND schema_id = SCHEMA_ID('dbo')
)
BEGIN
    CREATE TABLE [dbo].[ProductSpecifications]
    (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [ProductId] INT NOT NULL,
        [Name] NVARCHAR(255) NOT NULL,
        [Value] NVARCHAR(MAX) NULL,
        [UnitName] NVARCHAR(255) NULL
    );
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.tables WHERE name = 'ProductParameters' AND schema_id = SCHEMA_ID('dbo')
)
BEGIN
    CREATE TABLE [dbo].[ProductParameters]
    (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [ProductId] INT NOT NULL,
        [CategoryParameterId] INT NOT NULL,
        [Value] NVARCHAR(MAX) NULL,
        [IsForProduct] BIT NOT NULL DEFAULT(0)
    );
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_ProductSpecifications_Products'
)
BEGIN
    ALTER TABLE [dbo].[ProductSpecifications]
    ADD CONSTRAINT FK_ProductSpecifications_Products
        FOREIGN KEY ([ProductId])
        REFERENCES [dbo].[Products]([Id])
        ON DELETE CASCADE;
END
GO