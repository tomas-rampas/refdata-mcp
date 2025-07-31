# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is an MCP-RAG Banking Reference Data System - a .NET-based Model Context Protocol server with Retrieval-Augmented Generation capabilities designed for banking reference data teams. The system helps teams query policies, procedures, and reference data using natural language while maintaining accuracy and compliance standards.

## Code Guidelines

- Always comment public classes including its purpose
- Always comment public methods including its purpose, if method comes form interface put the comment there and only refer to it in the class
- Run `dotnet build` in .\src directory after every completed task and fix all errors

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

### Current Phase
- 🚧 **Phase 5: Client Application** - Starting with Blazor WASM client implementation

### Key Implementation Notes
- All services are registered in DI container with appropriate lifetimes
- API includes comprehensive Swagger documentation
- Health check endpoints are available for monitoring
- Background ingestion service is configured to run periodically
- CORS is configured for local development

## Recent Updates (2025-07-31)
- Completed Phase 4: Application Services & API
- Created all API controllers (Chat, Ingestion, Health)
- Configured dependency injection for all services
- Added comprehensive appsettings.json configuration
- Enabled Swagger/OpenAPI documentation
- Configured CORS for Blazor client integration