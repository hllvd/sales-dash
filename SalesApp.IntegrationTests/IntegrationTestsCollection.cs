using Xunit;

namespace SalesApp.IntegrationTests
{
    // 1. Contracts Collection
    public class ContractsTestFactory : TestWebApplicationFactory
    {
        public ContractsTestFactory() : base("SalesApp.Contracts.Tests.db") { }
    }

    [CollectionDefinition("Contracts Tests")]
    public class ContractsTestsCollection : ICollectionFixture<ContractsTestFactory> { }

    // 2. Imports Collection
    public class ImportsTestFactory : TestWebApplicationFactory
    {
        public ImportsTestFactory() : base("SalesApp.Imports.Tests.db") { }
    }

    [CollectionDefinition("Imports Tests")]
    public class ImportsTestsCollection : ICollectionFixture<ImportsTestFactory> { }

    // 3. Users Collection
    public class UsersTestFactory : TestWebApplicationFactory
    {
        public UsersTestFactory() : base("SalesApp.Users.Tests.db") { }
    }

    [CollectionDefinition("Users Tests")]
    public class UsersTestsCollection : ICollectionFixture<UsersTestFactory> { }

    // 4. Misc Collection
    public class MiscTestFactory : TestWebApplicationFactory
    {
        public MiscTestFactory() : base("SalesApp.Misc.Tests.db") { }
    }

    [CollectionDefinition("Misc Tests")]
    public class MiscTestsCollection : ICollectionFixture<MiscTestFactory> { }

    // Legacy Fallback Collection
    [CollectionDefinition("Integration Tests")]
    public class IntegrationTestsCollection : ICollectionFixture<TestWebApplicationFactory> { }
}

