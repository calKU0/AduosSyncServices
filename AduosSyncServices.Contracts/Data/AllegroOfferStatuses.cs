namespace AduosSyncServices.Contracts.Data
{
    /// <summary>
    /// Allegro offer publication statuses, as used both in the Allegro API's publication payloads
    /// and in the AllegroOffers.Status column. Kept as string constants (not an enum) because the
    /// values travel as raw strings through the API and the database.
    /// </summary>
    public static class AllegroOfferStatuses
    {
        public const string Active = "ACTIVE";
        public const string Inactive = "INACTIVE";
        public const string Ended = "ENDED";
    }
}
