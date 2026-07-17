CREATE OR ALTER PROCEDURE [dbo].[AllegroOrders_GetToUpdateInAllegro]
    @Account INT,
    @IntegrationCompany INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT *
    FROM AllegroOrders o
    LEFT JOIN AllegroOrderItems i ON o.Id = i.AllegroOrderId
    WHERE
        o.SentToExternalCompany = 1
        AND NULLIF(o.ExternalOrderId, 0) IS NOT NULL
        AND NULLIF(o.ExternalOrderStatus, '') IS NOT NULL
        AND IntegrationCompany = @IntegrationCompany
        AND Account = @Account
        AND o.Status = 2
        AND o.IsDropshipping = 1
        AND o.Source = 0
        AND o.RealizeStatus NOT IN (6, 7, 8, 5)
END
GO