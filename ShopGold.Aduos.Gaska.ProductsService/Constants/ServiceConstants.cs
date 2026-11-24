using AduosSyncServices.Contracts.Data.Enums;
using AduosSyncServices.Infrastructure.Helpers;

namespace ShopGold.Aduos.Gaska.ProductsService.Constants
{
    public static class ServiceConstants
    {
        public const string ServiceName = "ShopGoldAduosGaskaProductsService";
        public const IntegrationCompany Company = IntegrationCompany.Gaska;

        // Konto Allegro nie jest tu używane, ale repozytoria wspólne z serwisem Allegro go wymagają.
        public const AllegroAccount Account = AllegroAccount.Aduos;

        // Zdjęcia pobiera serwis Allegro - ten serwis tylko je czyta i wgrywa do sklepu.
        public const string ImagesFolder = ImageHelper.DefaultImagesFolder;
    }
}
