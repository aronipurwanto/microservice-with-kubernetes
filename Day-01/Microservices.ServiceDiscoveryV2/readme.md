# Microservices Service Discovery Demo

A demonstration of microservices architecture with service discovery and API Gateway pattern using .NET 8 and C#.

## 📋 Overview

This solution demonstrates a simple microservices architecture with:
- **Product Service** - Manages product catalog
- **Order Service** - Handles order processing  
- **Service Discovery API** - Acts as API Gateway with service registry
- **Shared Models** - Common DTOs shared between services

## 🏗️ Architecture

```
┌─────────────────┐     ┌──────────────────┐     ┌─────────────────┐
│   Product       │     │ Service Discovery│     │    Order        │
│   Service       │     │ API (Gateway)    │     │   Service       │
│  (Port: 5001)   │◄───►│   (Port: 7001)   │◄───►│  (Port: 6001)   │
└─────────────────┘     └──────────────────┘     └─────────────────┘
```

## 🚀 Getting Started

### Prerequisites

- .NET 8.0 SDK
- JetBrains Rider (or any C# IDE)
- Postman or curl for API testing

### Installation & Running

1. **Clone or Download the solution**
   ```bash
   git clone <repository-url>
   cd Microservices.ServiceDiscovery.Demo
   ```

2. **Open in Rider**
   - Open Rider and select "Open Solution"
   - Navigate to the solution folder and open `Microservices.ServiceDiscovery.Demo.sln`

3. **Setup Multiple Startup Projects**
   - Right-click on the Solution → Properties
   - Select "Multiple startup projects"
   - Set all three services to "Start":
     - `ProductService`
     - `OrderService` 
     - `ServiceDiscovery.API`
   - Click OK

4. **Run the Solution**
   - Click the Run button in Rider
   - All three services will start automatically

### Alternative: Manual Startup

If you prefer to run services manually:

```bash
# Terminal 1 - Product Service
cd ProductService
dotnet run

# Terminal 2 - Order Service  
cd OrderService
dotnet run

# Terminal 3 - Service Discovery API
cd ServiceDiscovery.API
dotnet run
```

## 📡 API Endpoints

### Product Service (http://localhost:5001)
- `GET /` - Health check
- `GET /products` - Get all products
- `GET /products/{id}` - Get product by ID
- `GET /service-info` - Service discovery information

### Order Service (http://localhost:6001) 
- `GET /` - Health check
- `GET /orders` - Get all orders
- `GET /orders/{id}` - Get order by ID
- `POST /orders` - Create new order
- `GET /service-info` - Service discovery information

### Service Discovery API (http://localhost:7001)
- `GET /` - Gateway health check
- `GET /discovery/services` - List all registered services
- `GET /discovery/services/{serviceName}` - Get specific service instances
- `GET /gateway/products` - Proxy to ProductService products
- `GET /gateway/orders` - Proxy to OrderService orders  
- `GET /gateway/health` - Aggregate health check of all services
- `POST /discovery/register` - Register a new service (dynamic registration)
- `DELETE /discovery/unregister/{serviceName}/{serviceUrl}` - Unregister a service

## 🔧 Service Configuration

### Port Configuration
- **ProductService**: http://localhost:5001, https://localhost:5002
- **OrderService**: http://localhost:6001, https://localhost:6002  
- **ServiceDiscovery.API**: http://localhost:7001, https://localhost:7002

### In-Memory Service Registry
The service registry maintains a list of available services:
```json
{
  "ProductService": ["http://localhost:5001", "https://localhost:5002"],
  "OrderService": ["http://localhost:6001", "https://localhost:6002"]
}
```

## 📊 Sample Data

### Products
```json
[
  {
    "id": 1,
    "name": "Laptop",
    "price": 999.99,
    "stock": 10
  },
  {
    "id": 2, 
    "name": "Mouse",
    "price": 29.99,
    "stock": 50
  }
]
```

### Orders
```json
[
  {
    "id": 1,
    "productId": 1,
    "quantity": 1,
    "customerName": "John Doe",
    "orderDate": "2024-01-15T10:30:00"
  }
]
```

## 🧪 Testing the Application

### Test Direct Service Access
```bash
# Test Product Service
curl http://localhost:5001/products

# Test Order Service  
curl http://localhost:6001/orders

# Test Service Info
curl http://localhost:5001/service-info
```

### Test Through API Gateway
```bash
# List all registered services
curl http://localhost:7001/discovery/services

# Get products through gateway
curl http://localhost:7001/gateway/products

# Get orders through gateway  
curl http://localhost:7001/gateway/orders

# Check aggregated health
curl http://localhost:7001/gateway/health
```

### Test Service Registration
```bash
# Register a new service dynamically
curl -X POST http://localhost:7001/discovery/register \
  -H "Content-Type: application/json" \
  -d '{"serviceName": "PaymentService", "serviceUrl": "http://localhost:8001"}'
```

## 🛠️ Project Structure

```
Microservices.ServiceDiscovery.Demo/
├── ServiceDiscovery.API/
│   ├── Program.cs
│   └── appsettings.json
├── ProductService/
│   ├── Program.cs  
│   └── appsettings.json
├── OrderService/
│   ├── Program.cs
│   └── appsettings.json
└── Shared/
    └── Models/
        ├── Product.cs
        └── Order.cs
```

## 🔍 Key Features Demonstrated

- **Service Registration**: Static registration in service registry
- **Service Discovery**: Gateway discovers and routes to services
- **API Gateway Pattern**: Single entry point for clients
- **Health Monitoring**: Individual and aggregated health checks
- **Dynamic Registration**: REST endpoints for service registration
- **In-Memory Registry**: Simple service registry implementation

## 🚀 Next Steps & Enhancements

This is a basic implementation. For production use, consider:

1. **Service Discovery Tools**: Integrate with Consul, Eureka, or etcd
2. **Load Balancing**: Add client-side or server-side load balancing
3. **Circuit Breaker**: Implement resilience patterns with Polly
4. **Configuration**: Use external configuration providers
5. **Containerization**: Dockerize each service
6. **Logging & Monitoring**: Add structured logging and metrics
7. **Security**: Implement authentication and authorization
8. **Persistence**: Add database integration

## 📝 Notes

- This demo uses in-memory data storage (no database)
- Service registry is in-memory and will reset on application restart
- For dynamic registration, services should implement health checks and heartbeat mechanisms
- In production, consider using dedicated service discovery tools

## 🤝 Contributing

Feel free to extend this demo with additional features and improvements!

## 📄 License

This project is for demonstration purposes.
