CREATE OR ALTER PROCEDURE dbo.Products_GetByCode
    @Code NVARCHAR(255),
    @IntegrationCompany INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT IntegrationId AS ProductId, Code
    FROM Products
    WHERE Code = @Code AND IntegrationCompany = @IntegrationCompany;
END
GO