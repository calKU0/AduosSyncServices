IF NOT EXISTS (
    SELECT 1 
    FROM sys.columns 
    WHERE Name = N'Account' 
      AND Object_ID = Object_ID(N'dbo.AllegroOffers')
)
BEGIN
    ALTER TABLE dbo.AllegroOffers
    ADD Account varchar(100) NULL;
END