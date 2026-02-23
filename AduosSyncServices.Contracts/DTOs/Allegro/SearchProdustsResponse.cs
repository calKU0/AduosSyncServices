using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace AduosSyncServices.Contracts.DTOs.Allegro
{
    public class SearchProdustsResponse
    {
        [JsonPropertyName("products")]
        public List<Product> Products { get; set; }

        [JsonPropertyName("filters")]
        public List<Filter> Filters { get; set; }

        public class Address
        {
            [JsonPropertyName("countryCode")]
            public string CountryCode { get; set; }

            [JsonPropertyName("street")]
            public string Street { get; set; }

            [JsonPropertyName("postalCode")]
            public string PostalCode { get; set; }

            [JsonPropertyName("city")]
            public string City { get; set; }
        }

        public class AiCoCreatedContent
        {
            [JsonPropertyName("paths")]
            public List<string> Paths { get; set; }
        }

        public class Categories
        {
            [JsonPropertyName("subcategories")]
            public List<Subcategory> Subcategories { get; set; }

            [JsonPropertyName("path")]
            public List<Path> Path { get; set; }
        }

        public class Category
        {
            [JsonPropertyName("id")]
            public string Id { get; set; }

            [JsonPropertyName("path")]
            public List<Path> Path { get; set; }

            [JsonPropertyName("similar")]
            public List<Similar> Similar { get; set; }
        }

        public class Contact
        {
            [JsonPropertyName("email")]
            public string Email { get; set; }

            [JsonPropertyName("phoneNumber")]
            public string PhoneNumber { get; set; }

            [JsonPropertyName("formUrl")]
            public string FormUrl { get; set; }
        }

        public class Description
        {
            [JsonPropertyName("sections")]
            public List<Section> Sections { get; set; }
        }

        public class Filter
        {
            [JsonPropertyName("id")]
            public string Id { get; set; }

            [JsonPropertyName("type")]
            public string Type { get; set; }

            [JsonPropertyName("name")]
            public string Name { get; set; }

            [JsonPropertyName("values")]
            public List<Value> Values { get; set; }

            [JsonPropertyName("minValue")]
            public int MinValue { get; set; }

            [JsonPropertyName("maxValue")]
            public int MaxValue { get; set; }

            [JsonPropertyName("unit")]
            public string Unit { get; set; }
        }

        public class Image
        {
            [JsonPropertyName("url")]
            public string Url { get; set; }
        }

        public class Item
        {
            [JsonPropertyName("type")]
            public string Type { get; set; }
        }

        public class NextPage
        {
            [JsonPropertyName("id")]
            public string Id { get; set; }
        }

        public class Options
        {
            [JsonPropertyName("identifiesProduct")]
            public bool IdentifiesProduct { get; set; }

            [JsonPropertyName("isGTIN")]
            public bool IsGTIN { get; set; }

            [JsonPropertyName("isTrusted")]
            public bool IsTrusted { get; set; }

            [JsonPropertyName("isAiCoCreated")]
            public bool IsAiCoCreated { get; set; }
        }

        public class Parameter
        {
            [JsonPropertyName("id")]
            public string Id { get; set; }

            [JsonPropertyName("name")]
            public string Name { get; set; }

            [JsonPropertyName("rangeValue")]
            public RangeValue RangeValue { get; set; }

            [JsonPropertyName("values")]
            public string Values { get; set; }

            [JsonPropertyName("valuesIds")]
            public string ValuesIds { get; set; }

            [JsonPropertyName("unit")]
            public object Unit { get; set; }

            [JsonPropertyName("options")]
            public Options Options { get; set; }
        }

        public class Path
        {
            [JsonPropertyName("id")]
            public string Id { get; set; }

            [JsonPropertyName("name")]
            public string Name { get; set; }
        }

        public class ProducerData
        {
            [JsonPropertyName("tradeName")]
            public string TradeName { get; set; }

            [JsonPropertyName("address")]
            public Address Address { get; set; }

            [JsonPropertyName("contact")]
            public Contact Contact { get; set; }
        }

        public class Product
        {
            [JsonPropertyName("id")]
            public string Id { get; set; }

            [JsonPropertyName("name")]
            public string Name { get; set; }

            [JsonPropertyName("description")]
            public Description Description { get; set; }

            [JsonPropertyName("category")]
            public Category Category { get; set; }

            [JsonPropertyName("images")]
            public List<Image> Images { get; set; }

            //[JsonPropertyName("parameters")]
            //public List<Parameter> Parameters { get; set; }

            [JsonPropertyName("aiCoCreatedContent")]
            public AiCoCreatedContent AiCoCreatedContent { get; set; }

            [JsonPropertyName("trustedContent")]
            public TrustedContent TrustedContent { get; set; }

            [JsonPropertyName("hasProtectedBrand")]
            public bool HasProtectedBrand { get; set; }

            [JsonPropertyName("productSafety")]
            public ProductSafety ProductSafety { get; set; }

            [JsonPropertyName("publication")]
            public Publication Publication { get; set; }
        }

        public class ProductSafety
        {
            [JsonPropertyName("responsibleProducers")]
            public List<ResponsibleProducer> ResponsibleProducers { get; set; }

            [JsonPropertyName("safetyInformation")]
            public SafetyInformation SafetyInformation { get; set; }
        }

        public class Publication
        {
            [JsonPropertyName("status")]
            public string Status { get; set; }
        }

        public class RangeValue
        {
            [JsonPropertyName("from")]
            public string From { get; set; }

            [JsonPropertyName("to")]
            public string To { get; set; }
        }

        public class ResponsibleProducer
        {
            [JsonPropertyName("id")]
            public string Id { get; set; }

            [JsonPropertyName("name")]
            public string Name { get; set; }

            [JsonPropertyName("producerData")]
            public ProducerData ProducerData { get; set; }
        }

        public class SafetyInformation
        {
            [JsonPropertyName("type")]
            public string Type { get; set; }

            [JsonPropertyName("description")]
            public string Description { get; set; }
        }

        public class Section
        {
            [JsonPropertyName("items")]
            public List<Item> Items { get; set; }
        }

        public class Similar
        {
            [JsonPropertyName("id")]
            public string Id { get; set; }

            [JsonPropertyName("path")]
            public List<Path> Path { get; set; }
        }

        public class Subcategory
        {
            [JsonPropertyName("id")]
            public string Id { get; set; }

            [JsonPropertyName("name")]
            public string Name { get; set; }

            [JsonPropertyName("count")]
            public int Count { get; set; }
        }

        public class TrustedContent
        {
            [JsonPropertyName("paths")]
            public List<string> Paths { get; set; }

            [JsonPropertyName("productPaths")]
            public List<string> ProductPaths { get; set; }
        }

        public class Value
        {
            [JsonPropertyName("name")]
            public string Name { get; set; }

            [JsonPropertyName("value")]
            public string Value1 { get; set; }

            [JsonPropertyName("idSuffix")]
            public string IdSuffix { get; set; }

            [JsonPropertyName("count")]
            public int Count { get; set; }

            [JsonPropertyName("selected")]
            public bool Selected { get; set; }
        }
    }
}