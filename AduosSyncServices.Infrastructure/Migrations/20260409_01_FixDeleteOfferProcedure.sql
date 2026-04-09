CREATE OR ALTER PROCEDURE [dbo].[AllegroOffers_DeleteByProductId]
    @ProductId INT,
    @Account INT,
    @IntegrationCompany INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Code NVARCHAR(255);
    DECLARE @OfferId NVARCHAR(255);

    SELECT @Code = Code
    FROM Products
    WHERE Id = @ProductId AND IntegrationCompany = @IntegrationCompany;

    IF @Code IS NULL
    BEGIN
        SELECT CAST(NULL AS NVARCHAR(255)) AS Code;
        RETURN;
    END

    SELECT @OfferId = Id
    FROM AllegroOffers
    WHERE ExternalId = @Code AND @Account = Account;

    IF @OfferId IS NOT NULL
    BEGIN
        DELETE FROM AllegroOfferAttributes
        WHERE OfferId = @OfferId;

        DELETE FROM AllegroOfferDescriptions
        WHERE OfferId = @OfferId;

        DELETE FROM AllegroOffers
        WHERE Id = @OfferId;
    END

    SELECT @Code AS Code;
END
GO;

CREATE OR ALTER PROCEDURE [dbo].[AllegroOffers_Delete]
    @OfferId varchar(150) = null
AS
BEGIN
    SET NOCOUNT ON;

    IF @OfferId IS NOT NULL
    BEGIN
        DELETE FROM AllegroOfferAttributes
        WHERE OfferId = @OfferId;

        DELETE FROM AllegroOfferDescriptions
        WHERE OfferId = @OfferId;

        DELETE FROM AllegroOffers
        WHERE Id = @OfferId;
    END
END