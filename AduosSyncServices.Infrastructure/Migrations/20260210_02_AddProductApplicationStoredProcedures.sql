CREATE OR ALTER PROCEDURE dbo.ProductApplications_DeleteByProductId
    @ProductId INT
AS
BEGIN
    SET NOCOUNT ON;

    DELETE FROM ProductApplications
    WHERE ProductId = @ProductId;
END
GO

CREATE OR ALTER PROCEDURE dbo.ProductApplications_Insert
    @ProductId INT,
    @ApplicationId INT,
    @ParentID INT,
    @Name NVARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO ProductApplications (ProductId, ApplicationId, ParentID, Name)
    VALUES (@ProductId, @ApplicationId, @ParentID, @Name);
END
GO
