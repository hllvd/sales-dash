using Xunit;

namespace SalesApp.IntegrationTests
{
    [CollectionDefinition("Integration Tests")]
    public class TestCollection : ICollectionFixture<TestWebApplicationFactory>
    {
    }
}