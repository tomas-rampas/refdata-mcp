# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files
COPY ["McpServer.Api/McpServer.Api.csproj", "McpServer.Api/"]
COPY ["McpServer.Application/McpServer.Application.csproj", "McpServer.Application/"]
COPY ["McpServer.Infrastructure/McpServer.Infrastructure.csproj", "McpServer.Infrastructure/"]
COPY ["McpServer.Core/McpServer.Core.csproj", "McpServer.Core/"]

# Restore dependencies
RUN dotnet restore "McpServer.Api/McpServer.Api.csproj"

# Copy everything else
COPY . .

# Build the application
WORKDIR "/src/McpServer.Api"
RUN dotnet build "McpServer.Api.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "McpServer.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Final stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 80
EXPOSE 443

# Copy published files
COPY --from=publish /app/publish .

# Set environment variables
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:80

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD curl -f http://localhost:80/health || exit 1

# Run the application
ENTRYPOINT ["dotnet", "McpServer.Api.dll"]