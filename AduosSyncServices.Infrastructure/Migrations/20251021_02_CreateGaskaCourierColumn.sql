-- Add GaskaCourier column to AllegroOrderItems
IF NOT EXISTS (
    SELECT * 
    FROM sys.columns 
    WHERE Name = 'GaskaCourier' 
      AND Object_ID = Object_ID('AllegroOrderItems')
)
BEGIN
    ALTER TABLE AllegroOrderItems
    ADD GaskaCourier NVARCHAR(30) NULL;
END

-- Add PaymentType column to AllegroOrders
IF NOT EXISTS (
    SELECT * 
    FROM sys.columns 
    WHERE Name = 'PaymentType' 
      AND Object_ID = Object_ID('AllegroOrders')
)
BEGIN
    ALTER TABLE AllegroOrders
    ADD PaymentType INT NOT NULL;
END

-- Add Amount column to AllegroOrders
IF NOT EXISTS (
    SELECT * 
    FROM sys.columns 
    WHERE Name = 'Amount' 
      AND Object_ID = Object_ID('AllegroOrders')
)
BEGIN
    ALTER TABLE AllegroOrders
    ADD Amount DECIMAL(18,2) NOT NULL;
END