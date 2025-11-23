# Create solution directory
mkdir Microservices.ServiceDiscovery.Demo
cd Microservices.ServiceDiscovery.Demo

# Create solution
dotnet new sln -n Microservices.ServiceDiscovery.Demo

# Create projects
dotnet new webapi -n ApiGateway -f net8.0
dotnet new webapi -n OrderService -f net8.0
dotnet new webapi -n UserService -f net8.0
dotnet new webapi -n InventoryService -f net8.0
dotnet new xunit -n ApiGateway.Tests -f net8.0
dotnet new xunit -n Integration.Tests -f net8.0

# Add projects to solution
dotnet sln add ApiGateway/ApiGateway.csproj
dotnet sln add OrderService/OrderService.csproj
dotnet sln add UserService/UserService.csproj
dotnet sln add InventoryService/InventoryService.csproj
dotnet sln add ApiGateway.Tests/ApiGateway.Tests.csproj
dotnet sln add Integration.Tests/Integration.Tests.csproj


# **Microservices Service Discovery Demo**

A comprehensive .NET 10 demonstration of microservices architecture with Consul-based service discovery, featuring API Gateway pattern, health checks, and resilience patterns.

## 🏗️ Architecture Overview

```
┌─────────────────┐    ┌──────────────────┐    ┌─────────────────┐
│   API Gateway   │───▶│   Consul Server  │◀───│   Order Service  │
│   (Port 5000)   │    │   (Port 8500)    │    │   (Port 5002)    │
└─────────────────┘    └──────────────────┘    └─────────────────┘
         │                       │                       │
         │                       │                       │
         ▼                       ▼                       ▼
┌─────────────────┐    ┌──────────────────┐    ┌─────────────────┐
│   Client Apps   │    │   Service Mesh   │    │   User Service   │
│                 │    │                  │    │   (Port 5003)    │
└─────────────────┘    └──────────────────┘    └─────────────────┘
```

## 🚀 Features

- **Service Discovery**: Automatic service registration and discovery using Consul
- **Health Monitoring**: Built-in health checks with Consul integration
- **Resilience Patterns**: Retry policies and circuit breakers with Polly
- **API Gateway**: Centralized entry point with service routing
- **Docker Support**: Containerized deployment with Docker Compose
- **.NET 10**: Built with the latest .NET 10 framework
- **OpenAPI**: Swagger documentation for all APIs

## 📋 Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop)
- [JetBrains Rider](https://www.jetbrains.com/rider/) or [Visual Studio 2022](https://visualstudio.microsoft.com/)
- [Git](https://git-scm.com/)

## 🛠️ Technologies Used

- **Framework**: .NET 10 (ASP.NET Core)
- **Service Discovery**: HashiCorp Consul
- **Resilience**: Polly with HTTP policies
- **Containerization**: Docker & Docker Compose
- **API Documentation**: Swagger/OpenAPI
- **Testing**: xUnit (Test projects included)

## 📁 Project Structure

```
Microservices.ServiceDiscovery.Demo/
├── 📁 src/
│   ├── 📁 services/
│   │   ├── 📁 ApiGateway/                 # Main API Gateway service
│   │   │   ├── Controllers/
│   │   │   ├── Program.cs
│   │   │   ├── appsettings.json
│   │   │   └── Dockerfile
│   │   ├── 📁 OrderService/               # Order management service
│   │   │   ├── Controllers/
│   │   │   ├── Program.cs
│   │   │   ├── appsettings.json
│   │   │   └── Dockerfile
│   │   ├── 📁 UserService/                # User management service
│   │   │   ├── Controllers/
│   │   │   ├── Program.cs
│   │   │   ├── appsettings.json
│   │   │   └── Dockerfile
│   │   └── 📁 InventoryService/           # Inventory management service
│   │       ├── Controllers/
│   │       ├── Program.cs
│   │       ├── appsettings.json
│   │       └── Dockerfile
│   └── 📁 infrastructure/
│       └── docker-compose.yml            # Docker Compose for all services
├── 📁 tests/
│   ├── 📁 ApiGateway.Tests/              # Unit tests for API Gateway
│   ├── 📁 OrderService.Tests/            # Unit tests for Order Service
│   └── 📁 Integration.Tests/             # Integration tests
├── 📁 docs/                              # Additional documentation
├── README.md
└── Microservices.ServiceDiscovery.Demo.sln
```

## 🏃‍♂️ Quick Start

### Option 1: Run with Docker Compose (Recommended)

```bash
# Clone the repository
git clone <repository-url>
cd Microservices.ServiceDiscovery.Demo

# Start all services
docker-compose -f src/infrastructure/docker-compose.yml up -d

# Check running services
docker ps
```

### Option 2: Run Manually with .NET CLI

```bash
# Start Consul (requires Docker)
docker run -d --name=consul -p 8500:8500 -p 8600:8600/udp consul agent -server -ui -node=server-1 -bootstrap-expect=1 -client=0.0.0.0

# Terminal 1 - Order Service
cd src/services/OrderService
dotnet run --urls="http://localhost:5002"

# Terminal 2 - User Service  
cd src/services/UserService
dotnet run --urls="http://localhost:5003"

# Terminal 3 - API Gateway
cd src/services/ApiGateway
dotnet run --urls="http://localhost:5000"
```

### Option 3: Run with Rider

1. Open the solution in JetBrains Rider
2. Set multiple startup projects:
   - `ApiGateway`
   - `OrderService` 
   - `UserService`
3. Run the solution (F5 or click Run)

## 🌐 Access Points

| Service | URL | Description |
|---------|-----|-------------|
| **Consul UI** | http://localhost:8500 | Service discovery dashboard |
| **API Gateway** | http://localhost:5000 | Main entry point |
| **Order Service** | http://localhost:5002 | Direct order service access |
| **User Service** | http://localhost:5003 | Direct user service access |
| **API Documentation** | http://localhost:5000/swagger | Swagger UI for API Gateway |

## 📡 API Endpoints

### Service Discovery Endpoints (API Gateway)

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/service-discovery/services` | List all registered services |
| `GET` | `/service-discovery/services/{serviceName}` | Get instances of a specific service |
| `GET` | `/service-discovery/healthy-services` | Get only healthy service instances |
| `GET` | `/service-discovery/manual-call/{serviceName}` | Manually call a service via discovery |
| `GET` | `/service-discovery/load-balance/{serviceName}` | Demo load balancing across instances |

### Order Service Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/api/orders` | Get all orders |
| `GET` | `/api/orders/{id}` | Get order by ID |
| `POST` | `/api/orders` | Create new order |
| `GET` | `/api/orders/customer/{customerId}` | Get customer orders |
| `GET` | `/health` | Health check endpoint |

### Health Check Endpoints

All services expose health checks at `/health` that are monitored by Consul.

## 🔧 Configuration

### Consul Configuration

All services are configured to connect to Consul via `appsettings.json`:

```json
{
  "Consul": {
    "Url": "http://localhost:8500"
  },
  "Service": {
    "Port": 5002,
    "Name": "order-service"
  }
}
```

### Resilience Configuration

The API Gateway uses Polly for resilience:

- **Retry Policy**: 3 retries with exponential backoff
- **Circuit Breaker**: Opens after 3 failures, resets after 60 seconds

## 🧪 Testing the Setup

### 1. Verify Service Registration

```bash
# Check registered services in Consul
curl http://localhost:8500/v1/agent/services

# Or use the API Gateway endpoint
curl http://localhost:5000/service-discovery/services
```

### 2. Test Service Discovery

```bash
# Manual service call through discovery
curl http://localhost:5000/service-discovery/manual-call/order-service

# Load balancing demo
curl http://localhost:5000/service-discovery/load-balance/order-service
```

### 3. Test Health Checks

```bash
# Check API Gateway health
curl http://localhost:5000/health

# Check Order Service health directly
curl http://localhost:5002/health

# Check detailed health
curl http://localhost:5002/health/detailed
```

### 4. Test Order Service API

```bash
# Get all orders
curl http://localhost:5000/service-discovery/manual-call/order-service

# Create a new order
curl -X POST http://localhost:5002/api/orders \
  -H "Content-Type: application/json" \
  -d '{"customerId": "customer-123", "totalAmount": 199.99}'
```

## 🐳 Docker Commands

### Build and Run Individual Services

```bash
# Build Order Service
docker build -f src/services/OrderService/Dockerfile -t order-service:latest .

# Run Order Service
docker run -d -p 5002:80 --name order-service order-service:latest
```

### Scale Services

```bash
# Scale Order Service to 3 instances
docker-compose up --scale order-service=3
```

### View Logs

```bash
# View all logs
docker-compose logs

# View specific service logs
docker-compose logs order-service

# Follow logs in real-time
docker-compose logs -f api-gateway
```

### Stop Services

```bash
# Stop all services
docker-compose down

# Stop and remove volumes
docker-compose down -v
```

## 🔍 Monitoring and Debugging

### Consul UI Features

1. **Services Tab**: View all registered services and their health status
2. **Nodes Tab**: See the Consul cluster nodes
3. **Key/Value Tab**: Access Consul's key-value store
4. **Health Checks**: Monitor service health check status

### Logging

All services use structured logging with different log levels:

- **Development**: Detailed logging
- **Production**: Warning and above only

### Health Check Monitoring

Consul automatically monitors services and removes unhealthy instances from the registry.

## 🚀 Deployment

### Production Considerations

1. **Consul Cluster**: Use a multi-node Consul cluster for production
2. **Security**: Enable ACLs and TLS in Consul
3. **Monitoring**: Integrate with monitoring solutions (Prometheus, Grafana)
4. **Load Balancing**: Use dedicated load balancers for production traffic
5. **Secrets Management**: Use HashiCorp Vault for secrets

### Environment Variables

Key environment variables for configuration:

```bash
ASPNETCORE_ENVIRONMENT=Production
Consul__Url=http://consul-server:8500
Service__Name=order-service
Service__Port=80
```

## 🛠️ Development

### Adding a New Service

1. Create new Web API project in `src/services/`
2. Add Consul client and health checks
3. Configure service registration in `Program.cs`
4. Update `docker-compose.yml`
5. Add to solution file

### Code Structure Guidelines

- Keep services loosely coupled
- Use async/await for all I/O operations
- Implement proper error handling
- Follow RESTful API conventions
- Use structured logging

### Testing

```bash
# Run unit tests
dotnet test

# Run specific test project
dotnet test tests/ApiGateway.Tests/

# Run with coverage (if configured)
dotnet test --collect:"XPlat Code Coverage"
```

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Add tests
5. Submit a pull request

## 📝 License

This project is licensed under the MIT License - see the LICENSE file for details.

## 🙏 Acknowledgments

- [HashiCorp Consul](https://www.consul.io/) for service discovery
- [.NET Team](https://dotnet.microsoft.com/) for ASP.NET Core
- [Polly](https://github.com/App-vNext/Polly) for resilience patterns
- [JetBrains](https://www.jetbrains.com/rider/) for excellent IDE support

## 📞 Support

For issues and questions:

1. Check the [Consul documentation](https://www.consul.io/docs)
2. Review [.NET 10 documentation](https://docs.microsoft.com/en-us/dotnet/core/)
3. Create an issue in the project repository

---

**Happy Coding!** 🎉
