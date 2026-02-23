CREATE OR ALTER PROCEDURE dbo.AllegroTokens_GetByTokenName
    @TokenName NVARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP 1 *
    FROM AllegroTokenEntities
    WHERE TokenName = @TokenName;
END
GO

CREATE OR ALTER PROCEDURE dbo.AllegroTokens_Upsert
    @AccessToken NVARCHAR(MAX),
    @RefreshToken NVARCHAR(MAX),
    @ExpiryDateUtc DATETIME2,
    @TokenName NVARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE AllegroTokenEntities
    SET AccessToken = @AccessToken,
        RefreshToken = @RefreshToken,
        ExpiryDateUtc = @ExpiryDateUtc
    WHERE TokenName = @TokenName;

    IF @@ROWCOUNT = 0
    BEGIN
        INSERT INTO AllegroTokenEntities (AccessToken, RefreshToken, ExpiryDateUtc, TokenName)
        VALUES (@AccessToken, @RefreshToken, @ExpiryDateUtc, @TokenName);
    END
END
GO

CREATE OR ALTER PROCEDURE dbo.AllegroTokens_DeleteByTokenName
    @TokenName NVARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON;

    DELETE FROM AllegroTokenEntities
    WHERE TokenName = @TokenName;
END
GO
