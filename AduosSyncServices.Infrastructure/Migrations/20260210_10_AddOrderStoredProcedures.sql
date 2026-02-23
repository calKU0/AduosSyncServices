CREATE OR ALTER PROCEDURE dbo.AllegroOrders_GetToUpdateExternalInfo
    @IntegrationCompany INT,
    @Account INT,
    @NotWithExternalOrderStatus VARCHAR(100) = 'Zrealizowane'
AS
BEGIN
    SET NOCOUNT ON;

    SELECT *
    FROM AllegroOrders o
    LEFT JOIN AllegroOrderItems i ON o.Id = i.AllegroOrderId
    WHERE
        o.SentToExternalCompany = 1 
        AND IntegrationCompany = @IntegrationCompany
        AND Account = @Account
        AND ISNULL(o.ExternalOrderStatus,'') <> @NotWithExternalOrderStatus
        AND o.ExternalOrderId IS NOT NULL;
END
GO

CREATE OR ALTER PROCEDURE dbo.AllegroOrders_GetToUpdateInAllegro
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
        AND o.ExternalOrderId IS NOT NULL
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
GO

CREATE OR ALTER PROCEDURE dbo.AllegroOrders_GetPendingForExternalCompany
    @ReadyStatus INT,
    @DelayMinutes INT,
    @NewStatus INT,
    @Account INT,
    @IntegrationCompany INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT *
    FROM AllegroOrders o
    LEFT JOIN AllegroOrderItems i ON o.Id = i.AllegroOrderId
    WHERE
        o.SentToExternalCompany = 0
        AND IntegrationCompany = @IntegrationCompany
        AND Account = @Account
        AND o.Status = @ReadyStatus
        AND o.RealizeStatus = @NewStatus
        AND DATEDIFF(MINUTE, o.CreatedAt, SYSUTCDATETIME()) >= @DelayMinutes;
END
GO

CREATE OR ALTER PROCEDURE dbo.AllegroOrders_MarkAsOrderedInExternalCompany
    @OrderId INT,
    @ExternalOrderId INT
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE AllegroOrders
    SET
        SentToExternalCompany = 1,
        ExternalOrderId = @ExternalOrderId
    WHERE Id = @OrderId;
END
GO

CREATE OR ALTER PROCEDURE dbo.AllegroOrders_Save
    @AllegroId NVARCHAR(100),
    @MessageToSeller NVARCHAR(MAX) = NULL,
    @Note NVARCHAR(MAX) = NULL,
    @Status INT,
    @RealizeStatus INT,
    @Amount DECIMAL(18, 2),
    @ClientNickname NVARCHAR(200),
    @RecipientFirstName NVARCHAR(100),
    @RecipientLastName NVARCHAR(100),
    @RecipientStreet NVARCHAR(200),
    @RecipientCity NVARCHAR(100),
    @RecipientPostalCode NVARCHAR(20),
    @RecipientCountry NVARCHAR(100),
    @RecipientCompanyName NVARCHAR(200) = NULL,
    @RecipientEmail NVARCHAR(200) = NULL,
    @RecipientPhoneNumber NVARCHAR(50) = NULL,
    @DeliveryMethodId NVARCHAR(50),
    @DeliveryMethodName NVARCHAR(100),
    @CancellationDate DATETIME2 = NULL,
    @CreatedAt DATETIME2,
    @Revision NVARCHAR(50),
    @SentToExternalCompany BIT,
    @ExternalOrderId INT,
    @PaymentType INT,
    @ExternalOrderStatus NVARCHAR(100) = NULL,
    @ExternalOrderNumber NVARCHAR(50) = NULL,
    @ExternalDeliveryName NVARCHAR(100) = NULL,
    @Account INT,
    @IntegrationCompany INT,
    @Id INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (SELECT 1 FROM AllegroOrders WHERE AllegroId = @AllegroId)
    BEGIN
        UPDATE AllegroOrders
        SET
            MessageToSeller = @MessageToSeller,
            Note = @Note,
            Status = @Status,
            RealizeStatus = @RealizeStatus,
            Amount = @Amount,
            ClientNickname = @ClientNickname,
            RecipientFirstName = @RecipientFirstName,
            RecipientLastName = @RecipientLastName,
            RecipientStreet = @RecipientStreet,
            RecipientCity = @RecipientCity,
            RecipientPostalCode = @RecipientPostalCode,
            RecipientCountry = @RecipientCountry,
            RecipientCompanyName = @RecipientCompanyName,
            RecipientEmail = @RecipientEmail,
            RecipientPhoneNumber = @RecipientPhoneNumber,
            DeliveryMethodId = @DeliveryMethodId,
            DeliveryMethodName = @DeliveryMethodName,
            CancellationDate = @CancellationDate,
            CreatedAt = @CreatedAt,
            Revision = @Revision,
            PaymentType = @PaymentType,
            Account = @Account,
            IntegrationCompany = @IntegrationCompany
        WHERE AllegroId = @AllegroId;

        SELECT @Id = Id FROM AllegroOrders WHERE AllegroId = @AllegroId;
    END
    ELSE
    BEGIN
        INSERT INTO AllegroOrders (
            AllegroId, MessageToSeller, Note, Status, RealizeStatus, Amount, ClientNickname,
            RecipientFirstName, RecipientLastName, RecipientStreet, RecipientCity, RecipientPostalCode, RecipientCountry,
            RecipientCompanyName, RecipientEmail, RecipientPhoneNumber,
            DeliveryMethodId, DeliveryMethodName, CancellationDate, CreatedAt, Revision,
            SentToExternalCompany, ExternalOrderId, PaymentType, ExternalOrderStatus, ExternalOrderNumber, ExternalDeliveryName,
            Account, IntegrationCompany
        )
        VALUES (
            @AllegroId, @MessageToSeller, @Note, @Status, @RealizeStatus, @Amount, @ClientNickname,
            @RecipientFirstName, @RecipientLastName, @RecipientStreet, @RecipientCity, @RecipientPostalCode, @RecipientCountry,
            @RecipientCompanyName, @RecipientEmail, @RecipientPhoneNumber,
            @DeliveryMethodId, @DeliveryMethodName, @CancellationDate, @CreatedAt, @Revision,
            @SentToExternalCompany, @ExternalOrderId, @PaymentType, @ExternalOrderStatus, @ExternalOrderNumber, @ExternalDeliveryName,
            @Account, @IntegrationCompany
        );

        SET @Id = SCOPE_IDENTITY();
    END
END
GO

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

CREATE OR ALTER PROCEDURE dbo.AllegroOrderItems_Upsert
    @AllegroOrderId INT,
    @OrderItemId NVARCHAR(100),
    @ProductId INT,
    @OfferId NVARCHAR(100),
    @OfferName NVARCHAR(200),
    @ExternalId NVARCHAR(100),
    @PriceGross NVARCHAR(50),
    @Currency NVARCHAR(10),
    @Quantity INT,
    @ExternalCourier NVARCHAR(100) = NULL,
    @ExternalTrackingNumber NVARCHAR(100) = NULL,
    @BoughtAt DATETIME2
AS
BEGIN
    SET NOCOUNT ON;

    MERGE INTO AllegroOrderItems AS target
    USING (SELECT
              @AllegroOrderId AS AllegroOrderId,
              @OrderItemId AS OrderItemId
          ) AS source
    ON target.AllegroOrderId = source.AllegroOrderId AND target.OrderItemId = source.OrderItemId
    WHEN MATCHED THEN
        UPDATE SET
            ProductId = @ProductId,
            OfferId = @OfferId,
            OfferName = @OfferName,
            ExternalId = @ExternalId,
            PriceGross = @PriceGross,
            Currency = @Currency,
            Quantity = @Quantity,
            BoughtAt = @BoughtAt
    WHEN NOT MATCHED THEN
        INSERT (
            AllegroOrderId, ProductId, OrderItemId, OfferId, OfferName, ExternalId, PriceGross, Currency, Quantity, BoughtAt
        )
        VALUES (
            @AllegroOrderId, @ProductId, @OrderItemId, @OfferId, @OfferName, @ExternalId, @PriceGross, @Currency, @Quantity, @BoughtAt
        );
END
GO

CREATE OR ALTER PROCEDURE dbo.AllegroOrders_SetEmailSent
    @OrderId INT
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE AllegroOrders
    SET EmailSent = 1
    WHERE Id = @OrderId;
END
GO

CREATE OR ALTER PROCEDURE dbo.AllegroOrders_UpdateExternalInfo
    @Id INT,
    @ExternalOrderStatus NVARCHAR(100) = NULL,
    @ExternalOrderNumber NVARCHAR(50) = NULL,
    @ExternalDeliveryName NVARCHAR(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE AllegroOrders
    SET
        ExternalOrderStatus = @ExternalOrderStatus,
        ExternalOrderNumber = @ExternalOrderNumber,
        ExternalDeliveryName = @ExternalDeliveryName
    WHERE Id = @Id;
END
GO

CREATE OR ALTER PROCEDURE dbo.AllegroOrderItems_UpdateExternalInfo
    @AllegroOrderId INT,
    @OrderItemId NVARCHAR(100),
    @ProductId INT,
    @ExternalCourier NVARCHAR(100) = NULL,
    @ExternalTrackingNumber NVARCHAR(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE AllegroOrderItems
    SET
        ProductId = @ProductId,
        ExternalCourier = @ExternalCourier,
        ExternalTrackingNumber = @ExternalTrackingNumber
    WHERE AllegroOrderId = @AllegroOrderId AND OrderItemId = @OrderItemId;
END
GO
