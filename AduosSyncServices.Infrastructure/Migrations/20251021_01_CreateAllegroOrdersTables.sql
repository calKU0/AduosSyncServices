-- Create AllegroOrders table if it doesn't exist
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='AllegroOrders' AND xtype='U')
BEGIN
    CREATE TABLE AllegroOrders (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        AllegroId NVARCHAR(100) NOT NULL,
        MessageToSeller NVARCHAR(MAX) NOT NULL,
        Note NVARCHAR(MAX) NULL,
        Status INT NOT NULL,
        RealizeStatus INT NOT NULL,
        RecipientFirstName NVARCHAR(100) NOT NULL,
        RecipientLastName NVARCHAR(100) NOT NULL,
        RecipientStreet NVARCHAR(200) NOT NULL,
        RecipientCity NVARCHAR(100) NOT NULL,
        RecipientPostalCode NVARCHAR(20) NOT NULL,
        RecipientCountry NVARCHAR(100) NOT NULL,
        RecipientCompanyName NVARCHAR(200) NULL,
        RecipientEmail NVARCHAR(200) NULL,
        RecipientPhoneNumber NVARCHAR(50) NULL,
        DeliveryMethodId NVARCHAR(50) NOT NULL,
        DeliveryMethodName NVARCHAR(100) NOT NULL,
        CancellationDate DATETIME2 NULL,
        CreatedAt DATETIME2 NOT NULL,
        Revision NVARCHAR(50) NOT NULL,
        SentToGaska BIT NOT NULL DEFAULT 0,
        GaskaOrderId INT NOT NULL DEFAULT 0,
        GaskaOrderStatus NVARCHAR(100) NULL,
        GaskaOrderNumber NVARCHAR(50) NULL,
        GaskaDeliveryName NVARCHAR(100) NULL
    )
END

-- Create AllegroOrderItems table if it doesn't exist
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='AllegroOrderItems' AND xtype='U')
BEGIN
    CREATE TABLE AllegroOrderItems (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        AllegroOrderId INT NOT NULL,
        GaskaItemId INT NOT NULL,
        OrderItemId NVARCHAR(100) NOT NULL,
        OfferId NVARCHAR(100) NOT NULL,
        OfferName NVARCHAR(200) NOT NULL,
        ExternalId NVARCHAR(100) NOT NULL,
        PriceGross NVARCHAR(50) NOT NULL,
        Currency NVARCHAR(10) NOT NULL,
        Quantity INT NOT NULL,
        GaskaTrackingNumber NVARCHAR(100) NULL,
        BoughtAt DATETIME2 NOT NULL,
        FOREIGN KEY (AllegroOrderId) REFERENCES AllegroOrders(Id),
        FOREIGN KEY (GaskaItemId) REFERENCES Products(Id)
    )
END
