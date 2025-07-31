using MongoDB.Bson;
using MongoDB.Driver;
using McpServer.Core.Entities;
using McpServer.Core.Interfaces;

namespace McpServer.Infrastructure.VectorStore;

/// <summary>
/// Implements vector storage and similarity search using MongoDB Atlas Vector Search.
/// Stores document chunks with embeddings and enables semantic retrieval for RAG queries.
/// </summary>
public class MongoVectorStore : IVectorStore
{
    private readonly IMongoCollection<BsonDocument> _collection;
    private readonly string _vectorIndexName = "vector_index";

    public MongoVectorStore(IMongoDatabase database)
    {
        _collection = database.GetCollection<BsonDocument>("document_chunks");
        
        // Ensure vector search index exists
        Task.Run(async () => await EnsureVectorIndexAsync());
    }

    /// <inheritdoc cref="IVectorStore.StoreEmbeddingAsync"/>
    public async Task<string> StoreEmbeddingAsync(DocumentChunk chunk, CancellationToken cancellationToken = default)
    {
        var document = new BsonDocument
        {
            ["_id"] = string.IsNullOrEmpty(chunk.Id) ? ObjectId.GenerateNewId().ToString() : chunk.Id,
            ["sourceId"] = chunk.SourceId,
            ["content"] = chunk.Content,
            ["embedding"] = new BsonArray(chunk.Embedding.Select(f => new BsonDouble(f))),
            ["metadata"] = new BsonDocument
            {
                ["title"] = chunk.Metadata.Title,
                ["department"] = chunk.Metadata.Department,
                ["documentType"] = chunk.Metadata.DocumentType.ToString(),
                ["effectiveDate"] = chunk.Metadata.EffectiveDate.HasValue 
                    ? BsonValue.Create(chunk.Metadata.EffectiveDate.Value) 
                    : BsonNull.Value,
                ["version"] = chunk.Metadata.Version
            },
            ["createdAt"] = chunk.CreatedAt
        };

        await _collection.InsertOneAsync(document, cancellationToken: cancellationToken);
        return document["_id"].AsString;
    }

    /// <inheritdoc cref="IVectorStore.SearchSimilarAsync"/>
    public async Task<IEnumerable<DocumentChunk>> SearchSimilarAsync(float[] queryEmbedding, int topK = 5, float minimumSimilarityScore = 0.0f, CancellationToken cancellationToken = default)
    {
        // MongoDB Atlas Vector Search query
        var pipeline = new[]
        {
            new BsonDocument("$vectorSearch", new BsonDocument
            {
                ["index"] = _vectorIndexName,
                ["path"] = "embedding",
                ["queryVector"] = new BsonArray(queryEmbedding.Select(f => new BsonDouble(f))),
                ["numCandidates"] = topK * 10, // Over-fetch for better results
                ["limit"] = topK
            }),
            new BsonDocument("$project", new BsonDocument
            {
                ["_id"] = 1,
                ["sourceId"] = 1,
                ["content"] = 1,
                ["embedding"] = 1,
                ["metadata"] = 1,
                ["createdAt"] = 1,
                ["score"] = new BsonDocument("$meta", "vectorSearchScore")
            })
        };

        var results = await _collection.Aggregate<BsonDocument>(pipeline, cancellationToken: cancellationToken)
            .ToListAsync(cancellationToken);

        return results
            .Where(doc => doc.GetValue("score", 0.0).AsDouble >= minimumSimilarityScore)
            .Select(doc => new DocumentChunk
            {
                Id = doc["_id"].AsString,
                SourceId = doc["sourceId"].AsString,
                Content = doc["content"].AsString,
                Embedding = doc["embedding"].AsBsonArray.Select(v => (float)v.AsDouble).ToArray(),
                Metadata = new DocumentMetadata
                {
                    Title = doc["metadata"]["title"].AsString,
                    Department = doc["metadata"]["department"].AsString,
                    DocumentType = Enum.Parse<Core.Enums.DocumentType>(doc["metadata"]["documentType"].AsString),
                    EffectiveDate = doc["metadata"]["effectiveDate"].IsBsonNull ? null : doc["metadata"]["effectiveDate"].ToUniversalTime(),
                    Version = doc["metadata"]["version"].AsString
                },
                CreatedAt = doc["createdAt"].ToUniversalTime()
            });
    }

    private async Task EnsureVectorIndexAsync()
    {
        try
        {
            // Note: Vector index creation requires MongoDB Atlas
            // This is a placeholder - actual index creation depends on Atlas configuration
            var indexKeys = Builders<BsonDocument>.IndexKeys.Ascending("embedding");
            var indexModel = new CreateIndexModel<BsonDocument>(indexKeys, new CreateIndexOptions
            {
                Name = _vectorIndexName
            });

            await _collection.Indexes.CreateOneAsync(indexModel);
        }
        catch (Exception ex)
        {
            // Log error - index might already exist or require Atlas configuration
            Console.WriteLine($"Vector index creation note: {ex.Message}");
        }
    }
}