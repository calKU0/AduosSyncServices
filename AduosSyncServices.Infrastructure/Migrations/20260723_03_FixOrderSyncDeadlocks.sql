-- Order sync saves orders in parallel (and can additionally run concurrently from the OrdersService
-- worker and the ServicesManager refresh), which surfaced frequent 1205 deadlocks. Root cause: every
-- lookup in the save path was an unindexed full-table scan inside a transaction:
--   * AllegroOrders_Save probes/updates by AllegroId          - no index on AllegroId,
--   * AllegroOrderItems_Upsert MERGEs on (AllegroOrderId, OrderItemId) - no index on that key,
--   * Products_GetByCode filters by (Code, IntegrationCompany) - no index on Code.
-- Scans take shared locks across whole tables while sibling transactions hold exclusive locks on the
-- rows they just wrote - a textbook deadlock mill. Fix: seek-able (unique) indexes for all three
-- paths, plus UPDLOCK/HOLDLOCK on the upsert probes so two saves of the SAME key serialize instead
-- of deadlocking between their read and write. (Verified before creating the unique indexes: live
-- data has 0 duplicate AllegroIds and 0 duplicate (AllegroOrderId, OrderItemId) pairs.)

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AllegroOrders_AllegroId' AND object_id = OBJECT_ID('dbo.AllegroOrders'))
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX IX_AllegroOrders_AllegroId
        ON dbo.AllegroOrders (AllegroId);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AllegroOrderItems_AllegroOrderId_OrderItemId' AND object_id = OBJECT_ID('dbo.AllegroOrderItems'))
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX IX_AllegroOrderItems_AllegroOrderId_OrderItemId
        ON dbo.AllegroOrderItems (AllegroOrderId, OrderItemId);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Products_IntegrationCompany_Code' AND object_id = OBJECT_ID('dbo.Products'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Products_IntegrationCompany_Code
        ON dbo.Products (IntegrationCompany, Code);
END
GO

-- Same body as before, but the existence probe takes an update-range lock (UPDLOCK, HOLDLOCK) so a
-- concurrent save of the same AllegroId waits instead of deadlocking, and the row Id is captured by
-- that single seek (the old version scanned the table three times per call).
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
    @Source INT = 0,
    @Id INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    SET @Id = NULL;
    SELECT @Id = Id
    FROM AllegroOrders WITH (UPDLOCK, HOLDLOCK)
    WHERE AllegroId = @AllegroId;

    IF @Id IS NOT NULL
    BEGIN
        -- Deliberately never touches InternalStatusId (manager-only metadata, survives re-syncs)
        -- nor the SentToExternalCompany/External* placement columns.
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
        WHERE Id = @Id;
    END
    ELSE
    BEGIN
        INSERT INTO AllegroOrders (
            AllegroId, MessageToSeller, Note, Status, RealizeStatus, Amount, ClientNickname,
            RecipientFirstName, RecipientLastName, RecipientStreet, RecipientCity, RecipientPostalCode, RecipientCountry,
            RecipientCompanyName, RecipientEmail, RecipientPhoneNumber,
            DeliveryMethodId, DeliveryMethodName, CancellationDate, CreatedAt, Revision,
            SentToExternalCompany, ExternalOrderId, PaymentType, ExternalOrderStatus, ExternalOrderNumber, ExternalDeliveryName,
            Account, IntegrationCompany, Source
        )
        VALUES (
            @AllegroId, @MessageToSeller, @Note, @Status, @RealizeStatus, @Amount, @ClientNickname,
            @RecipientFirstName, @RecipientLastName, @RecipientStreet, @RecipientCity, @RecipientPostalCode, @RecipientCountry,
            @RecipientCompanyName, @RecipientEmail, @RecipientPhoneNumber,
            @DeliveryMethodId, @DeliveryMethodName, @CancellationDate, @CreatedAt, @Revision,
            @SentToExternalCompany, @ExternalOrderId, @PaymentType, @ExternalOrderStatus, @ExternalOrderNumber, @ExternalDeliveryName,
            @Account, @IntegrationCompany, @Source
        );

        SET @Id = SCOPE_IDENTITY();
    END
END
GO

-- HOLDLOCK makes the MERGE's match probe hold its range lock until the write, closing the classic
-- "two MERGEs both see no match, both insert / deadlock" race.
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
    @ShippingRate VARCHAR(100) = NULL,
    @BoughtAt DATETIME2
AS
BEGIN
    SET NOCOUNT ON;

    MERGE INTO AllegroOrderItems WITH (HOLDLOCK) AS target
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
            BoughtAt = @BoughtAt,
            ShippingRate = @ShippingRate
    WHEN NOT MATCHED THEN
        INSERT (
            AllegroOrderId, ProductId, OrderItemId, OfferId, OfferName, ExternalId, PriceGross, Currency, Quantity, BoughtAt, ShippingRate
        )
        VALUES (
            @AllegroOrderId, @ProductId, @OrderItemId, @OfferId, @OfferName, @ExternalId, @PriceGross, @Currency, @Quantity, @BoughtAt, @ShippingRate
        );
END
GO
