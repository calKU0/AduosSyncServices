CREATE OR ALTER PROCEDURE [dbo].[Products_UpdateDefaultCategoryByCode]
    @ProductCode NVARCHAR(255),
    @CategoryId INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @UpdatedProducts TABLE (ProductId INT);

    UPDATE dbo.Products
    SET DefaultAllegroCategory = @CategoryId,
        UpdatedDate = SYSUTCDATETIME()
    OUTPUT INSERTED.Id INTO @UpdatedProducts(ProductId)
    WHERE Code = @ProductCode
      AND (
            DefaultAllegroCategory IS NULL
            OR DefaultAllegroCategory <> @CategoryId
          );

    -- Delete parameters only for actually updated products
    DELETE pp
    FROM dbo.ProductParameters pp
    INNER JOIN @UpdatedProducts u ON pp.ProductId = u.ProductId;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[Products_UpdateDefaultCategoryById]
    @ProductId INT,
    @CategoryId INT
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE Products
    SET DefaultAllegroCategory = @CategoryId,
        UpdatedDate = SYSUTCDATETIME()
    WHERE Id = @ProductId
      AND (
            DefaultAllegroCategory IS NULL
            OR DefaultAllegroCategory <> @CategoryId
          );

    IF @@ROWCOUNT > 0
    BEGIN
        DELETE FROM dbo.ProductParameters
        WHERE ProductId = @ProductId;
    END
END
GO