IF NOT EXISTS (
    SELECT 1 
    FROM sys.columns 
    WHERE Name = N'Account' 
      AND Object_ID = Object_ID(N'dbo.AllegroImages')
)
BEGIN
    ALTER TABLE dbo.AllegroImages
    ADD Account INT NOT NULL DEFAULT 1;
END