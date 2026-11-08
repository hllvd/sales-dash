using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Moq;
using SalesApp.Utils;
using Xunit;

namespace SalesApp.Tests
{
    public class ConfigurationHierarchyTests
    {
        [Fact]
        public void ParseDotEnv_WithEmptyOrNull_ReturnsEmptyDictionary()
        {
            var res1 = ConfigurationHierarchy.ParseDotEnv(null);
            var res2 = ConfigurationHierarchy.ParseDotEnv("");
            var res3 = ConfigurationHierarchy.ParseDotEnv("   \n\r  ");

            res1.Should().BeEmpty();
            res2.Should().BeEmpty();
            res3.Should().BeEmpty();
        }

        [Fact]
        public void ParseDotEnv_WithValidLinesAndComments_ParsesCorrectly()
        {
            var content = @"
# This is a comment
KEY1=value1
KEY2=""quoted value""
KEY3='single quoted value'

# Another comment
EMPTY_KEY=
AWS__DataCacheTable=custom-cache
INLINE_COMMENT=my_value # some note
";

            var result = ConfigurationHierarchy.ParseDotEnv(content);

            result.Should().ContainKey("KEY1").WhoseValue.Should().Be("value1");
            result.Should().ContainKey("KEY2").WhoseValue.Should().Be("quoted value");
            result.Should().ContainKey("KEY3").WhoseValue.Should().Be("single quoted value");
            result.Should().ContainKey("EMPTY_KEY").WhoseValue.Should().Be("");
            result.Should().ContainKey("AWS__DataCacheTable").WhoseValue.Should().Be("custom-cache");
            result.Should().ContainKey("INLINE_COMMENT").WhoseValue.Should().Be("my_value");
            result.Should().NotContainKey("# This is a comment");
        }

        [Fact]
        public void ResolveDotEnvFilePaths_ForE2E_IncludesDevelopmentAndE2EFiles()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "salesapp_test_env_" + Guid.NewGuid());
            Directory.CreateDirectory(tempDir);
            try
            {
                File.WriteAllText(Path.Combine(tempDir, ".env"), "A=1");
                File.WriteAllText(Path.Combine(tempDir, ".env.development"), "B=2");
                File.WriteAllText(Path.Combine(tempDir, ".env.e2e"), "C=3");
                File.WriteAllText(Path.Combine(tempDir, ".env.production"), "D=4");

                var resolved = ConfigurationHierarchy.ResolveDotEnvFilePaths(tempDir, "E2E");

                resolved.Should().HaveCount(3);
                resolved.Should().Contain(p => p.EndsWith(".env"));
                resolved.Should().Contain(p => p.EndsWith(".env.development"));
                resolved.Should().Contain(p => p.EndsWith(".env.e2e"));
                resolved.Should().NotContain(p => p.EndsWith(".env.production"));
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }

        [Fact]
        public void ConfigureAppConfigurationHierarchy_WhenE2E_DevelopmentOverridesBase_AndE2EOverridesDevelopment()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "salesapp_test_config_" + Guid.NewGuid());
            Directory.CreateDirectory(tempDir);
            try
            {
                // Level 1: appsettings.json
                File.WriteAllText(Path.Combine(tempDir, "appsettings.json"), @"{
                    ""AWS"": {
                        ""Region"": ""us-east-1"",
                        ""DataCacheTable"": ""salesapp-data-cache-prod"",
                        ""HelpdeskTable"": ""salesapp-helpdesk-prod""
                    },
                    ""ConnectionStrings"": {
                        ""DefaultConnection"": ""Data Source=data/SalesApp.db""
                    }
                }");

                // Level 2: appsettings.Development.json
                File.WriteAllText(Path.Combine(tempDir, "appsettings.Development.json"), @"{
                    ""AWS"": {
                        ""DataCacheTable"": ""dev-salesapp-data-cache""
                    }
                }");

                // Level 3: appsettings.E2E.json
                File.WriteAllText(Path.Combine(tempDir, "appsettings.E2E.json"), @"{
                    ""ConnectionStrings"": {
                        ""DefaultConnection"": ""Data Source=data/SalesApp.E2E.db""
                    }
                }");

                var envMock = new Mock<IHostEnvironment>();
                envMock.Setup(e => e.EnvironmentName).Returns("E2E");
                envMock.Setup(e => e.ContentRootPath).Returns(tempDir);
                envMock.Setup(e => e.ContentRootFileProvider).Returns(new PhysicalFileProvider(tempDir));

                var builder = new ConfigurationBuilder();
                builder.SetBasePath(tempDir);

                ConfigurationHierarchy.ConfigureAppConfigurationHierarchy(builder, envMock.Object);
                var config = builder.Build();

                // 1. Level 1 retained if not overridden
                config["AWS:Region"].Should().Be("us-east-1");
                config["AWS:HelpdeskTable"].Should().Be("salesapp-helpdesk-prod");

                // 2. Level 2 (Development) overrides Level 1 (Production/Base) in E2E mode
                config["AWS:DataCacheTable"].Should().Be("dev-salesapp-data-cache");

                // 3. Level 3 (E2E) overrides Level 1
                config["ConnectionStrings:DefaultConnection"].Should().Be("Data Source=data/SalesApp.E2E.db");
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }

        [Fact]
        public void ConfigureAppConfigurationHierarchy_WhenDotEnvPresent_DotEnvOverridesJsonSettings()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "salesapp_test_dotenv_" + Guid.NewGuid());
            Directory.CreateDirectory(tempDir);
            try
            {
                // Level 1: appsettings.json
                File.WriteAllText(Path.Combine(tempDir, "appsettings.json"), @"{
                    ""AWS"": {
                        ""DataCacheTable"": ""prod-cache""
                    }
                }");

                // Level 2: appsettings.Development.json
                File.WriteAllText(Path.Combine(tempDir, "appsettings.Development.json"), @"{
                    ""AWS"": {
                        ""DataCacheTable"": ""dev-cache""
                    }
                }");

                // Level 4: .env file
                File.WriteAllText(Path.Combine(tempDir, ".env"), "AWS__DataCacheTable=env-override-cache\nCUSTOM_VAR=custom");

                var envMock = new Mock<IHostEnvironment>();
                envMock.Setup(e => e.EnvironmentName).Returns("E2E");
                envMock.Setup(e => e.ContentRootPath).Returns(tempDir);
                envMock.Setup(e => e.ContentRootFileProvider).Returns(new PhysicalFileProvider(tempDir));

                var builder = new ConfigurationBuilder();
                builder.SetBasePath(tempDir);

                ConfigurationHierarchy.ConfigureAppConfigurationHierarchy(builder, envMock.Object);
                var config = builder.Build();

                // Level 4 overrides lower levels
                config["AWS:DataCacheTable"].Should().Be("env-override-cache");
                config["CUSTOM_VAR"].Should().Be("custom");
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }

        [Fact]
        public void ConfigureAppConfigurationHierarchy_WhenDevelopment_OverridesHelpdeskTableFromDevelopmentJson()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "salesapp_test_dev_" + Guid.NewGuid());
            Directory.CreateDirectory(tempDir);
            try
            {
                // Level 1: appsettings.json (Production base)
                File.WriteAllText(Path.Combine(tempDir, "appsettings.json"), @"{
                    ""AWS"": {
                        ""HelpdeskTable"": ""salesapp-helpdesk""
                    }
                }");

                // Level 2: appsettings.Development.json
                File.WriteAllText(Path.Combine(tempDir, "appsettings.Development.json"), @"{
                    ""AWS"": {
                        ""HelpdeskTable"": ""dev-salesapp-helpdesk""
                    }
                }");

                var envMock = new Mock<IHostEnvironment>();
                envMock.Setup(e => e.EnvironmentName).Returns("Development");
                envMock.Setup(e => e.ContentRootPath).Returns(tempDir);
                envMock.Setup(e => e.ContentRootFileProvider).Returns(new PhysicalFileProvider(tempDir));

                var builder = new ConfigurationBuilder();
                builder.SetBasePath(tempDir);

                ConfigurationHierarchy.ConfigureAppConfigurationHierarchy(builder, envMock.Object);
                var config = builder.Build();

                config["AWS:HelpdeskTable"].Should().Be("dev-salesapp-helpdesk");
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }

        [Fact]
        public void ConfigureAppConfigurationHierarchy_WhenDevelopment_OverridesPbiScrapeSettingsFromDevelopmentJson()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "salesapp_test_pbi_dev_" + Guid.NewGuid());
            Directory.CreateDirectory(tempDir);
            try
            {
                // Level 1: appsettings.json (Production base)
                File.WriteAllText(Path.Combine(tempDir, "appsettings.json"), @"{
                    ""AWS"": {
                        ""DynamoDbTable"": ""pbi_scrape_logs"",
                        ""SqsQueueUrl"": ""https://sqs.us-east-1.amazonaws.com/797163826126/hdev-sales-dash"",
                        ""SqsResultsQueueUrl"": ""https://sqs.us-east-1.amazonaws.com/797163826126/hdev-sales-dash"",
                        ""SqsJobsQueueUrl"": ""https://sqs.us-east-1.amazonaws.com/797163826126/hdev-sales-scrape-jobs""
                    }
                }");

                // Level 2: appsettings.Development.json
                File.WriteAllText(Path.Combine(tempDir, "appsettings.Development.json"), @"{
                    ""AWS"": {
                        ""DynamoDbTable"": ""dev-pbi_scrape_logs"",
                        ""SqsQueueUrl"": ""https://sqs.us-east-1.amazonaws.com/797163826126/dev-hdev-sales-dash"",
                        ""SqsResultsQueueUrl"": ""https://sqs.us-east-1.amazonaws.com/797163826126/dev-hdev-sales-dash"",
                        ""SqsJobsQueueUrl"": ""https://sqs.us-east-1.amazonaws.com/797163826126/dev-hdev-sales-scrape-jobs""
                    }
                }");

                var envMock = new Mock<IHostEnvironment>();
                envMock.Setup(e => e.EnvironmentName).Returns("Development");
                envMock.Setup(e => e.ContentRootPath).Returns(tempDir);
                envMock.Setup(e => e.ContentRootFileProvider).Returns(new PhysicalFileProvider(tempDir));

                var builder = new ConfigurationBuilder();
                builder.SetBasePath(tempDir);

                ConfigurationHierarchy.ConfigureAppConfigurationHierarchy(builder, envMock.Object);
                var config = builder.Build();

                config["AWS:DynamoDbTable"].Should().Be("dev-pbi_scrape_logs");
                config["AWS:SqsQueueUrl"].Should().Be("https://sqs.us-east-1.amazonaws.com/797163826126/dev-hdev-sales-dash");
                config["AWS:SqsResultsQueueUrl"].Should().Be("https://sqs.us-east-1.amazonaws.com/797163826126/dev-hdev-sales-dash");
                config["AWS:SqsJobsQueueUrl"].Should().Be("https://sqs.us-east-1.amazonaws.com/797163826126/dev-hdev-sales-scrape-jobs");
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }
    }
}
