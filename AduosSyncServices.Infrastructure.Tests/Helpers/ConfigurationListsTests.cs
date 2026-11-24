using AduosSyncServices.Infrastructure.Helpers;
using Microsoft.Extensions.Configuration;
using System.Text;
using Xunit;

namespace AduosSyncServices.Infrastructure.Tests.Helpers
{
    public class ConfigurationListsTests
    {
        private static IConfigurationSection Section(string json) =>
            new ConfigurationBuilder()
                .AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(json)))
                .Build()
                .GetSection("AppSettings:CategoriesId");

        [Fact]
        public void JsonList_IsRead() =>
            Assert.Equal(new[] { 101, 202 }, ConfigurationLists.ReadIntList(Section("{\"AppSettings\":{\"CategoriesId\":[101, 202]}}")));

        [Fact]
        public void LegacyCommaSeparatedText_IsRead_NotTurnedIntoEmptyList() =>
            // Standardowe wiązanie konfiguracji dałoby tu pustą listę (= cały katalog na Allegro).
            Assert.Equal(new[] { 101, 202, 303 }, ConfigurationLists.ReadIntList(Section("{\"AppSettings\":{\"CategoriesId\":\"101, 202;303\"}}")));

        [Fact]
        public void DuplicatesZerosAndGarbage_AreSkipped() =>
            Assert.Equal(new[] { 5, 7 }, ConfigurationLists.ReadIntList(Section("{\"AppSettings\":{\"CategoriesId\":[5, 0, 5, \"x\", 7]}}")));

        [Theory]
        [InlineData("{\"AppSettings\":{\"CategoriesId\":[]}}")]
        [InlineData("{\"AppSettings\":{\"CategoriesId\":\"\"}}")]
        [InlineData("{\"AppSettings\":{}}")]
        public void Empty_OrMissing_GivesEmptyList(string json) =>
            Assert.Empty(ConfigurationLists.ReadIntList(Section(json)));
    }
}
