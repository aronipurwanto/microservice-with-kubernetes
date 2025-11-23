# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project file and restore as distinct layers
COPY ["src/services/InventoryService/InventoryService.csproj", "src/services/InventoryService/"]
RUN dotnet restore "src/services/InventoryService/InventoryService.csproj"

# Copy everything else and build
COPY . .
WORKDIR "/src/src/services/InventoryService"
RUN dotnet build "InventoryService.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "InventoryService.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Install curl for health checks
RUN apt-get update && \
    apt-get install -y curl && \
    rm -rf /var/lib/apt/lists/*

# Create a non-root user for security
RUN groupadd -r appgroup && \
    useradd -r -g appgroup appuser && \
    chown -R appuser:appgroup /app

# Switch to non-root user
USER appuser

# Copy published application
COPY --from=publish --chown=appuser:appgroup /app/publish .

# Expose port
EXPOSE 80

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD curl -f http://localhost/health || exit 1

# Entry point
ENTRYPOINT ["dotnet", "InventoryService.dll"]