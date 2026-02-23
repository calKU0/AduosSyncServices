CREATE OR ALTER PROCEDURE dbo.AllegroImages_Add
    @ProductId INT,
    @Url NVARCHAR(2048),
    @Account NVARCHAR(2048)
AS
BEGIN
    SET NOCOUNT ON;

    DELETE FROM dbo.AllegroImages
    WHERE ProductId = @ProductId
      AND Url = @Url
      AND Account = @Account;

    INSERT INTO dbo.AllegroImages (ProductId, Url, Connected, Account)
    VALUES (@ProductId, @Url, 0, @Account);

    SELECT CAST(SCOPE_IDENTITY() AS INT) AS Id;
END
GO

CREATE OR ALTER PROCEDURE dbo.AllegroImages_DeleteNotConnectedByProductId
    @ProductId INT,
    @Account INT
AS
BEGIN
    SET NOCOUNT ON;

    DELETE FROM dbo.AllegroImages
    WHERE ProductId = @ProductId
      AND Connected = 0
      AND Account = @Account;
END
GO

CREATE OR ALTER PROCEDURE dbo.AllegroImages_MarkConnectedByProductId
    @ProductId INT,
    @Account INT
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.AllegroImages
    SET Connected = 1
    WHERE ProductId = @ProductId AND Account = @Account;
END
GO
