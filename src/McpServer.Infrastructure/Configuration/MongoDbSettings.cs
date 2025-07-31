namespace McpServer.Infrastructure.Configuration;

/// <summary>
/// Configuration settings for MongoDB Atlas connection used for vector storage.
/// Defines connection parameters for the document chunk vector database.
/// </summary>
public class MongoDbSettings
{
    public string ConnectionString { get; set; } = "mongodb://localhost:27017";
    public string DatabaseName { get; set; } = "mcprag_banking";
}