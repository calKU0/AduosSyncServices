using AduosSyncServices.Contracts.Models;
using Allegro.Aduos.Gaska.ProductsService.Services.Allegro;
using Xunit;

namespace Allegro.Aduos.Gaska.ProductsService.Tests.Helpers;

public class AllegroOfferServiceHelpersTests
{
    [Theory]
    [InlineData("{\"errors\":[{\"code\":\"GallerySizeException\",\"message\":\"You can add up to 16 images\"}]}", 16)]
    [InlineData("{\"errors\":[{\"code\":\"GallerySizeException\",\"userMessage\":\"Możesz dodać maksymalnie 12 zdjęć\"}]}", 12)]
    public void GalleryTooLargeLimit_ReadsLimitFromMessage(string body, int expected)
    {
        Assert.Equal(expected, AllegroOfferService.GalleryTooLargeLimit(body));
    }

    [Theory]
    [InlineData("{\"errors\":[{\"code\":\"GallerySizeException\",\"message\":\"You must add at least 1 image\"}]}")]
    [InlineData("{\"errors\":[{\"code\":\"ParameterNameNotFoundException\"}]}")]
    [InlineData("")]
    public void GalleryTooLargeLimit_ReturnsNull_ForOtherErrors(string body)
    {
        Assert.Null(AllegroOfferService.GalleryTooLargeLimit(body));
    }

    [Theory]
    [InlineData(17, 16, 16)]
    [InlineData(16, 16, 15)] // Allegro doliczyło zdjęcia z katalogu - zdejmujemy jedno
    [InlineData(2, 16, 1)]
    public void ReducedGallerySize_AlwaysShrinks(int own, int limit, int expected)
    {
        Assert.Equal(expected, AllegroOfferService.ReducedGallerySize(own, limit));
    }

    [Fact]
    public void TrimImages_KeepsFirstImagesWithoutDuplicates()
    {
        var images = new[] { "a", "b", "b", "c", "d" }.Select(u => new AllegroImages { Url = u }).ToList();

        Assert.Equal(new[] { "a", "b", "c" }, AllegroOfferService.TrimImages(images, 3).Select(i => i.Url));
        Assert.Equal(new[] { "a" }, AllegroOfferService.TrimImages(images, 0).Select(i => i.Url));
    }

    [Fact]
    public void IsDuplicateActiveOffer_DetectsPolishAndEnglishMessages()
    {
        Assert.True(AllegroOfferService.IsDuplicateActiveOffer("Przedmiot, który chcesz wystawić znajduje się już na liście Twoich aktywnych ofert.", ""));
        Assert.True(AllegroOfferService.IsDuplicateActiveOffer("", "The item is already on the list of your active offers"));
        Assert.False(AllegroOfferService.IsDuplicateActiveOffer("Inny błąd", "Other"));
    }
}
