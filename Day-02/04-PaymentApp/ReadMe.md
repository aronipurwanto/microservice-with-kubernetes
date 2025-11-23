# Create solution and directory structure
dotnet new sln -n PaymentSolution
mkdir PaymentApp
cd PaymentApp

# Create projects with .NET 10.0
dotnet new webapi -n PaymentApi -f net10.0
dotnet new classlib -n PaymentCore -f net10.0
dotnet new classlib -n PaymentModels -f net10.0
dotnet new classlib -n PaymentData -f net10.0
dotnet new xunit -n PaymentTests -f net10.0

# Add projects to solution
dotnet sln add PaymentApi/PaymentApi.csproj
dotnet sln add PaymentCore/PaymentCore.csproj
dotnet sln add PaymentModels/PaymentModels.csproj
dotnet sln add PaymentData/PaymentData.csproj
dotnet sln add PaymentTests/PaymentTests.csproj

# Add project references
dotnet add PaymentApi/PaymentApi.csproj reference PaymentCore/PaymentCore.csproj
dotnet add PaymentApi/PaymentApi.csproj reference PaymentModels/PaymentModels.csproj
dotnet add PaymentApi/PaymentApi.csproj reference PaymentData/PaymentData.csproj
dotnet add PaymentCore/PaymentCore.csproj reference PaymentModels/PaymentModels.csproj
dotnet add PaymentCore/PaymentCore.csproj reference PaymentData/PaymentData.csproj
dotnet add PaymentTests/PaymentTests.csproj reference PaymentApi/PaymentApi.csproj
dotnet add PaymentTests/PaymentTests.csproj reference PaymentCore/PaymentCore.csproj
dotnet add PaymentTests/PaymentTests.csproj reference PaymentModels/PaymentModels.csproj
# Hands-on Lab: Deploying a Sample Payment Application with .NET 10.0, Docker, and Rider

## Lab Overview
In this hands-on lab, you'll build and deploy a complete payment processing application using .NET 10.0, C#, Docker, and JetBrains Rider. You'll learn how to create a multi-project solution, implement payment processing logic, containerize the application, and deploy it using Docker.

## Prerequisites
- .NET 10.0 SDK installed
- JetBrains Rider IDE
- Docker Desktop running
- Git installed
- Basic knowledge of C# and .NET

---

## Step-by-Step Instructions

### Step 1: Create the Project Structure

#### 1.1 Create Solution and Projects
Open terminal and execute the following commands:

```bash
# Create solution and directory structure
dotnet new sln -n PaymentSolution
mkdir PaymentApp
cd PaymentApp

# Create projects with .NET 10.0
dotnet new webapi -n PaymentApi -f net10.0
dotnet new classlib -n PaymentCore -f net10.0
dotnet new classlib -n PaymentModels -f net10.0
dotnet new classlib -n PaymentData -f net10.0
dotnet new xunit -n PaymentTests -f net10.0

# Add projects to solution
dotnet sln add PaymentApi/PaymentApi.csproj
dotnet sln add PaymentCore/PaymentCore.csproj
dotnet sln add PaymentModels/PaymentModels.csproj
dotnet sln add PaymentData/PaymentData.csproj
dotnet sln add PaymentTests/PaymentTests.csproj

# Add project references
dotnet add PaymentApi/PaymentApi.csproj reference PaymentCore/PaymentCore.csproj
dotnet add PaymentApi/PaymentApi.csproj reference PaymentModels/PaymentModels.csproj
dotnet add PaymentApi/PaymentApi.csproj reference PaymentData/PaymentData.csproj
dotnet add PaymentCore/PaymentCore.csproj reference PaymentModels/PaymentModels.csproj
dotnet add PaymentCore/PaymentCore.csproj reference PaymentData/PaymentData.csproj
dotnet add PaymentTests/PaymentTests.csproj reference PaymentApi/PaymentApi.csproj
dotnet add PaymentTests/PaymentTests.csproj reference PaymentCore/PaymentCore.csproj
dotnet add PaymentTests/PaymentTests.csproj reference PaymentModels/PaymentModels.csproj
```

### Step 2: Define the Models

#### 2.1 Create Payment Models
In the PaymentModels project, create the following files:
- PaymentRequest.cs
- PaymentResponse.cs
- PaymentResult.cs
- Transaction.cs

### Step 3: Create Data Layer

#### 3.1 Create Database Context
In the PaymentData project, create:
- PaymentDbContext.cs

#### 3.2 Create Repository Pattern
In the PaymentData project, create:
- ITransactionRepository.cs
- TransactionRepository.cs

### Step 4: Create Payment Service

#### 4.1 Create Service Interface
In the PaymentCore project, create:
- IPaymentService.cs

#### 4.2 Implement Payment Service
In the PaymentCore project, create:
- PaymentService.cs

### Step 5: Create API Controllers

#### 5.1 Create Payments Controller
In the PaymentApi/Controllers folder, create:
- PaymentsController.cs

#### 5.2 Create Health Check Controller
In the PaymentApi/Controllers folder, create:
- HealthController.cs

### Step 6: Configure the Application

#### 6.1 Configure Program.cs
Update the PaymentApi/Program.cs file with service registrations and middleware configuration.

#### 6.2 Configure App Settings
Update the PaymentApi/appsettings.json file with connection strings and logging configuration.

### Step 7: Create Docker Configuration

#### 7.1 Create Dockerfile
In the PaymentApi project directory, create Dockerfile.

#### 7.2 Create Docker Compose File
In the solution root, create docker-compose.yml.

#### 7.3 Create Docker Ignore File
In the solution root, create .dockerignore.

### Step 8: Create Unit Tests

#### 8.1 Create Payment Service Tests
In PaymentTests project, create PaymentServiceTests.cs.

### Step 9: Build and Run with Docker

#### 9.1 Build the Docker Image
```bash
# Build the image
docker build -t payment-api -f PaymentApi/Dockerfile .

# Or use docker-compose
docker-compose build
```

#### 9.2 Run the Application
```bash
# Run with docker-compose
docker-compose up -d

# Check running containers
docker ps

# View logs
docker-compose logs -f payment-api
```

#### 9.3 Run Tests
```bash
# Run unit tests
dotnet test

# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage"
```

### Step 10: Test the Application

#### 10.1 Using Swagger UI
Open your browser and navigate to:
```
http://localhost:8080/swagger
```

#### 10.2 Test API Endpoints
```bash
# Health check
curl http://localhost:8080/health

# Process a payment
curl -X POST "http://localhost:8080/api/payments" \
-H "Content-Type: application/json" \
-d '{
  "cardNumber": "4111111111111111",
  "cardHolderName": "John Doe",
  "expiryMonth": 12,
  "expiryYear": 2025,
  "cvv": "123",
  "amount": 100.00,
  "currency": "USD",
  "merchantId": "TEST_MERCHANT_001"
}'

# Get transaction by ID
curl "http://localhost:8080/api/payments/TRANSACTION_ID"

# Get merchant transactions
curl "http://localhost:8080/api/payments/merchant/TEST_MERCHANT_001"
```

### Step 11: Clean Up

```bash
# Stop and remove containers
docker-compose down

# Remove volumes
docker-compose down -v

# Remove images
docker rmi payment-api
```

## Project Structure
```
PaymentSolution/
├── PaymentApi/ (Web API project)
├── PaymentCore/ (Business logic)
├── PaymentModels/ (Data models)
├── PaymentData/ (Data access layer)
├── PaymentTests/ (Unit tests)
├── docker-compose.yml
└── PaymentSolution.sln
```

## Next Steps for Enhancement
- Add authentication and authorization
- Implement real payment gateway integration
- Add retry policies and circuit breakers
- Set up application monitoring and logging
- Create CI/CD pipeline
- Add database migrations
- Implement rate limiting and security features

## Troubleshooting
- Ensure Docker Desktop is running before building
- Verify .NET 10.0 SDK is properly installed
- Check port conflicts if application fails to start
- Review Docker logs for runtime issues

This lab provides a complete foundation for building and deploying a payment processing application using modern development practices and containerization.
