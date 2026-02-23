namespace AduosSyncServices.Contracts.DTOs.Allegro
{
    public class OffersResponse
    {
        public List<Offer> Offers { get; set; }
        public int Count { get; set; }
        public int TotalCount { get; set; }
    }

    public class Additional
    {
        public string Id { get; set; }
    }

    public class Base
    {
        public string Id { get; set; }
    }

    public class CurrentPrice
    {
        public string Amount { get; set; }
        public string Currency { get; set; }
    }

    public class Marketplaces
    {
        public Base Base { get; set; }
        public List<Additional> Additional { get; set; }
    }

    public class Offer
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public Category Category { get; set; }
        public PrimaryImage PrimaryImage { get; set; }
        public SellingMode SellingMode { get; set; }
        public SaleInfo SaleInfo { get; set; }
        public Stock Stock { get; set; }
        public Stats Stats { get; set; }
        public Publication Publication { get; set; }
        public AfterSalesServices AfterSalesServices { get; set; }
        public AdditionalServices AdditionalServices { get; set; }
        public External External { get; set; }
        public Delivery Delivery { get; set; }
        public B2b B2b { get; set; }
        public FundraisingCampaign FundraisingCampaign { get; set; }
        public AdditionalMarketplaces AdditionalMarketplaces { get; set; }
    }

    public class PriceAutomation
    {
        public Rule Rule { get; set; }
    }

    public class PrimaryImage
    {
        public string Url { get; set; }
    }

    public class Rule
    {
        public string Id { get; set; }
    }

    public class SaleInfo
    {
        public CurrentPrice CurrentPrice { get; set; }
        public int BiddersCount { get; set; }
    }

    public class Stats
    {
        public int WatchersCount { get; set; }
        public int VisitsCount { get; set; }
    }
}