using System.ComponentModel;

namespace AduosSyncServices.Contracts.Settings
{
    public enum DeliveryHandlingTime
    {
        [Description("Natychmiast")]
        PT0S,

        [Description("1 dzień")]
        PT24H,

        [Description("2 dni")]
        P2D,

        [Description("3 dni")]
        P3D,

        [Description("4 dni")]
        P4D,

        [Description("5 dni")]
        P5D,

        [Description("7 dni")]
        P7D,

        [Description("10 dni")]
        P10D,

        [Description("14 dni")]
        P14D,

        [Description("21 dni")]
        P21D,

        [Description("30 dni")]
        P30D,

        [Description("60 dni")]
        P60D
    }
}
