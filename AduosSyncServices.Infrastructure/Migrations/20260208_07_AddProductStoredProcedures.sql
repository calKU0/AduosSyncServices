CREATE OR ALTER PROCEDURE dbo.Products_GetAll
@IntegrationCompany INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT * FROM Products WHERE IntegrationCompany = @IntegrationCompany;
END
GO

CREATE OR ALTER PROCEDURE dbo.Products_GetWithoutAllegroId
@IntegrationCompany INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT *
    FROM Products
    WHERE AllegroId IS NULL AND IntegrationCompany = @IntegrationCompany;
END
GO

CREATE OR ALTER PROCEDURE dbo.Products_GetToUpdateParameters
@IntegrationCompany INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        p.Id,
        p.Code,
        p.Name,
        p.Description,
        p.Ean,
        p.Weight,
        p.Fits,
        p.SupplierName,
        p.InStock,
        p.Unit,
        p.CurrencyPrice,
        p.PriceNet,
        p.PriceGross,
        p.DefaultAllegroCategory,
        p.Package,
        p.CreatedDate,
        p.UpdatedDate,
        ps.Id,
        ps.ProductId,
        ps.Name,
        ps.Value,
        ps.UnitName
    FROM Products p
    LEFT JOIN ProductSpecifications ps ON ps.ProductId = p.Id
    WHERE NOT EXISTS (
        SELECT 1
        FROM ProductParameters pp
        WHERE pp.ProductId = p.Id
    )
      AND IntegrationCompany = @IntegrationCompany
    ORDER BY p.Id;
END
GO

CREATE OR ALTER PROCEDURE dbo.Products_GetToUpload
    @MinProductStock INT,
    @IntegrationCompany INT,
    @Account INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        p.Id,
        p.Code,
        p.Name,
        p.Description,
        p.Ean,
        p.Weight,
        p.Fits,
        p.SupplierName,
        p.InStock,
        p.Unit,
        p.CurrencyPrice,
        p.PriceNet,
        p.PriceGross,
        p.DefaultAllegroCategory,
        p.Package,
        p.CreatedDate,
        p.UpdatedDate,
        p.Substitutes,
        p.AllegroId,
        ps.Id,
        ps.ProductId,
        ps.Name,
        ps.Value,
        ps.UnitName,
        pp.Id,
        pp.ProductId,
        pp.CategoryParameterId,
        cp.Name,
        pp.Value,
        pp.IsForProduct
    FROM Products p
    LEFT JOIN ProductSpecifications ps ON ps.ProductId = p.Id
    JOIN ProductParameters pp ON pp.ProductId = p.Id
    JOIN CategoryParameters cp ON cp.Id = pp.CategoryParameterId
    LEFT JOIN AllegroOffers ao ON ao.ExternalId = p.Code AND ao.Account = @Account
    WHERE p.InStock >= @MinProductStock
      AND NULLIF(p.DefaultAllegroCategory, 0) IS NOT NULL
      AND ao.Id IS NULL
      AND IntegrationCompany = @IntegrationCompany
    ORDER BY p.Id;
END
GO

CREATE OR ALTER PROCEDURE dbo.Products_GetWithoutDefaultCategory
@IntegrationCompany INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        p.Id,
        p.Code,
        p.Name,
        p.Description,
        p.Ean,
        p.Weight,
        p.Fits,
        p.SupplierName,
        p.InStock,
        p.Unit,
        p.CurrencyPrice,
        p.PriceNet,
        p.PriceGross,
        p.DefaultAllegroCategory,
        p.Package,
        p.CreatedDate,
        p.UpdatedDate,
        ps.Id,
        ps.ProductId,
        ps.Name,
        ps.Value,
        ps.UnitName
    FROM Products p
    LEFT JOIN ProductSpecifications ps ON ps.ProductId = p.Id
    JOIN dbo.ProductCategories pc ON p.Id = pc.ProductId 
    WHERE NULLIF(p.DefaultAllegroCategory, 0) IS NULL
      AND IntegrationCompany = @IntegrationCompany
    ORDER BY p.Id;
END
GO

CREATE OR ALTER PROCEDURE dbo.Products_UpdateDefaultCategoryById
    @ProductId INT,
    @CategoryId INT
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE Products
    SET DefaultAllegroCategory = @CategoryId,
        UpdatedDate = SYSUTCDATETIME()
    WHERE Id = @ProductId;
END
GO

CREATE OR ALTER PROCEDURE dbo.Products_UpdateDefaultCategoryByCode
    @ProductCode NVARCHAR(255),
    @CategoryId INT
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE Products
    SET DefaultAllegroCategory = @CategoryId,
        UpdatedDate = SYSUTCDATETIME()
    WHERE Code = @ProductCode;
END
GO

CREATE OR ALTER PROCEDURE dbo.Products_UpdateAllegroId
    @ProductId INT,
    @AllegroId NVARCHAR(255),
    @CategoryId INT
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE Products
    SET AllegroId = @AllegroId,
        DefaultAllegroCategory = @CategoryId
    WHERE Id = @ProductId;
END
GO

CREATE OR ALTER PROCEDURE dbo.Products_UpdateStockByCode
    @ProductCode NVARCHAR(255),
    @Stock INT
AS
BEGIN
    UPDATE Products
    SET InStock = @Stock,
        UpdatedDate = SYSUTCDATETIME()
    WHERE Code = @ProductCode;
END
GO

CREATE OR ALTER PROCEDURE dbo.Products_Upsert
    @Code NVARCHAR(255),
    @Name NVARCHAR(255),
    @Description NVARCHAR(MAX),
    @Ean NVARCHAR(50),
    @Weight FLOAT,
    @Fits NVARCHAR(MAX) = NULL,
    @Substitutes NVARCHAR(MAX) = NULL,
    @Unit NVARCHAR(50),
    @Currency NVARCHAR(50),
    @PriceNet DECIMAL(18, 2),
    @PriceGross DECIMAL(18, 2),
    @Package DECIMAL(18, 2),
    @IntegrationCompany NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;

    MERGE Products AS target
    USING (SELECT @Code AS Code) AS source
    ON target.Code = source.Code
    WHEN MATCHED THEN
        UPDATE SET
            Name = LEFT(@Name,
                CASE
                    WHEN LEN(@Name) <= 75 THEN LEN(@Name)
                    ELSE 75 - CHARINDEX(' ', REVERSE(LEFT(@Name, 75))) + 1
                END),
            Description = @Description,
            IntegrationCompany = @IntegrationCompany,
            Ean = @Ean,
            Weight = @Weight,
            Fits = NULLIF(@Fits,''),
            Substitutes = NULLIF(@Substitutes,''),
            Unit = @Unit,
            CurrencyPrice = @Currency,
            PriceNet = @PriceNet,
            PriceGross = @PriceGross,
            Package = @Package,
            UpdatedDate = SYSUTCDATETIME()
    WHEN NOT MATCHED THEN
        INSERT (Code, Name, Description, Ean, Weight, Fits, Substitutes, Unit, CurrencyPrice,
                PriceNet, PriceGross, Package, CreatedDate, UpdatedDate, IntegrationCompany)
        VALUES (@Code, @Name, @Description, @Ean, @Weight, NULLIF(@Fits,''), NULLIF(@Substitutes,''), @Unit, @Currency,
                @PriceNet, @PriceGross, @Package, SYSUTCDATETIME(), SYSUTCDATETIME(), @IntegrationCompany)
    OUTPUT inserted.Id;
END
GO

CREATE OR ALTER PROCEDURE dbo.ProductSpecifications_DeleteByProductId
    @ProductId INT
AS
BEGIN
    SET NOCOUNT ON;

    DELETE FROM ProductSpecifications
    WHERE ProductId = @ProductId;
END
GO

CREATE OR ALTER PROCEDURE dbo.ProductSpecifications_Insert
    @ProductId INT,
    @Name NVARCHAR(255),
    @Value NVARCHAR(MAX),
    @UnitName NVARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO ProductSpecifications (ProductId, Name, Value, UnitName)
    VALUES (@ProductId, @Name, @Value, @UnitName);
END
GO

CREATE OR ALTER PROCEDURE dbo.ProductCategories_DeleteByProductId
    @ProductId INT
AS
BEGIN
    SET NOCOUNT ON;

    DELETE FROM ProductCategories
    WHERE ProductId = @ProductId;
END
GO

CREATE OR ALTER PROCEDURE dbo.ProductCategories_Insert
    @ProductId INT,
    @Name NVARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO ProductCategories (ProductId, Name)
    VALUES (@ProductId, @Name);
END
GO
