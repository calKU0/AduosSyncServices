-- Szczegóły z Gąski (kategorie, parametry, zdjęcia, zastosowania) pobieramy najpierw dla produktów na stanie (InStock >= 1),
-- dopiero potem dla pozostałych. Dzienny limit zapytań (ProductPerDay) idzie w pierwszej kolejności na produkty,
-- które można od razu wystawić. GROUP BY zamiast DISTINCT, bo DISTINCT nie pozwala sortować po stanie.
CREATE OR ALTER PROCEDURE dbo.Products_GetForDetailUpdate
    @Limit INT,
    @IntegrationCompany INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT p.IntegrationId
    FROM Products p
    WHERE NOT EXISTS (SELECT 1 FROM ProductCategories pc WHERE pc.ProductId = p.Id)
      AND IntegrationCompany = @IntegrationCompany
    GROUP BY p.IntegrationId
    ORDER BY MAX(CASE WHEN p.InStock >= 1 THEN 1 ELSE 0 END) DESC, p.IntegrationId
    OFFSET 0 ROWS FETCH NEXT @Limit ROWS ONLY;
END
