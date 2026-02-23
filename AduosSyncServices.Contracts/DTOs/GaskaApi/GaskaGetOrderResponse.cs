using System.Text.Json.Serialization;

namespace AduosSyncServices.Contracts.DTOs.Allegro.GaskaApi
{
    public class GaskaGetOrderResponse
    {
        [JsonPropertyName("order")]
        public OrderDto Order { get; set; } = new();

        [JsonPropertyName("result")]
        public int Result { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        public class OrderDto
        {
            [JsonPropertyName("orderId")]
            public int OrderId { get; set; }

            [JsonPropertyName("orderNumber")]
            public string OrderNumber { get; set; } = string.Empty;

            [JsonPropertyName("orderCustomerNumber")]
            public string OrderCustomerNumber { get; set; } = string.Empty;

            [JsonPropertyName("orderCustomerId")]
            public int OrderCustomerId { get; set; }

            [JsonPropertyName("orderCustomerName")]
            public string OrderCustomerName { get; set; } = string.Empty;

            [JsonPropertyName("orderCustomerStreet")]
            public string OrderCustomerStreet { get; set; } = string.Empty;

            [JsonPropertyName("orderCustomerPostCode")]
            public string OrderCustomerPostCode { get; set; } = string.Empty;

            [JsonPropertyName("orderCustomerCity")]
            public string OrderCustomerCity { get; set; } = string.Empty;

            [JsonPropertyName("orderCustomerPerson")]
            public string OrderCustomerPerson { get; set; } = string.Empty;

            [JsonPropertyName("orderDate")]
            public DateTime OrderDate { get; set; }

            [JsonPropertyName("orderRecipientId")]
            public int OrderRecipientId { get; set; }

            [JsonPropertyName("orderRecipientName")]
            public string OrderRecipientName { get; set; } = string.Empty;

            [JsonPropertyName("orderRecipientStreet")]
            public string OrderRecipientStreet { get; set; } = string.Empty;

            [JsonPropertyName("orderRecipientCity")]
            public string OrderRecipientCity { get; set; } = string.Empty;

            [JsonPropertyName("orderRecipientPostCode")]
            public string OrderRecipientPostCode { get; set; } = string.Empty;

            [JsonPropertyName("orderRecipientPhone")]
            public string OrderRecipientPhone { get; set; } = string.Empty;

            [JsonPropertyName("realizeDate")]
            public DateTime RealizeDate { get; set; }

            [JsonPropertyName("totalAmount")]
            public decimal TotalAmount { get; set; }

            [JsonPropertyName("totalVatAmount")]
            public decimal TotalVatAmount { get; set; }

            [JsonPropertyName("totalGrossAmount")]
            public decimal TotalGrossAmount { get; set; }

            [JsonPropertyName("currency")]
            public string Currency { get; set; } = string.Empty;

            [JsonPropertyName("statusId")]
            public int StatusId { get; set; }

            [JsonPropertyName("status")]
            public string Status { get; set; } = string.Empty;

            [JsonPropertyName("weight")]
            public string Weight { get; set; } = string.Empty;

            [JsonPropertyName("delivery")]
            public string Delivery { get; set; } = string.Empty;

            [JsonPropertyName("dropshippingAccountNumber")]
            public string DropshippingAccountNumber { get; set; } = string.Empty;

            [JsonPropertyName("dropshippingAmount")]
            public decimal DropshippingAmount { get; set; }

            [JsonPropertyName("notice")]
            public string Notice { get; set; } = string.Empty;

            [JsonPropertyName("items")]
            public List<Item> Items { get; set; } = new();
        }

        public class Item
        {
            [JsonPropertyName("id")]
            public int Id { get; set; }

            [JsonPropertyName("codeGaska")]
            public string CodeGaska { get; set; } = string.Empty;

            [JsonPropertyName("codeCustomer")]
            public string CodeCustomer { get; set; } = string.Empty;

            [JsonPropertyName("name")]
            public string Name { get; set; } = string.Empty;

            [JsonPropertyName("unit")]
            public string Unit { get; set; } = string.Empty;

            [JsonPropertyName("ean")]
            public string Ean { get; set; } = string.Empty;

            [JsonPropertyName("customerMessage")]
            public string CustomerMessage { get; set; } = string.Empty;

            [JsonPropertyName("quantityOrdered")]
            public decimal QuantityOrdered { get; set; }

            [JsonPropertyName("quantityRealized")]
            public decimal QuantityRealized { get; set; }

            [JsonPropertyName("quantityAvailable")]
            public decimal QuantityAvailable { get; set; }

            [JsonPropertyName("priceNet")]
            public decimal PriceNet { get; set; }

            [JsonPropertyName("vat")]
            public decimal Vat { get; set; }

            [JsonPropertyName("totalNetAmount")]
            public decimal TotalNetAmount { get; set; }

            [JsonPropertyName("totalGrossAmount")]
            public decimal TotalGrossAmount { get; set; }

            [JsonPropertyName("realizeDate")]
            public DateTime RealizeDate { get; set; }

            [JsonPropertyName("realizeInvoiceNumber")]
            public string? RealizeInvoiceNumber { get; set; }

            [JsonPropertyName("realizeDelivery")]
            public string? RealizeDelivery { get; set; }

            [JsonPropertyName("realizeTrackingNumber")]
            public string? RealizeTrackingNumber { get; set; }

            [JsonPropertyName("realizeDeliveryStatus")]
            public string? RealizeDeliveryStatus { get; set; }
        }
    }
}