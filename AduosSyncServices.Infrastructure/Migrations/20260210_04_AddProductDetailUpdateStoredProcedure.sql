CREATE OR ALTER PROCEDURE dbo.Products_GetForDetailUpdate
    @Limit INT,
    @IntegrationCompany INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT *
    FROM Products p
    WHERE NOT EXISTS (SELECT 1 FROM Category pc WHERE pc.ProductId = p.Id)
      AND IntegrationCompany = @IntegrationCompany
    ORDER BY p.Id
    OFFSET 0 ROWS FETCH NEXT @Limit ROWS ONLY;
END
GO
