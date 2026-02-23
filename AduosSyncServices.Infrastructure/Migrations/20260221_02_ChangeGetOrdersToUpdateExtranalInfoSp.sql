CREATE TYPE dbo.ShippingRateList AS TABLE
(
    ShippingRate VARCHAR(100)
)
GO

CREATE OR ALTER PROCEDURE [dbo].[AllegroOrders_GetToUpdateExternalInfo]
    @IntegrationCompany INT,
    @Account INT,
    @NotWithExternalOrderStatus VARCHAR(100) = 'Zrealizowane',
    @ShippingRates dbo.ShippingRateList READONLY
AS
BEGIN
    SET NOCOUNT ON;

    SELECT *
    FROM AllegroOrders o
    LEFT JOIN AllegroOrderItems i ON o.Id = i.AllegroOrderId
    WHERE
        o.SentToExternalCompany = 1
        AND o.IntegrationCompany = @IntegrationCompany
        AND o.Account = @Account
        AND ISNULL(o.ExternalOrderStatus,'') <> @NotWithExternalOrderStatus
        AND o.ExternalOrderId IS NOT NULL
        AND EXISTS (
            SELECT 1
            FROM @ShippingRates sr
            WHERE sr.ShippingRate = i.ShippingRate
        );
END