using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace McpServer.Api.IntegrationTests.Fixtures;

public abstract class IntegrationTestBase : IClassFixture<McpServerWebApplicationFactory>
{
    protected readonly McpServerWebApplicationFactory Factory;
    protected readonly HttpClient Client;
    protected readonly JsonSerializerOptions JsonOptions;

    protected IntegrationTestBase(McpServerWebApplicationFactory factory)
    {
        Factory = factory;
        Client = factory.CreateClient();
        
        JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    protected async Task<T?> GetAsync<T>(string url)
    {
        var response = await Client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions);
    }

    protected async Task<HttpResponseMessage> PostAsync<TRequest>(string url, TRequest content)
    {
        return await Client.PostAsJsonAsync(url, content, JsonOptions);
    }

    protected async Task<TResponse?> PostAsync<TRequest, TResponse>(string url, TRequest content)
    {
        var response = await PostAsync(url, content);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TResponse>(JsonOptions);
    }

    protected T GetRequiredService<T>() where T : notnull
    {
        using var scope = Factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<T>();
    }
}