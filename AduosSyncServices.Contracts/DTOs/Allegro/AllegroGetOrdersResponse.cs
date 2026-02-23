using AduosSyncServices.Contracts.Data.Enums;
using System.Text.Json.Serialization;

namespace AduosSyncServices.Contracts.DTOs.Allegro
{
    public class AllegroGetOrdersResponse
    {
        [JsonPropertyName("checkoutForms")]
        public List<CheckoutForm> CheckoutForms { get; set; }

        [JsonPropertyName("count")]
        public int Count { get; set; }

        [JsonPropertyName("totalCount")]
        public int TotalCount { get; set; }

        public class Address
        {
            [JsonPropertyName("street")]
            public string Street { get; set; }

            [JsonPropertyName("city")]
            public string City { get; set; }

            [JsonPropertyName("postCode")]
            public string PostCode { get; set; }

            [JsonPropertyName("countryCode")]
            public string CountryCode { get; set; }

            [JsonPropertyName("firstName")]
            public string FirstName { get; set; }

            [JsonPropertyName("lastName")]
            public string LastName { get; set; }

            [JsonPropertyName("zipCode")]
            public string ZipCode { get; set; }

            [JsonPropertyName("companyName")]
            public string CompanyName { get; set; }

            [JsonPropertyName("phoneNumber")]
            public string PhoneNumber { get; set; }

            [JsonPropertyName("modifiedAt")]
            public string ModifiedAt { get; set; }

            [JsonPropertyName("company")]
            public Company Company { get; set; }

            [JsonPropertyName("naturalPerson")]
            public NaturalPerson NaturalPerson { get; set; }
        }

        public class Buyer
        {
            [JsonPropertyName("id")]
            public string Id { get; set; }

            [JsonPropertyName("email")]
            public string Email { get; set; }

            [JsonPropertyName("login")]
            public string Login { get; set; }

            [JsonPropertyName("firstName")]
            public string FirstName { get; set; }

            [JsonPropertyName("lastName")]
            public string LastName { get; set; }

            [JsonPropertyName("companyName")]
            public string CompanyName { get; set; }

            [JsonPropertyName("guest")]
            public bool Guest { get; set; }

            [JsonPropertyName("personalIdentity")]
            public string PersonalIdentity { get; set; }

            [JsonPropertyName("phoneNumber")]
            public string PhoneNumber { get; set; }

            [JsonPropertyName("preferences")]
            public Preferences Preferences { get; set; }

            [JsonPropertyName("address")]
            public Address Address { get; set; }
        }

        public class Cancellation
        {
            [JsonPropertyName("date")]
            public DateTime Date { get; set; }
        }

        public class CheckoutForm
        {
            [JsonPropertyName("id")]
            public string Id { get; set; }

            [JsonPropertyName("messageToSeller")]
            public string MessageToSeller { get; set; }

            [JsonPropertyName("buyer")]
            public Buyer Buyer { get; set; }

            [JsonPropertyName("payment")]
            public Payment Payment { get; set; }

            [JsonPropertyName("status")]
            public AllegroCheckoutFormStatus Status { get; set; }

            [JsonPropertyName("fulfillment")]
            public Fulfillment Fulfillment { get; set; }

            [JsonPropertyName("delivery")]
            public Delivery Delivery { get; set; }

            [JsonPropertyName("invoice")]
            public Invoice Invoice { get; set; }

            [JsonPropertyName("lineItems")]
            public List<LineItem> LineItems { get; set; }

            [JsonPropertyName("surcharges")]
            public List<Surcharge> Surcharges { get; set; }

            [JsonPropertyName("discounts")]
            public List<Discount> Discounts { get; set; }

            [JsonPropertyName("note")]
            public Note Note { get; set; }

            [JsonPropertyName("marketplace")]
            public Marketplace Marketplace { get; set; }

            [JsonPropertyName("summary")]
            public Summary Summary { get; set; }

            [JsonPropertyName("updatedAt")]
            public DateTime UpdatedAt { get; set; }

            [JsonPropertyName("revision")]
            public string Revision { get; set; }
        }

        public class Company
        {
            [JsonPropertyName("name")]
            public string Name { get; set; }

            [JsonPropertyName("ids")]
            public List<Id> Ids { get; set; }

            [JsonPropertyName("vatPayerStatus")]
            public string VatPayerStatus { get; set; }

            [JsonPropertyName("taxId")]
            public string TaxId { get; set; }
        }

        public class Cost
        {
            [JsonPropertyName("amount")]
            public string Amount { get; set; }

            [JsonPropertyName("currency")]
            public string Currency { get; set; }
        }

        public class Delivery
        {
            [JsonPropertyName("address")]
            public Address Address { get; set; }

            [JsonPropertyName("method")]
            public Method Method { get; set; }

            [JsonPropertyName("pickupPoint")]
            public PickupPoint PickupPoint { get; set; }

            [JsonPropertyName("cost")]
            public Cost Cost { get; set; }

            [JsonPropertyName("time")]
            public Time Time { get; set; }

            [JsonPropertyName("smart")]
            public bool Smart { get; set; }

            [JsonPropertyName("cancellation")]
            public Cancellation Cancellation { get; set; }
        }

        public class Deposit
        {
            [JsonPropertyName("price")]
            public Price Price { get; set; }
        }

        public class Discount
        {
            [JsonPropertyName("type")]
            public string Type { get; set; }
        }

        public class Dispatch
        {
            [JsonPropertyName("from")]
            public DateTime From { get; set; }

            [JsonPropertyName("to")]
            public DateTime To { get; set; }
        }

        public class External
        {
            [JsonPropertyName("id")]
            public string Id { get; set; }
        }

        public class Fulfillment
        {
            [JsonPropertyName("status")]
            public AllegroOrderStatus Status { get; set; }

            [JsonPropertyName("shipmentSummary")]
            public ShipmentSummary ShipmentSummary { get; set; }
        }

        public class Guaranteed
        {
            [JsonPropertyName("from")]
            public DateTime From { get; set; }

            [JsonPropertyName("to")]
            public DateTime To { get; set; }
        }

        public class Id
        {
            [JsonPropertyName("type")]
            public string Type { get; set; }

            [JsonPropertyName("value")]
            public string Value { get; set; }
        }

        public class Invoice
        {
            [JsonPropertyName("required")]
            public bool Required { get; set; }

            [JsonPropertyName("address")]
            public Address Address { get; set; }

            [JsonPropertyName("dueDate")]
            public string DueDate { get; set; }

            [JsonPropertyName("features")]
            public List<string> Features { get; set; }
        }

        public class LineItem
        {
            [JsonPropertyName("id")]
            public string Id { get; set; }

            [JsonPropertyName("offer")]
            public Offer Offer { get; set; }

            [JsonPropertyName("quantity")]
            public int Quantity { get; set; }

            [JsonPropertyName("originalPrice")]
            public OriginalPrice OriginalPrice { get; set; }

            [JsonPropertyName("price")]
            public Price Price { get; set; }

            [JsonPropertyName("deposit")]
            public Deposit Deposit { get; set; }

            [JsonPropertyName("reconciliation")]
            public Reconciliation Reconciliation { get; set; }

            [JsonPropertyName("selectedAdditionalServices")]
            public List<SelectedAdditionalService> SelectedAdditionalServices { get; set; }

            [JsonPropertyName("vouchers")]
            public List<Voucher> Vouchers { get; set; }

            [JsonPropertyName("tax")]
            public Tax Tax { get; set; }

            [JsonPropertyName("boughtAt")]
            public DateTime BoughtAt { get; set; }

            [JsonPropertyName("discounts")]
            public List<Discount> Discounts { get; set; }
        }

        public class Marketplace
        {
            [JsonPropertyName("id")]
            public string Id { get; set; }
        }

        public class Method
        {
            [JsonPropertyName("id")]
            public string Id { get; set; }

            [JsonPropertyName("name")]
            public string Name { get; set; }
        }

        public class NaturalPerson
        {
            [JsonPropertyName("firstName")]
            public string FirstName { get; set; }

            [JsonPropertyName("lastName")]
            public string LastName { get; set; }
        }

        public class Note
        {
            [JsonPropertyName("text")]
            public string Text { get; set; }
        }

        public class Offer
        {
            [JsonPropertyName("id")]
            public string Id { get; set; }

            [JsonPropertyName("name")]
            public string Name { get; set; }

            [JsonPropertyName("external")]
            public External External { get; set; }

            [JsonPropertyName("productSet")]
            public ProductSet ProductSet { get; set; }
        }

        public class OriginalPrice
        {
            [JsonPropertyName("amount")]
            public string Amount { get; set; }

            [JsonPropertyName("currency")]
            public string Currency { get; set; }
        }

        public class PaidAmount
        {
            [JsonPropertyName("amount")]
            public string Amount { get; set; }

            [JsonPropertyName("currency")]
            public string Currency { get; set; }
        }

        public class Payment
        {
            [JsonPropertyName("id")]
            public string Id { get; set; }

            [JsonPropertyName("type")]
            public AllegroPaymentType Type { get; set; }

            [JsonPropertyName("provider")]
            public string Provider { get; set; }

            [JsonPropertyName("paidAmount")]
            public PaidAmount PaidAmount { get; set; }

            [JsonPropertyName("reconciliation")]
            public Reconciliation Reconciliation { get; set; }

            [JsonPropertyName("features")]
            public List<string> Features { get; set; }
        }

        public class PickupPoint
        {
            [JsonPropertyName("id")]
            public string Id { get; set; }

            [JsonPropertyName("name")]
            public string Name { get; set; }

            [JsonPropertyName("description")]
            public string Description { get; set; }

            [JsonPropertyName("address")]
            public Address Address { get; set; }
        }

        public class Preferences
        {
            [JsonPropertyName("language")]
            public string Language { get; set; }
        }

        public class Price
        {
            [JsonPropertyName("amount")]
            public string Amount { get; set; }

            [JsonPropertyName("currency")]
            public string Currency { get; set; }
        }

        public class Product
        {
            [JsonPropertyName("id")]
            public string Id { get; set; }

            [JsonPropertyName("quantity")]
            public int Quantity { get; set; }
        }

        public class ProductSet
        {
            [JsonPropertyName("products")]
            public List<Product> Products { get; set; }
        }

        public class Reconciliation
        {
            [JsonPropertyName("amount")]
            public string Amount { get; set; }

            [JsonPropertyName("currency")]
            public string Currency { get; set; }

            [JsonPropertyName("value")]
            public Value Value { get; set; }

            [JsonPropertyName("type")]
            public string Type { get; set; }

            [JsonPropertyName("quantity")]
            public int Quantity { get; set; }
        }

        public class SelectedAdditionalService
        {
            [JsonPropertyName("definitionId")]
            public string DefinitionId { get; set; }

            [JsonPropertyName("name")]
            public string Name { get; set; }

            [JsonPropertyName("price")]
            public Price Price { get; set; }

            [JsonPropertyName("quantity")]
            public int Quantity { get; set; }
        }

        public class ShipmentSummary
        {
            [JsonPropertyName("lineItemsSent")]
            public string LineItemsSent { get; set; }
        }

        public class Summary
        {
            [JsonPropertyName("totalToPay")]
            public TotalToPay TotalToPay { get; set; }
        }

        public class Surcharge
        {
            [JsonPropertyName("id")]
            public string Id { get; set; }

            [JsonPropertyName("type")]
            public string Type { get; set; }

            [JsonPropertyName("provider")]
            public string Provider { get; set; }

            [JsonPropertyName("finishedAt")]
            public DateTime FinishedAt { get; set; }

            [JsonPropertyName("paidAmount")]
            public PaidAmount PaidAmount { get; set; }

            [JsonPropertyName("reconciliation")]
            public Reconciliation Reconciliation { get; set; }

            [JsonPropertyName("features")]
            public List<string> Features { get; set; }
        }

        public class Tax
        {
            [JsonPropertyName("rate")]
            public string Rate { get; set; }

            [JsonPropertyName("subject")]
            public string Subject { get; set; }

            [JsonPropertyName("exemption")]
            public string Exemption { get; set; }
        }

        public class Time
        {
            [JsonPropertyName("guaranteed")]
            public Guaranteed Guaranteed { get; set; }

            [JsonPropertyName("dispatch")]
            public Dispatch Dispatch { get; set; }
        }

        public class TotalToPay
        {
            [JsonPropertyName("amount")]
            public string Amount { get; set; }

            [JsonPropertyName("currency")]
            public string Currency { get; set; }
        }

        public class Value
        {
            [JsonPropertyName("amount")]
            public string Amount { get; set; }

            [JsonPropertyName("currency")]
            public string Currency { get; set; }
        }

        public class Voucher
        {
            [JsonPropertyName("code")]
            public string Code { get; set; }

            [JsonPropertyName("type")]
            public string Type { get; set; }

            [JsonPropertyName("status")]
            public string Status { get; set; }

            [JsonPropertyName("externalTransactionId")]
            public string ExternalTransactionId { get; set; }

            [JsonPropertyName("value")]
            public Value Value { get; set; }
        }
    }
}