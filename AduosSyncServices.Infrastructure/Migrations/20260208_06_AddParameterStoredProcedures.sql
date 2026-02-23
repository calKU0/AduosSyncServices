CREATE OR ALTER PROCEDURE dbo.ProductParameters_Insert
    @ProductId INT,
    @CategoryParameterId INT,
    @Value NVARCHAR(MAX),
    @IsForProduct BIT
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO ProductParameters
        (ProductId, CategoryParameterId, Value, IsForProduct)
    VALUES
        (@ProductId, @CategoryParameterId, @Value, @IsForProduct);
END
GO

CREATE OR ALTER PROCEDURE dbo.ProductParameters_Update
    @Id INT,
    @CategoryParameterId INT,
    @Value NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE ProductParameters
    SET CategoryParameterId = @CategoryParameterId,
        Value = @Value
    WHERE Id = @Id;
END
GO
