using System.ComponentModel;

namespace AduosSyncServices.Contracts.Settings
{
    public enum DeliveryRuleType
    {
        [Description("Standardowy")]
        Standard = 0,

        [Description("Gabarytowy")]
        BulkyType = 1,

        [Description("Niestandardowy")]
        CustomType = 2,
    }
}
