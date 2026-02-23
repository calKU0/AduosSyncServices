namespace AduosSyncServices.Infrastructure.Helpers
{
    public static class ConjugationHelper
    {
        public static string Unit(int packQty, string unit)
        {
            if (string.IsNullOrWhiteSpace(unit))
                return string.Empty;

            unit = unit.Trim().ToLower().Replace(".", "");
            unit = unit == "opak" ? "szt" : unit;

            switch (unit)
            {
                case "mb":
                    if (packQty == 1)
                        return "metr bieżący";
                    else if (packQty >= 2 && packQty <= 4)
                        return "metry bieżące";
                    else
                        return "metrów bieżących";

                case "szt":
                    if (packQty == 1)
                        return "sztuka";
                    else if (packQty >= 2 && packQty <= 4)
                        return "sztuki";
                    else
                        return "sztuk";

                case "kpl":
                    return packQty == 1 ? "komplet" : "komplety";

                case "para":
                    return packQty == 1 ? "para" : "par";

                case "kg":
                    return "kg";

                default:
                    return unit;
            }
        }
    }
}