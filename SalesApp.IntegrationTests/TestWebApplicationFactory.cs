using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace SalesApp.IntegrationTests
{
    public class TestWebApplicationFactory : WebApplicationFactory<TestStartup>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseStartup<TestStartup>();
        }

        protected override IWebHostBuilder CreateWebHostBuilder()
        {
            return new WebHostBuilder()
                .UseStartup<TestStartup>()
                .UseContentRoot(Directory.GetCurrentDirectory());
        }

        protected override void SetContentRoot(IWebHostBuilder builder)
        {
            builder.UseContentRoot(Directory.GetCurrentDirectory());
        }
    }
}