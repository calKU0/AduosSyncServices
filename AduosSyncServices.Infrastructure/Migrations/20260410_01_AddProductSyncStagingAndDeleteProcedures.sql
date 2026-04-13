IF TYPE_ID(N'dbo.ProductIntegrationIdType') IS NULL
BEGIN
    EXEC('CREATE TYPE dbo.ProductIntegrationIdType AS TABLE
    (
        IntegrationId INT NOT NULL
    );');
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.tables
    WHERE name = 'ProductSyncStaging' AND schema_id = SCHEMA_ID('dbo')
)
BEGIN
    CREATE TABLE dbo.ProductSyncStaging
    (
        Id BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        SyncRunId UNIQUEIDENTIFIER NOT NULL,
        IntegrationCompany INT NOT NULL,
        IntegrationId INT NOT NULL,
        CreatedDate DATETIME2 NOT NULL CONSTRAINT DF_ProductSyncStaging_CreatedDate DEFAULT (SYSUTCDATETIME())
    );
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'IX_ProductSyncStaging_SyncRunCompanyIntegrationId'
      AND object_id = OBJECT_ID(N'dbo.ProductSyncStaging')
)
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX IX_ProductSyncStaging_SyncRunCompanyIntegrationId
    ON dbo.ProductSyncStaging (SyncRunId, IntegrationCompany, IntegrationId);
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'IX_Products_IntegrationCompany_IntegrationId'
      AND object_id = OBJECT_ID(N'dbo.Products')
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_Products_IntegrationCompany_IntegrationId
    ON dbo.Products (IntegrationCompany, IntegrationId);
END
GO

CREATE OR ALTER PROCEDURE dbo.ProductSyncStaging_InsertBatch
    @SyncRunId UNIQUEIDENTIFIER,
    @IntegrationCompany INT,
    @Items dbo.ProductIntegrationIdType READONLY
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO dbo.ProductSyncStaging (SyncRunId, IntegrationCompany, IntegrationId)
    SELECT DISTINCT @SyncRunId, @IntegrationCompany, i.IntegrationId
    FROM @Items i
    WHERE i.IntegrationId > 0;
END
GO

CREATE OR ALTER PROCEDURE dbo.Products_DeleteMissingBySyncRun
    @SyncRunId UNIQUEIDENTIFIER,
    @IntegrationCompany INT,
    @BatchSize INT = 10000
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (
        SELECT 1
        FROM dbo.ProductSyncStaging s
        WHERE s.SyncRunId = @SyncRunId
          AND s.IntegrationCompany = @IntegrationCompany
    )
    BEGIN
        SELECT 0;
        RETURN;
    END

    ;WITH ToDelete AS
    (
        SELECT TOP (@BatchSize) p.Id
        FROM dbo.Products p
        LEFT JOIN dbo.ProductSyncStaging s
            ON s.SyncRunId = @SyncRunId
           AND s.IntegrationCompany = @IntegrationCompany
           AND s.IntegrationId = p.IntegrationId
        WHERE p.IntegrationCompany = @IntegrationCompany
          AND (p.IntegrationId IS NULL OR s.IntegrationId IS NULL)
        ORDER BY p.Id
    )
    DELETE p
    FROM dbo.Products p
    INNER JOIN ToDelete d ON d.Id = p.Id;

    SELECT @@ROWCOUNT;
END
GO

CREATE OR ALTER PROCEDURE dbo.ProductSyncStaging_ClearRun
    @SyncRunId UNIQUEIDENTIFIER,
    @IntegrationCompany INT
AS
BEGIN
    SET NOCOUNT ON;

    DELETE FROM dbo.ProductSyncStaging
    WHERE SyncRunId = @SyncRunId
      AND IntegrationCompany = @IntegrationCompany;
END
GO

CREATE OR ALTER PROCEDURE dbo.ProductSyncStaging_ClearOlderThanDays
    @RetentionDays INT = 2
AS
BEGIN
    SET NOCOUNT ON;

    DELETE FROM dbo.ProductSyncStaging
    WHERE CreatedDate < DATEADD(DAY, -ABS(@RetentionDays), SYSUTCDATETIME());
END
GO
