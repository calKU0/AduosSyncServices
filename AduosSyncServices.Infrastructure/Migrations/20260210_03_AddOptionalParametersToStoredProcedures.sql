CREATE OR ALTER PROCEDURE [dbo].[Products_Upsert]
    @Code NVARCHAR(255),
    @CustomerCode NVARCHAR(255) = NULL,
    @Name NVARCHAR(255),
    @SupplierName NVARCHAR(255) = NULL,
    @SupplierLogo NVARCHAR(255) = NULL,
    @DeliveryType INT = 0,
    @Description NVARCHAR(MAX),
    @Ean NVARCHAR(50),
    @Weight FLOAT,
    @InStock INT = NULL,
    @Fits NVARCHAR(MAX) = NULL,
    @Substitutes NVARCHAR(MAX) = NULL,
    @Unit NVARCHAR(50),
    @Currency NVARCHAR(50),
    @PriceNet DECIMAL(18, 2),
    @PriceGross DECIMAL(18, 2),
    @Package DECIMAL(18, 2) = NULL,
    @IntegrationCompany NVARCHAR(50),
    @IntegrationId INT = NULL
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
            CustomerCode = NULLIF(@CustomerCode,''),
            Description = @Description,
            IntegrationCompany = @IntegrationCompany,
            DeliveryType = @DeliveryType,
            Ean = @Ean,
            InStock = ISNULL(@InStock, 0),
            SupplierName = ISNULL(NULLIF(@SupplierName,''), SupplierName),
            SupplierLogo = ISNULL(NULLIF(@SupplierLogo,''), SupplierLogo),
            Weight = @Weight,
            Fits = ISNULL(NULLIF(@Fits,''), Fits),
            Substitutes = ISNULL(NULLIF(@Substitutes,''), Substitutes),
            Unit = @Unit,
            CurrencyPrice = @Currency,
            PriceNet = @PriceNet,
            PriceGross = @PriceGross,
            Package = ISNULL(@Package, Package),
            IntegrationId = @IntegrationId,
            UpdatedDate = SYSUTCDATETIME()
    WHEN NOT MATCHED THEN
        INSERT (Code, CustomerCode, SupplierName, SupplierLogo, Name, Description, Ean, Weight, Fits, Substitutes, Unit, CurrencyPrice, DeliveryType,
                PriceNet, PriceGross, InStock, Package, CreatedDate, UpdatedDate, IntegrationCompany, IntegrationId)
        VALUES (@Code, NULLIF(@CustomerCode,''), NULLIF(@SupplierName,''), NULLIF(@SupplierLogo,''), @Name, @Description, @Ean, @Weight, NULLIF(@Fits,''), NULLIF(@Substitutes,''), @Unit, @Currency, @DeliveryType,
                @PriceNet, @PriceGross, ISNULL(@InStock, 0), @Package, SYSUTCDATETIME(), SYSUTCDATETIME(), @IntegrationCompany, @IntegrationId)
    OUTPUT inserted.Id;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[Products_GetToUpdateParameters]
@IntegrationCompany INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT *
    FROM Products p
    LEFT JOIN ProductApplications pa ON pa.ProductId = p.Id
    LEFT JOIN ProductSpecifications ps ON ps.ProductId = p.Id
    WHERE NOT EXISTS (
        SELECT 1
        FROM ProductParameters pp
        WHERE pp.ProductId = p.Id
    )
      AND IntegrationCompany = @IntegrationCompany AND (p.DefaultAllegroCategory != 0 or p.AllegroId != 0)
    ORDER BY p.Id;
END
GO;


CREATE OR ALTER PROCEDURE [dbo].[CategoryParameters_GetByCategoryId]
    @CategoryId INT,
    @OnlyForOffers INT = 1
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        cp.*,
        cpv.Id AS ValueId, cpv.Value, cpv.CategoryParameterId
    FROM CategoryParameters cp
    LEFT JOIN CategoryParameterValues cpv ON cp.Id = cpv.CategoryParameterId
    WHERE cp.CategoryId = @CategoryId
      AND (
            @OnlyForOffers <> 1
            OR cp.DescribesProduct = 0
          );
END
GO

CREATE OR ALTER  PROCEDURE [dbo].[ProductParameters_Update]
    @ProductId INT,
    @ParameterId INT,
    @Value NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @catParamId INT = (SELECT cp.Id
                  FROM CategoryParameters cp
                  INNER JOIN Products p ON p.DefaultAllegroCategory = cp.CategoryId
                  WHERE p.Id = @ProductId AND cp.ParameterId = @ParameterId)

    UPDATE ProductParameters
            SET Value = @Value
            WHERE ProductId = @ProductId AND CategoryParameterId = @catParamId
END
GO