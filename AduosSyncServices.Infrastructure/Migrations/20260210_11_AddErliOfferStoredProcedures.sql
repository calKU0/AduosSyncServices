CREATE OR ALTER PROCEDURE dbo.AllegroOffers_GetWithDetails
    @Account INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT Id, ExistsInErli
    FROM AllegroOffers
    WHERE Account = @Account;
END
GO

CREATE OR ALTER PROCEDURE dbo.AllegroOffers_UpdateExistsInErli
    @Id NVARCHAR(100),
    @ExistsInErli BIT
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE AllegroOffers
    SET ExistsInErli = @ExistsInErli
    WHERE Id = @Id;
END
GO

CREATE OR ALTER PROCEDURE dbo.AllegroOffers_GetForErliCreation
    @Account INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT *
    FROM AllegroOffers o
    JOIN AllegroOfferDescriptions d ON o.Id = d.OfferId
    LEFT JOIN AllegroOfferAttributes a on a.OfferId = o.Id and a.type in ('dictionary', 'string', 'number', 'float', 'int')
    WHERE o.Account = 1 AND ExistsInErli = 0
      AND o.Status in ('ACTIVE', 'ENDED')
      AND Price > 0 AND Stock > 0
      AND CategoryId != 0 AND CategoryId is not null
      AND Account = @Account;
END
GO

CREATE OR ALTER PROCEDURE dbo.AllegroOffers_GetForErliUpdate
    @Account INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT *
    FROM AllegroOffers o
    JOIN AllegroOfferDescriptions d ON o.Id = d.OfferId
    LEFT JOIN AllegroOfferAttributes a on a.OfferId = o.Id and a.type in ('dictionary', 'string', 'number', 'float', 'int')
    WHERE ExistsInErli = 1 AND Price > 0 AND CategoryId != 0 AND CategoryId is not null AND Account = @Account;
END
GO
