CREATE OR ALTER PROCEDURE dbo.AllegroImages_DeleteByProductId
    @ProductId INT,
    @Account INT
AS
BEGIN
    SET NOCOUNT ON;

    DELETE FROM dbo.AllegroImages
    WHERE ProductId = @ProductId
      AND Account = @Account;

    SELECT @@ROWCOUNT AS DeletedCount;
END
GO
