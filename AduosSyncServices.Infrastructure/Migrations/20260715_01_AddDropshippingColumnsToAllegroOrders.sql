IF NOT EXISTS (
    SELECT 1 
    FROM sys.columns 
    WHERE Name = N'IsDropshipping' 
      AND Object_ID = Object_ID(N'dbo.AllegroOrders')
)
BEGIN
    ALTER TABLE dbo.AllegroOrders
    ADD IsDropshipping BIT NOT NULL DEFAULT 0;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[AllegroOrders_MarkAsOrderedInExternalCompany]
    @OrderId INT,
    @ExternalOrderId INT,
    @IsDropshipping BIT
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE AllegroOrders
    SET
        SentToExternalCompany = 1,
        ExternalOrderId = @ExternalOrderId,
        IsDropshipping = @IsDropshipping
    WHERE Id = @OrderId;
END
GO

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
        AND o.RealizeStatus NOT IN (6, 7, 8, 5)
END
GO

CREATE OR ALTER PROCEDURE [dbo].[AllegroOrders_GetAllOrdersForExtranalCompany]
    @Account INT,
    @IntegrationCompany INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT *
    FROM AllegroOrders o
    LEFT JOIN AllegroOrderItems i ON o.Id = i.AllegroOrderId
    WHERE IntegrationCompany = @IntegrationCompany AND Account = @Account
END
GO

DROP PROCEDURE [dbo].[AllegroOrders_GetPendingForExternalCompany]
GO

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
        AND o.ExternalOrderId IS NOT NULL
END
