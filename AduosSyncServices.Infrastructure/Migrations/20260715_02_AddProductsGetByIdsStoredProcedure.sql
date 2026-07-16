IF TYPE_ID('dbo.ProductIdType') IS NULL
BEGIN
    EXEC('CREATE TYPE dbo.ProductIdType AS TABLE
    (
        Id INT NOT NULL
    )')
END
GO

CREATE OR ALTER PROCEDURE dbo.Products_GetByIds
    @Ids dbo.ProductIdType READONLY,
    @IntegrationCompany INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT p.*
    FROM Products p
    INNER JOIN @Ids i ON p.Id = i.Id
    WHERE p.IntegrationCompany = @IntegrationCompany;
END
GO
