-- Synchronizacja Gąska -> ShopGold oraz wspólne kategorie kanałów sprzedaży.
--
-- Produkty z Gąski pobiera tylko serwis Allegro. Każdy kanał (Allegro, ShopGold) zapisuje swoje
-- skonfigurowane kategorie Gąski w SyncCategories, a serwis Allegro pobiera sumę kategorii wszystkich
-- kanałów. ProductSyncCategories pamięta, w której pobranej kategorii był produkt - dzięki temu Allegro
-- wystawia tylko produkty ze swoich kategorii, a ShopGold dostaje tylko produkty ze swoich.

IF NOT EXISTS (
    SELECT 1 FROM sys.tables WHERE name = 'SyncCategories' AND schema_id = SCHEMA_ID('dbo')
)
BEGIN
    CREATE TABLE dbo.SyncCategories
    (
        Id INT IDENTITY(1,1) NOT NULL,
        Channel NVARCHAR(50) NOT NULL,
        IntegrationCompany INT NOT NULL,
        CategoryId INT NOT NULL,
        CreatedDate DATETIME2 NOT NULL CONSTRAINT DF_SyncCategories_CreatedDate DEFAULT (SYSUTCDATETIME()),

        CONSTRAINT PK_SyncCategories PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_SyncCategories UNIQUE (Channel, IntegrationCompany, CategoryId)
    );
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.tables WHERE name = 'ProductSyncCategories' AND schema_id = SCHEMA_ID('dbo')
)
BEGIN
    CREATE TABLE dbo.ProductSyncCategories
    (
        ProductId INT NOT NULL,
        CategoryId INT NOT NULL,

        CONSTRAINT PK_ProductSyncCategories PRIMARY KEY CLUSTERED (ProductId, CategoryId),
        CONSTRAINT FK_ProductSyncCategories_Products
            FOREIGN KEY (ProductId)
            REFERENCES dbo.Products (Id)
            ON DELETE CASCADE
    );

    CREATE NONCLUSTERED INDEX IX_ProductSyncCategories_CategoryId
        ON dbo.ProductSyncCategories (CategoryId) INCLUDE (ProductId);
END
GO

IF TYPE_ID(N'dbo.SyncCategoryIdType') IS NULL
BEGIN
    EXEC('CREATE TYPE dbo.SyncCategoryIdType AS TABLE
    (
        CategoryId INT NOT NULL
    );');
END
GO

-- Zapisuje kategorie jednego kanału; kategorie usunięte z konfiguracji kanału znikają.
CREATE OR ALTER PROCEDURE dbo.SyncCategories_ReplaceByChannel
    @Channel NVARCHAR(50),
    @IntegrationCompany INT,
    @Categories dbo.SyncCategoryIdType READONLY
AS
BEGIN
    SET NOCOUNT ON;

    MERGE dbo.SyncCategories AS target
    USING
    (
        SELECT DISTINCT @Channel AS Channel, @IntegrationCompany AS IntegrationCompany, c.CategoryId
        FROM @Categories c
        WHERE c.CategoryId <> 0
    ) AS src
    ON target.Channel = src.Channel
       AND target.IntegrationCompany = src.IntegrationCompany
       AND target.CategoryId = src.CategoryId
    WHEN NOT MATCHED BY TARGET THEN
        INSERT (Channel, IntegrationCompany, CategoryId)
        VALUES (src.Channel, src.IntegrationCompany, src.CategoryId)
    WHEN NOT MATCHED BY SOURCE
         AND target.Channel = @Channel
         AND target.IntegrationCompany = @IntegrationCompany THEN
        DELETE;
END
GO

-- Kategorie skonfigurowane we wszystkich kanałach (z nazwą kanału) - serwis pobierający pobiera ich sumę.
CREATE OR ALTER PROCEDURE dbo.SyncCategories_GetByCompany
    @IntegrationCompany INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT Channel, CategoryId
    FROM dbo.SyncCategories
    WHERE IntegrationCompany = @IntegrationCompany;
END
GO

-- Powiązania produktów z jedną kategorią pobraną w całości w tym przebiegu.
-- Produkty, które z kategorii wypadły, tracą powiązanie tylko z nią.
CREATE OR ALTER PROCEDURE dbo.ProductSyncCategories_ReplaceForCategory
    @IntegrationCompany INT,
    @CategoryId INT,
    @Items dbo.ProductIntegrationIdType READONLY
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Mapped TABLE (ProductId INT NOT NULL PRIMARY KEY);

    INSERT INTO @Mapped (ProductId)
    SELECT DISTINCT p.Id
    FROM @Items i
    JOIN dbo.Products p
        ON p.IntegrationId = i.IntegrationId
       AND p.IntegrationCompany = @IntegrationCompany;

    DELETE psc
    FROM dbo.ProductSyncCategories psc
    JOIN dbo.Products p ON p.Id = psc.ProductId AND p.IntegrationCompany = @IntegrationCompany
    WHERE psc.CategoryId = @CategoryId
      AND NOT EXISTS (SELECT 1 FROM @Mapped m WHERE m.ProductId = psc.ProductId);

    INSERT INTO dbo.ProductSyncCategories (ProductId, CategoryId)
    SELECT m.ProductId, @CategoryId
    FROM @Mapped m
    WHERE NOT EXISTS
    (
        SELECT 1 FROM dbo.ProductSyncCategories psc
        WHERE psc.ProductId = m.ProductId AND psc.CategoryId = @CategoryId
    );
END
GO

-- Usuwa powiązania z kategoriami, których nie ma już w konfiguracji żadnego kanału.
CREATE OR ALTER PROCEDURE dbo.ProductSyncCategories_DeleteNotIn
    @IntegrationCompany INT,
    @Categories dbo.SyncCategoryIdType READONLY
AS
BEGIN
    SET NOCOUNT ON;

    DELETE psc
    FROM dbo.ProductSyncCategories psc
    JOIN dbo.Products p ON p.Id = psc.ProductId AND p.IntegrationCompany = @IntegrationCompany
    WHERE NOT EXISTS (SELECT 1 FROM @Categories c WHERE c.CategoryId = psc.CategoryId);

    SELECT @@ROWCOUNT;
END
GO

-- Czy produkt może być sprzedawany w kanale.
-- Kanał bez skonfigurowanych kategorii nie filtruje. Produkt bez żadnego powiązania (pobrany przed
-- wprowadzeniem powiązań) też przechodzi - inaczej po wdrożeniu, do pierwszej pełnej synchronizacji,
-- wszystkie oferty Allegro zostałyby zakończone.
CREATE OR ALTER FUNCTION dbo.Products_IsAllowedInChannel
(
    @ProductId INT,
    @Channel NVARCHAR(50),
    @IntegrationCompany INT
)
RETURNS TABLE
AS
RETURN
(
    SELECT CAST(
        CASE
            WHEN NOT EXISTS (SELECT 1 FROM dbo.SyncCategories s WHERE s.Channel = @Channel AND s.IntegrationCompany = @IntegrationCompany) THEN 1
            WHEN NOT EXISTS (SELECT 1 FROM dbo.ProductSyncCategories l WHERE l.ProductId = @ProductId) THEN 1
            WHEN EXISTS
            (
                SELECT 1
                FROM dbo.ProductSyncCategories l
                JOIN dbo.SyncCategories s
                    ON s.CategoryId = l.CategoryId
                   AND s.Channel = @Channel
                   AND s.IntegrationCompany = @IntegrationCompany
                WHERE l.ProductId = @ProductId
            ) THEN 1
            ELSE 0
        END AS BIT) AS IsAllowed
);
GO

-- Allegro wystawia tylko produkty ze swoich kategorii (kategorie ShopGold też są pobierane do bazy).
CREATE OR ALTER PROCEDURE dbo.Products_GetToUpload
    @MinProductStock INT,
    @MinProductPrice DECIMAL(15,4),
    @IntegrationCompany INT,
    @Account INT
AS
BEGIN
    SET NOCOUNT ON;

    ;WITH EligibleProducts AS
    (
        SELECT
            p.Id,
            p.Code,
            p.Name,
            p.Description,
            p.Ean,
            p.Weight,
            p.Fits,
            p.SupplierName,
            p.InStock,
            p.Unit,
            p.CurrencyPrice,
            p.PriceNet,
            p.PriceGross,
            p.DefaultAllegroCategory,
            p.Package,
            p.CreatedDate,
            p.UpdatedDate,
            p.Substitutes,
            p.AllegroId,
            p.DeliveryType
        FROM dbo.Products p
        CROSS APPLY dbo.Products_IsAllowedInChannel(p.Id, N'Allegro', @IntegrationCompany) ch
        WHERE p.IntegrationCompany = @IntegrationCompany
          AND p.IsArchived = 0
          AND ch.IsAllowed = 1
          AND p.InStock >= @MinProductStock
          AND p.PriceNet >= @MinProductPrice
          AND NULLIF(p.DefaultAllegroCategory, 0) IS NOT NULL
          AND EXISTS (SELECT 1 FROM dbo.ProductCategories rc WHERE rc.ProductId = p.Id)
          AND NOT EXISTS
          (
              SELECT 1
              FROM dbo.AllegroOffers ao
              WHERE ao.ExternalId = p.Code
                AND ao.Account = @Account
          )
    )
    SELECT
        p.Id,
        p.Code,
        p.Name,
        p.Description,
        p.Ean,
        p.Weight,
        p.Fits,
        p.SupplierName,
        p.InStock,
        p.Unit,
        p.CurrencyPrice,
        p.PriceNet,
        p.PriceGross,
        p.DefaultAllegroCategory,
        p.Package,
        p.CreatedDate,
        p.UpdatedDate,
        p.Substitutes,
        p.AllegroId,
        p.DeliveryType,
        ps.Id,
        ps.ProductId,
        ps.Name,
        ps.Value,
        ps.UnitName,
        pp.Id,
        pp.ProductId,
        pp.CategoryParameterId,
        cp.Name,
        pp.Value,
        pp.IsForProduct,
        ap.Id,
        ap.ApplicationId,
        ap.Name,
        ap.ParentID,
        ap.ProductId,
        pack.Id,
        pack.PackEan,
        pack.PackGrossWeight,
        pack.PackNettWeight,
        pack.PackQty,
        pack.PackRequired,
        pack.PackUnit,
        pack.ProductId
    FROM EligibleProducts p
    LEFT JOIN dbo.ProductSpecifications ps ON ps.ProductId = p.Id
    JOIN dbo.ProductParameters pp ON pp.ProductId = p.Id
    JOIN dbo.CategoryParameters cp ON cp.Id = pp.CategoryParameterId
    LEFT JOIN dbo.ProductApplications ap ON ap.ProductId = p.Id
    LEFT JOIN dbo.ProductPackages pack ON pack.ProductId = p.Id
    ORDER BY p.Id;
END
GO

-- Oferta produktu spoza kategorii Allegro (kategoria została tylko w ShopGold) jest kończona
-- tak samo jak oferta produktu archiwalnego - IsArchived w wyniku oznacza "nie sprzedawaj na Allegro".
CREATE OR ALTER PROCEDURE [dbo].[AllegroOffers_GetOffersToUpdate]
    @DeliveryNames NVARCHAR(MAX),
    @IntegrationCompany INT,
    @Account INT
AS
BEGIN
    SET NOCOUNT ON;

    CREATE TABLE #OffersWithProducts
    (
        OfferId NVARCHAR(255) NOT NULL,
        ExternalId NVARCHAR(255) NULL,
        OfferName NVARCHAR(255) NOT NULL,
        CategoryId INT NOT NULL,
        Status NVARCHAR(50) NOT NULL,
        StartingAt DATETIME2 NOT NULL,
        DeliveryName NVARCHAR(255) NULL,
        ProductId INT NOT NULL,
        AllegroId NVARCHAR(255) NULL,
        Code NVARCHAR(255) NOT NULL,
        ProductName NVARCHAR(255) NOT NULL,
        Description NVARCHAR(MAX) NULL,
        Ean NVARCHAR(50) NULL,
        Weight FLOAT NOT NULL,
        Fits NVARCHAR(MAX) NULL,
        SupplierName NVARCHAR(255) NULL,
        Substitutes NVARCHAR(MAX) NULL,
        InStock FLOAT NOT NULL,
        Unit NVARCHAR(50) NULL,
        CurrencyPrice NVARCHAR(50) NULL,
        PriceNet DECIMAL(18, 2) NOT NULL,
        PriceGross DECIMAL(18, 2) NOT NULL,
        DefaultAllegroCategory INT NOT NULL,
        Package DECIMAL(18, 2) NOT NULL,
        DeliveryType INT NULL,
        IsArchived BIT NOT NULL,
        CreatedDate DATETIME2 NOT NULL,
        UpdatedDate DATETIME2 NOT NULL
    );

    INSERT INTO #OffersWithProducts
    (
        OfferId, ExternalId, OfferName, CategoryId, Status, StartingAt, DeliveryName,
        ProductId, AllegroId, Code, ProductName, Description, Ean, Weight, Fits, SupplierName,
        Substitutes, InStock, Unit, CurrencyPrice, PriceNet, PriceGross, DefaultAllegroCategory,
        Package, DeliveryType, IsArchived, CreatedDate, UpdatedDate
    )
    SELECT
        ao.Id, ao.ExternalId, ao.Name, ao.CategoryId, ao.Status, ao.StartingAt, ao.DeliveryName,
        p.Id, p.AllegroId, p.Code, p.Name, p.Description,
        p.Ean, p.Weight, p.Fits, p.SupplierName, p.Substitutes, p.InStock, p.Unit,
        p.CurrencyPrice, p.PriceNet, p.PriceGross, p.DefaultAllegroCategory, p.Package, p.DeliveryType,
        CAST(CASE WHEN p.IsArchived = 1 OR ch.IsAllowed = 0 THEN 1 ELSE 0 END AS BIT),
        p.CreatedDate, p.UpdatedDate
    FROM AllegroOffers ao
    INNER JOIN Products p ON p.Code = ao.ExternalId AND p.IntegrationCompany = @IntegrationCompany
    CROSS APPLY dbo.Products_IsAllowedInChannel(p.Id, N'Allegro', @IntegrationCompany) ch
    WHERE ao.Status IN ('ACTIVE', 'ENDED') and Account = @Account
        AND ao.DeliveryName IN (SELECT value FROM STRING_SPLIT(@DeliveryNames, ','));

    SELECT
        OfferId AS Id,
        ExternalId,
        OfferName AS Name,
        CategoryId,
        Status,
        StartingAt,
        DeliveryName,
        ProductId AS Id,
        AllegroId,
        Code,
        ProductName AS Name,
        Description,
        Ean,
        Weight,
        Fits,
        SupplierName,
        Substitutes,
        InStock,
        Unit,
        CurrencyPrice,
        PriceNet,
        PriceGross,
        DefaultAllegroCategory,
        Package,
        DeliveryType,
        IsArchived,
        CreatedDate,
        UpdatedDate
    FROM #OffersWithProducts;

    SELECT DISTINCT ai.*
    FROM AllegroImages ai
    WHERE ai.ProductId IN (SELECT DISTINCT ProductId FROM #OffersWithProducts)
      AND ai.Connected = 1 AND Account = @Account;

    SELECT ps.*
    FROM ProductSpecifications ps
    WHERE ps.ProductId IN (SELECT DISTINCT ProductId FROM #OffersWithProducts);

    SELECT pa.*
    FROM ProductApplications pa
    WHERE pa.ProductId IN (SELECT DISTINCT ProductId FROM #OffersWithProducts);

    SELECT pack.*
    FROM ProductPackages pack
    WHERE pack.ProductId IN (SELECT DISTINCT ProductId FROM #OffersWithProducts);

    SELECT param.Id,
    param.ProductId,
    param.CategoryParameterId,
    param.Value,
    param.IsForProduct,
    catParam.Name
    FROM ProductParameters param
    join dbo.CategoryParameters catParam on param.CategoryParameterId = catParam.Id
    WHERE param.ProductId IN (SELECT DISTINCT ProductId FROM #OffersWithProducts);
END
GO

-- Stan produktów wysłanych do ShopGold. Klucz to kod produktu (numer katalogowy w sklepie), nie Id -
-- produkt usunięty z bazy po archiwizacji musi jeszcze zostać wyłączony w sklepie.
IF NOT EXISTS (
    SELECT 1 FROM sys.tables WHERE name = 'ShopGoldProducts' AND schema_id = SCHEMA_ID('dbo')
)
BEGIN
    CREATE TABLE dbo.ShopGoldProducts
    (
        Code NVARCHAR(255) NOT NULL,
        IntegrationCompany INT NOT NULL,
        ProductId INT NULL,
        FieldsJson NVARCHAR(MAX) NOT NULL,
        ImagesHash NVARCHAR(64) NULL,
        IsActive BIT NOT NULL,
        CreatedDate DATETIME2 NOT NULL CONSTRAINT DF_ShopGoldProducts_CreatedDate DEFAULT (SYSUTCDATETIME()),
        UpdatedDate DATETIME2 NOT NULL CONSTRAINT DF_ShopGoldProducts_UpdatedDate DEFAULT (SYSUTCDATETIME()),

        CONSTRAINT PK_ShopGoldProducts PRIMARY KEY CLUSTERED (IntegrationCompany, Code)
    );
END
GO

IF TYPE_ID(N'dbo.ShopGoldProductStateType') IS NULL
BEGIN
    EXEC('CREATE TYPE dbo.ShopGoldProductStateType AS TABLE
    (
        Code NVARCHAR(255) NOT NULL PRIMARY KEY,
        ProductId INT NULL,
        FieldsJson NVARCHAR(MAX) NOT NULL,
        ImagesHash NVARCHAR(64) NULL,
        IsActive BIT NOT NULL
    );');
END
GO

CREATE OR ALTER PROCEDURE dbo.ShopGoldProducts_GetAll
    @IntegrationCompany INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT Code, ProductId, FieldsJson, ImagesHash, IsActive
    FROM dbo.ShopGoldProducts
    WHERE IntegrationCompany = @IntegrationCompany;
END
GO

CREATE OR ALTER PROCEDURE dbo.ShopGoldProducts_UpsertBatch
    @IntegrationCompany INT,
    @Items dbo.ShopGoldProductStateType READONLY
AS
BEGIN
    SET NOCOUNT ON;

    MERGE dbo.ShopGoldProducts AS target
    USING (SELECT * FROM @Items) AS src
    ON target.IntegrationCompany = @IntegrationCompany AND target.Code = src.Code
    WHEN MATCHED THEN
        UPDATE SET
            ProductId = src.ProductId,
            FieldsJson = src.FieldsJson,
            ImagesHash = src.ImagesHash,
            IsActive = src.IsActive,
            UpdatedDate = SYSUTCDATETIME()
    WHEN NOT MATCHED THEN
        INSERT (Code, IntegrationCompany, ProductId, FieldsJson, ImagesHash, IsActive)
        VALUES (src.Code, @IntegrationCompany, src.ProductId, src.FieldsJson, src.ImagesHash, src.IsActive);
END
GO

-- Produkty do ShopGold: z kategorii kanału ShopGold, niearchiwalne, z pobranymi szczegółami (kategorie Gąski).
-- Kanał ShopGold bez kategorii nie dostaje nic - inaczej trafiłby tam cały katalog Allegro.
CREATE OR ALTER PROCEDURE dbo.ShopGold_GetProducts
    @IntegrationCompany INT
AS
BEGIN
    SET NOCOUNT ON;

    CREATE TABLE #Ids (Id INT NOT NULL PRIMARY KEY);

    INSERT INTO #Ids (Id)
    SELECT DISTINCT p.Id
    FROM dbo.Products p
    JOIN dbo.ProductSyncCategories l ON l.ProductId = p.Id
    JOIN dbo.SyncCategories s
        ON s.CategoryId = l.CategoryId
       AND s.Channel = N'ShopGold'
       AND s.IntegrationCompany = @IntegrationCompany
    WHERE p.IntegrationCompany = @IntegrationCompany
      AND p.IsArchived = 0
      AND EXISTS (SELECT 1 FROM dbo.ProductCategories pc WHERE pc.ProductId = p.Id);

    SELECT p.Id, p.Code, p.CustomerCode, p.Name, p.Description, p.Ean, p.Weight, p.SupplierName,
           p.Substitutes, p.InStock, p.Unit, p.CurrencyPrice, p.PriceNet, p.PriceGross, p.Package,
           p.DeliveryType, p.IntegrationId, p.IsArchived
    FROM dbo.Products p
    JOIN #Ids i ON i.Id = p.Id;

    SELECT pc.ProductId, pc.Name
    FROM dbo.ProductCategories pc
    JOIN #Ids i ON i.Id = pc.ProductId;

    SELECT ps.ProductId, ps.Name, ps.Value, ps.UnitName
    FROM dbo.ProductSpecifications ps
    JOIN #Ids i ON i.Id = ps.ProductId;

    SELECT pa.ProductId, pa.ApplicationId, pa.ParentID, pa.Name
    FROM dbo.ProductApplications pa
    JOIN #Ids i ON i.Id = pa.ProductId;

    SELECT pack.ProductId, pack.PackUnit, pack.PackQty, pack.PackNettWeight, pack.PackGrossWeight, pack.PackEan, pack.PackRequired
    FROM dbo.ProductPackages pack
    JOIN #Ids i ON i.Id = pack.ProductId;
END
GO

-- Dopasowanie kategorii Allegro (i dalej parametry) tylko dla produktów sprzedawanych na Allegro -
-- produkty z kategorii samego ShopGold nie zużywają limitów API Allegro.
CREATE OR ALTER PROCEDURE dbo.Products_GetWithoutDefaultCategory
@IntegrationCompany INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        p.Id,
        p.Code,
        p.Name,
        p.Description,
        p.Ean,
        p.Weight,
        p.Fits,
        p.SupplierName,
        p.InStock,
        p.Unit,
        p.CurrencyPrice,
        p.PriceNet,
        p.PriceGross,
        p.DefaultAllegroCategory,
        p.Package,
        p.CreatedDate,
        p.UpdatedDate,
        ps.Id,
        ps.ProductId,
        ps.Name,
        ps.Value,
        ps.UnitName
    FROM Products p
    CROSS APPLY dbo.Products_IsAllowedInChannel(p.Id, N'Allegro', @IntegrationCompany) ch
    LEFT JOIN ProductSpecifications ps ON ps.ProductId = p.Id
    JOIN dbo.ProductCategories pc ON p.Id = pc.ProductId
    WHERE NULLIF(p.DefaultAllegroCategory, 0) IS NULL
      AND IntegrationCompany = @IntegrationCompany
      AND ch.IsAllowed = 1
    ORDER BY p.Id;
END
GO
