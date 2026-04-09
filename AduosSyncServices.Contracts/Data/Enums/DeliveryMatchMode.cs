using System.ComponentModel;

namespace AduosSyncServices.Contracts.Data.Enums
{
    public enum DeliveryMatchMode
    {
        [Description("Po wymiarach i wadze")]
        Weight = 0,

        [Description("Po cenie")]
        Price = 1
    }
}
