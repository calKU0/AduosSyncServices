IF NOT EXISTS (
    SELECT 1 
    FROM sys.columns 
    WHERE Name = N'ShippingRate' 
      AND Object_ID = Object_ID(N'dbo.AllegroOrderItems')
)
BEGIN
    ALTER TABLE dbo.AllegroOrderItems
    ADD ShippingRate VARCHAR(100) NULL;
END
GO


CREATE OR ALTER PROCEDURE [dbo].[AllegroOrderItems_Upsert]
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
