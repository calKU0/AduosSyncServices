-- Internal order statuses: a user-managed, global list of workflow labels (name + colour) that an
-- operator assigns to orders. Orders default to no internal status (InternalStatusId NULL). Deleting a
-- status reverts every order using it back to "no status" (FK ON DELETE SET NULL). The status is user
-- metadata, so AllegroOrders_Save deliberately never touches InternalStatusId - it survives re-syncs.
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'OrderInternalStatuses')
BEGIN
    CREATE TABLE dbo.OrderInternalStatuses
    (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        Name NVARCHAR(100) NOT NULL,
        Color NVARCHAR(20) NOT NULL CONSTRAINT DF_OrderInternalStatuses_Color DEFAULT '#3B82F6',
        CreatedDate DATETIME2 NOT NULL CONSTRAINT DF_OrderInternalStatuses_CreatedDate DEFAULT SYSUTCDATETIME()
    );

    -- Case-insensitive uniqueness (default collation) so the same label can't be added twice.
    CREATE UNIQUE INDEX UX_OrderInternalStatuses_Name ON dbo.OrderInternalStatuses (Name);
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns WHERE Name = N'InternalStatusId' AND Object_ID = OBJECT_ID(N'dbo.AllegroOrders')
)
BEGIN
    ALTER TABLE dbo.AllegroOrders ADD InternalStatusId INT NULL;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_AllegroOrders_OrderInternalStatuses'
)
BEGIN
    ALTER TABLE dbo.AllegroOrders
    ADD CONSTRAINT FK_AllegroOrders_OrderInternalStatuses
        FOREIGN KEY (InternalStatusId) REFERENCES dbo.OrderInternalStatuses (Id) ON DELETE SET NULL;
END
GO

CREATE OR ALTER PROCEDURE dbo.OrderInternalStatuses_GetAll
AS
BEGIN
    SET NOCOUNT ON;
    SELECT Id, Name, Color, CreatedDate
    FROM dbo.OrderInternalStatuses
    ORDER BY Name;
END
GO

CREATE OR ALTER PROCEDURE dbo.OrderInternalStatuses_Add
    @Name NVARCHAR(100),
    @Color NVARCHAR(20),
    @Id INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.OrderInternalStatuses (Name, Color) VALUES (@Name, @Color);
    SET @Id = SCOPE_IDENTITY();
END
GO

CREATE OR ALTER PROCEDURE dbo.OrderInternalStatuses_Update
    @Id INT,
    @Name NVARCHAR(100),
    @Color NVARCHAR(20)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.OrderInternalStatuses
    SET Name = @Name, Color = @Color
    WHERE Id = @Id;
END
GO

CREATE OR ALTER PROCEDURE dbo.OrderInternalStatuses_Delete
    @Id INT
AS
BEGIN
    SET NOCOUNT ON;
    -- Orders referencing this status are reset to NULL by the FK's ON DELETE SET NULL.
    DELETE FROM dbo.OrderInternalStatuses WHERE Id = @Id;
END
GO

-- Bulk-assigns (or clears, when @InternalStatusId IS NULL) the internal status for a set of orders.
-- @OrderIds is a comma-separated list of AllegroOrders.Id. Scoped to the account/company for safety.
CREATE OR ALTER PROCEDURE dbo.AllegroOrders_SetInternalStatus
    @OrderIds NVARCHAR(MAX),
    @InternalStatusId INT = NULL,
    @IntegrationCompany INT,
    @Account INT
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE o
    SET o.InternalStatusId = @InternalStatusId
    FROM dbo.AllegroOrders o
    INNER JOIN STRING_SPLIT(@OrderIds, ',') s ON TRY_CONVERT(INT, s.value) = o.Id
    WHERE o.IntegrationCompany = @IntegrationCompany
      AND o.Account = @Account;
END
GO
