using FluentAssertions;
using McpServer.Api.IntegrationTests.Fixtures;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace McpServer.Api.IntegrationTests.Controllers;

public class HealthControllerTests : IntegrationTestBase
{
    public HealthControllerTests(McpServerWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Health_Should_Return_Ok_When_Service_Is_Healthy()
    {
        // Act
        var response = await Client.GetAsync("/api/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Healthy");
    }

    [Fact]
    public async Task Health_Should_Return_Service_Status_Details()
    {
        // Act
        var response = await Client.GetAsync("/api/health");
        var result = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>(JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Should().NotBeNull();
        result.Should().ContainKey("status");
        result!["status"].ToString().Should().Be("Healthy");
    }

    [Fact]
    public async Task Ready_Should_Return_Ok_When_Service_Is_Ready()
    {
        // Act
        var response = await Client.GetAsync("/api/health/ready");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Ready");
    }

    [Fact]
    public async Task Live_Should_Return_Ok_When_Service_Is_Alive()
    {
        // Act
        var response = await Client.GetAsync("/api/health/live");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Live");
    }
}