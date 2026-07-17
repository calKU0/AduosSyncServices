using System.ComponentModel;

namespace AduosSyncServices.Contracts.Data.Enums
{
    public enum OrderSource
    {
        [Description("Allegro")]
        Allegro = 0,
        [Description("Ręczne")]
        Manual = 1
    }
}
