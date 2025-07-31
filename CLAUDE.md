# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is an MCP-RAG Banking Reference Data System - a .NET-based Model Context Protocol server with Retrieval-Augmented Generation capabilities designed for banking reference data teams. The system helps teams query policies, procedures, and reference data using natural language while maintaining accuracy and compliance standards.

## Code Guidelines

- Always provide comment for public classes including its purpose
- Always provide comment public methods including its purpose, if method comes form interface put the comment there and only refer to it in the class
- Always create unit tests for classes or set of classes which should be unit tested. Include positive and negative test scenarios automatically
- Run `dotnet build` in .\src directory after every completed task and fix all errors and warnings
- Rubn `dotnet test --no-build --verbosity normal` and fix all errors and warnings

## Architecture

The system consists of:
- **Data Sources**: Jira, Confluence, and local files
- **Document Processing**: Specialized parsers for banking documents
- **Vector Storage**: MongoDB with vector search
- **LLM Integration**: Local Ollama instance
- **Background Services**: Timer-based ingestion
- **API & Client**: RESTful API with Blazor WASM frontend

## Project Status

### Completed Phases
- ✅ **Phase 1: Foundation Setup** - Docker setup, project structure, solution and projects created
- ✅ **Phase 2: Core Domain Models** - All entities, interfaces, and enums implemented
- ✅ **Phase 3: Infrastructure Implementation** - All infrastructure services, parsers, and clients implemented
- ✅ **Phase 4: Application Services & API** - Application services, DTOs, API controllers, DI configuration, and settings completed
- ✅ **Phase 5: Client Application** - Blazor WASM client with Chat and Ingestion pages, ApiClient service, and banking-themed UI

### Current Phase
- ✅ **Phase 6: Docker & Deployment** - Dockerfiles and docker-compose configuration completed
- 🚧 **Phase 7: Testing & Documentation** - Unit and integration tests in progress (tasks 172-181 completed)

### Key Implementation Notes
- All services are registered in DI container with appropriate lifetimes
- API includes comprehensive Swagger documentation
- Health check endpoints are available for monitoring
- Background ingestion service is configured to run periodically
- CORS is configured for local development
- Blazor client includes chat interface and ingestion management
- Banking-appropriate styling applied throughout the UI

## Vector Database Considerations

### Current Limitation
- MongoDB vector search (`$vectorSearch`) is only available in MongoDB Atlas (cloud service)
- Local MongoDB containers do not support vector search operations
- Integration tests requiring vector search are marked as skipped

### Alternative Vector Databases for Local Development
1. **Apache Cassandra 5.0** - Native vector search support in open-source version
2. **PostgreSQL with pgvector** - Mature extension for vector operations
3. **Qdrant** - Purpose-built vector database with excellent Docker support
4. **Weaviate** - Vector-first database with GraphQL API
5. **ChromaDB** - Lightweight embedded vector database

### MongoDB Atlas CLI Option
- MongoDB offers Atlas CLI for local development with vector search
- Requires `atlas deployments setup` command
- Creates local Atlas deployment that supports `$vectorSearch`

## Recent Updates (2025-07-31)
- Completed Phase 5: Client Application
- Created Chat.razor page with message history and typing indicators
- Created Ingestion.razor page with status monitoring and job history
- Implemented ApiClient service for API communication
- Added navigation menu and banking-themed styling
- Updated MainLayout with professional banking UI
- Completed Phase 6: Docker & Deployment
- Created Dockerfiles for API and Client projects
- Updated docker-compose.yml with .NET services
- Added environment variable configuration
- Created appsettings.Docker.json
- Progressing Phase 7: Testing & Documentation
- Created tests folder structure
- Created McpServer.Core.Tests project with FluentAssertions and Moq
- Added comprehensive entity validation tests (78 tests, all passing)
- Created McpServer.Application.Tests project
- Added comprehensive RagService tests (13 tests, all passing)
- Added comprehensive IngestionService tests (15 tests, all passing)
- Created McpServer.Api.IntegrationTests project with WebApplicationFactory and Testcontainers
- Added API endpoint tests for Health, Chat, and Ingestion controllers
- Added MongoDB integration tests using Testcontainers
- Added Ollama client tests with mock HTTP handlers
- All test projects added to solution
- Total: 106 unit tests + integration tests (78 Core + 28 Application + Integration)
- Fixed MongoDB DI registration in API
- Updated integration tests to handle MongoDB Atlas requirements
- Researched Cassandra 5.0 as alternative vector database solution