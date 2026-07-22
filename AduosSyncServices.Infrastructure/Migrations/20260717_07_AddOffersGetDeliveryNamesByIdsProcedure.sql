-- Returns Id + DeliveryName for only the offers whose ids are passed in (the offers referenced by the
-- orders currently being synced), instead of pulling the whole offer catalog. @Ids is a comma-separated
-- list of Allegro offer ids (numeric strings, never contain commas). Used by the order sync to resolve
-- each line item's shipping method locally.
CREATE OR ALTER PROCEDURE dbo.AllegroOffers_GetDeliveryNamesByIds
    @Ids NVARCHAR(MAX),
    @Account INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT o.Id, o.DeliveryName
    FROM dbo.AllegroOffers o
    INNER JOIN STRING_SPLIT(@Ids, ',') s ON s.value = o.Id
    WHERE o.Account = @Account;
END
