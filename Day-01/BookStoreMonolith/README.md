# Microservices Bookstore Solution

## 📋 Overview

A comprehensive microservices-based e-commerce solution built with .NET 8, featuring independently deployable services for user management, product catalog, order processing, and payment handling.

## 🏗️ Architecture

This solution follows microservices architecture principles with:
- **Independent Services**: Each service owns its data and business logic
- **Database per Service**: Isolated data storage for each domain
- **Containerized Deployment**: Docker support for all services
- **API-First Design**: RESTful APIs with Swagger documentation
- **Health Monitoring**: Built-in health checks for service reliability

## 📁 Solution Structure

```
BookstoreMicroservices/
├── UserService/           # User management service
├── ProductService/        # Product catalog service  
├── OrderService/          # Order processing service
├── PaymentService/        # Payment processing service
├── SharedModels/          # Shared DTOs and models
└── docker-compose.yml     # Container orchestration
```

## 🚀 Services

### 1. UserService (`http://localhost:5001`)
**Responsibilities**: User registration, authentication, profile management
- **Port**: 5001
- **Database**: UserService
- **Key Endpoints**:
  - `GET /api/users` - List all users
  - `POST /api/users` - Create new user
  - `GET /api/users/{id}` - Get user by ID
  - `PUT /api/users/{id}` - Update user
  - `DELETE /api/users/{id}` - Delete user

### 2. ProductService (`http://localhost:5002`)
**Responsibilities**: Product catalog, inventory management, categories
- **Port**: 5002
- **Database**: ProductService
- **Key Endpoints**:
  - `GET /api/products` - List all products
  - `POST /api/products` - Create new product
  - `GET /api/products/{id}` - Get product by ID
  - `GET /api/categories` - List product categories

### 3. OrderService (`http://localhost:5003`)
**Responsibilities**: Order processing, order items, order status management
- **Port**: 5003
- **Database**: OrderService
- **Key Endpoints**:
  - `GET /api/orders` - List orders (with pagination)
  - `POST /api/orders` - Create new order
  - `GET /api/orders/{id}` - Get order details
  - `PUT /api/orders/{id}/status` - Update order status
  - `POST /api/orders/{id}/cancel` - Cancel order
  - `GET /api/orders/user/{userId}` - Get user orders

### 4. PaymentService (`http://localhost:5004`)
**Responsibilities**: Payment processing, transaction management, refunds
- **Port**: 5004
- **Database**: PaymentService
- **Key Endpoints**:
  - `POST /api/payments` - Process payment
  - `GET /api/payments/{id}` - Get payment details
  - `POST /api/payments/{id}/refund` - Process refund
  - `GET /api/payments/order/{orderId}` - Get payments by order
  - `PUT /api/payments/{id}/status` - Update payment status

## 🛠️ Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [SQL Server](https://www.microsoft.com/en-us/sql-server/sql-server-downloads) (or LocalDB)
- [Docker](https://www.docker.com/products/docker-desktop) (optional)

## 🏃‍♂️ Quick Start

### Option 1: Run with Docker (Recommended)

```bash
# Build and start all services
docker-compose build
docker-compose up -d

# Check service status
docker-compose ps

# View logs for a specific service
docker-compose logs user-service
docker-compose logs product-service
docker-compose logs order-service
docker-compose logs payment-service
```

### Option 2: Run Locally with .NET

```bash
# Restore dependencies
dotnet restore

# Build solution
dotnet build

# Run services individually (in separate terminals)
dotnet run --project UserService
dotnet run --project ProductService  
dotnet run --project OrderService
dotnet run --project PaymentService
```

## 🧪 Testing the Services

### Health Checks
```bash
curl http://localhost:5001/health
curl http://localhost:5002/health
curl http://localhost:5003/health
curl http://localhost:5004/health
```

### Sample API Calls

**Create a User:**
```bash
curl -X POST http://localhost:5001/api/users \
  -H "Content-Type: application/json" \
  -d '{
    "email": "john.doe@example.com",
    "firstName": "John",
    "lastName": "Doe",
    "password": "securepassword123"
  }'
```

**Create a Product:**
```bash
curl -X POST http://localhost:5002/api/products \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Microservices Architecture Book",
    "description": "Learn to build scalable microservices",
    "price": 49.99,
    "stockQuantity": 100
  }'
```

**Create an Order:**
```bash
curl -X POST http://localhost:5003/api/orders \
  -H "Content-Type: application/json" \
  -d '{
    "userId": "user-guid-here",
    "shippingAddress": "123 Main St, City, Country",
    "orderItems": [
      {
        "productId": "product-guid-here",
        "productName": "Microservices Architecture Book",
        "quantity": 2,
        "unitPrice": 49.99
      }
    ]
  }'
```

**Process a Payment:**
```bash
curl -X POST http://localhost:5004/api/payments \
  -H "Content-Type: application/json" \
  -d '{
    "orderId": "order-guid-here", 
    "amount": 99.98,
    "paymentMethod": "CreditCard",
    "cardToken": "tok_123456"
  }'
```

## 📚 API Documentation

Each service provides Swagger UI for interactive API documentation:

- **UserService**: http://localhost:5001/swagger
- **ProductService**: http://localhost:5002/swagger  
- **OrderService**: http://localhost:5003/swagger
- **PaymentService**: http://localhost:5004/swagger

## 🗃️ Database

Each service uses its own SQL Server database:
- **UserService**: `UserService` database
- **ProductService**: `ProductService` database  
- **OrderService**: `OrderService` database
- **PaymentService**: `PaymentService` database

Databases are automatically created and seeded with sample data on first run.

## 🔧 Configuration

### Connection Strings
Update connection strings in each service's `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=ServiceName;Trusted_Connection=true;TrustServerCertificate=true"
  }
}
```

### Port Configuration
Services run on predefined ports:
- UserService: 5001
- ProductService: 5002  
- OrderService: 5003
- PaymentService: 5004

## 🐳 Docker Configuration

The solution includes:
- **Individual Dockerfiles** for each service
- **docker-compose.yml** for orchestration
- **Health checks** for container monitoring
- **Network configuration** for service communication

## 📊 Monitoring

### Health Checks
All services include health check endpoints at `/health` that verify:
- Database connectivity
- Service responsiveness
- Overall service health

### Logging
Structured logging is implemented using ILogger with:
- Console output in development
- Correlation ID support
- Error tracking and monitoring

## 🚀 Deployment

### Development
```bash
docker-compose up -d
```

### Production Considerations
- Use environment-specific appsettings
- Configure proper SQL Server instances
- Set up reverse proxy (nginx/Traefik)
- Implement service discovery
- Configure logging and monitoring

## 🔄 Development Workflow

1. **Make code changes** in respective services
2. **Rebuild containers**: `docker-compose build`
3. **Restart services**: `docker-compose up -d`
4. **Test APIs** via Swagger or curl commands
5. **Check logs** for debugging: `docker-compose logs [service]`

## 🛡️ Security Features

- CORS configuration for cross-origin requests
- Input validation with Data Annotations
- SQL injection protection via Entity Framework
- Secure configuration management

## 📈 Scalability

Each service can be scaled independently:
```bash
# Scale OrderService to 3 instances
docker-compose up -d --scale order-service=3
```

## 🧹 Cleanup

```bash
# Stop and remove containers
docker-compose down

# Remove volumes (including databases)
docker-compose down -v

# Remove all images
docker-compose down --rmi all
```

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Add tests if applicable
5. Submit a pull request

## 📄 License

This project is licensed under the MIT License.

## 🆘 Support

For issues and questions:
1. Check service logs: `docker-compose logs [service]`
2. Verify database connectivity
3. Test health endpoints
4. Check port availability

---

**Happy Coding!** 
