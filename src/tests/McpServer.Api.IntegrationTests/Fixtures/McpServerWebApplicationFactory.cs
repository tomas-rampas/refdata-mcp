using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using McpServer.Core.Interfaces;
using Moq;
using Testcontainers.MongoDb;
using Xunit;

namespace McpServer.Api.IntegrationTests.Fixtures;

public class McpServerWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly MongoDbContainer _mongoContainer = new MongoDbBuilder()
        .WithImage("mongo:7.0")
        .WithPortBinding(27017, true)
        .Build();

    public Mock<ILlmClient> MockLlmClient { get; } = new();
    public Mock<IDocumentLoader> MockFileLoader { get; } = new();
    public Mock<IDocumentLoader> MockJiraLoader { get; } = new();
    public Mock<IDocumentLoader> MockConfluenceLoader { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove real services
            RemoveServices<ILlmClient>(services);
            RemoveServices<IDocumentLoader>(services);
            RemoveServices<IHostedService>(services);
            RemoveServices<MongoDB.Driver.IMongoClient>(services);
            RemoveServices<MongoDB.Driver.IMongoDatabase>(services);

            // Add mocked services
            services.AddSingleton(MockLlmClient.Object);
            services.AddSingleton(MockFileLoader.Object);
            services.AddSingleton(MockJiraLoader.Object);
            services.AddSingleton(MockConfluenceLoader.Object);

            // Configure test MongoDB connection
            services.Configure<McpServer.Infrastructure.Configuration.MongoDbSettings>(options =>
            {
                options.ConnectionString = _mongoContainer.GetConnectionString();
                options.DatabaseName = "McpServerTestDb";
            });

            // Register MongoDB for tests
            services.AddSingleton<MongoDB.Driver.IMongoClient>(sp => 
                new MongoDB.Driver.MongoClient(_mongoContainer.GetConnectionString()));
            services.AddSingleton<MongoDB.Driver.IMongoDatabase>(sp =>
            {
                var client = sp.GetRequiredService<MongoDB.Driver.IMongoClient>();
                return client.GetDatabase("McpServerTestDb");
            });
        });

        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddConsole();
        });

        builder.UseEnvironment("Testing");
    }

    private static void RemoveServices<TService>(IServiceCollection services)
    {
        var descriptors = services.Where(d => d.ServiceType == typeof(TService)).ToList();
        foreach (var descriptor in descriptors)
        {
            services.Remove(descriptor);
        }
    }

    public async Task InitializeAsync()
    {
        await _mongoContainer.StartAsync();
    }

    public new async Task DisposeAsync()
    {
        await _mongoContainer.DisposeAsync();
        await base.DisposeAsync();
    }
}