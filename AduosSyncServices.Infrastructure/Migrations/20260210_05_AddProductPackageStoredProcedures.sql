CREATE OR ALTER PROCEDURE dbo.ProductPackages_DeleteByProductId
    @ProductId INT
AS
BEGIN
    SET NOCOUNT ON;

    DELETE FROM ProductPackages
    WHERE ProductId = @ProductId;
END
GO

CREATE OR ALTER PROCEDURE dbo.ProductPackages_Insert
    @ProductId INT,
    @PackUnit NVARCHAR(50),
    @PackQty FLOAT,
    @PackNettWeight FLOAT,
    @PackGrossWeight FLOAT,
    @PackEan NVARCHAR(50),
    @PackRequired INT
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO ProductPackages (ProductId, PackUnit, PackQty, PackNettWeight, PackGrossWeight, PackEan, PackRequired)
    VALUES (@ProductId, @PackUnit, @PackQty, @PackNettWeight, @PackGrossWeight, @PackEan, @PackRequired);
END
GO
