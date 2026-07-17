-- GaskaOrderPlacementService's timeout paths persist ExternalOrderId = 0 as a "placed, but we never
-- got a real Gąska id back" placeholder (see MarkAsOrderedInExternalCompany(order.Id, 0, ...)).
-- AllegroOrders_GetToUpdateExternalInfo's "AND o.ExternalOrderId IS NOT NULL" doesn't catch that -
-- 0 is not NULL - so those orders get reprocessed by UpdateOrderGaskaInfo and call GetOrder(0)
-- against Gąska every cycle until someone manually fixes the row. Treat 0 like missing here too,
-- matching the NULLIF(o.ExternalOrderId, 0) pattern already used by AllegroOrders_GetToUpdateInAllegro.
CREATE OR ALTER PROCEDURE [dbo].[AllegroOrders_GetToUpdateExternalInfo]
    @IntegrationCompany INT,
    @Account INT
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
        AND ISNULL(o.ExternalOrderStatus,'') NOT IN ('Zrealizowane', 'Zrealizowano')
        AND NULLIF(o.ExternalOrderId, 0) IS NOT NULL
END
