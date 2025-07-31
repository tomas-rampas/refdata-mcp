#!/bin/bash

# Wait for Ollama to be ready
echo "Waiting for Ollama to start..."
until curl -s http://localhost:11434/api/version > /dev/null; do
    sleep 2
done

echo "Ollama is ready. Pulling phi3.5 model..."

# Pull the phi3.5 model for banking document processing
curl -X POST http://localhost:11434/api/pull -d '{
  "name": "phi3.5"
}'

echo "Model pull initiated. This may take several minutes..."

# Check if model is available
while true; do
    if curl -s http://localhost:11434/api/tags | grep -q "phi3.5"; then
        echo "Model phi3.5 successfully pulled!"
        break
    fi
    sleep 5
done

echo "Ollama setup complete!"