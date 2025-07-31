using McpServer.Application.Services;
using McpServer.Core.Interfaces;
using McpServer.Infrastructure.BackgroundServices;
using McpServer.Infrastructure.Configuration;
using McpServer.Infrastructure.DocumentLoaders;
using McpServer.Infrastructure.LlmClients;
using McpServer.Infrastructure.Parsers;
using McpServer.Infrastructure.Services;
using McpServer.Infrastructure.VectorStore;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Add controllers
builder.Services.AddControllers();

// Add API Explorer and Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "MCP-RAG Banking Reference Data API",
        Version = "v1",
        Description = "API for querying banking policies, procedures, and reference data using natural language.",
        Contact = new OpenApiContact
        {
            Name = "Banking Reference Data Team",
            Email = "refdata@bank.com"
        }
    });

    // Include XML comments for better API documentation
    var xmlFiles = Directory.GetFiles(AppContext.BaseDirectory, "*.xml", SearchOption.TopDirectoryOnly);
    foreach (var xmlFile in xmlFiles)
    {
        c.IncludeXmlComments(xmlFile);
    }
});

// Configure settings from appsettings.json
builder.Services.Configure<MongoDbSettings>(builder.Configuration.GetSection("MongoDB"));
builder.Services.Configure<OllamaSettings>(builder.Configuration.GetSection("Ollama"));
builder.Services.Configure<JiraSettings>(builder.Configuration.GetSection("Jira"));
builder.Services.Configure<ConfluenceSettings>(builder.Configuration.GetSection("Confluence"));
builder.Services.Configure<IngestionSettings>(builder.Configuration.GetSection("Ingestion"));

// Register infrastructure services
builder.Services.AddSingleton<IVectorStore, MongoVectorStore>();
builder.Services.AddSingleton<ILlmClient, OllamaClient>();
builder.Services.AddSingleton<IBankingDocumentParser, BankingDocumentParser>();
builder.Services.AddSingleton<IChunkingService, ChunkingService>();

// Register document loaders
builder.Services.AddSingleton<IDocumentLoader, LocalFileLoader>();
builder.Services.AddSingleton<IDocumentLoader, JiraDocumentLoader>();
builder.Services.AddSingleton<IDocumentLoader, ConfluenceDocumentLoader>();

// Register application services
builder.Services.AddScoped<RagService>();
builder.Services.AddScoped<IngestionService>();

// Register background services
builder.Services.AddHostedService<IngestionBackgroundService>();

// Add HTTP client for external service calls
builder.Services.AddHttpClient();

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowBlazorClient",
        policy =>
        {
            policy.WithOrigins(
                    "http://localhost:5000",
                    "https://localhost:5001",
                    "http://localhost:3000",
                    "https://localhost:3001")
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        });
});

// Add health checks
builder.Services.AddHealthChecks();

// Configure logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "MCP-RAG Banking API v1");
        c.RoutePrefix = string.Empty; // Serve Swagger UI at the app's root
    });
}

app.UseHttpsRedirection();

// Use CORS
app.UseCors("AllowBlazorClient");

// Map controllers
app.MapControllers();

// Map health check endpoints
app.MapHealthChecks("/health");

// Log startup information
var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("MCP-RAG Banking Reference Data API started successfully");
logger.LogInformation("Environment: {Environment}", app.Environment.EnvironmentName);
logger.LogInformation("Swagger UI available at: {SwaggerUrl}", 
    app.Environment.IsDevelopment() ? "https://localhost:5001" : "[Production URL]");

app.Run();