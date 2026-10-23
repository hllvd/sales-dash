using System;
using System.Linq;
using FluentAssertions;
using SalesApp.CareerPlan;
using Xunit;

namespace SalesApp.Tests.CareerPlan
{
    public class CareerPlanConfigParserTests
    {
        [Fact]
        public void Parse_ValidJson_ReturnsCompleteConfig()
        {
            var json = @"
{
  ""closingDay"": 15,
  ""levels"": {
    ""Alef"": {
      ""requirements"": {
        ""all"": [
          {
            ""label"": ""Produção 12 meses ≥ Meta"",
            ""metric"": ""production"",
            ""windowMonths"": 12,
            ""op"": "">="",
            ""value"": { ""ref"": ""targetLevel.salesGoal"" }
          }
        ]
      }
    },
    ""Guimel"": {
      ""requirements"": {
        ""all"": [
          {
            ""label"": ""Retenção estrita 15 meses ≥ Retenção do nível"",
            ""metric"": ""strictRetention"",
            ""windowMonths"": 15,
            ""eligibility"": { ""minAgeMonths"": 2 },
            ""op"": "">="",
            ""value"": { ""ref"": ""targetLevel.retention"" }
          },
          {
            ""label"": ""Produção 12 meses ≥ Meta"",
            ""metric"": ""production"",
            ""windowMonths"": 12,
            ""op"": "">="",
            ""value"": { ""ref"": ""targetLevel.salesGoal"" }
          }
        ]
      }
    }
  }
}";

            var result = CareerPlanConfigParser.Parse(json);

            result.IsSuccess.Should().BeTrue();
            result.Config.Should().NotBeNull();
            result.Config!.ClosingDay.Should().Be(15);
            result.Config.Levels.Should().ContainKey("Alef");
            result.Config.Levels.Should().ContainKey("Guimel");

            var guimelReq = result.Config.Levels["Guimel"].Requirements;
            guimelReq.Type.Should().Be(RuleNodeType.All);
            guimelReq.Children.Should().HaveCount(2);

            var retRule = guimelReq.Children![0];
            retRule.Metric.Should().Be(MetricKind.StrictRetention);
            retRule.WindowMonths.Should().Be(15);
            retRule.Eligibility.Should().NotBeNull();
            retRule.Eligibility!.MinAgeMonths.Should().Be(2);
            retRule.Op.Should().Be(ComparisonOp.GreaterThanOrEqual);
            retRule.Value!.Reference.Should().Be("targetLevel.retention");
        }

        [Fact]
        public void Parse_InvalidJson_ReturnsErrors()
        {
            var result = CareerPlanConfigParser.Parse("{ not valid json }");
            result.IsSuccess.Should().BeFalse();
            result.Errors.Should().NotBeEmpty();
        }

        [Fact]
        public void Parse_InvalidClosingDay_ReturnsError()
        {
            var json = @"{ ""closingDay"": 35, ""levels"": {} }";
            var result = CareerPlanConfigParser.Parse(json);
            result.IsSuccess.Should().BeFalse();
            result.Errors.Should().Contain(e => e.Contains("closingDay must be between 1 and 28"));
        }

        [Fact]
        public void Parse_UnrecognizedMetric_ReturnsError()
        {
            var json = @"
{
  ""closingDay"": 15,
  ""levels"": {
    ""Alef"": {
      ""requirements"": {
        ""metric"": ""magic_sales"",
        ""op"": "">="",
        ""value"": 100
      }
    }
  }
}";
            var result = CareerPlanConfigParser.Parse(json);
            result.IsSuccess.Should().BeFalse();
            result.Errors.Should().Contain(e => e.Contains("is not recognized"));
        }
    }
}
