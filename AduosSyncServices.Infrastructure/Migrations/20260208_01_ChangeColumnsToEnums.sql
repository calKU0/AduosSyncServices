IF EXISTS (
    SELECT 1
    FROM sys.columns c
    JOIN sys.types t ON c.user_type_id = t.user_type_id
    WHERE c.name = N'IntegrationCompany'
      AND c.object_id = OBJECT_ID(N'dbo.Products')
      AND t.name IN (N'nvarchar', N'varchar')
)
BEGIN
    EXEC('ALTER TABLE dbo.Products ADD IntegrationCompanyTemp INT NULL');

    EXEC('ALTER TABLE dbo.Products DROP COLUMN IntegrationCompany');

    EXEC('EXEC sp_rename
        ''dbo.Products.IntegrationCompanyTemp'',
        ''IntegrationCompany'',
        ''COLUMN''
    ');
END

IF EXISTS (
    SELECT 1
    FROM sys.columns c
    JOIN sys.types t ON c.user_type_id = t.user_type_id
    WHERE c.name = N'Account'
      AND c.object_id = OBJECT_ID(N'dbo.AllegroOffers')
      AND t.name IN (N'nvarchar', N'varchar')
)
BEGIN
    EXEC('ALTER TABLE dbo.AllegroOffers ADD AccountTemp INT NULL');

    EXEC('ALTER TABLE dbo.AllegroOffers DROP COLUMN Account');

    EXEC('EXEC sp_rename
        ''dbo.AllegroOffers.AccountTemp'',
        ''Account'',
        ''COLUMN''
    ');
END

