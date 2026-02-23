IF NOT EXISTS (
    SELECT 1
    FROM sys.tables t
    INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
    WHERE t.name = 'ProductCategories'
      AND s.name = 'dbo'
)
BEGIN
    CREATE TABLE dbo.ProductCategories
    (
        Id INT IDENTITY(1,1) NOT NULL,
        ProductId INT NOT NULL,
        Name NVARCHAR(255) NOT NULL,

        CONSTRAINT PK_Categories PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_Categoryies_Product
            FOREIGN KEY (ProductId)
            REFERENCES dbo.Products (Id)
            ON DELETE CASCADE
    );
END