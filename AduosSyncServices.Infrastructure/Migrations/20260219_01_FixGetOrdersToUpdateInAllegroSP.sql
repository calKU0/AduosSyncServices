CREATE OR ALTER PROCEDURE [dbo].[AllegroOrders_GetToUpdateInAllegro]
    @NewStatus INT,
    @ProcessingStatus INT,
    @ReadyForShipmentStatus INT,
    @ReadyForPickupStatus INT,
    @SentStatus INT,
    @ReadyStatus INT,
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
        AND o.Status = @ReadyStatus
        AND o.RealizeStatus IN (
            @NewStatus,
            @ProcessingStatus,
            @ReadyForShipmentStatus,
            @ReadyForPickupStatus,
            @SentStatus
        );
END