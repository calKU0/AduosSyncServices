-- Adds support for manually-created orders (placed directly through the ServicesManager Orders panel
-- rather than synced from Allegro). Source = 0 (Allegro, the existing default for every synced order)
-- or 1 (Manual) - manual orders are otherwise ordinary AllegroOrders rows (with a synthetic AllegroId),
-- so the whole existing grid/filter/placement pipeline works on them unchanged.
IF NOT EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE Name = N'Source'
      AND Object_ID = OBJECT_ID(N'dbo.AllegroOrders')
)
BEGIN
    ALTER TABLE dbo.AllegroOrders
    ADD Source INT NOT NULL CONSTRAINT DF_AllegroOrders_Source DEFAULT (0);
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
    @Source INT = 0,
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

-- Products eligible for a manually-placed order: not archived, and currently listed with an active
-- Allegro offer (mirrors "podpięte na allegro i mają ofertę" - linked to Allegro with an offer).
CREATE OR ALTER PROCEDURE dbo.Products_SearchOrderable
    @SearchTerm NVARCHAR(200),
    @IntegrationCompany INT,
    @Account INT,
    @MaxResults INT = 50
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP (@MaxResults)
        p.Id AS ProductId,
        p.Code,
        p.Name,
        p.InStock,
        p.PriceNet AS PurchasePriceNet,
        p.PriceGross AS PurchasePriceGross,
        o.Price AS SalePrice,
        JSON_VALUE(o.Images, '$[0]') AS ImageUrl
    FROM dbo.Products p
    INNER JOIN dbo.AllegroOffers o ON o.ProductId = p.Id AND o.Account = @Account
    WHERE p.IntegrationCompany = @IntegrationCompany
      AND p.IsArchived = 0
      AND o.Status = 'ACTIVE'
      AND (p.Name LIKE '%' + @SearchTerm + '%' OR p.Code LIKE '%' + @SearchTerm + '%')
    ORDER BY p.Name;
END
GO


CREATE OR ALTER PROCEDURE [dbo].[Products_GetByIntegrationId]
    @IntegrationId INT,
    @IntegrationCompany INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP 1 *
    FROM Products p
    WHERE IntegrationId = @IntegrationId AND IntegrationCompany = @IntegrationCompany
    ORDER BY CreatedDate DESC;
END