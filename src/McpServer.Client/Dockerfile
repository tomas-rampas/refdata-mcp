# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project file
COPY ["McpServer.Client/McpServer.Client.csproj", "McpServer.Client/"]

# Restore dependencies
RUN dotnet restore "McpServer.Client/McpServer.Client.csproj"

# Copy everything else
COPY McpServer.Client/ McpServer.Client/

# Build the application
WORKDIR "/src/McpServer.Client"
RUN dotnet build "McpServer.Client.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "McpServer.Client.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Nginx stage for serving static files
FROM nginx:alpine AS final
WORKDIR /usr/share/nginx/html

# Remove default nginx static files
RUN rm -rf ./*

# Copy published wwwroot files from build stage
COPY --from=publish /app/publish/wwwroot .

# Copy nginx configuration
COPY McpServer.Client/nginx.conf /etc/nginx/nginx.conf

# Expose port 80
EXPOSE 80

# Start nginx
CMD ["nginx", "-g", "daemon off;"]