using AduosSyncServices.Contracts.Data.Enums;
using AduosSyncServices.Infrastructure.Helpers;

namespace Allegro.Aduos.Gaska.ProductsService.Constants
{
    public static class ServiceConstants
    {
        public const IntegrationCompany Company = IntegrationCompany.Gaska;
        public const AllegroAccount Account = AllegroAccount.Aduos;
        // Single source of truth lives next to ImageHelper so the ServicesManager UI reads from the
        // same folder this service writes to.
        public const string ImagesFolder = ImageHelper.DefaultImagesFolder;
    }
}
