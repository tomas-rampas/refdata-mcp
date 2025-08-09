# MCP-RAG Reference Data System

A .NET-based Model Context Protocol server with Retrieval-Augmented Generation capabilities designed for reference data teams. This system provides natural language query capabilities over policies, procedures, and reference data while maintaining compliance and audit requirements.

## System Requirements

- **Docker & Docker Compose**: Docker Compose v2 (latest version)
- **RAM**: Minimum 16GB (32GB recommended)
- **Storage**: At least 20GB free space for Docker volumes
- **CPU**: Multi-core processor recommended for concurrent services

## Architecture Overview

The system consists of the following services:
- **PostgreSQL**: Shared database for Jira and Confluence
- **MongoDB**: Chat history and metadata storage
- **Elasticsearch**: Vector search engine
- **Ollama**: Local LLM service (Phi-3.5-mini model)
- **Jira**: Document source and issue tracking
- **Confluence**: Document source and knowledge base

## Quick Start

### 1. Clone the Repository
```bash
git clone <repository-url>
cd refdata-mcp
```

### 2. Start All Services
```bash
# Start all Docker services in detached mode
docker compose -f docker/docker-compose.yml up -d
```

### 3. Verify Services are Running
```bash
# Check service status
docker compose -f docker/docker-compose.yml ps

# View service logs
docker compose -f docker/docker-compose.yml logs -f
```

### 4. Install Ollama Model
After the Ollama service is running, install the Phi-3.5-mini model:

```bash
# Pull the Phi-3.5-mini model
docker exec ollama_llm ollama pull phi3.5:3.8b-mini-instruct-q4_K_M

# Verify model installation
docker exec ollama_llm ollama list
```

### 5. Verify Service Endpoints

Once all services are running, verify they are accessible:

#### Core Services
- **Ollama API**: http://localhost:11434
- **MongoDB**: localhost:27017
- **Elasticsearch**: http://localhost:9200
- **PostgreSQL**: localhost:5432

#### External Applications
- **Jira**: http://localhost:8080 (requires initial setup)
- **Confluence**: http://localhost:8090 (requires initial setup)

## Service Configuration

### Default Credentials

| Service | Username | Password |
|---------|----------|----------|
| PostgreSQL | myuser | mysecretpassword |
| MongoDB | myuser | mysecretpassword |
| Elasticsearch | N/A | No authentication (dev only) |

### Resource Allocation

- **Elasticsearch**: 1GB JVM heap
- **Ollama**: 6GB memory limit
- **Other services**: Default Docker limits

## Installation Steps

### Step 1: Prerequisites
Ensure Docker and Docker Compose are installed:
```bash
# Check Docker version
docker --version

# Check Docker Compose version
docker compose version
```

### Step 2: Environment Setup
```bash
# Create necessary directories (if not already present)
mkdir -p docker

# Ensure docker-compose.yml is in place
ls docker/docker-compose.yml
```

### Step 3: Start Infrastructure Services
```bash
# Start database and search services first
docker compose -f docker/docker-compose.yml up -d postgres mongodb elasticsearch

# Wait for services to be ready (check logs)
docker compose -f docker/docker-compose.yml logs postgres mongodb elasticsearch
```

### Step 4: Start Application Services
```bash
# Start Jira, Confluence, and Ollama
docker compose -f docker/docker-compose.yml up -d jira confluence ollama

# Monitor startup progress
docker compose -f docker/docker-compose.yml logs -f jira confluence ollama
```

### Step 5: Configure Ollama Models
```bash
# Wait for Ollama to be ready
docker exec ollama_llm ollama --version

# Pull the required model (this may take several minutes)
docker exec ollama_llm ollama pull phi3.5:3.8b-mini-instruct-q4_K_M

# Optional: Pull additional models for different use cases
docker exec ollama_llm ollama pull llama3.2:3b-instruct-q4_K_M
```

## Verification Commands

### Check Service Health
```bash
# Check all container status
docker compose -f docker/docker-compose.yml ps

# Test Ollama API
curl http://localhost:11434/api/tags

# Test Elasticsearch
curl http://localhost:9200/_cluster/health

# Test MongoDB connection
docker exec mongo_db mongosh --username myuser --password mysecretpassword --eval "db.adminCommand('ismaster')"
```

### Test Model Inference
```bash
# Test Ollama model with a simple query
curl -X POST http://localhost:11434/api/generate \
  -H "Content-Type: application/json" \
  -d '{
    "model": "phi3.5:3.8b-mini-instruct-q4_K_M",
    "prompt": "What is artificial intelligence?",
    "stream": false
  }'
```

## Service Management

### Starting Services
```bash
# Start all services
docker compose -f docker/docker-compose.yml up -d

# Start specific service
docker compose -f docker/docker-compose.yml up -d ollama
```

### Stopping Services
```bash
# Stop all services
docker compose -f docker/docker-compose.yml down

# Stop and remove volumes (WARNING: This deletes all data)
docker compose -f docker/docker-compose.yml down -v
```

### Viewing Logs
```bash
# View all service logs
docker compose -f docker/docker-compose.yml logs -f

# View specific service logs
docker compose -f docker/docker-compose.yml logs -f ollama
```

## Initial Setup Requirements

### Jira Setup (First Time)
1. Navigate to http://localhost:8080
2. Follow the setup wizard
3. Configure PostgreSQL connection:
   - **Host**: postgres_db
   - **Database**: jiradb
   - **Username**: myuser
   - **Password**: mysecretpassword

### Confluence Setup (First Time)
1. Navigate to http://localhost:8090
2. Follow the setup wizard
3. Configure PostgreSQL connection:
   - **Host**: postgres_db
   - **Database**: confluencedb
   - **Username**: myuser
   - **Password**: mysecretpassword

## Troubleshooting

### Common Issues

#### Out of Memory Errors
```bash
# Increase Docker memory allocation
# Go to Docker Desktop Settings > Resources > Memory
# Allocate at least 16GB RAM
```

#### Ollama Model Download Fails
```bash
# Check Ollama service status
docker logs ollama_llm

# Try pulling model manually
docker exec -it ollama_llm bash
ollama pull phi3.5:3.8b-mini-instruct-q4_K_M
```

#### Service Connection Issues
```bash
# Check if ports are available
netstat -tulpn | grep -E ':(5432|27017|9200|11434|8080|8090)'

# Restart problematic service
docker compose -f docker/docker-compose.yml restart <service-name>
```

### Logs and Debugging
```bash
# Check system resources
docker system df
docker system prune # Clean up unused resources

# Monitor resource usage
docker stats

# Access service containers
docker exec -it ollama_llm bash
docker exec -it mongo_db mongosh
```

## Development Workflow

For development of the .NET application:

1. Start infrastructure services
2. Build and run .NET projects locally
3. Configure connection strings to use localhost ports
4. Test integration with Docker services

See `CLAUDE.md` for detailed development guidance.

## Support

For issues and questions:
- Check service logs first
- Ensure all prerequisites are met
- Verify port availability
- Check Docker resource allocation