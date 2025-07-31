// Switch to mcprag database
db = db.getSiblingDB('mcprag');

// Create user for the application
db.createUser({
  user: 'mcprag_user',
  pwd: 'mcprag_password',
  roles: [
    { role: 'readWrite', db: 'mcprag' }
  ]
});

// Create collections
db.createCollection('document_chunks');
db.createCollection('ingestion_jobs');
db.createCollection('chat_requests');

// Create indexes for efficient queries
db.document_chunks.createIndex({ sourceId: 1 });
db.document_chunks.createIndex({ 'metadata.documentType': 1 });
db.document_chunks.createIndex({ 'metadata.department': 1 });
db.document_chunks.createIndex({ createdAt: -1 });

// Create vector search index for MongoDB Atlas Vector Search
// Note: This syntax is for MongoDB Atlas. For local MongoDB, you'll need to use a different approach
// or consider using a dedicated vector database alongside MongoDB
try {
  db.document_chunks.createIndex({
    "embedding": "cosmosSearch"
  }, {
    name: "vector_index",
    cosmosSearchOptions: {
      kind: "vector-ivf",
      numLists: 100,
      similarity: "COS",
      dimensions: 3072
    }
  });
} catch (e) {
  print("Note: Vector index creation failed. This is expected for non-Atlas MongoDB instances.");
  print("The application will use alternative search methods or you can integrate a dedicated vector DB.");
}

// Create indexes for other collections
db.ingestion_jobs.createIndex({ status: 1, startedAt: -1 });
db.chat_requests.createIndex({ userId: 1, timestamp: -1 });

print('Database initialization completed successfully!');