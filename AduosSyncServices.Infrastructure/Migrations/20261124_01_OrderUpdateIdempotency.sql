-- Koniec wysyłania w kółko tych samych statusów i numerów przesyłek do Allegro (jak w JSAGROSyncServices).
--
-- Dotąd serwis zamówień co cykl wysyłał wszystkie numery przesyłek każdego zamówienia do aktualizacji,
-- a status wracał, bo RealizeStatus w bazie pokazywał stan sprzed naszej wysyłki.
--
--   AllegroOrderShipments - numery przesyłek, o których wiemy, że są już w Allegro,
--   ShipmentsCheckedAt    - kiedy porównaliśmy przesyłki zamówienia z tym, co ma Allegro.

IF OBJECT_ID(N'dbo.AllegroOrderShipments', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AllegroOrderShipments
    (
        Id             INT IDENTITY(1, 1) NOT NULL CONSTRAINT PK_AllegroOrderShipments PRIMARY KEY,
        AllegroOrderId INT           NOT NULL CONSTRAINT FK_AllegroOrderShipments_Order REFERENCES dbo.AllegroOrders (Id) ON DELETE CASCADE,
        CarrierId      NVARCHAR(50)  NOT NULL,
        Waybill        NVARCHAR(100) NOT NULL,
        SentAt         DATETIME2     NOT NULL CONSTRAINT DF_AllegroOrderShipments_SentAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT UQ_AllegroOrderShipments UNIQUE (AllegroOrderId, CarrierId, Waybill)
    );
END
GO

IF COL_LENGTH('dbo.AllegroOrders', 'ShipmentsCheckedAt') IS NULL
    ALTER TABLE dbo.AllegroOrders ADD ShipmentsCheckedAt DATETIME2 NULL;
GO

CREATE OR ALTER PROCEDURE dbo.AllegroOrderShipments_GetByOrder
    @AllegroOrderId INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT CarrierId, Waybill
    FROM dbo.AllegroOrderShipments
    WHERE AllegroOrderId = @AllegroOrderId;
END
GO

-- Idempotentne: ten sam numer można zgłosić wielokrotnie (np. po odczycie z Allegro).
CREATE OR ALTER PROCEDURE dbo.AllegroOrderShipments_Add
    @AllegroOrderId INT,
    @CarrierId NVARCHAR(50),
    @Waybill NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;

    IF @Waybill IS NULL OR LTRIM(RTRIM(@Waybill)) = N''
        RETURN;

    INSERT INTO dbo.AllegroOrderShipments (AllegroOrderId, CarrierId, Waybill)
    SELECT @AllegroOrderId, ISNULL(@CarrierId, N''), @Waybill
    WHERE NOT EXISTS (
        SELECT 1 FROM dbo.AllegroOrderShipments s
        WHERE s.AllegroOrderId = @AllegroOrderId
          AND s.CarrierId = ISNULL(@CarrierId, N'')
          AND s.Waybill = @Waybill);
END
GO

CREATE OR ALTER PROCEDURE dbo.AllegroOrders_MarkShipmentsChecked
    @AllegroOrderId INT
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.AllegroOrders
    SET ShipmentsCheckedAt = SYSUTCDATETIME()
    WHERE Id = @AllegroOrderId;
END
GO

-- Po udanej zmianie statusu w Allegro zapisujemy go u siebie - inaczej kolejny cykl
-- porównuje nowy status ze starym i wysyła to samo jeszcze raz.
CREATE OR ALTER PROCEDURE dbo.AllegroOrders_UpdateRealizeStatus
    @AllegroOrderId INT,
    @RealizeStatus INT
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.AllegroOrders
    SET RealizeStatus = @RealizeStatus
    WHERE Id = @AllegroOrderId;
END
GO
