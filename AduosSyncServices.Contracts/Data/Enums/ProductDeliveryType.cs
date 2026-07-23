using System.ComponentModel;

namespace AduosSyncServices.Contracts.Data.Enums
{
    // Mirrors Products.DeliveryType (int in the DB): the product's shipping category, used by
    // OfferFactory's delivery-rule matching and displayed in the orders UI.
    public enum ProductDeliveryType
    {
        [Description("Standardowy")]
        Standard = 0,

        [Description("Gabarytowy")]
        Bulky = 1,

        [Description("Niestandardowy")]
        Custom = 2
    }
}
