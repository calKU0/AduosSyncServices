using System.Text.Json.Serialization;

namespace AduosSyncServices.Contracts.DTOs.Allegro
{
    public class AllegroOfferDetails
    {
        public class Root
        {
            public List<ProductSet> ProductSet { get; set; }
            public string Id { get; set; }
            public string Name { get; set; }
            public Category Category { get; set; }
            public Delivery Delivery { get; set; }
            public Publication Publication { get; set; }
            public AdditionalMarketplaces AdditionalMarketplaces { get; set; }
            public B2b B2b { get; set; }
            public CompatibilityList CompatibilityList { get; set; }
            public string Language { get; set; }
            public Validation Validation { get; set; }
            public List<object> Warnings { get; set; }
            public AfterSalesServices AfterSalesServices { get; set; }
            public Discounts Discounts { get; set; }
            public Stock Stock { get; set; }
            public List<Parameter> Parameters { get; set; }
            public Contact Contact { get; set; }
            public DateTime CreatedAt { get; set; }
            public DateTime UpdatedAt { get; set; }
            public Payments Payments { get; set; }
            public SellingMode SellingMode { get; set; }
            public Location Location { get; set; }
            public List<string> Images { get; set; }
            public Description Description { get; set; }
            public External External { get; set; }
            public MessageToSellerSettings MessageToSellerSettings { get; set; }
        }

        public class Additional
        {
            public string Id { get; set; }
        }

        public class AdditionalMarketplaces
        {
            [JsonPropertyName("allegro-cz")]
            public AllegroCz AllegroCz { get; set; }
        }

        public class AdditionalServices
        {
            public string Id { get; set; }
        }

        public class AfterSalesServices
        {
            public ImpliedWarranty ImpliedWarranty { get; set; }
            public ReturnPolicy ReturnPolicy { get; set; }
            public Warranty Warranty { get; set; }
        }

        public class AllegroCz
        {
            public SellingMode SellingMode { get; set; }
            public Publication Publication { get; set; }
        }

        public class Attachment
        {
            public string Id { get; set; }
        }

        public class B2b
        {
            public bool BuyableOnlyByBusiness { get; set; }
        }

        public class Base
        {
            public string Id { get; set; }
        }

        public class Category
        {
            public string Id { get; set; }
        }

        public class CompatibilityList
        {
            public string Type { get; set; }
        }

        public class Contact
        {
            public string Id { get; set; }
        }

        public class Delivery
        {
            public string HandlingTime { get; set; }
            public ShippingRates ShippingRates { get; set; }
            public string AdditionalInfo { get; set; }
            public DateTime? ShipmentDate { get; set; }
        }

        public class Deposit
        {
            public string Id { get; set; }
            public int Quantity { get; set; }
        }

        public class Description
        {
            public List<Section> Sections { get; set; }
        }

        public class Discounts
        {
            public WholesalePriceList WholesalePriceList { get; set; }
        }

        public class Error
        {
            public string Code { get; set; }
            public string Details { get; set; }
            public string Message { get; set; }
            public string Path { get; set; }
            public string UserMessage { get; set; }
            public Metadata Metadata { get; set; }
        }

        public class External
        {
            public string Id { get; set; }
        }

        public class FundraisingCampaign
        {
            public string Id { get; set; }
        }

        public class ImpliedWarranty
        {
            public string Id { get; set; }
        }

        public class Item
        {
            public string Type { get; set; }
            public string Url { get; set; }
            public string Content { get; set; }
        }

        public class Location
        {
            public string City { get; set; }
            public string CountryCode { get; set; }
            public string PostCode { get; set; }
            public string Province { get; set; }
        }

        public class Marketplaces
        {
            public Base Base { get; set; }
            public List<Additional> Additional { get; set; }
        }

        public class MessageToSellerSettings
        {
            public string Mode { get; set; }
            public string Hint { get; set; }
        }

        public class Metadata
        {
            public string ProductId { get; set; }
        }

        public class MinimalPrice
        {
            public string Amount { get; set; }
            public string Currency { get; set; }
        }

        public class Parameter
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public RangeValue RangeValue { get; set; }
            public List<string> Values { get; set; }
            public List<string> ValuesIds { get; set; }
            public List<string> MaxAllowedPriceDecreasePercent { get; set; }
        }

        public class Payments
        {
            public string Invoice { get; set; }
        }

        public class Price
        {
            public string Amount { get; set; }
            public string Currency { get; set; }
        }

        public class Product
        {
            public string Id { get; set; }
            public Publication Publication { get; set; }
            public List<Parameter> Parameters { get; set; }
        }

        public class ProductSet
        {
            public Quantity Quantity { get; set; }
            public Product Product { get; set; }
            public ResponsiblePerson ResponsiblePerson { get; set; }
            public ResponsibleProducer ResponsibleProducer { get; set; }
            public SafetyInformation SafetyInformation { get; set; }
            public bool MarketedBeforeGPSRObligation { get; set; }
            public List<Deposit> Deposits { get; set; }
        }

        public class Publication
        {
            public string Status { get; set; }
            public string Duration { get; set; }
            public DateTime? StartingAt { get; set; }
            public bool Republish { get; set; }
            public DateTime? EndingAt { get; set; }
            public string EndedBy { get; set; }
            public Marketplaces Marketplaces { get; set; }
            public string State { get; set; }
        }

        public class Quantity
        {
            public int Value { get; set; }
        }

        public class RangeValue
        {
            public string From { get; set; }
            public string To { get; set; }
        }

        public class ResponsiblePerson
        {
            public string Id { get; set; }
        }

        public class ResponsibleProducer
        {
            public string Id { get; set; }
        }

        public class ReturnPolicy
        {
            public string Id { get; set; }
        }

        public class SafetyInformation
        {
            public string Type { get; set; }
            public string Description { get; set; }
        }

        public class Section
        {
            public List<Item> Items { get; set; }
        }

        public class SellingMode
        {
            public Price Price { get; set; }
            public string Format { get; set; }
            public MinimalPrice MinimalPrice { get; set; }
            public StartingPrice StartingPrice { get; set; }
        }

        public class ShippingRates
        {
            public string Id { get; set; }
        }

        public class SizeTable
        {
            public string Id { get; set; }
        }

        public class StartingPrice
        {
            public string Amount { get; set; }
            public string Currency { get; set; }
        }

        public class Stock
        {
            public int? Available { get; set; }
            public string Unit { get; set; }
        }

        public class TaxSettings
        {
            public List<Rate> Rates { get; set; }
            public string Subject { get; set; }
            public string Exemption { get; set; }
        }

        public class Validation
        {
            public List<Error> Errors { get; set; }
            public List<Warning> Warnings { get; set; }
            public DateTime? ValidatedAt { get; set; }
        }

        public class Warning
        {
            public string Code { get; set; }
            public string Details { get; set; }
            public string Message { get; set; }
            public string Path { get; set; }
            public string UserMessage { get; set; }
            public Metadata Metadata { get; set; }
        }

        public class Warranty
        {
            public string Id { get; set; }
        }

        public class WholesalePriceList
        {
            public string Id { get; set; }
        }
    }
}