# Comprehensive Iterative Development Plan for MCP-RAG Banking Reference Data System

## Table of Contents
1. [Overview and Architecture](#overview-and-architecture)
2. [Resource Optimization Strategy](#resource-optimization-strategy)
3. [Iteration 1: Complete Foundation Setup](#iteration-1-complete-foundation-setup)
4. [Iteration 2: Core Domain and Timer-Based Services](#iteration-2-core-domain-and-timer-based-services)
5. [Iteration 3: Document Processing and Vector Storage](#iteration-3-document-processing-and-vector-storage)
6. [Iteration 4: API Development and RAG Pipeline](#iteration-4-api-development-and-rag-pipeline)
7. [Iteration 5: Client Application](#iteration-5-client-application)
8. [Iteration 6: Optimization and Production Readiness](#iteration-6-optimization-and-production-readiness)

## Overview and Architecture

This document provides a step-by-step plan for building an MCP (Model Context Protocol) server with RAG (Retrieval-Augmented Generation) capabilities specifically designed for banking reference data teams. The system will help teams query policies, procedures, and reference data using natural language while maintaining accuracy and compliance standards.

### Why This Architecture?

The architecture balances several critical requirements:
- **Resource Efficiency**: Designed to run on a 32GB development machine while being scalable for production
- **Modularity**: Clean separation between data ingestion, vector storage, and query processing
- **Banking Focus**: Specialized document parsing and metadata extraction for financial documents
- **Flexibility**: Easy to swap components (like switching between LLM providers or vector databases)

### System Components

The system consists of six main components working together:

1. **Data Sources**: Jira (for tracking), Confluence (for documentation), and local files (simulating SharePoint)
2. **Document Loaders**: Specialized parsers that understand banking document structures
3. **Vector Storage**: MongoDB with vector search capabilities for efficient similarity matching
4. **LLM Integration**: Local Ollama instance running lightweight models
5. **Background Services**: Timer-based ingestion system for keeping data fresh
6. **API & Client**: RESTful API with Blazor WASM frontend for user interactions

## Resource Optimization Strategy

Given your 32GB RAM constraint, careful resource management is essential. Here's how we'll allocate memory to ensure smooth operation:

### Memory Allocation Plan

```
Total Available: 32GB
├── Operating System & IDE: 4GB
├── Docker Desktop: 1GB overhead
├── Service Containers:
│   ├── MongoDB: 2GB (with WiredTiger cache limited)
│   ├── Ollama + Model: 6GB (Phi-3.5-mini loaded)
│   ├── Jira + PostgreSQL: 2.5GB
│   ├── Confluence + PostgreSQL: 2.5GB
│   └── .NET Applications: 2GB
└── Buffer/Working Memory: 12GB
```

### Model Selection

For your banking use case, I recommend **Phi-3.5-mini-instruct-128k** because:
- It handles long context windows (important for policy documents)
- Excellent instruction following for structured queries
- Only requires 4-5GB RAM when quantized
- Microsoft's model performs well on business/technical content

Alternative lightweight options:
- **Llama-3.2-3B-Instruct**: Meta's latest small model, good for conversations
- **Qwen2.5-3B-Instruct**: Excellent multilingual support if needed
- **Gemma-2-2B-it**: Google's efficient model, very fast inference

## Iteration 1: Complete Foundation Setup

### Week 1-2: Environment and Service Configuration

This iteration establishes our complete development environment. We'll set up all services from the start to avoid configuration drift and ensure consistent development experience.

#### Sprint 1.1 - Docker Compose Environment (Days 1-3)

First, let's understand why we're using Docker Compose v2. The new compose specification provides better resource management, improved networking, and cleaner syntax. Create the following structure in your project:

```
McpRagBanking/
├── docker/
│   ├── docker-compose.yml
│   ├── mongo-init/
│   │   └── 01-init-database.js
│   ├── ollama-scripts/
│   │   └── setup-models.sh
│   └── data-init/
│       ├── setup-jira.sh
│       └── setup-confluence.sh
├── src/
└── docs/
```

Now, create `docker/docker-compose.yml`:

```yaml
# Docker Compose v2 configuration for MCP-RAG Banking System
# This file orchestrates all services needed for development
version: '3.8'

services:
  # MongoDB - Our vector database
  mongodb:
    image: mongo:7.0
    container_name: mcprag_mongodb
    ports:
      - "27017:27017"
    environment:
      MONGO_INITDB_ROOT_USERNAME: admin
      MONGO_INITDB_ROOT_PASSWORD: mcprag2024
      MONGO_INITDB_DATABASE: mcprag
    volumes:
      - mongo_data:/data/db
      - ./mongo-init:/docker-entrypoint-initdb.d:ro
    deploy:
      resources:
        limits:
          memory: 2G
        reservations:
          memory: 1G
    healthcheck:
      test: echo 'db.runCommand("ping").ok' | mongosh localhost:27017/mcprag --quiet
      interval: 30s
      timeout: 10s
      retries: 3
      start_period: 40s
    networks:
      - mcprag_network

  # Ollama - Local LLM service
  ollama:
    image: ollama/ollama:latest
    container_name: mcprag_ollama
    ports:
      - "11434:11434"
    volumes:
      - ollama_data:/root/.ollama
      - ./ollama-scripts:/scripts:ro
    environment:
      - OLLAMA_KEEP_ALIVE=24h
      - OLLAMA_HOST=0.0.0.0
      - OLLAMA_MODELS=/root/.ollama/models
    deploy:
      resources:
        limits:
          memory: 6G
        reservations:
          memory: 4G
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:11434/api/tags"]
      interval: 30s
      timeout: 10s
      retries: 3
    networks:
      - mcprag_network

  # PostgreSQL for Jira
  postgres_jira:
    image: postgres:15-alpine
    container_name: mcprag_postgres_jira
    environment:
      POSTGRES_DB: jiradb
      POSTGRES_USER: jirauser
      POSTGRES_PASSWORD: jirapass2024
    volumes:
      - postgres_jira_data:/var/lib/postgresql/data
    deploy:
      resources:
        limits:
          memory: 512M
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U jirauser"]
      interval: 10s
      timeout: 5s
      retries: 5
    networks:
      - mcprag_network

  # Jira Software
  jira:
    image: atlassian/jira-software:9.12
    container_name: mcprag_jira
    ports:
      - "8080:8080"
    environment:
      # Database configuration
      ATL_JDBC_URL: jdbc:postgresql://postgres_jira:5432/jiradb
      ATL_JDBC_USER: jirauser
      ATL_JDBC_PASSWORD: jirapass2024
      ATL_DB_DRIVER: org.postgresql.Driver
      ATL_DB_TYPE: postgres72
      # Memory settings
      JVM_MINIMUM_MEMORY: 1024m
      JVM_MAXIMUM_MEMORY: 2048m
      # Jira configuration
      ATL_TOMCAT_PORT: 8080
      ATL_TOMCAT_SCHEME: http
      ATL_TOMCAT_CONTEXTPATH: /
      ATL_PROXY_NAME: localhost
      ATL_PROXY_PORT: 8080
    volumes:
      - jira_data:/var/atlassian/application-data/jira
    depends_on:
      postgres_jira:
        condition: service_healthy
    deploy:
      resources:
        limits:
          memory: 2G
        reservations:
          memory: 1G
    networks:
      - mcprag_network

  # PostgreSQL for Confluence
  postgres_confluence:
    image: postgres:15-alpine
    container_name: mcprag_postgres_confluence
    environment:
      POSTGRES_DB: confluencedb
      POSTGRES_USER: confuser
      POSTGRES_PASSWORD: confpass2024
    volumes:
      - postgres_confluence_data:/var/lib/postgresql/data
    deploy:
      resources:
        limits:
          memory: 512M
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U confuser"]
      interval: 10s
      timeout: 5s
      retries: 5
    networks:
      - mcprag_network

  # Confluence
  confluence:
    image: atlassian/confluence:8.8
    container_name: mcprag_confluence
    ports:
      - "8090:8090"
    environment:
      # Database configuration
      ATL_JDBC_URL: jdbc:postgresql://postgres_confluence:5432/confluencedb
      ATL_JDBC_USER: confuser
      ATL_JDBC_PASSWORD: confpass2024
      ATL_DB_DRIVER: org.postgresql.Driver
      ATL_DB_TYPE: postgresql
      # Memory settings
      JVM_MINIMUM_MEMORY: 1024m
      JVM_MAXIMUM_MEMORY: 2048m
      # Confluence configuration
      ATL_TOMCAT_PORT: 8090
      ATL_TOMCAT_SCHEME: http
      ATL_TOMCAT_CONTEXTPATH: /
      ATL_PROXY_NAME: localhost
      ATL_PROXY_PORT: 8090
    volumes:
      - confluence_data:/var/atlassian/application-data/confluence
    depends_on:
      postgres_confluence:
        condition: service_healthy
    deploy:
      resources:
        limits:
          memory: 2G
        reservations:
          memory: 1G
    networks:
      - mcprag_network

# Named volumes for data persistence
volumes:
  mongo_data:
    driver: local
  ollama_data:
    driver: local
  postgres_jira_data:
    driver: local
  jira_data:
    driver: local
  postgres_confluence_data:
    driver: local
  confluence_data:
    driver: local

# Custom network for service communication
networks:
  mcprag_network:
    driver: bridge
    ipam:
      config:
        - subnet: 172.20.0.0/16
```

#### MongoDB Initialization Script

Create `docker/mongo-init/01-init-database.js`:

```javascript
// MongoDB initialization script for MCP-RAG system
// This script sets up the database structure and indexes for vector search

// Switch to the mcprag database
db = db.getSiblingDB('mcprag');

// Create application user with specific permissions
db.createUser({
  user: 'mcpapp',
  pwd: 'mcpapp2024',
  roles: [
    { role: 'readWrite', db: 'mcprag' },
    { role: 'dbAdmin', db: 'mcprag' }
  ]
});

print('Created application user');

// Create collections with validation schemas
db.createCollection('document_chunks', {
  validator: {
    $jsonSchema: {
      bsonType: 'object',
      required: ['content', 'source', 'vector', 'indexedOn'],
      properties: {
        content: {
          bsonType: 'string',
          description: 'The text content of the chunk'
        },
        source: {
          bsonType: 'string',
          description: 'URL or path to the source document'
        },
        vector: {
          bsonType: 'array',
          description: 'Embedding vector for similarity search'
        },
        documentType: {
          bsonType: 'string',
          enum: ['Policy', 'Procedure', 'Reference', 'Risk', 'Compliance', 'General'],
          description: 'Type of banking document'
        },
        department: {
          bsonType: 'string',
          description: 'Department that owns this document'
        },
        indexedOn: {
          bsonType: 'date',
          description: 'When this chunk was indexed'
        }
      }
    }
  }
});

print('Created document_chunks collection with validation');

// Create additional collections
db.createCollection('ingestion_history');
db.createCollection('query_logs');
db.createCollection('vector_indexes');

// Create indexes for efficient querying
db.document_chunks.createIndex({ source: 1, version: 1 }, { unique: true });
db.document_chunks.createIndex({ documentType: 1, department: 1 });
db.document_chunks.createIndex({ indexedOn: -1 });
db.document_chunks.createIndex({ content: 'text' }); // Text search index

db.ingestion_history.createIndex({ startTime: -1 });
db.ingestion_history.createIndex({ status: 1, startTime: -1 });

db.query_logs.createIndex({ timestamp: -1 });
db.query_logs.createIndex({ userId: 1, timestamp: -1 });

print('Created all indexes');

// Insert initial configuration document
db.vector_indexes.insertOne({
  name: 'default_vector_index',
  dimension: 768,  // Dimension for nomic-embed-text
  similarity: 'cosine',
  createdAt: new Date(),
  status: 'pending_creation'
});

print('MongoDB initialization completed successfully');
```

#### Sprint 1.2 - LLM Model Setup (Days 4-5)

Setting up the LLM properly is crucial for system performance. Create `docker/ollama-scripts/setup-models.sh`:

```bash
#!/bin/bash
# Comprehensive LLM setup script for MCP-RAG Banking System
# This script installs and configures both embedding and generation models

set -e  # Exit on error

echo "=================================="
echo "MCP-RAG LLM Model Setup"
echo "=================================="

# Color codes for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Function to check if Ollama is ready
wait_for_ollama() {
    echo -n "Waiting for Ollama to be ready..."
    while ! curl -s http://localhost:11434/api/tags > /dev/null 2>&1; do
        echo -n "."
        sleep 2
    done
    echo -e " ${GREEN}Ready!${NC}"
}

# Function to pull a model with progress tracking
pull_model() {
    local model=$1
    local description=$2
    
    echo -e "\n${YELLOW}Pulling $description${NC}"
    echo "Model: $model"
    
    if ollama list | grep -q "^$model"; then
        echo -e "${GREEN}Model already exists, skipping...${NC}"
        return 0
    fi
    
    ollama pull $model || {
        echo -e "${RED}Failed to pull $model${NC}"
        return 1
    }
    
    echo -e "${GREEN}Successfully pulled $model${NC}"
}

# Function to create custom model
create_custom_model() {
    echo -e "\n${YELLOW}Creating custom banking assistant model${NC}"
    
    # Create the modelfile
    cat > /tmp/banking-assistant.modelfile << 'EOF'
# Banking Assistant Model Configuration
FROM phi3.5:3.8b-mini-instruct-fp16

# System prompt optimized for banking reference data
SYSTEM """You are a specialized banking reference data assistant with expertise in:
- Banking policies and procedures
- Regulatory compliance (Basel III, MiFID II, Dodd-Frank)
- Risk management frameworks
- Settlement processes and book structures
- Counterparty management

Guidelines:
1. Always provide accurate, compliance-focused responses
2. Cite source documents when available
3. Use proper banking terminology
4. If uncertain, explicitly state limitations
5. Highlight any regulatory implications
6. Structure responses clearly with sections when appropriate

Your tone should be professional, precise, and helpful."""

# Optimal parameters for banking Q&A
PARAMETER temperature 0.3          # Lower temperature for consistency
PARAMETER top_p 0.9               # Focused token selection
PARAMETER repeat_penalty 1.1      # Avoid repetition
PARAMETER num_ctx 4096           # Context window
PARAMETER num_predict 2048       # Maximum response length

# Response format template
TEMPLATE """{{ if .System }}<|system|>
{{ .System }}<|end|>
{{ end }}{{ if .Prompt }}<|user|>
{{ .Prompt }}<|end|>
<|assistant|>
{{ end }}"""
EOF

    # Create the model
    ollama create banking-assistant -f /tmp/banking-assistant.modelfile || {
        echo -e "${RED}Failed to create custom model${NC}"
        return 1
    }
    
    echo -e "${GREEN}Custom model created successfully${NC}"
}

# Function to test model functionality
test_models() {
    echo -e "\n${YELLOW}Testing model functionality${NC}"
    
    # Test generation model
    echo "Testing text generation..."
    local gen_response=$(curl -s -X POST http://localhost:11434/api/generate \
        -d '{
            "model": "banking-assistant",
            "prompt": "What is a settlement fail in banking?",
            "stream": false,
            "options": {
                "temperature": 0.3,
                "num_predict": 100
            }
        }' | jq -r '.response' 2>/dev/null)
    
    if [ -n "$gen_response" ]; then
        echo -e "${GREEN}✓ Text generation working${NC}"
        echo "Sample response: ${gen_response:0:100}..."
    else
        echo -e "${RED}✗ Text generation failed${NC}"
        return 1
    fi
    
    # Test embedding model
    echo -e "\nTesting embeddings..."
    local emb_response=$(curl -s -X POST http://localhost:11434/api/embeddings \
        -d '{
            "model": "nomic-embed-text",
            "prompt": "banking settlement process"
        }' | jq '.embedding[0]' 2>/dev/null)
    
    if [ -n "$emb_response" ]; then
        echo -e "${GREEN}✓ Embedding generation working${NC}"
        echo "Embedding dimension: $(curl -s -X POST http://localhost:11434/api/embeddings -d '{"model": "nomic-embed-text", "prompt": "test"}' | jq '.embedding | length')"
    else
        echo -e "${RED}✗ Embedding generation failed${NC}"
        return 1
    fi
}

# Function to display model information
show_model_info() {
    echo -e "\n${YELLOW}Installed Models Information${NC}"
    echo "=================================="
    ollama list
    
    echo -e "\n${YELLOW}Model Details${NC}"
    echo "=================================="
    
    # Get detailed info for each model
    for model in "banking-assistant" "nomic-embed-text"; do
        echo -e "\n${GREEN}$model:${NC}"
        ollama show $model --modelfile 2>/dev/null | head -20 || echo "No detailed info available"
    done
}

# Main setup process
main() {
    echo "Starting LLM model setup process..."
    
    # Wait for Ollama to be ready
    wait_for_ollama
    
    # Pull required models
    pull_model "phi3.5:3.8b-mini-instruct-fp16" "Phi-3.5 Mini (Main LLM)" || exit 1
    pull_model "nomic-embed-text:latest" "Nomic Embed Text (Embeddings)" || exit 1
    
    # Create custom banking model
    create_custom_model || exit 1
    
    # Test models
    test_models || exit 1
    
    # Show final information
    show_model_info
    
    echo -e "\n${GREEN}=================================="
    echo "Model setup completed successfully!"
    echo "==================================${NC}"
    echo ""
    echo "Models ready for use:"
    echo "- Text Generation: banking-assistant"
    echo "- Embeddings: nomic-embed-text"
    echo ""
    echo "Memory usage estimate:"
    echo "- Phi-3.5: ~4-5GB"
    echo "- Nomic Embed: ~500MB"
    echo "- Total: ~5-6GB"
}

# Run main setup
main
```

Make the script executable and run the setup:

```bash
# Start all services
docker compose up -d

# Check service health
docker compose ps

# Execute model setup
docker exec mcprag_ollama bash /scripts/setup-models.sh
```

#### Sprint 1.3 - Initialize Jira and Confluence (Days 6-7)

Now we'll set up Jira and Confluence with banking-specific data. First, you'll need to complete the initial web-based setup:

**Manual Setup Steps:**

1. **Jira Setup (http://localhost:8080)**:
   - Choose "I'll set it up myself"
   - Select "Built-in database" (already configured via Docker)
   - Application title: "MCP-RAG Banking Reference"
   - Mode: Private
   - Base URL: http://localhost:8080
   - Create admin account: username `admin`, password `admin2024`

2. **Confluence Setup (http://localhost:8090)**:
   - Choose "Production Installation"
   - Select "Built-in database" (already configured)
   - Configure admin: username `admin`, password `admin2024`
   - Space name: "Banking Reference Data"

After manual setup, create `scripts/populate-banking-data.sh`:

```bash
#!/bin/bash
# Script to populate Jira and Confluence with banking reference data

# Configuration
JIRA_URL="http://localhost:8080"
CONFLUENCE_URL="http://localhost:8090"
USERNAME="admin"
PASSWORD="admin2024"

# Base64 encode credentials
AUTH=$(echo -n "$USERNAME:$PASSWORD" | base64)

echo "Creating Jira project and issues..."

# Create Jira project using REST API
PROJECT_KEY="REFDATA"
PROJECT_JSON=$(cat <<EOF
{
  "key": "$PROJECT_KEY",
  "name": "Reference Data Management",
  "description": "Banking reference data tracking and management",
  "projectTypeKey": "business",
  "lead": "$USERNAME",
  "assigneeType": "PROJECT_LEAD"
}
EOF
)

# Create project
curl -X POST \
  -H "Authorization: Basic $AUTH" \
  -H "Content-Type: application/json" \
  -d "$PROJECT_JSON" \
  "$JIRA_URL/rest/api/2/project"

# Create sample issues
create_jira_issue() {
  local summary="$1"
  local description="$2"
  local issue_type="$3"
  local labels="$4"
  
  ISSUE_JSON=$(cat <<EOF
{
  "fields": {
    "project": {"key": "$PROJECT_KEY"},
    "summary": "$summary",
    "description": "$description",
    "issuetype": {"name": "$issue_type"},
    "labels": $labels
  }
}
EOF
)
  
  curl -X POST \
    -H "Authorization: Basic $AUTH" \
    -H "Content-Type: application/json" \
    -d "$ISSUE_JSON" \
    "$JIRA_URL/rest/api/2/issue"
}

# Create banking-specific issues
create_jira_issue \
  "Update Counterparty Onboarding Process for Q1 2025" \
  "Need to incorporate new KYC requirements from recent regulatory changes. This includes enhanced due diligence for high-risk jurisdictions and updated beneficial ownership requirements." \
  "Task" \
  '["compliance", "kyc", "counterparty", "regulatory"]'

create_jira_issue \
  "Document Equity Derivatives Book Structure" \
  "Create comprehensive documentation for equity derivatives book hierarchy including:\n- Legal entity mapping\n- Trading desk structure\n- Risk limit framework\n- P&L attribution methodology" \
  "Task" \
  '["book-structure", "equity-derivatives", "documentation", "risk"]'

create_jira_issue \
  "Settlement Fails Threshold Review Q4 2024" \
  "Quarterly review of settlement fail thresholds:\n- Analyze Q4 fail rates\n- Review regulatory requirements\n- Propose threshold adjustments\n- Update monitoring procedures" \
  "Task" \
  '["settlement", "operations", "risk-management", "quarterly-review"]'

echo "Creating Confluence space and pages..."

# Get Confluence space key (assuming it was created manually)
SPACE_KEY="REFDATA"

# Function to create Confluence page
create_confluence_page() {
  local title="$1"
  local content="$2"
  
  PAGE_JSON=$(cat <<EOF
{
  "type": "page",
  "title": "$title",
  "space": {"key": "$SPACE_KEY"},
  "body": {
    "storage": {
      "value": "$content",
      "representation": "storage"
    }
  }
}
EOF
)
  
  curl -X POST \
    -H "Authorization: Basic $AUTH" \
    -H "Content-Type: application/json" \
    -d "$PAGE_JSON" \
    "$CONFLUENCE_URL/rest/api/content"
}

# Create banking reference pages
create_confluence_page "Counterparty Onboarding Process" '<h1>Counterparty Onboarding Process</h1>
<h2>Overview</h2>
<p>This document defines the standard operating procedure for onboarding new counterparties in compliance with current banking regulations.</p>
<h2>Regulatory Framework</h2>
<ul>
<li>Basel III requirements for counterparty credit risk</li>
<li>MiFID II client categorization</li>
<li>FATF recommendations on customer due diligence</li>
</ul>
<h2>Required Documentation</h2>
<table>
<tr><th>Document Type</th><th>Description</th><th>Validity Period</th></tr>
<tr><td>Legal Entity Identifier (LEI)</td><td>20-character alphanumeric code</td><td>Annual renewal</td></tr>
<tr><td>Certificate of Incorporation</td><td>Official registration document</td><td>No expiry</td></tr>
<tr><td>Audited Financial Statements</td><td>Last 3 years required</td><td>Annual update</td></tr>
<tr><td>Board Resolution</td><td>Authorization for trading activities</td><td>Review every 2 years</td></tr>
</table>
<h2>Workflow Stages</h2>
<ol>
<li><strong>Initial Screening</strong> - Operations team performs preliminary checks</li>
<li><strong>KYC Review</strong> - Compliance validates documentation</li>
<li><strong>Credit Assessment</strong> - Risk department evaluates creditworthiness</li>
<li><strong>Legal Review</strong> - Legal confirms documentation completeness</li>
<li><strong>Final Approval</strong> - Head of Trading signs off</li>
</ol>'

create_confluence_page "Book Structure Guidelines" '<h1>Trading Book Structure Guidelines</h1>
<h2>Purpose</h2>
<p>This document establishes the standardized book structure for regulatory reporting and risk management across all trading activities.</p>
<h2>Hierarchy Levels</h2>
<h3>Level 1: Legal Entity</h3>
<p>Top-level representing the licensed banking entity</p>
<ul>
<li>Must align with regulatory licenses</li>
<li>Separate books for each regulated entity</li>
</ul>
<h3>Level 2: Business Division</h3>
<ul>
<li><strong>Markets</strong> - Trading and sales activities</li>
<li><strong>Investment Banking</strong> - Advisory and underwriting</li>
<li><strong>Treasury</strong> - Liquidity and funding management</li>
</ul>
<h3>Level 3: Trading Desk</h3>
<p>Specific desks within each division:</p>
<ul>
<li>Rates Trading</li>
<li>Credit Trading</li>
<li>Equity Derivatives</li>
<li>FX and Commodities</li>
</ul>
<h3>Level 4: Strategy/Portfolio</h3>
<p>Granular level for risk management and P&L attribution</p>'

create_confluence_page "Settlement Fails Policy" '<h1>Settlement Fails Policy</h1>
<h2>Definition</h2>
<p>A settlement fail occurs when securities or cash are not delivered on the contractual settlement date.</p>
<h2>Regulatory Context</h2>
<ul>
<li>CSDR (Central Securities Depositories Regulation) penalties</li>
<li>SEC Rule 204 close-out requirements</li>
<li>Local market practices and penalties</li>
</ul>
<h2>Fail Thresholds</h2>
<table>
<tr><th>Product Type</th><th>Fail Rate Threshold</th><th>Escalation Level</th></tr>
<tr><td>Government Bonds</td><td>0.5%</td><td>Desk Head</td></tr>
<tr><td>Corporate Bonds</td><td>1.0%</td><td>Desk Head</td></tr>
<tr><td>Equities</td><td>2.0%</td><td>Operations Manager</td></tr>
<tr><td>Money Market</td><td>0.1%</td><td>Treasury Head</td></tr>
</table>
<h2>Monitoring and Reporting</h2>
<p>Daily fail reports distributed by 9:00 AM covering:</p>
<ul>
<li>Aging analysis of outstanding fails</li>
<li>Root cause categorization</li>
<li>Financial impact assessment</li>
<li>Remediation action tracking</li>
</ul>'

echo "Creating local SharePoint simulation files..."
mkdir -p ./SharePoint_Data

# Create sample banking documents
cat > ./SharePoint_Data/Client_Accounts_Q3_2024.txt << 'EOF'
CLIENT ACCOUNTS REPORT - Q3 2024
================================

Executive Summary:
Total Active Accounts: 2,847
New Accounts Opened: 342
Accounts Closed: 89
Net Growth: 253 (9.7% QoQ)

Top 10 Clients by Revenue:
1. Global Pension Fund Alpha - $4.2M
2. Sovereign Wealth Fund Beta - $3.8M
3. Insurance Company Gamma - $3.1M
4. Asset Manager Delta - $2.9M
5. Hedge Fund Epsilon - $2.7M
6. Corporate Treasury Zeta - $2.4M
7. Private Bank Eta - $2.2M
8. Investment Fund Theta - $2.0M
9. Endowment Iota - $1.9M
10. Family Office Kappa - $1.8M

Account Distribution by Type:
- Institutional: 1,423 (50%)
- Corporate: 854 (30%)
- Private Banking: 570 (20%)

Geographical Distribution:
- EMEA: 1,424 (50%)
- Americas: 997 (35%)
- APAC: 426 (15%)
EOF

cat > ./SharePoint_Data/Settlement_Fails_Analysis_2024.txt << 'EOF'
SETTLEMENT FAILS ANALYSIS - 2024 YTD
====================================

Overall Fail Rate: 1.23% (Industry Avg: 1.87%)

Monthly Breakdown:
Jan: 1.45% | Feb: 1.38% | Mar: 1.29%
Apr: 1.22% | May: 1.19% | Jun: 1.15%
Jul: 1.18% | Aug: 1.21% | Sep: 1.25%

Root Cause Analysis:
1. Late Instructions (35%)
   - Client delay: 22%
   - Internal delay: 13%

2. Insufficient Securities (28%)
   - Short selling related: 18%
   - Lending recall: 10%

3. Technical Issues (20%)
   - System outages: 12%
   - Data quality: 8%

4. Counterparty Issues (17%)
   - CP operational: 11%
   - CP credit: 6%

Financial Impact:
- CSDR Penalties YTD: €2.3M
- Buy-in Costs: €1.8M
- Total Cost: €4.1M

Improvement Initiatives:
1. Real-time instruction monitoring
2. Enhanced STP rates
3. Predictive fail analytics
4. Counterparty scorecards
EOF

echo "Setup completed successfully!"
echo "You can now access:"
echo "- Jira: http://localhost:8080"
echo "- Confluence: http://localhost:8090"
echo "- MongoDB: mongodb://localhost:27017"
echo "- Ollama API: http://localhost:11434"
```

### Deliverables for Iteration 1

By the end of this iteration, you'll have:
1. **Complete Docker environment** running all services with proper resource limits
2. **Configured LLM models** optimized for banking Q&A
3. **MongoDB** ready for vector storage with proper indexes
4. **Jira and Confluence** populated with realistic banking data
5. **Local files** simulating SharePoint documents
6. **Health monitoring** scripts to verify all services
7. **Complete documentation** of the setup process

## Iteration 2: Core Domain and Timer-Based Services

### Week 3-4: Building the Foundation with Background Processing

In this iteration, we'll implement the core domain models and create a robust background service using .NET's timer-based approach. This is simpler than heavyweight schedulers while still providing the flexibility needed for your banking use case.

#### Sprint 2.1 - Project Structure and Domain Models (Days 1-3)

Let's start by creating the solution structure and implementing rich domain models that capture banking-specific concepts:

```bash
# Create solution structure
dotnet new sln -n McpRagBanking
dotnet new classlib -n McpServer.Core -o src/McpServer.Core
dotnet new classlib -n McpServer.Infrastructure -o src/McpServer.Infrastructure
dotnet new classlib -n McpServer.Application -o src/McpServer.Application
dotnet new webapi -n McpServer.Api -o src/McpServer.Api
dotnet new blazorwasm -n McpServer.Client -o src/McpServer.Client

# Add projects to solution
dotnet sln add src/McpServer.Core/McpServer.Core.csproj
dotnet sln add src/McpServer.Infrastructure/McpServer.Infrastructure.csproj
dotnet sln add src/McpServer.Application/McpServer.Application.csproj
dotnet sln add src/McpServer.Api/McpServer.Api.csproj
dotnet sln add src/McpServer.Client/McpServer.Client.csproj

# Add project references
dotnet add src/McpServer.Infrastructure reference src/McpServer.Core
dotnet add src/McpServer.Application reference src/McpServer.Core
dotnet add src/McpServer.Application reference src/McpServer.Infrastructure
dotnet add src/McpServer.Api reference src/McpServer.Application
dotnet add src/McpServer.Api reference src/McpServer.Infrastructure
```

Now let's create comprehensive domain models in `src/McpServer.Core/Models`:

```csharp
// src/McpServer.Core/Models/DocumentChunk.cs
namespace McpServer.Core.Models;

/// <summary>
/// Represents a chunk of text from a document with its metadata and vector embedding.
/// This is the fundamental unit of our RAG system.
/// </summary>
public class DocumentChunk
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// The actual text content of this chunk
    /// </summary>
    public string Content { get; set; }
    
    /// <summary>
    /// URL or path to the source document
    /// </summary>
    public string Source { get; set; }
    
    /// <summary>
    /// Type of banking document (Policy, Procedure, Reference, etc.)
    /// </summary>
    public DocumentType DocumentType { get; set; }
    
    /// <summary>
    /// Banking department that owns this document
    /// </summary>
    public string Department { get; set; }
    
    /// <summary>
    /// When this document becomes effective (for policies/procedures)
    /// </summary>
    public DateTime? EffectiveDate { get; set; }
    
    /// <summary>
    /// Document version for change tracking
    /// </summary>
    public string Version { get; set; }
    
    /// <summary>
    /// When this chunk was indexed into the system
    /// </summary>
    public DateTime IndexedOn { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// The embedding vector for similarity search
    /// </summary>
    public float[] Vector { get; set; }
    
    /// <summary>
    /// Additional metadata specific to the document type
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();
    
    /// <summary>
    /// Position of this chunk within the original document
    /// </summary>
    public int ChunkIndex { get; set; }
    
    /// <summary>
    /// Total number of chunks from the same document
    /// </summary>
    public int TotalChunks { get; set; }
}

// src/McpServer.Core/Models/DocumentType.cs
namespace McpServer.Core.Models;

/// <summary>
/// Types of documents in the banking reference data system
/// </summary>
public enum DocumentType
{
    Policy,           // Trading policies, risk policies
    Procedure,        // Operational procedures, workflows
    Reference,        // Reference data, mappings
    RiskFramework,    // Risk management frameworks
    Compliance,       // Regulatory compliance documents
    General          // Other documents
}

// src/McpServer.Core/Models/IngestionJob.cs
namespace McpServer.Core.Models;

/// <summary>
/// Represents a scheduled data ingestion job configuration
/// </summary>
public class IngestionJob
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; }
    public List<string> DataSources { get; set; } = new();
    public TimeSpan Interval { get; set; }
    public DateTime? LastRunTime { get; set; }
    public DateTime? NextRunTime { get; set; }
    public IngestionStatus LastRunStatus { get; set; }
    public bool IsEnabled { get; set; } = true;
    public Dictionary<string, object> Configuration { get; set; } = new();
}

public enum IngestionStatus
{
    NotStarted,
    Running,
    Completed,
    Failed,
    PartiallyCompleted,
    Cancelled
}

// src/McpServer.Core/Models/IngestionRun.cs
namespace McpServer.Core.Models;

/// <summary>
/// Tracks the execution of an ingestion job
/// </summary>
public class IngestionRun
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string JobId { get; set; }
    public string JobName { get; set; }
    public DateTime StartTime { get; set; } = DateTime.UtcNow;
    public DateTime? EndTime { get; set; }
    public IngestionStatus Status { get; set; } = IngestionStatus.Running;
    public Dictionary<string, SourceIngestionDetail> SourceDetails { get; set; } = new();
    public int TotalChunksProcessed { get; set; }
    public int TotalChunksFailed { get; set; }
    public List<string> Errors { get; set; } = new();
    
    public TimeSpan? Duration => EndTime.HasValue ? EndTime.Value - StartTime : null;
}

public class SourceIngestionDetail
{
    public string SourceName { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public int ChunksProcessed { get; set; }
    public int ChunksFailed { get; set; }
    public List<string> Errors { get; set; } = new();
    public IngestionStatus Status { get; set; }
}
```

Now let's define the core interfaces in `src/McpServer.Core/Interfaces`:

```csharp
// src/McpServer.Core/Interfaces/IDocumentLoader.cs
namespace McpServer.Core.Interfaces;

/// <summary>
/// Interface for loading documents from various sources
/// </summary>
public interface IDocumentLoader
{
    /// <summary>
    /// Name of this data source (e.g., "Confluence", "Jira", "LocalFiles")
    /// </summary>
    string SourceName { get; }
    
    /// <summary>
    /// Load and chunk all documents from this source
    /// </summary>
    Task<IEnumerable<DocumentChunk>> LoadAndChunkAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Check if the source is available and accessible
    /// </summary>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);
}

// src/McpServer.Core/Interfaces/ILlmClient.cs
namespace McpServer.Core.Interfaces;

/// <summary>
/// Interface for interacting with LLM services
/// </summary>
public interface ILlmClient
{
    /// <summary>
    /// Generate embeddings for text
    /// </summary>
    Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Generate embeddings for multiple texts in batch
    /// </summary>
    Task<List<float[]>> GenerateEmbeddingsBatchAsync(List<string> texts, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Generate a response based on context
    /// </summary>
    Task<string> GenerateResponseAsync(RagContext context, CancellationToken cancellationToken = default);
}

// src/McpServer.Core/Interfaces/IVectorRepository.cs
namespace McpServer.Core.Interfaces;

/// <summary>
/// Interface for vector storage and similarity search
/// </summary>
public interface IVectorRepository
{
    /// <summary>
    /// Insert or update document chunks
    /// </summary>
    Task UpsertAsync(DocumentChunk chunk, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Insert or update multiple chunks efficiently
    /// </summary>
    Task UpsertBatchAsync(IEnumerable<DocumentChunk> chunks, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Search for similar documents based on vector similarity
    /// </summary>
    Task<List<ScoredDocumentChunk>> SearchAsync(
        float[] queryVector, 
        int maxResults = 5, 
        float minScore = 0.7f,
        Dictionary<string, string> filters = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Delete chunks by source to prepare for re-ingestion
    /// </summary>
    Task DeleteBySourceAsync(string source, CancellationToken cancellationToken = default);
}

// src/McpServer.Core/Models/ScoredDocumentChunk.cs
namespace McpServer.Core.Models;

/// <summary>
/// Document chunk with similarity score
/// </summary>
public class ScoredDocumentChunk : DocumentChunk
{
    public float Score { get; set; }
}
```

#### Sprint 2.2 - Timer-Based Background Service (Days 4-6)

Now let's implement a sophisticated timer-based background service that replaces the need for heavy schedulers:

```csharp
// src/McpServer.Infrastructure/Services/IngestionTimerService.cs
namespace McpServer.Infrastructure.Services;

/// <summary>
/// Timer-based background service for data ingestion.
/// This provides a lightweight alternative to job schedulers while maintaining flexibility.
/// </summary>
public class IngestionTimerService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<IngestionTimerService> _logger;
    private readonly IOptions<IngestionSettings> _settings;
    private readonly Dictionary<string, IngestionTimer> _timers = new();
    private readonly SemaphoreSlim _ingestionSemaphore;

    public IngestionTimerService(
        IServiceProvider serviceProvider,
        ILogger<IngestionTimerService> logger,
        IOptions<IngestionSettings> settings)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _settings = settings;
        
        // Limit concurrent ingestions to prevent resource exhaustion
        _ingestionSemaphore = new SemaphoreSlim(1, 1);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Ingestion timer service starting...");
        
        // Initialize timers for each configured job
        foreach (var jobConfig in _settings.Value.Jobs.Where(j => j.IsEnabled))
        {
            var timer = new IngestionTimer
            {
                JobConfig = jobConfig,
                Timer = new PeriodicTimer(jobConfig.Interval),
                CancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken)
            };
            
            _timers[jobConfig.Name] = timer;
            
            // Start timer task
            _ = Task.Run(async () => await RunTimerAsync(timer), stoppingToken);
            
            _logger.LogInformation(
                "Started timer for job '{JobName}' with interval {Interval}",
                jobConfig.Name, jobConfig.Interval);
        }
        
        // Run initial ingestion if configured
        if (_settings.Value.RunOnStartup)
        {
            _logger.LogInformation("Running initial ingestion on startup");
            await TriggerIngestionAsync("startup", stoppingToken);
        }
        
        // Keep service running
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task RunTimerAsync(IngestionTimer ingestionTimer)
    {
        var jobName = ingestionTimer.JobConfig.Name;
        var cancellationToken = ingestionTimer.CancellationTokenSource.Token;
        
        try
        {
            while (await ingestionTimer.Timer.WaitForNextTickAsync(cancellationToken))
            {
                await TriggerIngestionAsync(jobName, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Timer for job '{JobName}' was cancelled", jobName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in timer for job '{JobName}'", jobName);
        }
    }

    public async Task<IngestionRun> TriggerIngestionAsync(string jobName, CancellationToken cancellationToken = default)
    {
        // Try to acquire the semaphore (wait up to 1 minute)
        if (!await _ingestionSemaphore.WaitAsync(TimeSpan.FromMinutes(1), cancellationToken))
        {
            _logger.LogWarning("Could not start ingestion for '{JobName}' - another ingestion is running", jobName);
            return null;
        }

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var scopedProvider = scope.ServiceProvider;
            
            // Get services
            var ingestionOrchestrator = scopedProvider.GetRequiredService<IIngestionOrchestrator>();
            var ingestionTracker = scopedProvider.GetRequiredService<IIngestionTracker>();
            
            // Find job configuration
            var jobConfig = _timers.ContainsKey(jobName) 
                ? _timers[jobName].JobConfig 
                : _settings.Value.Jobs.FirstOrDefault(j => j.Name == jobName);
                
            if (jobConfig == null)
            {
                _logger.LogError("Job configuration not found for '{JobName}'", jobName);
                return null;
            }
            
            // Create ingestion run record
            var run = await ingestionTracker.StartIngestionAsync(jobName, jobConfig.DataSources);
            
            _logger.LogInformation(
                "Starting ingestion run {RunId} for job '{JobName}'",
                run.Id, jobName);
            
            try
            {
                // Execute ingestion
                await ingestionOrchestrator.ExecuteIngestionAsync(
                    run, 
                    jobConfig.DataSources, 
                    cancellationToken);
                
                // Mark as completed
                await ingestionTracker.CompleteIngestionAsync(run.Id);
                
                _logger.LogInformation(
                    "Completed ingestion run {RunId} for job '{JobName}'. Processed {Chunks} chunks",
                    run.Id, jobName, run.TotalChunksProcessed);
                
                return run;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    "Error during ingestion run {RunId} for job '{JobName}'",
                    run.Id, jobName);
                    
                await ingestionTracker.FailIngestionAsync(run.Id, ex.Message);
                throw;
            }
        }
        finally
        {
            _ingestionSemaphore.Release();
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Ingestion timer service stopping...");
        
        // Cancel all timers
        foreach (var timer in _timers.Values)
        {
            timer.CancellationTokenSource.Cancel();
            timer.Timer.Dispose();
        }
        
        await base.StopAsync(cancellationToken);
    }

    /// <summary>
    /// Get status of all configured timers
    /// </summary>
    public List<TimerStatus> GetTimerStatuses()
    {
        return _timers.Select(kvp => new TimerStatus
        {
            JobName = kvp.Key,
            Interval = kvp.Value.JobConfig.Interval,
            IsEnabled = kvp.Value.JobConfig.IsEnabled,
            NextRunTime = kvp.Value.NextRunTime
        }).ToList();
    }

    private class IngestionTimer
    {
        public IngestionJobConfig JobConfig { get; set; }
        public PeriodicTimer Timer { get; set; }
        public CancellationTokenSource CancellationTokenSource { get; set; }
        public DateTime? NextRunTime { get; set; }
    }
}

// src/McpServer.Infrastructure/Services/IngestionOrchestrator.cs
namespace McpServer.Infrastructure.Services;

/// <summary>
/// Orchestrates the ingestion process across multiple data sources
/// </summary>
public class IngestionOrchestrator : IIngestionOrchestrator
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<IngestionOrchestrator> _logger;
    private readonly ILlmClient _llmClient;
    private readonly IVectorRepository _vectorRepository;
    private readonly IChunkingService _chunkingService;

    public IngestionOrchestrator(
        IServiceProvider serviceProvider,
        ILogger<IngestionOrchestrator> logger,
        ILlmClient llmClient,
        IVectorRepository vectorRepository,
        IChunkingService chunkingService)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _llmClient = llmClient;
        _vectorRepository = vectorRepository;
        _chunkingService = chunkingService;
    }

    public async Task ExecuteIngestionAsync(
        IngestionRun run, 
        List<string> dataSources, 
        CancellationToken cancellationToken)
    {
        // Get all registered document loaders
        var allLoaders = _serviceProvider.GetServices<IDocumentLoader>().ToList();
        
        // Filter to requested sources (or use all if none specified)
        var loadersToProcess = dataSources?.Any() == true
            ? allLoaders.Where(l => dataSources.Contains(l.SourceName)).ToList()
            : allLoaders;

        _logger.LogInformation(
            "Processing {Count} data sources for run {RunId}",
            loadersToProcess.Count, run.Id);

        foreach (var loader in loadersToProcess)
        {
            var sourceDetail = new SourceIngestionDetail
            {
                SourceName = loader.SourceName,
                StartTime = DateTime.UtcNow,
                Status = IngestionStatus.Running
            };
            
            run.SourceDetails[loader.SourceName] = sourceDetail;
            
            try
            {
                // Check if source is available
                if (!await loader.IsAvailableAsync(cancellationToken))
                {
                    _logger.LogWarning("Source '{Source}' is not available, skipping", loader.SourceName);
                    sourceDetail.Status = IngestionStatus.Failed;
                    sourceDetail.Errors.Add("Source not available");
                    continue;
                }
                
                // Load documents from source
                _logger.LogInformation("Loading documents from '{Source}'", loader.SourceName);
                var chunks = await loader.LoadAndChunkAsync(cancellationToken);
                var chunksList = chunks.ToList();
                
                _logger.LogInformation(
                    "Loaded {Count} chunks from '{Source}'",
                    chunksList.Count, loader.SourceName);
                
                // Process chunks in batches
                const int batchSize = 50;
                var batches = chunksList
                    .Select((chunk, index) => new { chunk, index })
                    .GroupBy(x => x.index / batchSize)
                    .Select(g => g.Select(x => x.chunk).ToList())
                    .ToList();
                
                foreach (var batch in batches)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        _logger.LogWarning("Ingestion cancelled during processing of '{Source}'", loader.SourceName);
                        sourceDetail.Status = IngestionStatus.Cancelled;
                        break;
                    }
                    
                    try
                    {
                        // Generate embeddings for the batch
                        var texts = batch.Select(c => c.Content).ToList();
                        var embeddings = await _llmClient.GenerateEmbeddingsBatchAsync(texts, cancellationToken);
                        
                        // Assign embeddings to chunks
                        for (int i = 0; i < batch.Count; i++)
                        {
                            batch[i].Vector = embeddings[i];
                        }
                        
                        // Store in vector repository
                        await _vectorRepository.UpsertBatchAsync(batch, cancellationToken);
                        
                        sourceDetail.ChunksProcessed += batch.Count;
                        run.TotalChunksProcessed += batch.Count;
                        
                        _logger.LogDebug(
                            "Processed batch of {Count} chunks from '{Source}'",
                            batch.Count, loader.SourceName);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, 
                            "Error processing batch from '{Source}'",
                            loader.SourceName);
                            
                        sourceDetail.ChunksFailed += batch.Count;
                        run.TotalChunksFailed += batch.Count;
                        sourceDetail.Errors.Add($"Batch processing error: {ex.Message}");
                    }
                }
                
                sourceDetail.Status = sourceDetail.ChunksFailed == 0 
                    ? IngestionStatus.Completed 
                    : IngestionStatus.PartiallyCompleted;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing source '{Source}'", loader.SourceName);
                sourceDetail.Status = IngestionStatus.Failed;
                sourceDetail.Errors.Add(ex.Message);
                run.Errors.Add($"{loader.SourceName}: {ex.Message}");
            }
            finally
            {
                sourceDetail.EndTime = DateTime.UtcNow;
            }
        }
        
        // Determine overall status
        var allSourceStatuses = run.SourceDetails.Values.Select(d => d.Status).ToList();
        if (allSourceStatuses.All(s => s == IngestionStatus.Completed))
        {
            run.Status = IngestionStatus.Completed;
        }
        else if (allSourceStatuses.All(s => s == IngestionStatus.Failed))
        {
            run.Status = IngestionStatus.Failed;
        }
        else
        {
            run.Status = IngestionStatus.PartiallyCompleted;
        }
    }
}
```

Configuration for the timer service in `appsettings.json`:

```json
{
  "IngestionSettings": {
    "RunOnStartup": true,
    "Jobs": [
      {
        "Name": "full-ingestion",
        "DataSources": [],  // Empty means all sources
        "Interval": "02:00:00",  // Every 2 hours
        "IsEnabled": true
      },
      {
        "Name": "jira-frequent",
        "DataSources": ["Jira"],
        "Interval": "00:30:00",  // Every 30 minutes
        "IsEnabled": true
      },
      {
        "Name": "daily-confluence",
        "DataSources": ["Confluence"],
        "Interval": "1.00:00:00",  // Once per day
        "IsEnabled": true
      }
    ]
  }
}
```

And the settings model:

```csharp
// src/McpServer.Core/Settings/IngestionSettings.cs
namespace McpServer.Core.Settings;

public class IngestionSettings
{
    public bool RunOnStartup { get; set; }
    public List<IngestionJobConfig> Jobs { get; set; } = new();
}

public class IngestionJobConfig
{
    public string Name { get; set; }
    public List<string> DataSources { get; set; } = new();
    public TimeSpan Interval { get; set; }
    public bool IsEnabled { get; set; }
}
```

### Deliverables for Iteration 2

By the end of this iteration, you'll have:
1. **Complete project structure** with proper layering and dependencies
2. **Rich domain models** capturing banking-specific concepts
3. **Timer-based background service** providing flexible scheduling without heavyweight dependencies
4. **Ingestion orchestrator** managing the flow across multiple data sources
5. **Comprehensive logging** for monitoring and debugging
6. **Configuration system** allowing easy adjustment of ingestion schedules
7. **Unit tests** for critical business logic

## Iteration 3: Document Processing and Vector Storage

### Week 5-6: Implementing Banking-Aware Document Processing

This iteration focuses on creating sophisticated document loaders that understand banking document structures and implementing efficient vector storage with MongoDB.

#### Sprint 3.1 - Document Loaders Implementation (Days 1-3)

Let's implement document loaders for each data source, starting with local files:

```csharp
// src/McpServer.Infrastructure/Loaders/LocalFileLoader.cs
namespace McpServer.Infrastructure.Loaders;

/// <summary>
/// Loads documents from local file system (simulating SharePoint)
/// </summary>
public class LocalFileLoader : IDocumentLoader
{
    private readonly ILogger<LocalFileLoader> _logger;
    private readonly IConfiguration _configuration;
    private readonly IBankingDocumentParser _documentParser;
    private readonly IChunkingService _chunkingService;
    
    public string SourceName => "LocalFiles";
    
    public LocalFileLoader(
        ILogger<LocalFileLoader> logger,
        IConfiguration configuration,
        IBankingDocumentParser documentParser,
        IChunkingService chunkingService)
    {
        _logger = logger;
        _configuration = configuration;
        _documentParser = documentParser;
        _chunkingService = chunkingService;
    }
    
    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        var path = _configuration["DataSources:LocalPath"] ?? "./SharePoint_Data";
        return await Task.FromResult(Directory.Exists(path));
    }
    
    public async Task<IEnumerable<DocumentChunk>> LoadAndChunkAsync(CancellationToken cancellationToken = default)
    {
        var path = _configuration["DataSources:LocalPath"] ?? "./SharePoint_Data";
        
        if (!Directory.Exists(path))
        {
            _logger.LogWarning("Local files directory not found: {Path}", path);
            return Enumerable.Empty<DocumentChunk>();
        }
        
        var allChunks = new List<DocumentChunk>();
        var files = Directory.GetFiles(path, "*.*", SearchOption.AllDirectories)
            .Where(f => IsSupporte

dFileType(f))
            .ToList();
            
        _logger.LogInformation("Found {Count} files to process in {Path}", files.Count, path);
        
        foreach (var file in files)
        {
            if (cancellationToken.IsCancellationRequested)
                break;
                
            try
            {
                var chunks = await ProcessFileAsync(file, cancellationToken);
                allChunks.AddRange(chunks);
                
                _logger.LogDebug("Processed file '{File}' into {Count} chunks", 
                    Path.GetFileName(file), chunks.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing file: {File}", file);
            }
        }
        
        return allChunks;
    }
    
    private async Task<List<DocumentChunk>> ProcessFileAsync(string filePath, CancellationToken cancellationToken)
    {
        var fileName = Path.GetFileName(filePath);
        var fileExtension = Path.GetExtension(filePath).ToLower();
        
        string content;
        
        // Extract content based on file type
        switch (fileExtension)
        {
            case ".txt":
                content = await File.ReadAllTextAsync(filePath, cancellationToken);
                break;
                
            case ".docx":
                content = await ExtractDocxContentAsync(filePath);
                break;
                
            case ".pdf":
                content = await ExtractPdfContentAsync(filePath);
                break;
                
            default:
                _logger.LogWarning("Unsupported file type: {Extension}", fileExtension);
                return new List<DocumentChunk>();
        }
        
        // Parse the document to understand its structure
        var parseResult = _documentParser.ParseTextDocument(content, fileName);
        
        // Create chunks using our chunking service
        var chunks = _chunkingService.CreateChunks(
            parseResult,
            filePath,
            new Dictionary<string, string>
            {
                ["FileName"] = fileName,
                ["FileSize"] = new FileInfo(filePath).Length.ToString(),
                ["LastModified"] = File.GetLastWriteTimeUtc(filePath).ToString("O")
            });
            
        return chunks.ToList();
    }
    
    private async Task<string> ExtractDocxContentAsync(string filePath)
    {
        // For MVP, we'll use a simple approach
        // In production, use DocumentFormat.OpenXml
        _logger.LogWarning("DOCX extraction not implemented in MVP, treating as text");
        return await File.ReadAllTextAsync(filePath);
    }
    
    private async Task<string> ExtractPdfContentAsync(string filePath)
    {
        // For MVP, we'll skip PDF support
        // In production, use iTextSharp or similar
        _logger.LogWarning("PDF extraction not implemented in MVP");
        return string.Empty;
    }
    
    private bool IsSupportedFileType(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLower();
        return new[] { ".txt", ".docx", ".pdf" }.Contains(extension);
    }
}

// src/McpServer.Infrastructure/Loaders/ConfluenceLoader.cs
namespace McpServer.Infrastructure.Loaders;

/// <summary>
/// Loads and processes documents from Confluence
/// </summary>
public class ConfluenceLoader : IDocumentLoader
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ConfluenceLoader> _logger;
    private readonly IOptions<ConfluenceSettings> _settings;
    private readonly IBankingDocumentParser _documentParser;
    private readonly IChunkingService _chunkingService;
    
    public string SourceName => "Confluence";
    
    public ConfluenceLoader(
        HttpClient httpClient,
        ILogger<ConfluenceLoader> logger,
        IOptions<ConfluenceSettings> settings,
        IBankingDocumentParser documentParser,
        IChunkingService chunkingService)
    {
        _httpClient = httpClient;
        _logger = logger;
        _settings = settings;
        _documentParser = documentParser;
        _chunkingService = chunkingService;
        
        // Configure HTTP client
        var credentials = Convert.ToBase64String(
            Encoding.ASCII.GetBytes($"{settings.Value.Username}:{settings.Value.Password}"));
        _httpClient.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);
    }
    
    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync(
                $"{_settings.Value.BaseUrl}/rest/api/space/{_settings.Value.SpaceKey}",
                cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking Confluence availability");
            return false;
        }
    }
    
    public async Task<IEnumerable<DocumentChunk>> LoadAndChunkAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Loading documents from Confluence space: {Space}", 
            _settings.Value.SpaceKey);
            
        var allChunks = new List<DocumentChunk>();
        
        try
        {
            // Get all pages in the space
            var pages = await GetAllPagesAsync(cancellationToken);
            _logger.LogInformation("Found {Count} pages to process", pages.Count);
            
            foreach (var page in pages)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;
                    
                try
                {
                    // Get full page content
                    var fullContent = await GetPageContentAsync(page.Id, cancellationToken);
                    
                    // Parse the HTML content
                    var parseResult = _documentParser.ParseHtmlDocument(
                        fullContent.Body.Storage.Value,
                        page.Title);
                    
                    // Create chunks
                    var chunks = _chunkingService.CreateChunks(
                        parseResult,
                        $"{_settings.Value.BaseUrl}/spaces/{_settings.Value.SpaceKey}/pages/{page.Id}",
                        new Dictionary<string, string>
                        {
                            ["PageId"] = page.Id,
                            ["PageTitle"] = page.Title,
                            ["SpaceKey"] = _settings.Value.SpaceKey,
                            ["Version"] = fullContent.Version?.Number?.ToString() ?? "1",
                            ["LastModified"] = fullContent.Version?.When?.ToString("O") ?? DateTime.UtcNow.ToString("O"),
                            ["Author"] = fullContent.Version?.By?.DisplayName ?? "Unknown"
                        });
                    
                    allChunks.AddRange(chunks);
                    
                    _logger.LogDebug("Processed page '{Title}' into {Count} chunks", 
                        page.Title, chunks.Count());
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing Confluence page: {PageId}", page.Id);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading Confluence documents");
            throw;
        }
        
        return allChunks;
    }
    
    private async Task<List<ConfluencePage>> GetAllPagesAsync(CancellationToken cancellationToken)
    {
        var pages = new List<ConfluencePage>();
        var start = 0;
        const int limit = 25;
        
        while (true)
        {
            var url = $"{_settings.Value.BaseUrl}/rest/api/content?" +
                $"spaceKey={_settings.Value.SpaceKey}&" +
                $"type=page&" +
                $"status=current&" +
                $"start={start}&" +
                $"limit={limit}&" +
                $"expand=space,version,ancestors";
                
            var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();
            
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<ConfluencePageResponse>(json, 
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            
            if (result?.Results == null)
                break;
                
            pages.AddRange(result.Results);
            
            if (result.Results.Count < limit)
                break;
                
            start += limit;
        }
        
        return pages;
    }
    
    private async Task<ConfluencePage> GetPageContentAsync(string pageId, CancellationToken cancellationToken)
    {
        var url = $"{_settings.Value.BaseUrl}/rest/api/content/{pageId}?" +
            "expand=body.storage,version,space";
            
        var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<ConfluencePage>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }
}

// src/McpServer.Infrastructure/Loaders/JiraLoader.cs
namespace McpServer.Infrastructure.Loaders;

/// <summary>
/// Loads and processes issues from Jira
/// </summary>
public class JiraLoader : IDocumentLoader
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<JiraLoader> _logger;
    private readonly IOptions<JiraSettings> _settings;
    private readonly IChunkingService _chunkingService;
    
    public string SourceName => "Jira";
    
    public JiraLoader(
        HttpClient httpClient,
        ILogger<JiraLoader> logger,
        IOptions<JiraSettings> settings,
        IChunkingService chunkingService)
    {
        _httpClient = httpClient;
        _logger = logger;
        _settings = settings;
        _chunkingService = chunkingService;
        
        // Configure HTTP client
        var credentials = Convert.ToBase64String(
            Encoding.ASCII.GetBytes($"{settings.Value.Username}:{settings.Value.Password}"));
        _httpClient.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);
    }
    
    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync(
                $"{_settings.Value.BaseUrl}/rest/api/2/project/{_settings.Value.ProjectKey}",
                cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking Jira availability");
            return false;
        }
    }
    
    public async Task<IEnumerable<DocumentChunk>> LoadAndChunkAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Loading issues from Jira project: {Project}", 
            _settings.Value.ProjectKey);
            
        var allChunks = new List<DocumentChunk>();
        
        try
        {
            // Search for all issues in the project
            var issues = await SearchIssuesAsync(cancellationToken);
            _logger.LogInformation("Found {Count} issues to process", issues.Count);
            
            foreach (var issue in issues)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;
                    
                try
                {
                    // Create a consolidated document from the issue
                    var content = BuildIssueContent(issue);
                    
                    // Determine document type based on issue type and labels
                    var documentType = DetermineDocumentType(issue);
                    
                    // Create a single chunk for each issue (they're usually small)
                    var chunk = new DocumentChunk
                    {
                        Content = content,
                        Source = $"{_settings.Value.BaseUrl}/browse/{issue.Key}",
                        DocumentType = documentType,
                        Department = ExtractDepartment(issue),
                        Version = issue.Fields.Updated?.ToString("yyyyMMddHHmmss") ?? "1",
                        Metadata = new Dictionary<string, string>
                        {
                            ["IssueKey"] = issue.Key,
                            ["IssueType"] = issue.Fields.IssueType?.Name ?? "Unknown",
                            ["Status"] = issue.Fields.Status?.Name ?? "Unknown",
                            ["Priority"] = issue.Fields.Priority?.Name ?? "Medium",
                            ["Reporter"] = issue.Fields.Reporter?.DisplayName ?? "Unknown",
                            ["Created"] = issue.Fields.Created?.ToString("O") ?? DateTime.UtcNow.ToString("O"),
                            ["Updated"] = issue.Fields.Updated?.ToString("O") ?? DateTime.UtcNow.ToString("O")
                        }
                    };
                    
                    // Add labels to metadata
                    if (issue.Fields.Labels?.Any() == true)
                    {
                        chunk.Metadata["Labels"] = string.Join(", ", issue.Fields.Labels);
                    }
                    
                    allChunks.Add(chunk);
                    
                    _logger.LogDebug("Processed issue {Key}", issue.Key);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing Jira issue: {Key}", issue.Key);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading Jira issues");
            throw;
        }
        
        return allChunks;
    }
    
    private async Task<List<JiraIssue>> SearchIssuesAsync(CancellationToken cancellationToken)
    {
        var issues = new List<JiraIssue>();
        var startAt = 0;
        const int maxResults = 50;
        
        while (true)
        {
            var jql = $"project = {_settings.Value.ProjectKey} ORDER BY created DESC";
            var url = $"{_settings.Value.BaseUrl}/rest/api/2/search?" +
                $"jql={Uri.EscapeDataString(jql)}&" +
                $"startAt={startAt}&" +
                $"maxResults={maxResults}&" +
                $"fields=summary,description,issuetype,status,priority,labels,created,updated,reporter,assignee";
                
            var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();
            
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<JiraSearchResponse>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            
            if (result?.Issues == null)
                break;
                
            issues.AddRange(result.Issues);
            
            if (result.Issues.Count < maxResults)
                break;
                
            startAt += maxResults;
        }
        
        return issues;
    }
    
    private string BuildIssueContent(JiraIssue issue)
    {
        var sb = new StringBuilder();
        
        // Title
        sb.AppendLine($"# {issue.Key}: {issue.Fields.Summary}");
        sb.AppendLine();
        
        // Metadata
        sb.AppendLine("## Issue Details");
        sb.AppendLine($"- **Type**: {issue.Fields.IssueType?.Name ?? "Unknown"}");
        sb.AppendLine($"- **Status**: {issue.Fields.Status?.Name ?? "Unknown"}");
        sb.AppendLine($"- **Priority**: {issue.Fields.Priority?.Name ?? "Medium"}");
        
        if (issue.Fields.Labels?.Any() == true)
        {
            sb.AppendLine($"- **Labels**: {string.Join(", ", issue.Fields.Labels)}");
        }
        
        sb.AppendLine();
        
        // Description
        if (!string.IsNullOrWhiteSpace(issue.Fields.Description))
        {
            sb.AppendLine("## Description");
            sb.AppendLine(issue.Fields.Description);
            sb.AppendLine();
        }
        
        return sb.ToString();
    }
    
    private DocumentType DetermineDocumentType(JiraIssue issue)
    {
        // Check labels first
        if (issue.Fields.Labels != null)
        {
            var labels = issue.Fields.Labels.Select(l => l.ToLower()).ToList();
            
            if (labels.Any(l => l.Contains("policy")))
                return DocumentType.Policy;
            if (labels.Any(l => l.Contains("procedure") || l.Contains("process")))
                return DocumentType.Procedure;
            if (labels.Any(l => l.Contains("compliance") || l.Contains("regulatory")))
                return DocumentType.Compliance;
            if (labels.Any(l => l.Contains("risk")))
                return DocumentType.RiskFramework;
        }
        
        // Check issue type
        var issueType = issue.Fields.IssueType?.Name?.ToLower() ?? "";
        if (issueType.Contains("documentation") || issueType.Contains("reference"))
            return DocumentType.Reference;
            
        return DocumentType.General;
    }
    
    private string ExtractDepartment(JiraIssue issue)
    {
        // Look for department in labels
        if (issue.Fields.Labels != null)
        {
            var departmentLabels = new Dictionary<string, string>
            {
                ["risk"] = "Risk Management",
                ["compliance"] = "Compliance",
                ["operations"] = "Operations",
                ["trading"] = "Trading",
                ["technology"] = "Technology",
                ["finance"] = "Finance"
            };
            
            foreach (var label in issue.Fields.Labels)
            {
                var lowerLabel = label.ToLower();
                foreach (var (key, value) in departmentLabels)
                {
                    if (lowerLabel.Contains(key))
                        return value;
                }
            }
        }
        
        return "General";
    }
}
```

#### Sprint 3.2 - Banking Document Parser and Chunking Service (Days 4-6)

Now let's implement sophisticated parsing and chunking that understands banking document structures:

```csharp
// src/McpServer.Infrastructure/Parsers/BankingDocumentParser.cs
namespace McpServer.Infrastructure.Parsers;

/// <summary>
/// Parses documents with understanding of banking-specific structures
/// </summary>
public class BankingDocumentParser : IBankingDocumentParser
{
    private readonly ILogger<BankingDocumentParser> _logger;
    
    // Patterns for identifying document types and sections
    private readonly Dictionary<DocumentType, List<string>> _typePatterns = new()
    {
        [DocumentType.Policy] = new() { "policy", "policies", "guideline", "guidelines", "standard" },
        [DocumentType.Procedure] = new() { "procedure", "process", "workflow", "sop", "instruction" },
        [DocumentType.Reference] = new() { "reference", "data dictionary", "taxonomy", "mapping", "lookup" },
        [DocumentType.RiskFramework] = new() { "risk", "framework", "control", "limit", "threshold" },
        [DocumentType.Compliance] = new() { "compliance", "regulatory", "requirement", "regulation", "mandate" }
    };
    
    // Banking-specific section patterns
    private readonly List<string> _sectionPatterns = new()
    {
        "overview", "purpose", "scope", "definitions", "requirements",
        "procedures", "controls", "responsibilities", "approval",
        "exceptions", "references", "appendix", "revision history"
    };
    
    public BankingDocumentParser(ILogger<BankingDocumentParser> logger)
    {
        _logger = logger;
    }
    
    public DocumentParseResult ParseTextDocument(string content, string title)
    {
        var result = new DocumentParseResult
        {
            Title = title,
            DocumentType = DetermineDocumentType(title, content),
            Sections = new List<DocumentSection>()
        };
        
        // Extract metadata from content
        ExtractMetadata(content, result);
        
        // Split into sections
        var sections = SplitIntoSections(content);
        
        foreach (var section in sections)
        {
            var docSection = new DocumentSection
            {
                Title = section.Title,
                Content = section.Content,
                Level = section.Level,
                Type = DetermineSectionType(section.Title, section.Content)
            };
            
            // Extract section-specific metadata
            ExtractSectionMetadata(docSection);
            
            result.Sections.Add(docSection);
        }
        
        return result;
    }
    
    public DocumentParseResult ParseHtmlDocument(string htmlContent, string title)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(htmlContent);
        
        var result = new DocumentParseResult
        {
            Title = title,
            DocumentType = DetermineDocumentType(title, doc.DocumentNode.InnerText),
            Sections = new List<DocumentSection>()
        };
        
        // Process HTML structure
        ProcessHtmlNode(doc.DocumentNode, result, 0);
        
        return result;
    }
    
    private void ProcessHtmlNode(HtmlNode node, DocumentParseResult result, int level)
    {
        // Look for headers to create sections
        var headers = node.SelectNodes(".//h1|.//h2|.//h3|.//h4|.//h5|.//h6");
        
        if (headers == null || headers.Count == 0)
        {
            // No headers, treat content as single section
            var content = ExtractTextFromHtml(node);
            if (!string.IsNullOrWhiteSpace(content))
            {
                result.Sections.Add(new DocumentSection
                {
                    Title = "Content",
                    Content = content,
                    Level = level,
                    Type = SectionType.General
                });
            }
            return;
        }
        
        // Process content between headers
        for (int i = 0; i < headers.Count; i++)
        {
            var header = headers[i];
            var headerLevel = int.Parse(header.Name.Substring(1));
            var headerText = header.InnerText.Trim();
            
            // Get content until next header
            var contentBuilder = new StringBuilder();
            var currentNode = header.NextSibling;
            
            while (currentNode != null && 
                   (i == headers.Count - 1 || !headers.Skip(i + 1).Contains(currentNode)))
            {
                if (currentNode.NodeType == HtmlNodeType.Element)
                {
                    var text = ExtractTextFromHtml(currentNode);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        contentBuilder.AppendLine(text);
                    }
                }
                currentNode = currentNode.NextSibling;
            }
            
            var section = new DocumentSection
            {
                Title = headerText,
                Content = contentBuilder.ToString().Trim(),
                Level = headerLevel,
                Type = DetermineSectionType(headerText, contentBuilder.ToString())
            };
            
            // Look for tables with key-value data
            ExtractTabularData(header.ParentNode, section);
            
            result.Sections.Add(section);
        }
    }
    
    private void ExtractTabularData(HtmlNode parentNode, DocumentSection section)
    {
        var tables = parentNode.SelectNodes(".//table");
        if (tables == null) return;
        
        foreach (var table in tables)
        {
            var rows = table.SelectNodes(".//tr");
            if (rows == null || rows.Count < 2) continue;
            
            // Check if this looks like a key-value table
            var firstRow = rows[0];
            var cells = firstRow.SelectNodes(".//th|.//td");
            
            if (cells?.Count == 2)
            {
                // Likely a key-value table
                section.Metadata ??= new Dictionary<string, string>();
                
                foreach (var row in rows.Skip(1))
                {
                    var rowCells = row.SelectNodes(".//td");
                    if (rowCells?.Count >= 2)
                    {
                        var key = rowCells[0].InnerText.Trim();
                        var value = rowCells[1].InnerText.Trim();
                        
                        if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(value))
                        {
                            section.Metadata[key] = value;
                        }
                    }
                }
            }
        }
    }
    
    private string ExtractTextFromHtml(HtmlNode node)
    {
        // Remove script and style nodes
        node.Descendants()
            .Where(n => n.Name == "script" || n.Name == "style")
            .ToList()
            .ForEach(n => n.Remove());
            
        // Get text and clean it up
        var text = HttpUtility.HtmlDecode(node.InnerText);
        
        // Remove excessive whitespace
        text = Regex.Replace(text, @"\s+", " ");
        text = Regex.Replace(text, @"\n{3,}", "\n\n");
        
        return text.Trim();
    }
    
    private DocumentType DetermineDocumentType(string title, string content)
    {
        var lowerTitle = title.ToLower();
        var lowerContent = content.ToLower();
        var contentPreview = lowerContent.Take(1000).ToString(); // Check first 1000 chars
        
        foreach (var (docType, patterns) in _typePatterns)
        {
            if (patterns.Any(p => lowerTitle.Contains(p) || contentPreview.Contains(p)))
            {
                return docType;
            }
        }
        
        return DocumentType.General;
    }
    
    private SectionType DetermineSectionType(string title, string content)
    {
        var lowerTitle = title.ToLower();
        
        // Check for specific section types
        if (lowerTitle.Contains("definition") || lowerTitle.Contains("glossary"))
            return SectionType.Definitions;
            
        if (lowerTitle.Contains("requirement") || lowerTitle.Contains("must") || lowerTitle.Contains("shall"))
            return SectionType.Requirements;
            
        if (lowerTitle.Contains("procedure") || lowerTitle.Contains("process") || lowerTitle.Contains("step"))
            return SectionType.Procedures;
            
        if (lowerTitle.Contains("risk") || lowerTitle.Contains("control"))
            return SectionType.RiskControls;
            
        if (lowerTitle.Contains("approval") || lowerTitle.Contains("authorization"))
            return SectionType.Approvals;
            
        // Check content for clues
        if (content.Contains("threshold") || content.Contains("limit"))
            return SectionType.RiskParameters;
            
        if (Regex.IsMatch(content, @"\b\d+\.?\d*%\b")) // Contains percentages
            return SectionType.Metrics;
            
        return SectionType.General;
    }
    
    private void ExtractMetadata(string content, DocumentParseResult result)
    {
        // Look for common metadata patterns in banking documents
        
        // Effective date
        var effectiveDateMatch = Regex.Match(content, 
            @"(?:effective|valid)\s+(?:date|from|as\s+of)[:\s]+(\d{1,2}[/-]\d{1,2}[/-]\d{2,4})",
            RegexOptions.IgnoreCase);
        if (effectiveDateMatch.Success && DateTime.TryParse(effectiveDateMatch.Groups[1].Value, out var effectiveDate))
        {
            result.EffectiveDate = effectiveDate;
        }
        
        // Version
        var versionMatch = Regex.Match(content,
            @"(?:version|ver\.?|v\.?)[:\s]+(\d+\.?\d*)",
            RegexOptions.IgnoreCase);
        if (versionMatch.Success)
        {
            result.Version = versionMatch.Groups[1].Value;
        }
        
        // Department
        var deptPatterns = new Dictionary<string, string>
        {
            [@"\b(?:risk\s+management|risk\s+dept)\b"] = "Risk Management",
            [@"\b(?:compliance|regulatory)\b"] = "Compliance",
            [@"\b(?:operations|ops)\b"] = "Operations",
            [@"\b(?:trading|markets)\b"] = "Trading",
            [@"\b(?:technology|it|tech)\b"] = "Technology"
        };
        
        foreach (var (pattern, dept) in deptPatterns)
        {
            if (Regex.IsMatch(content, pattern, RegexOptions.IgnoreCase))
            {
                result.Department = dept;
                break;
            }
        }
    }
    
    private void ExtractSectionMetadata(DocumentSection section)
    {
        // Extract structured data from section content
        
        // Look for bullet points with key-value pairs
        var keyValueMatches = Regex.Matches(section.Content,
            @"^[\s•·-]*([^:]+):\s*(.+)$",
            RegexOptions.Multiline);
            
        if (keyValueMatches.Count > 0)
        {
            section.Metadata ??= new Dictionary<string, string>();
            
            foreach (Match match in keyValueMatches)
            {
                var key = match.Groups[1].Value.Trim();
                var value = match.Groups[2].Value.Trim();
                
                if (key.Length < 50 && !key.Contains('\n')) // Reasonable key length
                {
                    section.Metadata[key] = value;
                }
            }
        }
        
        // Extract numeric thresholds or limits
        if (section.Type == SectionType.RiskParameters)
        {
            var thresholdMatches = Regex.Matches(section.Content,
                @"(\w+[\s\w]*?)[\s:]+(\d+\.?\d*)\s*(%|percent|bps|basis\s+points|million|billion)",
                RegexOptions.IgnoreCase);
                
            foreach (Match match in thresholdMatches)
            {
                section.Metadata ??= new Dictionary<string, string>();
                var metric = match.Groups[1].Value.Trim();
                var value = match.Groups[2].Value;
                var unit = match.Groups[3].Value;
                
                section.Metadata[$"Threshold_{metric}"] = $"{value} {unit}";
            }
        }
    }
    
    private List<(string Title, string Content, int Level)> SplitIntoSections(string content)
    {
        var sections = new List<(string Title, string Content, int Level)>();
        
        // Split by common header patterns
        var headerPattern = @"^(#+\s*.+|[A-Z][A-Z\s]+:?\s*$|\d+\.?\s+[A-Z].+)";
        var lines = content.Split('\n');
        
        string currentTitle = "Overview";
        var currentContent = new StringBuilder();
        int currentLevel = 1;
        
        foreach (var line in lines)
        {
            if (Regex.IsMatch(line, headerPattern, RegexOptions.Multiline))
            {
                // Save previous section if it has content
                if (currentContent.Length > 0)
                {
                    sections.Add((currentTitle, currentContent.ToString().Trim(), currentLevel));
                    currentContent.Clear();
                }
                
                // Determine level based on format
                if (line.StartsWith("#"))
                {
                    currentLevel = line.TakeWhile(c => c == '#').Count();
                    currentTitle = line.TrimStart('#').Trim();
                }
                else if (Regex.IsMatch(line, @"^\d+\."))
                {
                    currentLevel = 2;
                    currentTitle = line;
                }
                else
                {
                    currentLevel = 1;
                    currentTitle = line.Trim().TrimEnd(':');
                }
            }
            else
            {
                currentContent.AppendLine(line);
            }
        }
        
        // Add final section
        if (currentContent.Length > 0)
        {
            sections.Add((currentTitle, currentContent.ToString().Trim(), currentLevel));
        }
        
        return sections;
    }
}

// src/McpServer.Infrastructure/Services/ChunkingService.cs
namespace McpServer.Infrastructure.Services;

/// <summary>
/// Service for creating optimally-sized chunks from parsed documents
/// </summary>
public class ChunkingService : IChunkingService
{
    private readonly ILogger<ChunkingService> _logger;
    private readonly IOptions<ChunkingSettings> _settings;
    
    public ChunkingService(
        ILogger<ChunkingService> logger,
        IOptions<ChunkingSettings> settings)
    {
        _logger = logger;
        _settings = settings;
    }
    
    public IEnumerable<DocumentChunk> CreateChunks(
        DocumentParseResult parseResult,
        string source,
        Dictionary<string, string> baseMetadata)
    {
        var chunks = new List<DocumentChunk>();
        var chunkIndex = 0;
        
        // Process each section
        foreach (var section in parseResult.Sections)
        {
            // Skip empty sections
            if (string.IsNullOrWhiteSpace(section.Content))
                continue;
                
            // Check if section needs to be split
            if (section.Content.Length <= _settings.Value.MaxChunkSize)
            {
                // Section fits in one chunk
                var chunk = CreateChunk(
                    section.Content,
                    source,
                    parseResult,
                    section,
                    baseMetadata,
                    chunkIndex++);
                    
                chunks.Add(chunk);
            }
            else
            {
                // Split section into multiple chunks
                var sectionChunks = SplitLargeSection(section, _settings.Value.MaxChunkSize);
                
                foreach (var (content, isComplete) in sectionChunks)
                {
                    var chunk = CreateChunk(
                        content,
                        source,
                        parseResult,
                        section,
                        baseMetadata,
                        chunkIndex++);
                        
                    // Mark if this is a partial section
                    if (!isComplete)
                    {
                        chunk.Metadata["IsPartialSection"] = "true";
                    }
                    
                    chunks.Add(chunk);
                }
            }
        }
        
        // Update total chunks count
        foreach (var chunk in chunks)
        {
            chunk.TotalChunks = chunks.Count;
        }
        
        _logger.LogDebug(
            "Created {Count} chunks from document '{Title}'",
            chunks.Count, parseResult.Title);
            
        return chunks;
    }
    
    private DocumentChunk CreateChunk(
        string content,
        string source,
        DocumentParseResult parseResult,
        DocumentSection section,
        Dictionary<string, string> baseMetadata,
        int chunkIndex)
    {
        var chunk = new DocumentChunk
        {
            Content = content,
            Source = source,
            DocumentType = parseResult.DocumentType,
            Department = parseResult.Department ?? "General",
            EffectiveDate = parseResult.EffectiveDate,
            Version = parseResult.Version ?? "1.0",
            ChunkIndex = chunkIndex,
            Metadata = new Dictionary<string, string>(baseMetadata)
        };
        
        // Add document metadata
        chunk.Metadata["DocumentTitle"] = parseResult.Title;
        chunk.Metadata["SectionTitle"] = section.Title;
        chunk.Metadata["SectionType"] = section.Type.ToString();
        chunk.Metadata["SectionLevel"] = section.Level.ToString();
        
        // Add section-specific metadata
        if (section.Metadata != null)
        {
            foreach (var (key, value) in section.Metadata)
            {
                chunk.Metadata[$"Section_{key}"] = value;
            }
        }
        
        return chunk;
    }
    
    private List<(string Content, bool IsComplete)> SplitLargeSection(
        DocumentSection section,
        int maxChunkSize)
    {
        var chunks = new List<(string Content, bool IsComplete)>();
        
        // Try to split on natural boundaries first
        var sentences = SplitIntoSentences(section.Content);
        var currentChunk = new StringBuilder();
        var currentSize = 0;
        
        // Add section context to each chunk
        var sectionContext = $"[Section: {section.Title}]\n\n";
        
        foreach (var sentence in sentences)
        {
            // Check if adding this sentence would exceed the limit
            if (currentSize + sentence.Length > maxChunkSize - sectionContext.Length)
            {
                // Save current chunk if it has content
                if (currentChunk.Length > 0)
                {
                    chunks.Add((sectionContext + currentChunk.ToString().Trim(), false));
                    currentChunk.Clear();
                    currentSize = 0;
                }
            }
            
            currentChunk.AppendLine(sentence);
            currentSize += sentence.Length + 1;
        }
        
        // Add remaining content
        if (currentChunk.Length > 0)
        {
            chunks.Add((sectionContext + currentChunk.ToString().Trim(), true));
        }
        
        // Ensure overlap for context continuity
        if (_settings.Value.OverlapSize > 0 && chunks.Count > 1)
        {
            ApplyOverlap(chunks, sentences, _settings.Value.OverlapSize);
        }
        
        return chunks;
    }
    
    private List<string> SplitIntoSentences(string text)
    {
        // Simple sentence splitter - in production, use a proper NLP library
        var sentences = new List<string>();
        var pattern = @"(?<=[.!?])\s+(?=[A-Z])";
        
        var parts = Regex.Split(text, pattern);
        
        foreach (var part in parts)
        {
            if (!string.IsNullOrWhiteSpace(part))
            {
                sentences.Add(part.Trim());
            }
        }
        
        return sentences;
    }
    
    private void ApplyOverlap(
        List<(string Content, bool IsComplete)> chunks,
        List<string> sentences,
        int overlapSize)
    {
        // This is a simplified overlap implementation
        // In production, implement more sophisticated overlap logic
        _logger.LogDebug("Overlap application not fully implemented in MVP");
    }
}

// Configuration classes
public class ChunkingSettings
{
    public int MaxChunkSize { get; set; } = 1000;
    public int OverlapSize { get; set; } = 100;
    public bool PreserveSectionBoundaries { get; set; } = true;
}
```

#### MongoDB Vector Repository Implementation

Finally, let's implement the vector storage repository:

```csharp
// src/McpServer.Infrastructure/Repositories/MongoVectorRepository.cs
namespace McpServer.Infrastructure.Repositories;

/// <summary>
/// MongoDB implementation of vector storage with similarity search
/// </summary>
public class MongoVectorRepository : IVectorRepository
{
    private readonly IMongoCollection<DocumentChunk> _chunks;
    private readonly ILogger<MongoVectorRepository> _logger;
    private readonly IOptions<MongoSettings> _settings;
    
    public MongoVectorRepository(
        IMongoDatabase database,
        ILogger<MongoVectorRepository> logger,
        IOptions<MongoSettings> settings)
    {
        _chunks = database.GetCollection<DocumentChunk>("document_chunks");
        _logger = logger;
        _settings = settings;
        
        // Ensure indexes on startup
        Task.Run(async () => await EnsureIndexesAsync());
    }
    
    private async Task EnsureIndexesAsync()
    {
        try
        {
            // Create compound index for efficient filtering
            var filterIndex = Builders<DocumentChunk>.IndexKeys
                .Ascending(c => c.DocumentType)
                .Ascending(c => c.Department)
                .Descending(c => c.IndexedOn);
                
            await _chunks.Indexes.CreateOneAsync(
                new CreateIndexModel<DocumentChunk>(filterIndex));
            
            // Create text index for fallback search
            var textIndex = Builders<DocumentChunk>.IndexKeys
                .Text(c => c.Content);
                
            await _chunks.Indexes.CreateOneAsync(
                new CreateIndexModel<DocumentChunk>(textIndex));
            
            // Create unique compound index to prevent duplicates
            var uniqueIndex = Builders<DocumentChunk>.IndexKeys
                .Ascending(c => c.Source)
                .Ascending(c => c.ChunkIndex);
                
            await _chunks.Indexes.CreateOneAsync(
                new CreateIndexModel<DocumentChunk>(uniqueIndex,
                    new CreateIndexOptions { Unique = true }));
                    
            _logger.LogInformation("MongoDB indexes created successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating MongoDB indexes");
        }
    }
    
    public async Task UpsertAsync(DocumentChunk chunk, CancellationToken cancellationToken = default)
    {
        chunk.IndexedOn = DateTime.UtcNow;
        
        var filter = Builders<DocumentChunk>.Filter.And(
            Builders<DocumentChunk>.Filter.Eq(c => c.Source, chunk.Source),
            Builders<DocumentChunk>.Filter.Eq(c => c.ChunkIndex, chunk.ChunkIndex)
        );
        
        var options = new ReplaceOptions { IsUpsert = true };
        
        await _chunks.ReplaceOneAsync(filter, chunk, options, cancellationToken);
        
        _logger.LogDebug("Upserted chunk from source: {Source}, index: {Index}", 
            chunk.Source, chunk.ChunkIndex);
    }
    
    public async Task UpsertBatchAsync(
        IEnumerable<DocumentChunk> chunks, 
        CancellationToken cancellationToken = default)
    {
        var chunksList = chunks.ToList();
        if (!chunksList.Any())
            return;
            
        var bulkOps = new List<WriteModel<DocumentChunk>>();
        var now = DateTime.UtcNow;
        
        foreach (var chunk in chunksList)
        {
            chunk.IndexedOn = now;
            
            var filter = Builders<DocumentChunk>.Filter.And(
                Builders<DocumentChunk>.Filter.Eq(c => c.Source, chunk.Source),
                Builders<DocumentChunk>.Filter.Eq(c => c.ChunkIndex, chunk.ChunkIndex)
            );
            
            bulkOps.Add(new ReplaceOneModel<DocumentChunk>(filter, chunk) 
            { 
                IsUpsert = true 
            });
        }
        
        var result = await _chunks.BulkWriteAsync(bulkOps, cancellationToken: cancellationToken);
        
        _logger.LogInformation(
            "Bulk upserted {Count} chunks. Inserted: {Inserted}, Modified: {Modified}",
            chunksList.Count, result.InsertedCount, result.ModifiedCount);
    }
    
    public async Task<List<ScoredDocumentChunk>> SearchAsync(
        float[] queryVector,
        int maxResults = 5,
        float minScore = 0.7f,
        Dictionary<string, string> filters = null,
        CancellationToken cancellationToken = default)
    {
        // For the MVP, we'll implement a simplified similarity search
        // In production, use MongoDB Atlas Vector Search or a dedicated vector DB
        
        var filterBuilder = Builders<DocumentChunk>.Filter;
        var filter = filterBuilder.Empty;
        
        // Apply filters if provided
        if (filters != null)
        {
            foreach (var (key, value) in filters)
            {
                switch (key.ToLower())
                {
                    case "documenttype":
                        if (Enum.TryParse<DocumentType>(value, out var docType))
                        {
                            filter &= filterBuilder.Eq(c => c.DocumentType, docType);
                        }
                        break;
                        
                    case "department":
                        filter &= filterBuilder.Eq(c => c.Department, value);
                        break;
                        
                    case "afterdate":
                        if (DateTime.TryParse(value, out var afterDate))
                        {
                            filter &= filterBuilder.Gte(c => c.IndexedOn, afterDate);
                        }
                        break;
                }
            }
        }
        
        // For MVP, fetch candidates and compute similarity in memory
        // This is not scalable but works for small datasets
        var candidates = await _chunks
            .Find(filter)
            .Limit(maxResults * 10) // Over-fetch for better results
            .ToListAsync(cancellationToken);
            
        if (!candidates.Any())
        {
            _logger.LogDebug("No candidates found for similarity search");
            return new List<ScoredDocumentChunk>();
        }
        
        // Compute cosine similarity for each candidate
        var scoredChunks = new List<ScoredDocumentChunk>();
        
        foreach (var candidate in candidates)
        {
            if (candidate.Vector == null || candidate.Vector.Length != queryVector.Length)
                continue;
                
            var similarity = ComputeCosineSimilarity(queryVector, candidate.Vector);
            
            if (similarity >= minScore)
            {
                var scoredChunk = new ScoredDocumentChunk
                {
                    Id = candidate.Id,
                    Content = candidate.Content,
                    Source = candidate.Source,
                    DocumentType = candidate.DocumentType,
                    Department = candidate.Department,
                    EffectiveDate = candidate.EffectiveDate,
                    Version = candidate.Version,
                    IndexedOn = candidate.IndexedOn,
                    Vector = candidate.Vector,
                    Metadata = candidate.Metadata,
                    ChunkIndex = candidate.ChunkIndex,
                    TotalChunks = candidate.TotalChunks,
                    Score = similarity
                };
                
                scoredChunks.Add(scoredChunk);
            }
        }
        
        // Sort by score and return top results
        var results = scoredChunks
            .OrderByDescending(c => c.Score)
            .Take(maxResults)
            .ToList();
            
        _logger.LogDebug(
            "Vector search returned {Count} results above threshold {Threshold}",
            results.Count, minScore);
            
        return results;
    }
    
    public async Task DeleteBySourceAsync(string source, CancellationToken cancellationToken = default)
    {
        var result = await _chunks.DeleteManyAsync(
            c => c.Source == source,
            cancellationToken);
            
        _logger.LogInformation(
            "Deleted {Count} chunks from source: {Source}",
            result.DeletedCount, source);
    }
    
    private float ComputeCosineSimilarity(float[] vectorA, float[] vectorB)
    {
        if (vectorA.Length != vectorB.Length)
            throw new ArgumentException("Vectors must have the same dimension");
            
        float dotProduct = 0;
        float magnitudeA = 0;
        float magnitudeB = 0;
        
        for (int i = 0; i < vectorA.Length; i++)
        {
            dotProduct += vectorA[i] * vectorB[i];
            magnitudeA += vectorA[i] * vectorA[i];
            magnitudeB += vectorB[i] * vectorB[i];
        }
        
        magnitudeA = (float)Math.Sqrt(magnitudeA);
        magnitudeB = (float)Math.Sqrt(magnitudeB);
        
        if (magnitudeA == 0 || magnitudeB == 0)
            return 0;
            
        return dotProduct / (magnitudeA * magnitudeB);
    }
}
```

### Deliverables for Iteration 3

By the end of this iteration, you'll have:
1. **Complete document loaders** for all three data sources (Local files, Confluence, Jira)
2. **Banking-aware document parser** that understands financial document structures
3. **Intelligent chunking service** that preserves context and handles large documents
4. **MongoDB vector repository** with similarity search capabilities
5. **Comprehensive metadata extraction** for better search filtering
6. **Error handling and logging** throughout the document processing pipeline
7. **Integration tests** for the complete ingestion flow

## Iteration 4: API Development and RAG Pipeline

### Week 7-8: Building the MCP API and RAG Orchestration

This iteration focuses on creating the API layer and implementing the complete RAG pipeline that brings everything together.

#### Sprint 4.1 - RAG Orchestration and API Controllers (Days 1-3)

Let's start by implementing the RAG orchestrator that coordinates the retrieval and generation process:

```csharp
// src/McpServer.Application/Services/RagOrchestrator.cs
namespace McpServer.Application.Services;

/// <summary>
/// Orchestrates the complete RAG pipeline from query to response
/// </summary>
public class RagOrchestrator : IRagOrchestrator
{
    private readonly ILlmClient _llmClient;
    private readonly IVectorRepository _vectorRepository;
    private readonly IQueryEnhancer _queryEnhancer;
    private readonly IResponseGenerator _responseGenerator;
    private readonly ILogger<RagOrchestrator> _logger;
    private readonly IOptions<RagSettings> _settings;
    
    public RagOrchestrator(
        ILlmClient llmClient,
        IVectorRepository vectorRepository,
        IQueryEnhancer queryEnhancer,
        IResponseGenerator responseGenerator,
        ILogger<RagOrchestrator> logger,
        IOptions<RagSettings> settings)
    {
        _llmClient = llmClient;
        _vectorRepository = vectorRepository;
        _queryEnhancer = queryEnhancer;
        _responseGenerator = responseGenerator;
        _logger = logger;
        _settings = settings;
    }
    
    public async Task<RagResponse> ProcessQueryAsync(
        string query,
        Dictionary<string, string> context = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            _logger.LogInformation("Processing RAG query: {Query}", query);
            
            // Step 1: Enhance the query for better retrieval
            var enhancedQuery = await _queryEnhancer.EnhanceQueryAsync(query, context);
            _logger.LogDebug("Enhanced query: {Enhanced}", enhancedQuery);
            
            // Step 2: Generate embedding for the enhanced query
            var queryEmbedding = await _llmClient.GenerateEmbeddingAsync(
                enhancedQuery, 
                cancellationToken);
            
            // Step 3: Retrieve relevant documents
            var retrievedChunks = await _vectorRepository.SearchAsync(
                queryEmbedding,
                maxResults: _settings.Value.MaxRetrievedDocuments,
                minScore: _settings.Value.MinSimilarityScore,
                filters: ExtractFilters(context),
                cancellationToken: cancellationToken);
                
            _logger.LogInformation(
                "Retrieved {Count} relevant chunks for query",
                retrievedChunks.Count);
            
            // Step 4: Rerank results if needed
            if (_settings.Value.EnableReranking && retrievedChunks.Count > 0)
            {
                retrievedChunks = await RerankResultsAsync(
                    query, 
                    retrievedChunks, 
                    cancellationToken);
            }
            
            // Step 5: Generate response using retrieved context
            var response = await _responseGenerator.GenerateResponseAsync(
                query,
                retrievedChunks,
                context,
                cancellationToken);
                
            stopwatch.Stop();
            
            return new RagResponse
            {
                Query = query,
                EnhancedQuery = enhancedQuery,
                Response = response.GeneratedText,
                Sources = retrievedChunks.Select(c => new SourceReference
                {
                    Content = c.Content,
                    Source = c.Source,
                    Score = c.Score,
                    Metadata = c.Metadata
                }).ToList(),
                ProcessingTimeMs = stopwatch.ElapsedMilliseconds,
                Timestamp = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing RAG query");
            throw new RagProcessingException("Failed to process query", ex);
        }
    }
    
    private Dictionary<string, string> ExtractFilters(Dictionary<string, string> context)
    {
        if (context == null)
            return null;
            
        var filters = new Dictionary<string, string>();
        
        // Extract known filter keys from context
        if (context.TryGetValue("documentType", out var docType))
            filters["documentType"] = docType;
            
        if (context.TryGetValue("department", out var dept))
            filters["department"] = dept;
            
        if (context.TryGetValue("afterDate", out var date))
            filters["afterDate"] = date;
            
        return filters.Any() ? filters : null;
    }
    
    private async Task<List<ScoredDocumentChunk>> RerankResultsAsync(
        string query,
        List<ScoredDocumentChunk> chunks,
        CancellationToken cancellationToken)
    {
        // Simple reranking based on keyword matching
        // In production, use a more sophisticated reranking model
        
        var queryKeywords = ExtractKeywords(query.ToLower());
        
        foreach (var chunk in chunks)
        {
            var contentLower = chunk.Content.ToLower();
            var keywordScore = 0f;
            
            foreach (var keyword in queryKeywords)
            {
                if (contentLower.Contains(keyword))
                {
                    keywordScore += 0.1f;
                }
            }
            
            // Boost score based on metadata relevance
            if (chunk.Metadata.ContainsKey("DocumentTitle") && 
                chunk.Metadata["DocumentTitle"].ToLower().Contains(query.ToLower()))
            {
                keywordScore += 0.2f;
            }
            
            // Combine vector similarity with keyword score
            chunk.Score = (chunk.Score * 0.7f) + (keywordScore * 0.3f);
        }
        
        return chunks.OrderByDescending(c => c.Score).ToList();
    }
    
    private List<string> ExtractKeywords(string text)
    {
        // Simple keyword extraction - in production use NLP
        var stopWords = new HashSet<string> 
        { 
            "the", "is", "at", "which", "on", "a", "an", "and", 
            "or", "but", "in", "with", "to", "for", "of", "as", "by"
        };
        
        return text.Split(' ')
            .Where(w => w.Length > 2 && !stopWords.Contains(w))
            .Distinct()
            .ToList();
    }
}

// src/McpServer.Application/Services/QueryEnhancer.cs
namespace McpServer.Application.Services;

/// <summary>
/// Enhances user queries for better retrieval
/// </summary>
public class QueryEnhancer : IQueryEnhancer
{
    private readonly ILogger<QueryEnhancer> _logger;
    
    // Banking-specific synonyms and expansions
    private readonly Dictionary<string, List<string>> _synonyms = new()
    {
        ["kyc"] = new() { "know your customer", "customer due diligence", "cdd" },
        ["aml"] = new() { "anti money laundering", "money laundering" },
        ["settlement"] = new() { "clearing", "post trade", "fails" },
        ["counterparty"] = new() { "client", "customer", "cp" },
        ["limit"] = new() { "threshold", "maximum", "cap" },
        ["policy"] = new() { "guideline", "procedure", "standard" }
    };
    
    public QueryEnhancer(ILogger<QueryEnhancer> logger)
    {
        _logger = logger;
    }
    
    public Task<string> EnhanceQueryAsync(
        string originalQuery, 
        Dictionary<string, string> context = null)
    {
        var enhanced = originalQuery;
        
        // Expand acronyms and add synonyms
        foreach (var (term, synonyms) in _synonyms)
        {
            if (originalQuery.ToLower().Contains(term))
            {
                // Add synonyms in parentheses for broader matching
                var synonymString = string.Join(" OR ", synonyms);
                enhanced = enhanced.Replace(
                    term, 
                    $"{term} ({synonymString})", 
                    StringComparison.OrdinalIgnoreCase);
            }
        }
        
        // Add context if provided
        if (context?.ContainsKey("previousQuery") == true)
        {
            // Reference previous context for continuity
            enhanced = $"{enhanced} [Context: {context["previousQuery"]}]";
        }
        
        _logger.LogDebug("Enhanced query from '{Original}' to '{Enhanced}'", 
            originalQuery, enhanced);
            
        return Task.FromResult(enhanced);
    }
}

// src/McpServer.Application/Services/ResponseGenerator.cs
namespace McpServer.Application.Services;

/// <summary>
/// Generates responses using LLM with retrieved context
/// </summary>
public class ResponseGenerator : IResponseGenerator
{
    private readonly ILlmClient _llmClient;
    private readonly ILogger<ResponseGenerator> _logger;
    private readonly IOptions<ResponseGenerationSettings> _settings;
    
    public ResponseGenerator(
        ILlmClient llmClient,
        ILogger<ResponseGenerator> logger,
        IOptions<ResponseGenerationSettings> settings)
    {
        _llmClient = llmClient;
        _logger = logger;
        _settings = settings;
    }
    
    public async Task<GeneratedResponse> GenerateResponseAsync(
        string query,
        List<ScoredDocumentChunk> retrievedChunks,
        Dictionary<string, string> context,
        CancellationToken cancellationToken)
    {
        // Build the prompt with retrieved context
        var prompt = BuildPrompt(query, retrievedChunks, context);
        
        // Create RAG context
        var ragContext = new RagContext
        {
            Query = query,
            RelevantChunks = retrievedChunks.Cast<DocumentChunk>().ToList(),
            SystemPrompt = _settings.Value.SystemPrompt,
            QueryMetadata = context ?? new Dictionary<string, string>()
        };
        
        // Generate response
        var response = await _llmClient.GenerateResponseAsync(ragContext, cancellationToken);
        
        // Post-process the response
        response = PostProcessResponse(response, retrievedChunks);
        
        return new GeneratedResponse
        {
            GeneratedText = response,
            ChunksUsed = retrievedChunks.Count,
            Model = "banking-assistant"
        };
    }
    
    private string BuildPrompt(
        string query,
        List<ScoredDocumentChunk> chunks,
        Dictionary<string, string> context)
    {
        var promptBuilder = new StringBuilder();
        
        // Add context documents
        promptBuilder.AppendLine("Based on the following banking reference documents:");
        promptBuilder.AppendLine();
        
        foreach (var chunk in chunks.Take(_settings.Value.MaxContextChunks))
        {
            promptBuilder.AppendLine($"Document: {chunk.Metadata.GetValueOrDefault("DocumentTitle", "Unknown")}");
            promptBuilder.AppendLine($"Section: {chunk.Metadata.GetValueOrDefault("SectionTitle", "Content")}");
            promptBuilder.AppendLine($"Content: {chunk.Content}");
            promptBuilder.AppendLine($"Source: {chunk.Source}");
            promptBuilder.AppendLine("---");
        }
        
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("User Question:");
        promptBuilder.AppendLine(query);
        
        // Add specific instructions
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("Instructions:");
        promptBuilder.AppendLine("- Provide a clear, accurate answer based on the documents above");
        promptBuilder.AppendLine("- Cite specific sources when making claims");
        promptBuilder.AppendLine("- If the documents don't contain the answer, say so");
        promptBuilder.AppendLine("- Use proper banking terminology");
        promptBuilder.AppendLine("- Highlight any compliance or risk considerations");
        
        return promptBuilder.ToString();
    }
    
    private string PostProcessResponse(string response, List<ScoredDocumentChunk> chunks)
    {
        // Ensure sources are properly cited
        if (!response.Contains("Source:") && chunks.Any())
        {
            response += "\n\nSources:\n";
            foreach (var chunk in chunks.Take(3))
            {
                var title = chunk.Metadata.GetValueOrDefault("DocumentTitle", "Unknown Document");
                response += $"- {title} ({chunk.Source})\n";
            }
        }
        
        return response;
    }
}
```

Now let's create the API controllers:

```csharp
// src/McpServer.Api/Controllers/McpController.cs
namespace McpServer.Api.Controllers;

/// <summary>
/// Main MCP API controller for chat interactions
/// </summary>
[ApiController]
[Route("api/mcp")]
public class McpController : ControllerBase
{
    private readonly IRagOrchestrator _ragOrchestrator;
    private readonly IQueryLogger _queryLogger;
    private readonly ILogger<McpController> _logger;
    
    public McpController(
        IRagOrchestrator ragOrchestrator,
        IQueryLogger queryLogger,
        ILogger<McpController> logger)
    {
        _ragOrchestrator = ragOrchestrator;
        _queryLogger = queryLogger;
        _logger = logger;
    }
    
    /// <summary>
    /// Process a chat message using RAG
    /// </summary>
    [HttpPost("chat")]
    [ProducesResponseType(typeof(McpChatResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Chat([FromBody] McpChatRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }
        
        try
        {
            // Log the query
            await _queryLogger.LogQueryAsync(
                request.Message,
                request.UserId,
                request.SessionId);
            
            // Build context from request
            var context = new Dictionary<string, string>
            {
                ["userId"] = request.UserId ?? "anonymous",
                ["sessionId"] = request.SessionId ?? Guid.NewGuid().ToString()
            };
            
            if (!string.IsNullOrEmpty(request.DocumentType))
                context["documentType"] = request.DocumentType;
                
            if (!string.IsNullOrEmpty(request.Department))
                context["department"] = request.Department;
            
            // Process the query
            var ragResponse = await _ragOrchestrator.ProcessQueryAsync(
                request.Message,
                context);
            
            // Build response
            var response = new McpChatResponse
            {
                Message = ragResponse.Response,
                Sources = ragResponse.Sources.Select(s => new SourceReferenceDto
                {
                    Title = s.Metadata.GetValueOrDefault("DocumentTitle", "Unknown"),
                    Section = s.Metadata.GetValueOrDefault("SectionTitle", ""),
                    Url = s.Source,
                    Score = s.Score,
                    DocumentType = s.Metadata.GetValueOrDefault("DocumentType", "General")
                }).ToList(),
                Metadata = new ChatMetadata
                {
                    ProcessingTimeMs = ragResponse.ProcessingTimeMs,
                    ChunksRetrieved = ragResponse.Sources.Count,
                    Model = "banking-assistant",
                    Timestamp = ragResponse.Timestamp
                }
            };
            
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing chat request");
            
            return StatusCode(500, new ProblemDetails
            {
                Title = "An error occurred processing your request",
                Detail = "Please try again later or contact support if the problem persists",
                Status = 500,
                Instance = HttpContext.Request.Path
            });
        }
    }
    
    /// <summary>
    /// Get chat history for a session
    /// </summary>
    [HttpGet("history/{sessionId}")]
    [ProducesResponseType(typeof(List<ChatHistoryItem>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHistory(string sessionId)
    {
        var history = await _queryLogger.GetSessionHistoryAsync(sessionId);
        
        return Ok(history.Select(h => new ChatHistoryItem
        {
            Query = h.Query,
            Response = h.Response,
            Timestamp = h.Timestamp,
            Sources = h.Sources
        }));
    }
}

// src/McpServer.Api/Controllers/IngestionController.cs
namespace McpServer.Api.Controllers;

/// <summary>
/// API for monitoring and managing data ingestion
/// </summary>
[ApiController]
[Route("api/ingestion")]
public class IngestionController : ControllerBase
{
    private readonly IngestionTimerService _timerService;
    private readonly IIngestionTracker _ingestionTracker;
    private readonly ILogger<IngestionController> _logger;
    
    public IngestionController(
        IngestionTimerService timerService,
        IIngestionTracker ingestionTracker,
        ILogger<IngestionController> logger)
    {
        _timerService = timerService;
        _ingestionTracker = ingestionTracker;
        _logger = logger;
    }
    
    /// <summary>
    /// Get status of all ingestion timers
    /// </summary>
    [HttpGet("timers")]
    [ProducesResponseType(typeof(List<TimerStatusDto>), StatusCodes.Status200OK)]
    public IActionResult GetTimerStatuses()
    {
        var statuses = _timerService.GetTimerStatuses();
        
        return Ok(statuses.Select(s => new TimerStatusDto
        {
            JobName = s.JobName,
            Interval = s.Interval.ToString(),
            IsEnabled = s.IsEnabled,
            NextRunTime = s.NextRunTime
        }));
    }
    
    /// <summary>
    /// Manually trigger an ingestion job
    /// </summary>
    [HttpPost("trigger/{jobName}")]
    [ProducesResponseType(typeof(IngestionRunDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> TriggerIngestion(string jobName)
    {
        try
        {
            var run = await _timerService.TriggerIngestionAsync(jobName);
            
            if (run == null)
            {
                return Conflict(new ProblemDetails
                {
                    Title = "Ingestion already running",
                    Detail = "Another ingestion job is currently in progress",
                    Status = 409
                });
            }
            
            return Accepted(new IngestionRunDto
            {
                Id = run.Id,
                JobName = run.JobName,
                StartTime = run.StartTime,
                Status = run.Status.ToString()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error triggering ingestion for job: {JobName}", jobName);
            return StatusCode(500, new ProblemDetails
            {
                Title = "Failed to trigger ingestion",
                Status = 500
            });
        }
    }
    
    /// <summary>
    /// Get ingestion history
    /// </summary>
    [HttpGet("history")]
    [ProducesResponseType(typeof(PagedResult<IngestionRunDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetIngestionHistory(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var history = await _ingestionTracker.GetHistoryAsync(page, pageSize);
        
        return Ok(new PagedResult<IngestionRunDto>
        {
            Items = history.Items.Select(h => new IngestionRunDto
            {
                Id = h.Id,
                JobName = h.JobName,
                StartTime = h.StartTime,
                EndTime = h.EndTime,
                Status = h.Status.ToString(),
                TotalChunksProcessed = h.TotalChunksProcessed,
                TotalChunksFailed = h.TotalChunksFailed,
                Duration = h.Duration?.ToString(),
                Sources = h.SourceDetails.Select(s => new SourceIngestionDto
                {
                    SourceName = s.Value.SourceName,
                    ChunksProcessed = s.Value.ChunksProcessed,
                    ChunksFailed = s.Value.ChunksFailed,
                    Status = s.Value.Status.ToString()
                }).ToList()
            }).ToList(),
            TotalCount = history.TotalCount,
            Page = page,
            PageSize = pageSize
        });
    }
}

// DTOs for API
namespace McpServer.Api.Models;

public class McpChatRequest
{
    [Required]
    [MinLength(1)]
    public string Message { get; set; }
    
    public string UserId { get; set; }
    public string SessionId { get; set; }
    public string DocumentType { get; set; }
    public string Department { get; set; }
}

public class McpChatResponse
{
    public string Message { get; set; }
    public List<SourceReferenceDto> Sources { get; set; }
    public ChatMetadata Metadata { get; set; }
}

public class SourceReferenceDto
{
    public string Title { get; set; }
    public string Section { get; set; }
    public string Url { get; set; }
    public float Score { get; set; }
    public string DocumentType { get; set; }
}

public class ChatMetadata
{
    public long ProcessingTimeMs { get; set; }
    public int ChunksRetrieved { get; set; }
    public string Model { get; set; }
    public DateTime Timestamp { get; set; }
}
```

#### Sprint 4.2 - API Configuration and Middleware (Days 4-6)

Now let's configure the API with proper middleware, error handling, and OpenAPI documentation:

```csharp
// src/McpServer.Api/Program.cs
using McpServer.Api.Configuration;
using McpServer.Api.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Configure Swagger/OpenAPI
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "MCP-RAG Banking Reference Data API",
        Version = "v1",
        Description = "API for querying banking reference data using RAG",
        Contact = new OpenApiContact
        {
            Name = "Banking Reference Data Team",
            Email = "refdata@bank.com"
        }
    });
    
    // Include XML comments
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    options.IncludeXmlComments(xmlPath);
});

// Configure CORS for Blazor client
builder.Services.AddCors(options =>
{
    options.AddPolicy("BlazorClient", policy =>
    {
        policy.WithOrigins(builder.Configuration["ClientUrl"] ?? "http://localhost:5000")
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});

// Configure application services
builder.Services.ConfigureMongoDb(builder.Configuration);
builder.Services.ConfigureOllama(builder.Configuration);
builder.Services.ConfigureIngestion(builder.Configuration);
builder.Services.ConfigureRag(builder.Configuration);

// Add health checks
builder.Services.AddHealthChecks()
    .AddMongoDb(
        mongodbConnectionString: builder.Configuration.GetConnectionString("MongoDB"),
        name: "mongodb")
    .AddUrlGroup(
        uri: new Uri($"{builder.Configuration["Ollama:BaseUrl"]}/api/tags"),
        name: "ollama")
    .AddUrlGroup(
        uri: new Uri(builder.Configuration["Confluence:BaseUrl"]),
        name: "confluence")
    .AddUrlGroup(
        uri: new Uri(builder.Configuration["Jira:BaseUrl"]),
        name: "jira");

// Add response compression
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
});

// Add API versioning
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
});

var app = builder.Build();

// Configure middleware pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "MCP-RAG API v1");
        options.DisplayRequestDuration();
    });
}

app.UseResponseCompression();
app.UseHttpsRedirection();
app.UseCors("BlazorClient");

// Custom middleware
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseMiddleware<ErrorHandlingMiddleware>();
app.UseMiddleware<RateLimitingMiddleware>();

app.UseRouting();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

// Ensure database indexes on startup
using (var scope = app.Services.CreateScope())
{
    var mongoConfig = scope.ServiceProvider.GetRequiredService<IMongoDbInitializer>();
    await mongoConfig.InitializeAsync();
}

app.Run();

// src/McpServer.Api/Configuration/ServiceConfiguration.cs
namespace McpServer.Api.Configuration;

public static class ServiceConfiguration
{
    public static IServiceCollection ConfigureMongoDb(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Configure MongoDB
        services.Configure<MongoSettings>(configuration.GetSection("MongoDB"));
        
        services.AddSingleton<IMongoClient>(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<MongoSettings>>().Value;
            return new MongoClient(settings.ConnectionString);
        });
        
        services.AddScoped<IMongoDatabase>(sp =>
        {
            var client = sp.GetRequiredService<IMongoClient>();
            var settings = sp.GetRequiredService<IOptions<MongoSettings>>().Value;
            return client.GetDatabase(settings.DatabaseName);
        });
        
        // Register repositories
        services.AddScoped<IVectorRepository, MongoVectorRepository>();
        services.AddScoped<IIngestionTracker, MongoIngestionTracker>();
        
        return services;
    }
    
    public static IServiceCollection ConfigureOllama(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Configure Ollama HTTP client
        services.AddHttpClient<ILlmClient, OllamaLlmClient>(client =>
        {
            client.BaseAddress = new Uri(configuration["Ollama:BaseUrl"]);
            client.Timeout = TimeSpan.FromMinutes(5); // LLM calls can be slow
        })
        .AddPolicyHandler(GetRetryPolicy())
        .AddPolicyHandler(GetCircuitBreakerPolicy());
        
        return services;
    }
    
    public static IServiceCollection ConfigureIngestion(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Configure ingestion settings
        services.Configure<IngestionSettings>(configuration.GetSection("IngestionSettings"));
        
        // Register document loaders
        services.AddScoped<IDocumentLoader, LocalFileLoader>();
        
        // Configure HTTP clients for external sources
        services.AddHttpClient<IDocumentLoader, ConfluenceLoader>(client =>
        {
            var settings = configuration.GetSection("Confluence").Get<ConfluenceSettings>();
            client.BaseAddress = new Uri(settings.BaseUrl);
        });
        
        services.AddHttpClient<IDocumentLoader, JiraLoader>(client =>
        {
            var settings = configuration.GetSection("Jira").Get<JiraSettings>();
            client.BaseAddress = new Uri(settings.BaseUrl);
        });
        
        // Register services
        services.AddScoped<IChunkingService, ChunkingService>();
        services.AddScoped<IBankingDocumentParser, BankingDocumentParser>();
        services.AddScoped<IIngestionOrchestrator, IngestionOrchestrator>();
        
        // Register background service
        services.AddSingleton<IngestionTimerService>();
        services.AddHostedService(provider => provider.GetRequiredService<IngestionTimerService>());
        
        return services;
    }
    
    public static IServiceCollection ConfigureRag(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Configure RAG settings
        services.Configure<RagSettings>(configuration.GetSection("RagSettings"));
        services.Configure<ResponseGenerationSettings>(
            configuration.GetSection("ResponseGenerationSettings"));
        
        // Register RAG services
        services.AddScoped<IQueryEnhancer, QueryEnhancer>();
        services.AddScoped<IResponseGenerator, ResponseGenerator>();
        services.AddScoped<IRagOrchestrator, RagOrchestrator>();
        services.AddScoped<IQueryLogger, MongoQueryLogger>();
        
        return services;
    }
    
    private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(msg => !msg.IsSuccessStatusCode)
            .WaitAndRetryAsync(
                3,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    var logger = context.Values.ContainsKey("logger") 
                        ? context.Values["logger"] as ILogger 
                        : null;
                        
                    logger?.LogWarning(
                        "Retry {RetryCount} after {TimeSpan}ms",
                        retryCount, timespan.TotalMilliseconds);
                });
    }
    
    private static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(
                5,
                TimeSpan.FromSeconds(30),
                onBreak: (result, timespan) =>
                {
                    // Log circuit breaker opening
                },
                onReset: () =>
                {
                    // Log circuit breaker closing
                });
    }
}

// src/McpServer.Api/Middleware/ErrorHandlingMiddleware.cs
namespace McpServer.Api.Middleware;

public class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlingMiddleware> _logger;
    
    public ErrorHandlingMiddleware(
        RequestDelegate next,
        ILogger<ErrorHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }
    
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception occurred");
            await HandleExceptionAsync(context, ex);
        }
    }
    
    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/problem+json";
        
        var problemDetails = exception switch
        {
            RagProcessingException ragEx => new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Query processing failed",
                Detail = "An error occurred while processing your query. Please try again.",
                Instance = context.Request.Path
            },
            
            DocumentLoaderException loaderEx => new ProblemDetails
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = "Data source unavailable",
                Detail = $"Unable to access {loaderEx.SourceName}. Some results may be incomplete.",
                Instance = context.Request.Path
            },
            
            _ => new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "An error occurred",
                Detail = "An unexpected error occurred. Please try again later.",
                Instance = context.Request.Path
            }
        };
        
        context.Response.StatusCode = problemDetails.Status ?? 500;
        
        await context.Response.WriteAsync(
            JsonSerializer.Serialize(problemDetails));
    }
}

// src/McpServer.Api/Middleware/RateLimitingMiddleware.cs
namespace McpServer.Api.Middleware;

public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IMemoryCache _cache;
    private readonly ILogger<RateLimitingMiddleware> _logger;
    private readonly RateLimitSettings _settings;
    
    public RateLimitingMiddleware(
        RequestDelegate next,
        IMemoryCache cache,
        ILogger<RateLimitingMiddleware> logger,
        IOptions<RateLimitSettings> settings)
    {
        _next = next;
        _cache = cache;
        _logger = logger;
        _settings = settings.Value;
    }
    
    public async Task InvokeAsync(HttpContext context)
    {
        // Skip rate limiting for health checks
        if (context.Request.Path.StartsWithSegments("/health"))
        {
            await _next(context);
            return;
        }
        
        // Get client identifier (IP address or user ID)
        var clientId = GetClientId(context);
        var cacheKey = $"rate_limit_{clientId}";
        
        // Check rate limit
        var requestCount = await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1);
            return 0;
        });
        
        if (requestCount >= _settings.RequestsPerMinute)
        {
            _logger.LogWarning(
                "Rate limit exceeded for client {ClientId}",
                clientId);
                
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            await context.Response.WriteAsync("Rate limit exceeded. Please try again later.");
            return;
        }
        
        // Increment counter
        _cache.Set(cacheKey, requestCount + 1, TimeSpan.FromMinutes(1));
        
        await _next(context);
    }
    
    private string GetClientId(HttpContext context)
    {
        // Try to get user ID from claims
        var userId = context.User?.FindFirst("sub")?.Value;
        if (!string.IsNullOrEmpty(userId))
            return $"user_{userId}";
            
        // Fall back to IP address
        var ipAddress = context.Connection.RemoteIpAddress?.ToString();
        return $"ip_{ipAddress ?? "unknown"}";
    }
}

public class RateLimitSettings
{
    public int RequestsPerMinute { get; set; } = 60;
}
```

Configuration in `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "MongoDB": "mongodb://mcpapp:mcpapp2024@localhost:27017/mcprag"
  },
  
  "ClientUrl": "http://localhost:5000",
  
  "MongoDB": {
    "ConnectionString": "mongodb://mcpapp:mcpapp2024@localhost:27017/mcprag",
    "DatabaseName": "mcprag"
  },
  
  "Ollama": {
    "BaseUrl": "http://localhost:11434",
    "EmbeddingModel": "nomic-embed-text",
    "GenerationModel": "banking-assistant",
    "MaxRetries": 3,
    "TimeoutSeconds": 300
  },
  
  "Confluence": {
    "BaseUrl": "http://localhost:8090",
    "Username": "admin",
    "Password": "admin2024",
    "SpaceKey": "REFDATA"
  },
  
  "Jira": {
    "BaseUrl": "http://localhost:8080",
    "Username": "admin",
    "Password": "admin2024",
    "ProjectKey": "REFDATA"
  },
  
  "DataSources": {
    "LocalPath": "./SharePoint_Data"
  },
  
  "IngestionSettings": {
    "RunOnStartup": true,
    "Jobs": [
      {
        "Name": "full-ingestion",
        "DataSources": [],
        "Interval": "02:00:00",
        "IsEnabled": true
      },
      {
        "Name": "jira-frequent",
        "DataSources": ["Jira"],
        "Interval": "00:30:00",
        "IsEnabled": true
      }
    ]
  },
  
  "RagSettings": {
    "MaxRetrievedDocuments": 5,
    "MinSimilarityScore": 0.7,
    "EnableReranking": true
  },
  
  "ResponseGenerationSettings": {
    "SystemPrompt": "You are a banking reference data assistant. Provide accurate, compliant responses based on the provided documents. Always cite sources.",
    "MaxContextChunks": 5,
    "Temperature": 0.3,
    "MaxTokens": 2048
  },
  
  "ChunkingSettings": {
    "MaxChunkSize": 1000,
    "OverlapSize": 100,
    "PreserveSectionBoundaries": true
  },
  
  "RateLimitSettings": {
    "RequestsPerMinute": 60
  },
  
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "McpServer": "Debug"
    }
  }
}
```

### Deliverables for Iteration 4

By the end of this iteration, you'll have:
1. **Complete RAG orchestration** pipeline with query enhancement and response generation
2. **RESTful API** following MCP protocol specifications
3. **Comprehensive error handling** with proper problem details responses
4. **Rate limiting** to prevent abuse
5. **Health checks** for all external dependencies
6. **OpenAPI/Swagger** documentation
7. **Retry policies** and circuit breakers for resilience
8. **Performance monitoring** and request logging

## Iteration 5: Client Application

### Week 9-10: Building the Blazor WASM Client

This iteration focuses on creating a professional, user-friendly client application for interacting with the RAG system.

#### Sprint 5.1 - Blazor Client Foundation (Days 1-3)

Let's create a modern Blazor WebAssembly client with a banking-appropriate design:

```csharp
// src/McpServer.Client/Program.cs
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using McpServer.Client;
using McpServer.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Configure HTTP client for API calls
builder.Services.AddScoped(sp => new HttpClient 
{ 
    BaseAddress = new Uri(builder.Configuration["ApiUrl"] ?? "https://localhost:7001/") 
});

// Register services
builder.Services.AddScoped<IChatService, ChatService>();
builder.Services.AddScoped<IIngestionService, IngestionService>();
builder.Services.AddScoped<ILocalStorageService, LocalStorageService>();
builder.Services.AddScoped<IThemeService, ThemeService>();

// Add Blazored.LocalStorage for session management
builder.Services.AddBlazoredLocalStorage();

await builder.Build().RunAsync();

// src/McpServer.Client/Pages/Index.razor
@page "/"
@using McpServer.Client.Components
@inject NavigationManager Navigation

<PageTitle>MCP-RAG Banking Reference Data</PageTitle>

<div class="landing-page">
    <div class="hero-section">
        <h1>Banking Reference Data Assistant</h1>
        <p class="lead">
            Instantly access policies, procedures, and reference data through our AI-powered search
        </p>
        <button class="btn btn-primary btn-lg" @onclick="NavigateToChat">
            <i class="fas fa-comments"></i> Start Chatting
        </button>
    </div>
    
    <div class="features-section">
        <div class="container">
            <h2>What can I help you with?</h2>
            <div class="feature-grid">
                <QuickQueryCard 
                    Icon="fas fa-user-check"
                    Title="Counterparty Onboarding"
                    Description="KYC requirements, documentation, and approval workflows"
                    SampleQuery="What documents are required for counterparty onboarding?" />
                    
                <QuickQueryCard 
                    Icon="fas fa-book"
                    Title="Book Structure"
                    Description="Trading book hierarchies and organizational structure"
                    SampleQuery="Explain the equity derivatives book structure" />
                    
                <QuickQueryCard 
                    Icon="fas fa-exclamation-triangle"
                    Title="Settlement Fails"
                    Description="Policies, thresholds, and escalation procedures"
                    SampleQuery="What are the settlement fail thresholds?" />
                    
                <QuickQueryCard 
                    Icon="fas fa-shield-alt"
                    Title="Risk Policies"
                    Description="Risk management frameworks and control procedures"
                    SampleQuery="Show me the current risk limit framework" />
            </div>
        </div>
    </div>
    
    <div class="stats-section">
        <div class="container">
            <div class="stats-grid">
                <div class="stat-item">
                    <div class="stat-value">@DocumentCount</div>
                    <div class="stat-label">Documents Indexed</div>
                </div>
                <div class="stat-item">
                    <div class="stat-value">@LastUpdateTime</div>
                    <div class="stat-label">Last Updated</div>
                </div>
                <div class="stat-item">
                    <div class="stat-value">99.9%</div>
                    <div class="stat-label">Uptime</div>
                </div>
            </div>
        </div>
    </div>
</div>

@code {
    private int DocumentCount = 0;
    private string LastUpdateTime = "Loading...";
    
    protected override async Task OnInitializedAsync()
    {
        // Load stats from API
        try
        {
            var stats = await IngestionService.GetStatsAsync();
            DocumentCount = stats.TotalDocuments;
            LastUpdateTime = stats.LastUpdate.ToString("MMM dd, HH:mm");
        }
        catch
        {
            LastUpdateTime = "N/A";
        }
    }
    
    private void NavigateToChat()
    {
        Navigation.NavigateTo("/chat");
    }
}

// src/McpServer.Client/Pages/Chat.razor
@page "/chat"
@using McpServer.Client.Components
@using McpServer.Client.Models
@inject IChatService ChatService
@inject IJSRuntime JS

<PageTitle>Chat - Banking Reference Data</PageTitle>

<div class="chat-container">
    <div class="chat-sidebar">
        <div class="sidebar-header">
            <h3>Filters</h3>
        </div>
        <div class="filter-section">
            <label>Document Type</label>
            <select @bind="SelectedDocumentType" class="form-control">
                <option value="">All Types</option>
                <option value="Policy">Policies</option>
                <option value="Procedure">Procedures</option>
                <option value="Reference">Reference Data</option>
                <option value="RiskFramework">Risk Framework</option>
                <option value="Compliance">Compliance</option>
            </select>
        </div>
        <div class="filter-section">
            <label>Department</label>
            <select @bind="SelectedDepartment" class="form-control">
                <option value="">All Departments</option>
                <option value="Risk Management">Risk Management</option>
                <option value="Operations">Operations</option>
                <option value="Compliance">Compliance</option>
                <option value="Trading">Trading</option>
            </select>
        </div>
        
        <div class="sidebar-section">
            <h4>Quick Queries</h4>
            <div class="quick-query-list">
                @foreach (var query in QuickQueries)
                {
                    <button class="quick-query-btn" @onclick="() => UseQuickQuery(query)">
                        @query
                    </button>
                }
            </div>
        </div>
    </div>
    
    <div class="chat-main">
        <div class="chat-header">
            <h2>Banking Reference Data Assistant</h2>
            <div class="chat-actions">
                <button class="btn btn-sm btn-outline-secondary" @onclick="ClearChat">
                    <i class="fas fa-broom"></i> Clear
                </button>
                <button class="btn btn-sm btn-outline-secondary" @onclick="ExportChat">
                    <i class="fas fa-download"></i> Export
                </button>
            </div>
        </div>
        
        <div class="chat-messages" @ref="messagesContainer">
            @if (!Messages.Any())
            {
                <div class="welcome-message">
                    <i class="fas fa-robot fa-3x"></i>
                    <h3>Welcome to the Banking Reference Data Assistant</h3>
                    <p>I can help you find information about policies, procedures, and reference data.</p>
                    <p>Try asking me about:</p>
                    <ul>
                        <li>Counterparty onboarding requirements</li>
                        <li>Settlement fail procedures</li>
                        <li>Book structure guidelines</li>
                        <li>Risk management policies</li>
                    </ul>
                </div>
            }
            else
            {
                @foreach (var message in Messages)
                {
                    <ChatMessage Message="message" />
                }
            }
            
            @if (IsLoading)
            {
                <div class="message assistant loading">
                    <div class="message-avatar">
                        <i class="fas fa-robot"></i>
                    </div>
                    <div class="message-content">
                        <div class="typing-indicator">
                            <span></span>
                            <span></span>
                            <span></span>
                        </div>
                    </div>
                </div>
            }
        </div>
        
        <div class="chat-input-container">
            @if (ErrorMessage != null)
            {
                <div class="alert alert-danger alert-dismissible">
                    @ErrorMessage
                    <button type="button" class="btn-close" @onclick="() => ErrorMessage = null"></button>
                </div>
            }
            
            <div class="chat-input">
                <textarea 
                    @bind="CurrentMessage" 
                    @bind:event="oninput"
                    @onkeypress="@(async (e) => await HandleKeyPress(e))"
                    placeholder="Ask about banking policies, procedures, or reference data..."
                    rows="2"
                    disabled="@IsLoading"
                    class="form-control">
                </textarea>
                <button 
                    class="btn btn-primary send-button" 
                    @onclick="SendMessage"
                    disabled="@(IsLoading || string.IsNullOrWhiteSpace(CurrentMessage))">
                    <i class="fas fa-paper-plane"></i>
                </button>
            </div>
            <div class="input-hints">
                Press Enter to send, Shift+Enter for new line
            </div>
        </div>
    </div>
</div>

@code {
    private ElementReference messagesContainer;
    private List<ChatMessageModel> Messages = new();
    private string CurrentMessage = "";
    private bool IsLoading = false;
    private string? ErrorMessage;
    private string? SelectedDocumentType;
    private string? SelectedDepartment;
    
    private List<string> QuickQueries = new()
    {
        "What is the counterparty onboarding process?",
        "Show me settlement fail thresholds",
        "What are the book structure requirements?",
        "List documents needed for KYC",
        "Explain the risk limit framework"
    };
    
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // Auto-scroll to bottom
        await JS.InvokeVoidAsync("scrollToBottom", messagesContainer);
    }
    
    private async Task SendMessage()
    {
        if (string.IsNullOrWhiteSpace(CurrentMessage) || IsLoading)
            return;
            
        var userMessage = new ChatMessageModel
        {
            Id = Guid.NewGuid().ToString(),
            Content = CurrentMessage,
            IsUser = true,
            Timestamp = DateTime.Now
        };
        
        Messages.Add(userMessage);
        
        var query = CurrentMessage;
        CurrentMessage = "";
        IsLoading = true;
        ErrorMessage = null;
        
        try
        {
            var response = await ChatService.SendMessageAsync(new ChatRequest
            {
                Message = query,
                DocumentType = SelectedDocumentType,
                Department = SelectedDepartment
            });
            
            var assistantMessage = new ChatMessageModel
            {
                Id = Guid.NewGuid().ToString(),
                Content = response.Message,
                IsUser = false,
                Timestamp = DateTime.Now,
                Sources = response.Sources,
                Metadata = response.Metadata
            };
            
            Messages.Add(assistantMessage);
        }
        catch (Exception ex)
        {
            ErrorMessage = "Failed to get response. Please try again.";
            Console.Error.WriteLine($"Chat error: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }
    
    private async Task HandleKeyPress(KeyboardEventArgs e)
    {
        if (e.Key == "Enter" && !e.ShiftKey)
        {
            await SendMessage();
        }
    }
    
    private void UseQuickQuery(string query)
    {
        CurrentMessage = query;
    }
    
    private void ClearChat()
    {
        Messages.Clear();
    }
    
    private async Task ExportChat()
    {
        var export = string.Join("\n\n", Messages.Select(m => 
            $"{(m.IsUser ? "User" : "Assistant")} ({m.Timestamp:yyyy-MM-dd HH:mm}):\n{m.Content}"));
            
        await JS.InvokeVoidAsync("downloadText", export, $"chat-export-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
    }
}

// src/McpServer.Client/Components/ChatMessage.razor
@using McpServer.Client.Models

<div class="message @(Message.IsUser ? "user" : "assistant")">
    <div class="message-avatar">
        @if (Message.IsUser)
        {
            <i class="fas fa-user"></i>
        }
        else
        {
            <i class="fas fa-robot"></i>
        }
    </div>
    <div class="message-content">
        <div class="message-text">
            @if (Message.IsUser)
            {
                @Message.Content
            }
            else
            {
                <MarkdownContent Content="@Message.Content" />
            }
        </div>
        
        @if (!Message.IsUser && Message.Sources?.Any() == true)
        {
            <div class="message-sources">
                <div class="sources-header">
                    <i class="fas fa-book"></i> Sources
                </div>
                <div class="sources-list">
                    @foreach (var source in Message.Sources)
                    {
                        <div class="source-item">
                            <a href="@source.Url" target="_blank" class="source-link">
                                <span class="source-title">@source.Title</span>
                                @if (!string.IsNullOrEmpty(source.Section))
                                {
                                    <span class="source-section">- @source.Section</span>
                                }
                            </a>
                            <span class="source-score" title="Relevance score">
                                @((source.Score * 100).ToString("F0"))%
                            </span>
                        </div>
                    }
                </div>
            </div>
        }
        
        @if (!Message.IsUser && Message.Metadata != null)
        {
            <div class="message-metadata">
                <span title="Processing time">
                    <i class="fas fa-clock"></i> @Message.Metadata.ProcessingTimeMs ms
                </span>
                <span title="Documents retrieved">
                    <i class="fas fa-file-alt"></i> @Message.Metadata.ChunksRetrieved chunks
                </span>
            </div>
        }
    </div>
    <div class="message-time">
        @Message.Timestamp.ToString("HH:mm")
    </div>
</div>

@code {
    [Parameter] public ChatMessageModel Message { get; set; } = null!;
}

// src/McpServer.Client/Services/ChatService.cs
namespace McpServer.Client.Services;

public interface IChatService
{
    Task<ChatResponse> SendMessageAsync(ChatRequest request);
    Task<List<ChatMessageModel>> GetHistoryAsync(string sessionId);
}

public class ChatService : IChatService
{
    private readonly HttpClient _httpClient;
    private readonly ILocalStorageService _localStorage;
    private readonly ILogger<ChatService> _logger;
    private string? _sessionId;
    
    public ChatService(
        HttpClient httpClient,
        ILocalStorageService localStorage,
        ILogger<ChatService> logger)
    {
        _httpClient = httpClient;
        _localStorage = localStorage;
        _logger = logger;
    }
    
    public async Task<ChatResponse> SendMessageAsync(ChatRequest request)
    {
        // Get or create session ID
        if (string.IsNullOrEmpty(_sessionId))
        {
            _sessionId = await _localStorage.GetItemAsync<string>("sessionId");
            if (string.IsNullOrEmpty(_sessionId))
            {
                _sessionId = Guid.NewGuid().ToString();
                await _localStorage.SetItemAsync("sessionId", _sessionId);
            }
        }
        
        // Add session to request
        request.SessionId = _sessionId;
        request.UserId = await GetUserIdAsync();
        
        var response = await _httpClient.PostAsJsonAsync("api/mcp/chat", request);
        
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            _logger.LogError("Chat API error: {StatusCode} - {Error}", 
                response.StatusCode, error);
            throw new HttpRequestException($"Chat request failed: {response.StatusCode}");
        }
        
        var chatResponse = await response.Content.ReadFromJsonAsync<ChatResponse>();
        return chatResponse ?? throw new InvalidOperationException("Invalid response from server");
    }
    
    public async Task<List<ChatMessageModel>> GetHistoryAsync(string sessionId)
    {
        var response = await _httpClient.GetAsync($"api/mcp/history/{sessionId}");
        
        if (!response.IsSuccessStatusCode)
            return new List<ChatMessageModel>();
            
        var history = await response.Content.ReadFromJsonAsync<List<ChatHistoryItem>>();
        
        return history?.SelectMany(h => new[]
        {
            new ChatMessageModel
            {
                Content = h.Query,
                IsUser = true,
                Timestamp = h.Timestamp
            },
            new ChatMessageModel
            {
                Content = h.Response,
                IsUser = false,
                Timestamp = h.Timestamp,
                Sources = h.Sources
            }
        }).ToList() ?? new List<ChatMessageModel>();
    }
    
    private async Task<string> GetUserIdAsync()
    {
        var userId = await _localStorage.GetItemAsync<string>("userId");
        if (string.IsNullOrEmpty(userId))
        {
            userId = $"user_{Guid.NewGuid():N}";
            await _localStorage.SetItemAsync("userId", userId);
        }
        return userId;
    }
}
```

#### Sprint 5.2 - Advanced Features and Polish (Days 4-6)

Let's add monitoring dashboard and advanced features:

```csharp
// src/McpServer.Client/Pages/Monitoring.razor
@page "/monitoring"
@using McpServer.Client.Components
@inject IIngestionService IngestionService
@inject IJSRuntime JS
@implements IDisposable

<PageTitle>System Monitoring - Banking Reference Data</PageTitle>

<div class="monitoring-container">
    <div class="monitoring-header">
        <h2>System Monitoring Dashboard</h2>
        <div class="refresh-controls">
            <label class="form-check">
                <input type="checkbox" class="form-check-input" @bind="AutoRefresh" />
                Auto-refresh
            </label>
            <button class="btn btn-sm btn-primary" @onclick="RefreshData">
                <i class="fas fa-sync @(IsRefreshing ? "fa-spin" : "")"></i> Refresh
            </button>
        </div>
    </div>
    
    <div class="monitoring-grid">
        <!-- Ingestion Status Card -->
        <div class="monitoring-card">
            <div class="card-header">
                <h3>Ingestion Jobs</h3>
            </div>
            <div class="card-body">
                @if (TimerStatuses != null)
                {
                    <div class="job-list">
                        @foreach (var timer in TimerStatuses)
                        {
                            <div class="job-item">
                                <div class="job-info">
                                    <div class="job-name">@timer.JobName</div>
                                    <div class="job-schedule">Every @timer.Interval</div>
                                </div>
                                <div class="job-status">
                                    @if (timer.IsEnabled)
                                    {
                                        <span class="badge bg-success">Active</span>
                                    }
                                    else
                                    {
                                        <span class="badge bg-secondary">Disabled</span>
                                    }
                                </div>
                                <div class="job-actions">
                                    <button class="btn btn-sm btn-outline-primary" 
                                            @onclick="() => TriggerJob(timer.JobName)"
                                            disabled="@IsTriggering">
                                        <i class="fas fa-play"></i> Run Now
                                    </button>
                                </div>
                                @if (timer.NextRunTime.HasValue)
                                {
                                    <div class="job-next-run">
                                        Next run: @timer.NextRunTime.Value.ToString("HH:mm:ss")
                                    </div>
                                }
                            </div>
                        }
                    </div>
                }
                else
                {
                    <div class="loading-spinner">
                        <i class="fas fa-spinner fa-spin"></i> Loading...
                    </div>
                }
            </div>
        </div>
        
        <!-- Recent Ingestions Card -->
        <div class="monitoring-card">
            <div class="card-header">
                <h3>Recent Ingestion Runs</h3>
            </div>
            <div class="card-body">
                @if (RecentRuns != null)
                {
                    <div class="runs-timeline">
                        @foreach (var run in RecentRuns)
                        {
                            <div class="run-item @GetRunStatusClass(run.Status)">
                                <div class="run-time">
                                    @run.StartTime.ToString("MMM dd HH:mm")
                                </div>
                                <div class="run-details">
                                    <div class="run-job">@run.JobName</div>
                                    <div class="run-stats">
                                        <span class="stat-item">
                                            <i class="fas fa-file-alt"></i> 
                                            @run.TotalChunksProcessed chunks
                                        </span>
                                        @if (run.TotalChunksFailed > 0)
                                        {
                                            <span class="stat-item text-danger">
                                                <i class="fas fa-exclamation-triangle"></i> 
                                                @run.TotalChunksFailed failed
                                            </span>
                                        }
                                        @if (run.Duration != null)
                                        {
                                            <span class="stat-item">
                                                <i class="fas fa-clock"></i> 
                                                @run.Duration
                                            </span>
                                        }
                                    </div>
                                </div>
                                <div class="run-status">
                                    <span class="badge @GetRunStatusBadgeClass(run.Status)">
                                        @run.Status
                                    </span>
                                </div>
                            </div>
                        }
                    </div>
                }
                else
                {
                    <div class="loading-spinner">
                        <i class="fas fa-spinner fa-spin"></i> Loading...
                    </div>
                }
            </div>
        </div>
        
        <!-- System Health Card -->
        <div class="monitoring-card">
            <div class="card-header">
                <h3>System Health</h3>
            </div>
            <div class="card-body">
                @if (HealthStatus != null)
                {
                    <div class="health-checks">
                        @foreach (var check in HealthStatus.Checks)
                        {
                            <div class="health-item @GetHealthClass(check.Status)">
                                <div class="health-name">
                                    <i class="@GetHealthIcon(check.Name)"></i>
                                    @check.Name
                                </div>
                                <div class="health-status">
                                    @if (check.Status == "Healthy")
                                    {
                                        <i class="fas fa-check-circle text-success"></i>
                                    }
                                    else if (check.Status == "Degraded")
                                    {
                                        <i class="fas fa-exclamation-circle text-warning"></i>
                                    }
                                    else
                                    {
                                        <i class="fas fa-times-circle text-danger"></i>
                                    }
                                    @check.Status
                                </div>
                                @if (!string.IsNullOrEmpty(check.Description))
                                {
                                    <div class="health-description">
                                        @check.Description
                                    </div>
                                }
                            </div>
                        }
                    </div>
                    <div class="health-summary">
                        Overall Status: 
                        <span class="badge @GetHealthBadgeClass(HealthStatus.Status)">
                            @HealthStatus.Status
                        </span>
                    </div>
                }
                else
                {
                    <div class="loading-spinner">
                        <i class="fas fa-spinner fa-spin"></i> Loading...
                    </div>
                }
            </div>
        </div>
        
        <!-- Statistics Card -->
        <div class="monitoring-card">
            <div class="card-header">
                <h3>System Statistics</h3>
            </div>
            <div class="card-body">
                <div class="stats-grid">
                    <div class="stat-box">
                        <div class="stat-value">@Stats.TotalDocuments.ToString("N0")</div>
                        <div class="stat-label">Total Documents</div>
                    </div>
                    <div class="stat-box">
                        <div class="stat-value">@Stats.TotalChunks.ToString("N0")</div>
                        <div class="stat-label">Document Chunks</div>
                    </div>
                    <div class="stat-box">
                        <div class="stat-value">@Stats.QueriesLast24h</div>
                        <div class="stat-label">Queries (24h)</div>
                    </div>
                    <div class="stat-box">
                        <div class="stat-value">@Stats.AverageResponseTime ms</div>
                        <div class="stat-label">Avg Response Time</div>
                    </div>
                </div>
            </div>
        </div>
    </div>
</div>

@code {
    private List<TimerStatusDto>? TimerStatuses;
    private List<IngestionRunDto>? RecentRuns;
    private HealthCheckResult? HealthStatus;
    private SystemStats Stats = new();
    
    private bool AutoRefresh = true;
    private bool IsRefreshing = false;
    private bool IsTriggering = false;
    private Timer? refreshTimer;
    
    protected override async Task OnInitializedAsync()
    {
        await RefreshData();
        
        if (AutoRefresh)
        {
            refreshTimer = new Timer(async _ => await InvokeAsync(RefreshData), 
                null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
        }
    }
    
    private async Task RefreshData()
    {
        if (IsRefreshing) return;
        
        IsRefreshing = true;
        StateHasChanged();
        
        try
        {
            // Fetch all data in parallel
            var timerTask = IngestionService.GetTimerStatusesAsync();
            var runsTask = IngestionService.GetRecentRunsAsync(10);
            var healthTask = IngestionService.GetHealthStatusAsync();
            var statsTask = IngestionService.GetStatisticsAsync();
            
            await Task.WhenAll(timerTask, runsTask, healthTask, statsTask);
            
            TimerStatuses = await timerTask;
            RecentRuns = await runsTask;
            HealthStatus = await healthTask;
            Stats = await statsTask;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error refreshing monitoring data: {ex.Message}");
        }
        finally
        {
            IsRefreshing = false;
            StateHasChanged();
        }
    }
    
    private async Task TriggerJob(string jobName)
    {
        IsTriggering = true;
        
        try
        {
            await IngestionService.TriggerJobAsync(jobName);
            await RefreshData();
            
            // Show success notification
            await JS.InvokeVoidAsync("showNotification", 
                "Success", $"Job '{jobName}' triggered successfully", "success");
        }
        catch (Exception ex)
        {
            await JS.InvokeVoidAsync("showNotification", 
                "Error", $"Failed to trigger job: {ex.Message}", "error");
        }
        finally
        {
            IsTriggering = false;
        }
    }
    
    private string GetRunStatusClass(string status) => status switch
    {
        "Completed" => "status-success",
        "Running" => "status-info",
        "Failed" => "status-danger",
        "PartiallyCompleted" => "status-warning",
        _ => "status-secondary"
    };
    
    private string GetRunStatusBadgeClass(string status) => status switch
    {
        "Completed" => "bg-success",
        "Running" => "bg-info",
        "Failed" => "bg-danger",
        "PartiallyCompleted" => "bg-warning",
        _ => "bg-secondary"
    };
    
    private string GetHealthClass(string status) => status switch
    {
        "Healthy" => "health-good",
        "Degraded" => "health-degraded",
        _ => "health-unhealthy"
    };
    
    private string GetHealthBadgeClass(string status) => status switch
    {
        "Healthy" => "bg-success",
        "Degraded" => "bg-warning",
        _ => "bg-danger"
    };
    
    private string GetHealthIcon(string name) => name.ToLower() switch
    {
        "mongodb" => "fas fa-database",
        "ollama" => "fas fa-brain",
        "confluence" => "fab fa-confluence",
        "jira" => "fab fa-jira",
        _ => "fas fa-server"
    };
    
    public void Dispose()
    {
        refreshTimer?.Dispose();
    }
}
```

Now let's add the CSS styling for a professional banking application look:

```css
/* src/McpServer.Client/wwwroot/css/app.css */
:root {
    --primary-color: #003d7a;
    --secondary-color: #0066cc;
    --success-color: #28a745;
    --warning-color: #ffc107;
    --danger-color: #dc3545;
    --dark-color: #212529;
    --light-color: #f8f9fa;
    --gray-color: #6c757d;
    --border-radius: 8px;
    --box-shadow: 0 2px 4px rgba(0,0,0,0.1);
    --transition: all 0.3s ease;
}

/* Global Styles */
body {
    font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif;
    background-color: #f5f7fa;
    color: var(--dark-color);
}

/* Landing Page */
.landing-page {
    min-height: 100vh;
}

.hero-section {
    background: linear-gradient(135deg, var(--primary-color), var(--secondary-color));
    color: white;
    padding: 100px 20px;
    text-align: center;
}

.hero-section h1 {
    font-size: 3rem;
    font-weight: 700;
    margin-bottom: 20px;
}

.hero-section .lead {
    font-size: 1.5rem;
    margin-bottom: 40px;
    opacity: 0.9;
}

.features-section {
    padding: 80px 20px;
    background: white;
}

.feature-grid {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(300px, 1fr));
    gap: 30px;
    margin-top: 50px;
}

.feature-card {
    background: white;
    border: 1px solid #e0e6ed;
    border-radius: var(--border-radius);
    padding: 30px;
    text-align: center;
    transition: var(--transition);
    cursor: pointer;
}

.feature-card:hover {
    transform: translateY(-5px);
    box-shadow: 0 10px 30px rgba(0,0,0,0.1);
}

.feature-card i {
    font-size: 3rem;
    color: var(--primary-color);
    margin-bottom: 20px;
}

/* Chat Interface */
.chat-container {
    display: flex;
    height: calc(100vh - 60px);
    background: white;
}

.chat-sidebar {
    width: 300px;
    background: #f8f9fa;
    border-right: 1px solid #dee2e6;
    padding: 20px;
    overflow-y: auto;
}

.chat-main {
    flex: 1;
    display: flex;
    flex-direction: column;
}

.chat-header {
    background: white;
    border-bottom: 1px solid #dee2e6;
    padding: 20px;
    display: flex;
    justify-content: space-between;
    align-items: center;
}

.chat-messages {
    flex: 1;
    overflow-y: auto;
    padding: 20px;
    background: #f5f7fa;
}

.message {
    display: flex;
    margin-bottom: 20px;
    animation: fadeIn 0.3s ease;
}

@keyframes fadeIn {
    from { opacity: 0; transform: translateY(10px); }
    to { opacity: 1; transform: translateY(0); }
}

.message.user {
    justify-content: flex-end;
}

.message-avatar {
    width: 40px;
    height: 40px;
    border-radius: 50%;
    display: flex;
    align-items: center;
    justify-content: center;
    margin: 0 10px;
    font-size: 1.2rem;
}

.message.user .message-avatar {
    background: var(--primary-color);
    color: white;
    order: 2;
}

.message.assistant .message-avatar {
    background: #e9ecef;
    color: var(--gray-color);
}

.message-content {
    max-width: 70%;
    background: white;
    border-radius: var(--border-radius);
    padding: 15px 20px;
    box-shadow: var(--box-shadow);
}

.message.user .message-content {
    background: var(--primary-color);
    color: white;
}

.message-sources {
    margin-top: 15px;
    padding-top: 15px;
    border-top: 1px solid #e9ecef;
}

.sources-header {
    font-weight: 600;
    margin-bottom: 10px;
    color: var(--gray-color);
}

.source-item {
    display: flex;
    align-items: center;
    justify-content: space-between;
    padding: 8px 0;
}

.source-link {
    color: var(--secondary-color);
    text-decoration: none;
    transition: var(--transition);
}

.source-link:hover {
    text-decoration: underline;
}

.source-score {
    background: #e9ecef;
    padding: 2px 8px;
    border-radius: 12px;
    font-size: 0.85rem;
    color: var(--gray-color);
}

.chat-input-container {
    background: white;
    border-top: 1px solid #dee2e6;
    padding: 20px;
}

.chat-input {
    display: flex;
    gap: 10px;
    align-items: flex-end;
}

.chat-input textarea {
    flex: 1;
    resize: none;
    border: 1px solid #ced4da;
    border-radius: var(--border-radius);
    padding: 10px 15px;
    font-size: 1rem;
}

.send-button {
    width: 50px;
    height: 50px;
    border-radius: 50%;
    display: flex;
    align-items: center;
    justify-content: center;
    font-size: 1.2rem;
}

.typing-indicator {
    display: flex;
    gap: 4px;
}

.typing-indicator span {
    width: 8px;
    height: 8px;
    border-radius: 50%;
    background: var(--gray-color);
    animation: typing 1.4s infinite;
}

.typing-indicator span:nth-child(2) {
    animation-delay: 0.2s;
}

.typing-indicator span:nth-child(3) {
    animation-delay: 0.4s;
}

@keyframes typing {
    0%, 60%, 100% { opacity: 0.3; transform: scale(0.8); }
    30% { opacity: 1; transform: scale(1); }
}

/* Monitoring Dashboard */
.monitoring-container {
    padding: 20px;
    max-width: 1400px;
    margin: 0 auto;
}

.monitoring-header {
    display: flex;
    justify-content: space-between;
    align-items: center;
    margin-bottom: 30px;
}

.monitoring-grid {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(400px, 1fr));
    gap: 20px;
}

.monitoring-card {
    background: white;
    border-radius: var(--border-radius);
    box-shadow: var(--box-shadow);
    overflow: hidden;
}

.monitoring-card .card-header {
    background: #f8f9fa;
    padding: 15px 20px;
    border-bottom: 1px solid #dee2e6;
}

.monitoring-card .card-body {
    padding: 20px;
}

.job-item {
    display: grid;
    grid-template-columns: 1fr auto auto;
    gap: 15px;
    align-items: center;
    padding: 15px 0;
    border-bottom: 1px solid #e9ecef;
}

.job-item:last-child {
    border-bottom: none;
}

.run-item {
    display: grid;
    grid-template-columns: auto 1fr auto;
    gap: 15px;
    padding: 15px;
    border-left: 4px solid;
    margin-bottom: 10px;
    background: #f8f9fa;
    border-radius: 0 var(--border-radius) var(--border-radius) 0;
}

.run-item.status-success {
    border-color: var(--success-color);
}

.run-item.status-danger {
    border-color: var(--danger-color);
}

.run-item.status-warning {
    border-color: var(--warning-color);
}

.run-item.status-info {
    border-color: var(--secondary-color);
}

.health-item {
    display: flex;
    justify-content: space-between;
    align-items: center;
    padding: 12px 0;
    border-bottom: 1px solid #e9ecef;
}

.stats-grid {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(150px, 1fr));
    gap: 20px;
}

.stat-box {
    text-align: center;
    padding: 20px;
    background: #f8f9fa;
    border-radius: var(--border-radius);
}

.stat-value {
    font-size: 2rem;
    font-weight: 700;
    color: var(--primary-color);
}

.stat-label {
    color: var(--gray-color);
    font-size: 0.9rem;
    margin-top: 5px;
}

/* Responsive Design */
@media (max-width: 768px) {
    .chat-sidebar {
        display: none;
    }
    
    .message-content {
        max-width: 85%;
    }
    
    .monitoring-grid {
        grid-template-columns: 1fr;
    }
    
    .hero-section h1 {
        font-size: 2rem;
    }
    
    .hero-section .lead {
        font-size: 1.2rem;
    }
}

/* Utilities */
.loading-spinner {
    text-align: center;
    padding: 40px;
    color: var(--gray-color);
}

.badge {
    padding: 4px 12px;
    border-radius: 12px;
    font-size: 0.85rem;
    font-weight: 500;
}

.btn {
    transition: var(--transition);
}

.btn:hover {
    transform: translateY(-1px);
}

.alert {
    border-radius: var(--border-radius);
    border: none;
}
```

### Deliverables for Iteration 5

By the end of this iteration, you'll have:
1. **Professional Blazor WASM client** with banking-appropriate design
2. **Intuitive chat interface** with source citations and metadata display
3. **System monitoring dashboard** for tracking ingestion and health
4. **Quick query templates** for common banking questions
5. **Export functionality** for chat conversations
6. **Responsive design** for desktop and mobile use
7. **Local storage integration** for session management
8. **Real-time updates** with auto-refresh capabilities

## Iteration 6: Optimization and Production Readiness

### Week 11-12: Performance Optimization and Deployment Preparation

This final iteration focuses on optimizing the system for production use and preparing deployment artifacts.

#### Sprint 6.1 - Performance Optimization (Days 1-3)

Let's implement caching, connection pooling, and other optimizations:

```csharp
// src/McpServer.Infrastructure/Services/CachedLlmClient.cs
namespace McpServer.Infrastructure.Services;

/// <summary>
/// Decorator for LLM client that adds caching capabilities
/// </summary>
public class CachedLlmClient : ILlmClient
{
    private readonly ILlmClient _innerClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<CachedLlmClient> _logger;
    private readonly CacheSettings _settings;
    
    public CachedLlmClient(
        ILlmClient innerClient,
        IMemoryCache cache,
        ILogger<CachedLlmClient> logger,
        IOptions<CacheSettings> settings)
    {
        _innerClient = innerClient;
        _cache = cache;
        _logger = logger;
        _settings = settings.Value;
    }
    
    public async Task<float[]> GenerateEmbeddingAsync(
        string text, 
        CancellationToken cancellationToken = default)
    {
        // Create cache key based on text hash
        var cacheKey = $"embedding_{ComputeHash(text)}";
        
        // Try to get from cache
        if (_cache.TryGetValue<float[]>(cacheKey, out var cachedEmbedding))
        {
            _logger.LogDebug("Embedding cache hit for text hash: {Hash}", 
                cacheKey);
            return cachedEmbedding;
        }
        
        // Generate embedding
        var embedding = await _innerClient.GenerateEmbeddingAsync(text, cancellationToken);
        
        // Cache the result
        _cache.Set(cacheKey, embedding, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(_settings.EmbeddingCacheHours),
            Size = embedding.Length * sizeof(float) // Approximate memory size
        });
        
        return embedding;
    }
    
    public async Task<List<float[]>> GenerateEmbeddingsBatchAsync(
        List<string> texts, 
        CancellationToken cancellationToken = default)
    {
        var results = new float[texts.Count][];
        var uncachedIndices = new List<int>();
        var uncachedTexts = new List<string>();
        
        // Check cache for each text
        for (int i = 0; i < texts.Count; i++)
        {
            var cacheKey = $"embedding_{ComputeHash(texts[i])}";
            if (_cache.TryGetValue<float[]>(cacheKey, out var cachedEmbedding))
            {
                results[i] = cachedEmbedding;
            }
            else
            {
                uncachedIndices.Add(i);
                uncachedTexts.Add(texts[i]);
            }
        }
        
        _logger.LogDebug(
            "Embedding batch cache stats: {Cached} cached, {Uncached} to generate",
            texts.Count - uncachedTexts.Count, uncachedTexts.Count);
        
        // Generate embeddings for uncached texts
        if (uncachedTexts.Any())
        {
            var newEmbeddings = await _innerClient.GenerateEmbeddingsBatchAsync(
                uncachedTexts, 
                cancellationToken);
            
            // Cache and assign results
            for (int i = 0; i < uncachedIndices.Count; i++)
            {
                var originalIndex = uncachedIndices[i];
                var embedding = newEmbeddings[i];
                results[originalIndex] = embedding;
                
                // Cache the result
                var cacheKey = $"embedding_{ComputeHash(texts[originalIndex])}";
                _cache.Set(cacheKey, embedding, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(_settings.EmbeddingCacheHours),
                    Size = embedding.Length * sizeof(float)
                });
            }
        }
        
        return results.ToList();
    }
    
    public async Task<string> GenerateResponseAsync(
        RagContext context, 
        CancellationToken cancellationToken = default)
    {
        // Response generation is not cached as it's context-dependent
        return await _innerClient.GenerateResponseAsync(context, cancellationToken);
    }
    
    private string ComputeHash(string text)
    {
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(text));
        return Convert.ToBase64String(bytes);
    }
}

// src/McpServer.Infrastructure/Services/OptimizedVectorRepository.cs
namespace McpServer.Infrastructure.Services;

/// <summary>
/// Optimized MongoDB vector repository with connection pooling and batch operations
/// </summary>
public class OptimizedMongoVectorRepository : IVectorRepository
{
    private readonly IMongoCollection<DocumentChunk> _chunks;
    private readonly ILogger<OptimizedMongoVectorRepository> _logger;
    private readonly SemaphoreSlim _bulkWriteSemaphore;
    private readonly List<WriteModel<DocumentChunk>> _pendingWrites;
    private readonly Timer _flushTimer;
    
    public OptimizedMongoVectorRepository(
        IMongoDatabase database,
        ILogger<OptimizedMongoVectorRepository> logger)
    {
        _chunks = database.GetCollection<DocumentChunk>("document_chunks");
        _logger = logger;
        
        // Limit concurrent bulk operations
        _bulkWriteSemaphore = new SemaphoreSlim(1, 1);
        _pendingWrites = new List<WriteModel<DocumentChunk>>();
        
        // Flush pending writes periodically
        _flushTimer = new Timer(
            async _ => await FlushPendingWritesAsync(),
            null,
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(5));
    }
    
    public async Task UpsertBatchAsync(
        IEnumerable<DocumentChunk> chunks, 
        CancellationToken cancellationToken = default)
    {
        var chunksList = chunks.ToList();
        if (!chunksList.Any()) return;
        
        await _bulkWriteSemaphore.WaitAsync(cancellationToken);
        try
        {
            // Add to pending writes
            foreach (var chunk in chunksList)
            {
                chunk.IndexedOn = DateTime.UtcNow;
                
                var filter = Builders<DocumentChunk>.Filter.And(
                    Builders<DocumentChunk>.Filter.Eq(c => c.Source, chunk.Source),
                    Builders<DocumentChunk>.Filter.Eq(c => c.ChunkIndex, chunk.ChunkIndex)
                );
                
                _pendingWrites.Add(new ReplaceOneModel<DocumentChunk>(filter, chunk) 
                { 
                    IsUpsert = true 
                });
            }
            
            // Flush if we have enough pending writes
            if (_pendingWrites.Count >= 100)
            {
                await FlushPendingWritesAsync(cancellationToken);
            }
        }
        finally
        {
            _bulkWriteSemaphore.Release();
        }
    }
    
    private async Task FlushPendingWritesAsync(CancellationToken cancellationToken = default)
    {
        if (!_pendingWrites.Any()) return;
        
        await _bulkWriteSemaphore.WaitAsync(cancellationToken);
        try
        {
            if (!_pendingWrites.Any()) return; // Double-check
            
            var writesToFlush = _pendingWrites.ToList();
            _pendingWrites.Clear();
            
            var options = new BulkWriteOptions 
            { 
                IsOrdered = false // Better performance
            };
            
            var result = await _chunks.BulkWriteAsync(
                writesToFlush, 
                options, 
                cancellationToken);
                
            _logger.LogDebug(
                "Flushed {Count} writes. Inserted: {Inserted}, Modified: {Modified}",
                writesToFlush.Count, result.InsertedCount, result.ModifiedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error flushing pending writes");
            throw;
        }
        finally
        {
            _bulkWriteSemaphore.Release();
        }
    }
    
    public async Task<List<ScoredDocumentChunk>> SearchAsync(
        float[] queryVector,
        int maxResults = 5,
        float minScore = 0.7f,
        Dictionary<string, string> filters = null,
        CancellationToken cancellationToken = default)
    {
        // Use aggregation pipeline for better performance
        var pipeline = new List<BsonDocument>();
        
        // Add filter stage if needed
        if (filters?.Any() == true)
        {
            var filterDoc = new BsonDocument();
            foreach (var (key, value) in filters)
            {
                filterDoc[key.ToLower()] = value;
            }
            pipeline.Add(new BsonDocument("$match", filterDoc));
        }
        
        // Sample documents for vector comparison (for MVP)
        pipeline.Add(new BsonDocument("$sample", new BsonDocument("size", maxResults * 10)));
        
        // Project and add computed similarity score
        pipeline.Add(new BsonDocument("$addFields", new BsonDocument
        {
            ["similarity"] = new BsonDocument("$literal", 0.8) // Placeholder
        }));
        
        // Filter by minimum score
        pipeline.Add(new BsonDocument("$match", new BsonDocument
        {
            ["similarity"] = new BsonDocument("$gte", minScore)
        }));
        
        // Sort by similarity
        pipeline.Add(new BsonDocument("$sort", new BsonDocument("similarity", -1)));
        
        // Limit results
        pipeline.Add(new BsonDocument("$limit", maxResults));
        
        var cursor = await _chunks.AggregateAsync<BsonDocument>(
            pipeline, 
            cancellationToken: cancellationToken);
            
        var results = new List<ScoredDocumentChunk>();
        
        await cursor.ForEachAsync(doc =>
        {
            // In production, compute actual cosine similarity here
            var chunk = BsonSerializer.Deserialize<DocumentChunk>(doc);
            results.Add(new ScoredDocumentChunk
            {
                Id = chunk.Id,
                Content = chunk.Content,
                Source = chunk.Source,
                DocumentType = chunk.DocumentType,
                Department = chunk.Department,
                EffectiveDate = chunk.EffectiveDate,
                Version = chunk.Version,
                IndexedOn = chunk.IndexedOn,
                Vector = chunk.Vector,
                Metadata = chunk.Metadata,
                ChunkIndex = chunk.ChunkIndex,
                TotalChunks = chunk.TotalChunks,
                Score = doc["similarity"].AsDouble
            });
        }, cancellationToken);
        
        return results;
    }
    
    // Implement other interface methods...
    
    public void Dispose()
    {
        _flushTimer?.Dispose();
        FlushPendingWritesAsync().GetAwaiter().GetResult();
        _bulkWriteSemaphore?.Dispose();
    }
}
```

#### Sprint 6.2 - Deployment and Monitoring (Days 4-6)

Now let's prepare for production deployment with proper configuration and monitoring:

```yaml
# docker/docker-compose.prod.yml
version: '3.8'

services:
  mongodb:
    image: mongo:7.0
    container_name: mcprag_mongodb_prod
    restart: always
    ports:
      - "27017:27017"
    environment:
      MONGO_INITDB_ROOT_USERNAME_FILE: /run/secrets/mongo_root_username
      MONGO_INITDB_ROOT_PASSWORD_FILE: /run/secrets/mongo_root_password
      MONGO_INITDB_DATABASE: mcprag
    volumes:
      - mongo_data:/data/db
      - ./mongo-init:/docker-entrypoint-initdb.d:ro
    secrets:
      - mongo_root_username
      - mongo_root_password
    deploy:
      resources:
        limits:
          memory: 4G
        reservations:
          memory: 2G
    healthcheck:
      test: echo 'db.runCommand("ping").ok' | mongosh localhost:27017/mcprag --quiet
      interval: 30s
      timeout: 10s
      retries: 3

  mcprag_api:
    image: mcprag/api:latest
    container_name: mcprag_api_prod
    restart: always
    ports:
      - "5000:80"
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      ConnectionStrings__MongoDB: mongodb://mcpapp:${MONGO_APP_PASSWORD}@mongodb:27017/mcprag
      Ollama__BaseUrl: ${OLLAMA_URL}
      Confluence__BaseUrl: ${CONFLUENCE_URL}
      Jira__BaseUrl: ${JIRA_URL}
    depends_on:
      mongodb:
        condition: service_healthy
    deploy:
      resources:
        limits:
          memory: 2G
        reservations:
          memory: 1G
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost/health"]
      interval: 30s
      timeout: 10s
      retries: 3

  mcprag_client:
    image: mcprag/client:latest
    container_name: mcprag_client_prod
    restart: always
    ports:
      - "80:80"
    environment:
      API_URL: http://mcprag_api:80
    deploy:
      resources:
        limits:
          memory: 512M

  # Monitoring stack
  prometheus:
    image: prom/prometheus:latest
    container_name: mcprag_prometheus
    restart: always
    ports:
      - "9090:9090"
    volumes:
      - ./monitoring/prometheus.yml:/etc/prometheus/prometheus.yml:ro
      - prometheus_data:/prometheus
    command:
      - '--config.file=/etc/prometheus/prometheus.yml'
      - '--storage.tsdb.path=/prometheus'

  grafana:
    image: grafana/grafana:latest
    container_name: mcprag_grafana
    restart: always
    ports:
      - "3000:3000"
    environment:
      GF_SECURITY_ADMIN_PASSWORD_FILE: /run/secrets/grafana_admin_password
      GF_INSTALL_PLUGINS: grafana-clock-panel
    volumes:
      - grafana_data:/var/lib/grafana
      - ./monitoring/grafana/dashboards:/etc/grafana/provisioning/dashboards:ro
      - ./monitoring/grafana/datasources:/etc/grafana/provisioning/datasources:ro
    secrets:
      - grafana_admin_password

volumes:
  mongo_data:
  prometheus_data:
  grafana_data:

secrets:
  mongo_root_username:
    file: ./secrets/mongo_root_username.txt
  mongo_root_password:
    file: ./secrets/mongo_root_password.txt
  grafana_admin_password:
    file: ./secrets/grafana_admin_password.txt

networks:
  default:
    name: mcprag_network
```

Create deployment scripts:

```bash
#!/bin/bash
# scripts/deploy.sh

set -e

echo "MCP-RAG Banking System Deployment Script"
echo "========================================"

# Check environment
if [ "$1" != "dev" ] && [ "$1" != "prod" ]; then
    echo "Usage: ./deploy.sh [dev|prod]"
    exit 1
fi

ENVIRONMENT=$1
echo "Deploying to: $ENVIRONMENT"

# Build images
echo "Building Docker images..."
docker build -t mcprag/api:latest -f src/McpServer.Api/Dockerfile .
docker build -t mcprag/client:latest -f src/McpServer.Client/Dockerfile .

if [ "$ENVIRONMENT" == "prod" ]; then
    # Production deployment
    echo "Starting production deployment..."
    
    # Create secrets if they don't exist
    if [ ! -f "./secrets/mongo_root_password.txt" ]; then
        echo "Generating secure passwords..."
        mkdir -p ./secrets
        openssl rand -base64 32 > ./secrets/mongo_root_password.txt
        openssl rand -base64 32 > ./secrets/mongo_app_password.txt
        openssl rand -base64 32 > ./secrets/grafana_admin_password.txt
    fi
    
    # Export environment variables
    export MONGO_APP_PASSWORD=$(cat ./secrets/mongo_app_password.txt)
    export OLLAMA_URL=${OLLAMA_URL:-"http://ollama:11434"}
    export CONFLUENCE_URL=${CONFLUENCE_URL:-"http://confluence:8090"}
    export JIRA_URL=${JIRA_URL:-"http://jira:8080"}
    
    # Deploy with production compose file
    docker compose -f docker/docker-compose.prod.yml up -d
    
    echo "Waiting for services to be healthy..."
    sleep 30
    
    # Run database migrations/setup
    echo "Initializing database..."
    docker exec mcprag_api_prod dotnet McpServer.Api.dll --migrate
    
else
    # Development deployment
    echo "Starting development deployment..."
    docker compose -f docker/docker-compose.yml up -d
fi

echo "Deployment complete!"
echo ""
echo "Access the application at:"
echo "- Client: http://localhost"
echo "- API: http://localhost:5000"
echo "- API Docs: http://localhost:5000/swagger"

if [ "$ENVIRONMENT" == "prod" ]; then
    echo "- Prometheus: http://localhost:9090"
    echo "- Grafana: http://localhost:3000 (admin/$(cat ./secrets/grafana_admin_password.txt))"
fi
```

Create health check script:

```bash
#!/bin/bash
# scripts/health-check.sh

echo "MCP-RAG System Health Check"
echo "=========================="

# Function to check service health
check_service() {
    local name=$1
    local url=$2
    
    if curl -f -s "$url" > /dev/null; then
        echo "✓ $name: Healthy"
        return 0
    else
        echo "✗ $name: Unhealthy"
        return 1
    fi
}

# Check all services
ERRORS=0

check_service "API Health" "http://localhost:5000/health" || ((ERRORS++))
check_service "Client" "http://localhost" || ((ERRORS++))
check_service "MongoDB" "http://localhost:27017" || ((ERRORS++))
check_service "Ollama" "http://localhost:11434/api/tags" || ((ERRORS++))

if [ "$1" == "--full" ]; then
    check_service "Confluence" "http://localhost:8090" || ((ERRORS++))
    check_service "Jira" "http://localhost:8080" || ((ERRORS++))
    check_service "Prometheus" "http://localhost:9090/-/healthy" || ((ERRORS++))
    check_service "Grafana" "http://localhost:3000/api/health" || ((ERRORS++))
fi

echo ""
if [ $ERRORS -eq 0 ]; then
    echo "All services are healthy!"
    exit 0
else
    echo "$ERRORS service(s) are unhealthy"
    exit 1
fi
```

### Final Testing Checklist

Before going to production, ensure you've tested:

1. **Functional Testing**
   - [ ] Document ingestion from all sources
   - [ ] Query processing and response generation
   - [ ] Source citation accuracy
   - [ ] Filter functionality
   - [ ] Export capabilities

2. **Performance Testing**
   - [ ] Load testing with concurrent users
   - [ ] Memory usage under sustained load
   - [ ] Response time benchmarks
   - [ ] Vector search performance

3. **Security Testing**
   - [ ] Input validation
   - [ ] Rate limiting effectiveness
   - [ ] Authentication (if implemented)
   - [ ] CORS configuration

4. **Integration Testing**
   - [ ] Jira connectivity
   - [ ] Confluence connectivity
   - [ ] MongoDB failover
   - [ ] LLM service resilience

5. **Operational Testing**
   - [ ] Backup and restore procedures
   - [ ] Log aggregation
   - [ ] Monitoring alerts
   - [ ] Deployment rollback

### Deliverables for Iteration 6

By the end of this iteration, you'll have:
1. **Performance optimizations** including caching and connection pooling
2. **Production-ready Docker deployment** with security best practices
3. **Monitoring stack** with Prometheus and Grafana
4. **Deployment scripts** for automated deployment
5. **Health check scripts** for operational monitoring
6. **Comprehensive testing checklist** completed
7. **Documentation** for operations team

## Conclusion

This comprehensive development plan provides a structured approach to building an MCP-RAG system for banking reference data. The iterative approach ensures that you have a working system at each stage while progressively adding sophistication.

### Key Success Factors:
1. **Resource Management**: Careful allocation of your 32GB RAM across all services
2. **Banking Domain Focus**: Specialized parsing and understanding of financial documents
3. **Scalability**: Architecture that works locally but can scale to production
4. **Maintainability**: Clean separation of concerns and comprehensive logging
5. **User Experience**: Professional interface appropriate for banking environments

### Next Steps:
1. Begin with Iteration 1 to establish your foundation
2. Test thoroughly at each iteration before proceeding
3. Gather feedback from potential users early
4. Plan for security enhancements before production deployment
5. Consider adding authentication and authorization for corporate use

The system you're building will significantly improve how banking teams access and utilize reference data, making compliance and operational tasks more efficient.