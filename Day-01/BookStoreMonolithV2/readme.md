# BookStoreMonolith

A monolithic e-commerce book store application built with .NET 10.0 and Web API MVC pattern. This solution demonstrates a modular monolithic architecture with separate service boundaries for Orders, Payments, Users, and Products.

## 🏗️ Architecture Overview

```
BookStoreMonolith/
├── BookStoreMonolith.API/     # Main API Gateway & Composition
├── OrderService/              # Order management & processing
├── PaymentService/            # Payment processing & transactions
├── UserService/               # User management & profiles
├── ProductService/            # Product & category management
└── SharedModels/              # Shared DTOs and domain models
```

## 🚀 Features

- **Order Management**: Create, track, and manage book orders
- **Payment Processing**: Handle payments, refunds, and transactions
- **User Management**: User registration and profile management
- **Product Catalog**: Book inventory and category management
- **RESTful APIs**: Clean API design with proper HTTP verbs
- **In-Memory Storage**: No database required for development
- **Swagger Documentation**: Auto-generated API documentation

## 🛠️ Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or later
- Visual Studio 2022, VS Code, or any .NET-supported IDE
- Git for version control

## 📋 Step-by-Step Setup Instructions

### Step 1: Create Solution Structure

```bash
# Create root directory
mkdir BookStoreMonolith
cd BookStoreMonolith

# Create solution file
dotnet new sln -n BookStoreMonolith

# Create projects
dotnet new webapi -n BookStoreMonolith.API
dotnet new classlib -n OrderService
dotnet new classlib -n PaymentService
dotnet new classlib -n UserService
dotnet new classlib -n ProductService
dotnet new classlib -n SharedModels
```

### Step 2: Add Projects to Solution

```bash
dotnet sln add BookStoreMonolith.API/BookStoreMonolith.API.csproj
dotnet sln add OrderService/OrderService.csproj
dotnet sln add PaymentService/PaymentService.csproj
dotnet sln add UserService/UserService.csproj
dotnet sln add ProductService/ProductService.csproj
dotnet sln add SharedModels/SharedModels.csproj
```

### Step 3: Configure Project Dependencies

```bash
# OrderService dependencies
cd OrderService
dotnet add reference ../SharedModels/SharedModels.csproj
cd ..

# PaymentService dependencies  
cd PaymentService
dotnet add reference ../SharedModels/SharedModels.csproj
cd ..

# UserService dependencies
cd UserService
dotnet add reference ../SharedModels/SharedModels.csproj
cd ..

# ProductService dependencies
cd ProductService
dotnet add reference ../SharedModels/SharedModels.csproj
cd ..

# API dependencies
cd BookStoreMonolith.API
dotnet add reference ../OrderService/OrderService.csproj
dotnet add reference ../PaymentService/PaymentService.csproj
dotnet add reference ../UserService/UserService.csproj
dotnet add reference ../ProductService/ProductService.csproj
dotnet add reference ../SharedModels/SharedModels.csproj
cd ..
```

### Step 4: Create Folder Structure

Create the following folder structure in each project:

**SharedModels/**
```
SharedModels/
└── Models/
    ├── OrderModels.cs
    ├── PaymentModels.cs
    ├── ProductModels.cs
    └── UserModels.cs
```

**OrderService/**
```
OrderService/
├── Controllers/
│   └── OrdersController.cs
└── Services/
    └── OrderService.cs
```

**PaymentService/**
```
PaymentService/
├── Controllers/
│   └── PaymentsController.cs
└── Services/
    └── PaymentService.cs
```

**UserService/**
```
UserService/
├── Controllers/
│   └── UsersController.cs
└── Services/
    └── UserService.cs
```

**ProductService/**
```
ProductService/
├── Controllers/
│   └── ProductsController.cs
└── Services/
    └── ProductService.cs
```

**BookStoreMonolith.API/**
```
BookStoreMonolith.API/
├── Controllers/
│   └── HealthController.cs
├── Program.cs
└── appsettings.json
```

### Step 5: Add Required NuGet Packages

```bash
# For all service projects and API project
cd BookStoreMonolith.API
dotnet add package Microsoft.AspNetCore.OpenApi
dotnet add package Swashbuckle.AspNetCore
cd ..
```

### Step 6: Implement the Code

Copy the provided code files to their respective locations as shown in the folder structure above. Make sure to:

1. Place all SharedModels in the `SharedModels/Models/` directory
2. Implement services in their respective `Services/` folders
3. Implement controllers in their respective `Controllers/` folders
4. Update `Program.cs` in the API project with service registration

### Step 7: Build and Run

```bash
# Build the solution
dotnet build

# Run the application
cd BookStoreMonolith.API
dotnet run
```

The application will be available at `https://localhost:7000` and `http://localhost:5000`.

## 📚 API Endpoints

### Health Check
- `GET /api/health` - Check API status

### Orders
- `POST /api/orders` - Create a new order
- `GET /api/orders/{id}` - Get order by ID
- `GET /api/orders/user/{userId}` - Get user's orders
- `PUT /api/orders/{id}/status` - Update order status
- `DELETE /api/orders/{id}` - Delete order

### Payments
- `POST /api/payments` - Process payment
- `GET /api/payments/{id}` - Get payment by ID
- `GET /api/payments/order/{orderId}` - Get payment by order ID
- `PUT /api/payments/{id}/status` - Update payment status
- `POST /api/payments/{paymentId}/refund` - Process refund

### Users
- `POST /api/users` - Create new user
- `GET /api/users/{id}` - Get user by ID
- `GET /api/users/email/{email}` - Get user by email
- `GET /api/users` - Get all users
- `DELETE /api/users/{id}` - Delete user

### Products
- `POST /api/products` - Create new product
- `GET /api/products/{id}` - Get product by ID
- `GET /api/products` - Get all products
- `GET /api/products/category/{categoryId}` - Get products by category
- `PUT /api/products/{id}` - Update product
- `DELETE /api/products/{id}` - Delete product
- `POST /api/products/categories` - Create category
- `GET /api/products/categories` - Get all categories

## 🎯 Sample Usage

### Creating an Order

```bash
POST /api/orders
Content-Type: application/json

{
  "userId": "12345678-1234-1234-1234-123456789012",
  "shippingAddress": "123 Main St, City, Country",
  "customerNotes": "Please deliver after 5 PM",
  "orderItems": [
    {
      "productId": "12345678-1234-1234-1234-123456789012",
      "productName": "Clean Code",
      "productDescription": "A Handbook of Agile Software Craftsmanship",
      "quantity": 2,
      "unitPrice": 45.99
    }
  ]
}
```

### Processing Payment

```bash
POST /api/payments
Content-Type: application/json

{
  "orderId": "12345678-1234-1234-1234-123456789012",
  "amount": 91.98,
  "currency": "USD",
  "paymentMethod": "Credit Card",
  "cardToken": "tok_123456789",
  "description": "Payment for order #12345"
}
```

## 🗃️ Data Storage

This application uses in-memory storage with `ConcurrentDictionary` for thread-safe operations. All data is lost when the application restarts. For production use, consider replacing with a proper database.

### Pre-loaded Dummy Data

The application comes with sample data:

- **3 Users**: John Doe, Jane Smith, Bob Johnson
- **3 Categories**: Fiction, Non-Fiction, Technology
- **3 Products**: The Great Gatsby, Clean Code, Sapiens

## 🔧 Development

### Running in Development Mode

```bash
cd BookStoreMonolith.API
dotnet run
```

### Accessing Swagger UI

Once running, navigate to:
- Swagger UI: `https://localhost:7000/swagger`
- Health Check: `https://localhost:7000/api/health`

### Testing the APIs

Use tools like:
- Swagger UI (built-in)
- Postman
- curl commands
- Thunder Client (VS Code extension)

## 🚀 Deployment

### For Production

1. Update storage to use a real database
2. Configure proper authentication/authorization
3. Set up environment variables
4. Use a proper payment gateway integration
5. Implement logging and monitoring

### Build for Production

```bash
dotnet publish -c Release -o ./publish
```

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Add tests if applicable
5. Submit a pull request

## 📄 License

This project is for educational purposes. Feel free to use and modify as needed.

## 🆘 Troubleshooting

### Common Issues

1. **Build errors**: Ensure all projects target .NET 8.0+
2. **Missing references**: Verify project references are correctly set
3. **Port conflicts**: Change ports in `launchSettings.json`
4. **Swagger not loading**: Check if development environment is set

### Getting Help

- Check the .NET documentation
- Review the Swagger/OpenAPI documentation at `/swagger`
- Verify all project dependencies are restored

---

**Happy Coding!** 🎉
