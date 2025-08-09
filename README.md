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

| Service | Username | Password | Database |
|---------|----------|----------|----------|
| PostgreSQL | myuser | mysecretpassword | postgres, jiradb, confluencedb |
| MongoDB | myuser | mysecretpassword | - |
| Elasticsearch | N/A | No authentication (dev only) | - |

### Connecting to Services

#### PostgreSQL Connection
```bash
# Connect via Docker (recommended)
docker exec -it postgres_db psql -U myuser -d postgres

# Connect from host (requires psql client)
psql -h localhost -p 5432 -U myuser -d postgres

# List all databases
docker exec postgres_db psql -U myuser -d postgres -c "\l"
```

#### MongoDB Connection
```bash
# Connect via Docker
docker exec -it mongo_db mongosh --username myuser --password mysecretpassword

# Connect from host (requires mongosh client)
mongosh "mongodb://myuser:mysecretpassword@localhost:27017"
```

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

# Test PostgreSQL connection
docker exec postgres_db psql -U myuser -d postgres -c "\l"
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
3. **When prompted for database configuration, use these settings**:
   
   **IMPORTANT**: Use `postgres_db` as hostname (NOT localhost)
   
   | Setting | Value |
   |---------|-------|
   | Database Type | PostgreSQL |
   | Hostname | `postgres_db` |
   | Port | `5432` |
   | Database | `jiradb` |
   | Username | `myuser` |
   | Password | `mysecretpassword` |
   | JDBC URL | `jdbc:postgresql://postgres_db:5432/jiradb` |

4. Complete the license and administrator account setup

### Confluence Setup (First Time)
1. Navigate to http://localhost:8090
2. Follow the setup wizard
3. **When prompted for database configuration, use these settings**:
   
   **IMPORTANT**: Use `postgres_db` as hostname (NOT localhost)
   
   | Setting | Value |
   |---------|-------|
   | Database Type | PostgreSQL |
   | Hostname | `postgres_db` |
   | Port | `5432` |
   | Database | `confluencedb` |
   | Username | `myuser` |
   | Password | `mysecretpassword` |
   | JDBC URL | `jdbc:postgresql://postgres_db:5432/confluencedb` |

4. Complete the license and administrator account setup

## **Container Networking**

### **Important**: Container-to-Container Communication
- **From your host machine**: Use `localhost:5432` to connect to PostgreSQL
- **From containers (Jira/Confluence)**: Use `postgres_db:5432` to connect to PostgreSQL

### **Container Service Names**
All containers can reach each other using these hostnames:
- `postgres_db` - PostgreSQL database
- `mongo_db` - MongoDB  
- `elasticsearch_node` - Elasticsearch
- `ollama_llm` - Ollama LLM service
- `jira_instance` - Jira
- `confluence_instance` - Confluence

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

#### PostgreSQL Connection Issues
```bash
# Connect to PostgreSQL (correct way)
docker exec -it postgres_db psql -U myuser -d postgres

# If you get "No such file or directory" error when using psql directly:
# This means you're trying to connect via Unix socket instead of TCP
# Use the Docker exec method above, or install PostgreSQL client:
sudo apt-get install postgresql-client-common postgresql-client

# Then connect via TCP:
psql -h localhost -p 5432 -U myuser -d postgres

# Check PostgreSQL logs
docker logs postgres_db

# Verify databases were created
docker exec postgres_db psql -U myuser -d postgres -c "SELECT datname FROM pg_database;"
```

#### Jira/Confluence Database Connection Issues
If Jira or Confluence shows "Connection refused" errors:

```bash
# 1. Ensure PostgreSQL is running and healthy
docker logs postgres_db

# 2. Verify databases were created correctly
docker exec postgres_db psql -U myuser -d postgres -c "\l"

# 3. Check if containers are on the same network
docker network ls
docker network inspect refdata-mcp_refdata_network

# 4. Test connectivity between containers
docker exec jira_instance ping postgres_db
docker exec confluence_instance ping postgres_db

# 5. If databases weren't created correctly, reset PostgreSQL:
docker compose -f docker/docker-compose.yml stop postgres jira confluence
docker compose -f docker/docker-compose.yml rm postgres
docker volume ls | grep postgres  # Find the volume name
docker volume rm docker_postgres_data  # Replace with actual volume name
docker compose -f docker/docker-compose.yml up -d postgres
# Wait 10-15 seconds for PostgreSQL to initialize
docker compose -f docker/docker-compose.yml up -d jira confluence

# 6. Verify database creation
docker exec postgres_db psql -U myuser -d postgres -c "SELECT datname FROM pg_database WHERE datname IN ('jiradb', 'confluencedb');"
```

**Common Issue**: If you see a database named `"jiradb,confluencedb"` instead of two separate databases, this indicates the initialization script didn't run properly. Follow step 5 above to reset and recreate the PostgreSQL container.

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

## Scripts

### Sync-ConfluenceDoc.ps1

A cross-platform PowerShell script for scraping Oracle documentation and creating Confluence pages.

#### Prerequisites
- PowerShell 7+ (recommended for full cross-platform support)
- .NET SDK (for package management on Linux/macOS)

#### Usage

**Windows:**
```powershell
# Test cross-platform compatibility
.\scripts\Test-CrossPlatform.ps1 -Verbose

# Scrape and save JSON files
.\scripts\Sync-ConfluenceDoc.ps1 -Mode Save -Verbose

# Apply directly to Confluence
.\scripts\Sync-ConfluenceDoc.ps1 -Mode Apply -ConfluenceBaseUrl "http://localhost:8090" -ConfluenceSpaceKey "DOCS" -PersonalAccessToken "YOUR_TOKEN_HERE" -Verbose

# Apply and overwrite existing pages
.\scripts\Sync-ConfluenceDoc.ps1 -Mode Apply -ConfluenceBaseUrl "http://localhost:8090" -ConfluenceSpaceKey "DOCS" -PersonalAccessToken "YOUR_TOKEN_HERE" -OverwriteExisting -Verbose
```

**Linux/macOS:**
```bash
# Test cross-platform compatibility
pwsh scripts/Test-CrossPlatform.ps1 -Verbose

# Scrape and save JSON files
pwsh scripts/Sync-ConfluenceDoc.ps1 -Mode Save -Verbose

# Apply directly to Confluence
pwsh scripts/Sync-ConfluenceDoc.ps1 -Mode Apply -ConfluenceBaseUrl "http://localhost:8090" -ConfluenceSpaceKey "DOCS" -PersonalAccessToken "YOUR_TOKEN_HERE" -Verbose

# Apply and overwrite existing pages
pwsh scripts/Sync-ConfluenceDoc.ps1 -Mode Apply -ConfluenceBaseUrl "http://localhost:8090" -ConfluenceSpaceKey "DOCS" -PersonalAccessToken "YOUR_TOKEN_HERE" -OverwriteExisting -Verbose
```

#### Features
- Cross-platform compatibility (Windows, Linux, macOS)
- Automatic dependency management (HtmlAgilityPack)
- Confluence REST API integration
- Graceful rate limiting
- Robust error handling

## Support

For issues and questions:
- Check service logs first
- Ensure all prerequisites are met
- Verify port availability
- Check Docker resource allocation
- Run `Test-CrossPlatform.ps1` for PowerShell script issues