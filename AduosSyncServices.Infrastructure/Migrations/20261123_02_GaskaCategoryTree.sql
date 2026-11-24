-- Drzewo kategorii Gąski (z API /categories). Serwis Allegro odświeża je na początku każdego
-- pobierania produktów, a ServicesManager pokazuje z niego listę kategorii do wyboru
-- (tak jak w JSAGRO) - zamiast wpisywania numerów kategorii ręcznie.

IF OBJECT_ID(N'dbo.GaskaCategories', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.GaskaCategories
    (
        Id        INT            NOT NULL CONSTRAINT PK_GaskaCategories PRIMARY KEY,   -- id kategorii w Gąsce
        ParentId  INT            NULL,
        Name      NVARCHAR(255)  NOT NULL,
        Path      NVARCHAR(1000) NOT NULL,   -- "A > B > C" do wyświetlania
        Depth     INT            NOT NULL CONSTRAINT DF_GaskaCategories_Depth DEFAULT 0,
        UpdatedAt DATETIME2      NOT NULL CONSTRAINT DF_GaskaCategories_UpdatedAt DEFAULT SYSUTCDATETIME()
    );

    CREATE INDEX IX_GaskaCategories_Parent ON dbo.GaskaCategories (ParentId);
END
GO

IF TYPE_ID(N'dbo.GaskaCategoryNodeType') IS NULL
    CREATE TYPE dbo.GaskaCategoryNodeType AS TABLE
    (
        Id       INT           NOT NULL PRIMARY KEY,
        ParentId INT           NULL,
        Name     NVARCHAR(255) NOT NULL
    );
GO

-- Dodaje i aktualizuje węzły. Kategorie, które zniknęły z Gąski, zostają - mogą być nadal
-- wskazane w konfiguracji; ServicesManager pokaże je wtedy jako "brak na liście".
CREATE OR ALTER PROCEDURE dbo.GaskaCategories_Upsert
    @Nodes dbo.GaskaCategoryNodeType READONLY
AS
BEGIN
    SET NOCOUNT ON;

    MERGE dbo.GaskaCategories AS target
    USING @Nodes AS source
       ON target.Id = source.Id
    WHEN MATCHED AND (target.Name <> source.Name OR ISNULL(target.ParentId, -1) <> ISNULL(source.ParentId, -1)) THEN
        UPDATE SET Name = source.Name, ParentId = source.ParentId, UpdatedAt = SYSUTCDATETIME()
    WHEN NOT MATCHED BY TARGET THEN
        INSERT (Id, ParentId, Name, Path)
        VALUES (source.Id, source.ParentId, source.Name, source.Name);

    -- Rodzic spoza drzewa = korzeń (inaczej węzeł wypadłby z przeliczania ścieżek).
    WITH Tree AS
    (
        SELECT c.Id, CAST(c.Name AS NVARCHAR(1000)) AS Path, 0 AS Depth
        FROM dbo.GaskaCategories c
        WHERE c.ParentId IS NULL
           OR NOT EXISTS (SELECT 1 FROM dbo.GaskaCategories p WHERE p.Id = c.ParentId)

        UNION ALL

        SELECT ch.Id, CAST(t.Path + N' > ' + ch.Name AS NVARCHAR(1000)), t.Depth + 1
        FROM dbo.GaskaCategories ch
        JOIN Tree t ON ch.ParentId = t.Id
        WHERE t.Depth < 30   -- ochrona przed cyklem w danych z API
    )
    UPDATE c
    SET c.Path = t.Path, c.Depth = t.Depth
    FROM dbo.GaskaCategories c
    JOIN Tree t ON t.Id = c.Id
    WHERE c.Path <> t.Path OR c.Depth <> t.Depth
    OPTION (MAXRECURSION 100);
END
GO

CREATE OR ALTER PROCEDURE dbo.GaskaCategories_GetAll
AS
BEGIN
    SET NOCOUNT ON;

    SELECT Id, Path, Depth
    FROM dbo.GaskaCategories
    ORDER BY Path;
END
GO
