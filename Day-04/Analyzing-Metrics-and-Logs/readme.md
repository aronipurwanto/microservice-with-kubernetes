# Lab: Analyzing Metrics and Logs for Troubleshooting Microservices

This lab continues from the previous hands-on session and focuses on analyzing metrics and logs to troubleshoot issues in a microservices application deployed on Kubernetes.

## Lab Overview

We'll create three interacting services:
1. **Order Service** - Handles order processing
2. **Payment Service** - Processes payments
3. **Inventory Service** - Manages product inventory

We'll implement comprehensive observability and demonstrate troubleshooting scenarios.

## Project Structure

```
microservices-observability-lab/
├── src/
│   ├── OrderService/
│   ├── PaymentService/
│   └── InventoryService/
├── k8s/
│   ├── deployments/
│   ├── services/
│   └── monitoring/
├── .github/workflows/
├── scripts/
└── README.md
```

## Step 1: Create the Microservices

### Order Service

**OrderService/Program.cs:**
```csharp
using Microsoft.AspNetCore.Mvc;
using OrderService.Services;
using Prometheus;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHttpClient();
builder.Services.AddSingleton<IOrderProcessor, OrderProcessor>();
builder.Services.AddHostedService<MetricsCollectorService>();

builder.Services.AddHealthChecks()
    .AddCheck<OrderServiceHealthCheck>("order-service")
    .ForwardToPrometheus();

var app = builder.Build();

app.UseRouting();
app.UseHttpMetrics(options =>
{
    options.AddCustomLabel("service", _ => "order-service");
});

app.UseEndpoints(endpoints =>
{
    endpoints.MapMetrics();
    endpoints.MapHealthChecks("/health");
    endpoints.MapControllers();
});

app.Run();
```

**OrderService/Models/Order.cs:**
```csharp
namespace OrderService.Models;

public record Order(
    string OrderId,
    string CustomerId,
    List<OrderItem> Items,
    decimal TotalAmount,
    string Status,
    DateTime CreatedAt);

public record OrderItem(string ProductId, int Quantity, decimal Price);

public record CreateOrderRequest(
    string CustomerId,
    List<OrderItem> Items,
    string PaymentMethod);

public record OrderResponse(
    string OrderId,
    string Status,
    string Message);
```

**OrderService/Services/IOrderProcessor.cs:**
```csharp
using OrderService.Models;

namespace OrderService.Services;

public interface IOrderProcessor
{
    Task<OrderResponse> ProcessOrderAsync(CreateOrderRequest request);
    Task<Order> GetOrderAsync(string orderId);
}
```

**OrderService/Services/OrderProcessor.cs:**
```csharp
using System.Diagnostics;
using OrderService.Models;
using Prometheus;

namespace OrderService.Services;

public class OrderProcessor : IOrderProcessor
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OrderProcessor> _logger;
    private static readonly Random _random = new();

    // Metrics
    private static readonly Counter _ordersCreated = Metrics
        .CreateCounter("orders_created_total", "Total orders created");
    
    private static readonly Counter _orderFailures = Metrics
        .CreateCounter("order_failures_total", "Total order failures", 
            new CounterConfiguration { LabelNames = new[] { "failure_type" } });
    
    private static readonly Histogram _orderProcessingDuration = Metrics
        .CreateHistogram("order_processing_duration_seconds", 
            "Order processing duration");
    
    private static readonly Gauge _activeOrders = Metrics
        .CreateGauge("active_orders", "Number of active orders being processed");

    private readonly Dictionary<string, Order> _orders = new();

    public OrderProcessor(HttpClient httpClient, ILogger<OrderProcessor> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<OrderResponse> ProcessOrderAsync(CreateOrderRequest request)
    {
        using var timer = _orderProcessingDuration.NewTimer();
        _activeOrders.Inc();

        var orderId = Guid.NewGuid().ToString();
        _logger.LogInformation("Starting order processing for order {OrderId}", orderId);

        try
        {
            // Validate inventory
            var inventoryResponse = await ValidateInventoryAsync(request.Items);
            if (!inventoryResponse.IsSuccess)
            {
                _orderFailures.WithLabels("inventory_validation").Inc();
                _logger.LogWarning("Inventory validation failed for order {OrderId}: {Reason}", 
                    orderId, inventoryResponse.Message);
                return new OrderResponse(orderId, "Failed", inventoryResponse.Message);
            }

            // Process payment
            var paymentResponse = await ProcessPaymentAsync(orderId, request);
            if (!paymentResponse.IsSuccess)
            {
                _orderFailures.WithLabels("payment_failed").Inc();
                _logger.LogWarning("Payment failed for order {OrderId}: {Reason}", 
                    orderId, paymentResponse.Message);
                return new OrderResponse(orderId, "Failed", paymentResponse.Message);
            }

            // Update inventory
            var updateInventoryResponse = await UpdateInventoryAsync(request.Items);
            if (!updateInventoryResponse.IsSuccess)
            {
                _orderFailures.WithLabels("inventory_update").Inc();
                _logger.LogError("Inventory update failed for order {OrderId}", orderId);
                // In real scenario, we would compensate (refund payment)
            }

            var order = new Order(
                orderId,
                request.CustomerId,
                request.Items,
                request.Items.Sum(i => i.Quantity * i.Price),
                "Completed",
                DateTime.UtcNow
            );

            _orders[orderId] = order;
            _ordersCreated.Inc();

            _logger.LogInformation("Order {OrderId} completed successfully", orderId);
            return new OrderResponse(orderId, "Completed", "Order processed successfully");
        }
        catch (HttpRequestException ex)
        {
            _orderFailures.WithLabels("http_timeout").Inc();
            _logger.LogError(ex, "HTTP request failed while processing order {OrderId}", orderId);
            return new OrderResponse(orderId, "Failed", "Service temporarily unavailable");
        }
        catch (Exception ex)
        {
            _orderFailures.WithLabels("unexpected_error").Inc();
            _logger.LogError(ex, "Unexpected error processing order {OrderId}", orderId);
            return new OrderResponse(orderId, "Failed", "Internal server error");
        }
        finally
        {
            _activeOrders.Dec();
        }
    }

    private async Task<ServiceResponse> ValidateInventoryAsync(List<OrderItem> items)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                "http://inventory-service/api/inventory/validate", 
                new { Items = items });

            if (!response.IsSuccessStatusCode)
            {
                return new ServiceResponse(false, "Inventory validation failed");
            }

            var result = await response.Content.ReadFromJsonAsync<ServiceResponse>();
            return result ?? new ServiceResponse(false, "Invalid response from inventory service");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to call inventory service for validation");
            return new ServiceResponse(false, "Inventory service unavailable");
        }
    }

    private async Task<ServiceResponse> ProcessPaymentAsync(string orderId, CreateOrderRequest request)
    {
        try
        {
            var paymentRequest = new
            {
                OrderId = orderId,
                request.CustomerId,
                Amount = request.Items.Sum(i => i.Quantity * i.Price),
                request.PaymentMethod
            };

            var response = await _httpClient.PostAsJsonAsync(
                "http://payment-service/api/payments/process", 
                paymentRequest);

            if (!response.IsSuccessStatusCode)
            {
                return new ServiceResponse(false, "Payment processing failed");
            }

            var result = await response.Content.ReadFromJsonAsync<ServiceResponse>();
            return result ?? new ServiceResponse(false, "Invalid response from payment service");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to call payment service");
            return new ServiceResponse(false, "Payment service unavailable");
        }
    }

    private async Task<ServiceResponse> UpdateInventoryAsync(List<OrderItem> items)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                "http://inventory-service/api/inventory/update", 
                new { Items = items });

            return response.IsSuccessStatusCode 
                ? new ServiceResponse(true, "Inventory updated")
                : new ServiceResponse(false, "Inventory update failed");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to call inventory service for update");
            return new ServiceResponse(false, "Inventory service unavailable");
        }
    }

    public Task<Order> GetOrderAsync(string orderId)
    {
        if (_orders.TryGetValue(orderId, out var order))
        {
            return Task.FromResult(order);
        }
        return Task.FromResult<Order>(null);
    }
}

public record ServiceResponse(bool IsSuccess, string Message);
```

### Payment Service

**PaymentService/Program.cs:**
```csharp
using PaymentService.Services;
using Prometheus;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddSingleton<IPaymentGateway, PaymentGateway>();
builder.Services.AddHostedService<MetricsCollectorService>();

builder.Services.AddHealthChecks()
    .AddCheck<PaymentServiceHealthCheck>("payment-service")
    .ForwardToPrometheus();

var app = builder.Build();

app.UseRouting();
app.UseHttpMetrics(options =>
{
    options.AddCustomLabel("service", _ => "payment-service");
});

app.UseEndpoints(endpoints =>
{
    endpoints.MapMetrics();
    endpoints.MapHealthChecks("/health");
    endpoints.MapControllers();
});

app.Run();
```

**PaymentService/Services/PaymentGateway.cs:**
```csharp
using Prometheus;

namespace PaymentService.Services;

public interface IPaymentGateway
{
    Task<PaymentResult> ProcessPaymentAsync(PaymentRequest request);
}

public record PaymentRequest(
    string OrderId,
    string CustomerId,
    decimal Amount,
    string PaymentMethod);

public record PaymentResult(bool IsSuccess, string TransactionId, string ErrorMessage);

public class PaymentGateway : IPaymentGateway
{
    private readonly ILogger<PaymentGateway> _logger;
    private static readonly Random _random = new();

    // Metrics
    private static readonly Counter _paymentsProcessed = Metrics
        .CreateCounter("payments_processed_total", "Total payments processed");
    
    private static readonly Counter _paymentFailures = Metrics
        .CreateCounter("payment_failures_total", "Total payment failures",
            new CounterConfiguration { LabelNames = new[] { "failure_reason" } });
    
    private static readonly Histogram _paymentProcessingDuration = Metrics
        .CreateHistogram("payment_processing_duration_seconds", 
            "Payment processing duration");
    
    private static readonly Gauge _paymentSuccessRate = Metrics
        .CreateGauge("payment_success_rate", "Payment success rate");

    private int _successfulPayments = 0;
    private int _totalPayments = 0;

    public PaymentGateway(ILogger<PaymentGateway> logger)
    {
        _logger = logger;
    }

    public async Task<PaymentResult> ProcessPaymentAsync(PaymentRequest request)
    {
        using var timer = _paymentProcessingDuration.NewTimer();

        _logger.LogInformation("Processing payment for order {OrderId}, amount: {Amount}", 
            request.OrderId, request.Amount);

        _totalPayments++;
        
        try
        {
            // Simulate payment processing
            await Task.Delay(_random.Next(100, 500));

            // Simulate occasional failures
            if (_random.NextDouble() < 0.1) // 10% failure rate
            {
                _paymentFailures.WithLabels("gateway_declined").Inc();
                _logger.LogWarning("Payment declined by gateway for order {OrderId}", request.OrderId);
                UpdateSuccessRate();
                return new PaymentResult(false, null, "Payment declined by gateway");
            }

            // Simulate random timeouts
            if (_random.NextDouble() < 0.05) // 5% timeout rate
            {
                await Task.Delay(5000); // Simulate timeout
                _paymentFailures.WithLabels("timeout").Inc();
                _logger.LogError("Payment timeout for order {OrderId}", request.OrderId);
                UpdateSuccessRate();
                return new PaymentResult(false, null, "Payment gateway timeout");
            }

            var transactionId = Guid.NewGuid().ToString();
            _successfulPayments++;
            _paymentsProcessed.Inc();
            
            _logger.LogInformation("Payment successful for order {OrderId}, transaction: {TransactionId}", 
                request.OrderId, transactionId);
            
            UpdateSuccessRate();
            return new PaymentResult(true, transactionId, null);
        }
        catch (Exception ex)
        {
            _paymentFailures.WithLabels("unexpected_error").Inc();
            _logger.LogError(ex, "Unexpected error processing payment for order {OrderId}", request.OrderId);
            UpdateSuccessRate();
            return new PaymentResult(false, null, "Unexpected payment error");
        }
    }

    private void UpdateSuccessRate()
    {
        if (_totalPayments > 0)
        {
            _paymentSuccessRate.Set((double)_successfulPayments / _totalPayments);
        }
    }
}
```

### Inventory Service

**InventoryService/Program.cs:**
```csharp
using InventoryService.Services;
using Prometheus;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddSingleton<IInventoryManager, InventoryManager>();
builder.Services.AddHostedService<MetricsCollectorService>();

builder.Services.AddHealthChecks()
    .AddCheck<InventoryServiceHealthCheck>("inventory-service")
    .ForwardToPrometheus();

var app = builder.Build();

app.UseRouting();
app.UseHttpMetrics(options =>
{
    options.AddCustomLabel("service", _ => "inventory-service");
});

app.UseEndpoints(endpoints =>
{
    endpoints.MapMetrics();
    endpoints.MapHealthChecks("/health");
    endpoints.MapControllers();
});

app.Run();
```

**InventoryService/Services/InventoryManager.cs:**
```csharp
using Prometheus;

namespace InventoryService.Services;

public interface IInventoryManager
{
    Task<InventoryValidationResult> ValidateInventoryAsync(List<OrderItem> items);
    Task<bool> UpdateInventoryAsync(List<OrderItem> items);
    Task<InventoryItem> GetInventoryAsync(string productId);
}

public record OrderItem(string ProductId, int Quantity, decimal Price);
public record InventoryItem(string ProductId, string Name, int Stock, decimal Price);
public record InventoryValidationResult(bool IsValid, string Message, List<string> OutOfStockItems);

public class InventoryManager : IInventoryManager
{
    private readonly ILogger<InventoryManager> _logger;
    private static readonly Random _random = new();

    // Metrics
    private static readonly Counter _inventoryValidations = Metrics
        .CreateCounter("inventory_validations_total", "Total inventory validations");
    
    private static readonly Counter _inventoryUpdates = Metrics
        .CreateCounter("inventory_updates_total", "Total inventory updates");
    
    private static readonly Histogram _inventoryOperationDuration = Metrics
        .CreateHistogram("inventory_operation_duration_seconds", 
            "Inventory operation duration",
            new HistogramConfiguration { LabelNames = new[] { "operation" } });
    
    private static readonly Gauge _lowStockItems = Metrics
        .CreateGauge("low_stock_items", "Number of items with low stock");

    private readonly Dictionary<string, InventoryItem> _inventory = new()
    {
        { "1", new InventoryItem("1", "Laptop", 10, 999.99m) },
        { "2", new InventoryItem("2", "Mouse", 100, 25.50m) },
        { "3", new InventoryItem("3", "Keyboard", 50, 75.00m) },
        { "4", new InventoryItem("4", "Monitor", 30, 200.00m) },
        { "5", new InventoryItem("5", "Headphones", 5, 150.00m) } // Low stock item
    };

    public InventoryManager(ILogger<InventoryManager> logger)
    {
        _logger = logger;
        UpdateLowStockMetric();
    }

    public async Task<InventoryValidationResult> ValidateInventoryAsync(List<OrderItem> items)
    {
        using (_inventoryOperationDuration.WithLabels("validate").NewTimer())
        {
            _inventoryValidations.Inc();
            _logger.LogInformation("Validating inventory for {ItemCount} items", items.Count);

            await Task.Delay(_random.Next(50, 200)); // Simulate processing

            var outOfStockItems = new List<string>();

            foreach (var item in items)
            {
                if (!_inventory.TryGetValue(item.ProductId, out var inventoryItem))
                {
                    _logger.LogWarning("Product {ProductId} not found in inventory", item.ProductId);
                    return new InventoryValidationResult(false, $"Product {item.ProductId} not found", outOfStockItems);
                }

                if (inventoryItem.Stock < item.Quantity)
                {
                    outOfStockItems.Add(item.ProductId);
                    _logger.LogWarning("Insufficient stock for product {ProductId}. Requested: {Quantity}, Available: {Stock}",
                        item.ProductId, item.Quantity, inventoryItem.Stock);
                }
            }

            if (outOfStockItems.Any())
            {
                return new InventoryValidationResult(false, "Insufficient stock for some items", outOfStockItems);
            }

            return new InventoryValidationResult(true, "Inventory validation successful", outOfStockItems);
        }
    }

    public async Task<bool> UpdateInventoryAsync(List<OrderItem> items)
    {
        using (_inventoryOperationDuration.WithLabels("update").NewTimer())
        {
            _inventoryUpdates.Inc();
            _logger.LogInformation("Updating inventory for {ItemCount} items", items.Count);

            // Simulate occasional failures in inventory update
            if (_random.NextDouble() < 0.08) // 8% failure rate
            {
                _logger.LogError("Inventory update failed due to database connection issue");
                return false;
            }

            await Task.Delay(_random.Next(100, 300)); // Simulate processing

            foreach (var item in items)
            {
                if (_inventory.TryGetValue(item.ProductId, out var inventoryItem))
                {
                    var updatedItem = inventoryItem with { Stock = inventoryItem.Stock - item.Quantity };
                    _inventory[item.ProductId] = updatedItem;
                    
                    _logger.LogInformation("Updated inventory for product {ProductId}. New stock: {Stock}",
                        item.ProductId, updatedItem.Stock);
                }
            }

            UpdateLowStockMetric();
            return true;
        }
    }

    public Task<InventoryItem> GetInventoryAsync(string productId)
    {
        _inventory.TryGetValue(productId, out var item);
        return Task.FromResult(item);
    }

    private void UpdateLowStockMetric()
    {
        var lowStockCount = _inventory.Values.Count(item => item.Stock < 10);
        _lowStockItems.Set(lowStockCount);
    }
}
```

## Step 2: Add Health Checks and Metrics Collectors

**Common/MetricsCollectorService.cs:**
```csharp
using Prometheus;

namespace OrderService.Services;

public class MetricsCollectorService : BackgroundService
{
    private static readonly Gauge _memoryUsage = Metrics
        .CreateGauge("process_working_set_bytes", "Physical memory usage in bytes");

    private static readonly Gauge _cpuUsage = Metrics
        .CreateGauge("process_cpu_usage_percent", "CPU usage percentage");

    private static readonly Counter _backgroundTasks = Metrics
        .CreateCounter("background_tasks_executed_total", "Total background tasks executed");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Collect system metrics
                _memoryUsage.Set(Environment.WorkingSet);
                _cpuUsage.Set(Random.Shared.NextDouble() * 100); // Simulated for demo
                _backgroundTasks.Inc();

                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
```

**Common/HealthChecks.cs:**
```csharp
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace OrderService.Services;

public class OrderServiceHealthCheck : IHealthCheck
{
    private static readonly Random _random = new();

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Simulate health check with occasional degradation
            await Task.Delay(100, cancellationToken);
            
            return _random.Next(0, 20) switch
            {
                < 15 => HealthCheckResult.Healthy("Order service is healthy"),
                < 18 => HealthCheckResult.Degraded("Order service is degraded"),
                _ => HealthCheckResult.Unhealthy("Order service is unhealthy")
            };
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Health check failed", ex);
        }
    }
}
```

## Step 3: Kubernetes Deployment Manifests

**k8s/namespace.yaml:**
```yaml
apiVersion: v1
kind: Namespace
metadata:
  name: microservices-demo
  labels:
    name: microservices-demo
```

**k8s/configmaps.yaml:**
```yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: app-settings
  namespace: microservices-demo
data:
  LOG_LEVEL: "Information"
  ASPNETCORE_ENVIRONMENT: "Production"
```

**k8s/order-service.yaml:**
```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: order-service
  namespace: microservices-demo
  labels:
    app: order-service
spec:
  replicas: 2
  selector:
    matchLabels:
      app: order-service
  template:
    metadata:
      labels:
        app: order-service
      annotations:
        prometheus.io/scrape: "true"
        prometheus.io/port: "80"
        prometheus.io/path: "/metrics"
    spec:
      containers:
      - name: order-service
        image: order-service:1.0.0
        imagePullPolicy: IfNotPresent
        ports:
        - containerPort: 80
        env:
        - name: ASPNETCORE_URLS
          value: "http://*:80"
        - name: LOG_LEVEL
          valueFrom:
            configMapKeyRef:
              name: app-settings
              key: LOG_LEVEL
        resources:
          requests:
            memory: "128Mi"
            cpu: "100m"
          limits:
            memory: "256Mi"
            cpu: "200m"
        livenessProbe:
          httpGet:
            path: /health
            port: 80
          initialDelaySeconds: 30
          periodSeconds: 10
        readinessProbe:
          httpGet:
            path: /health
            port: 80
          initialDelaySeconds: 5
          periodSeconds: 5
---
apiVersion: v1
kind: Service
metadata:
  name: order-service
  namespace: microservices-demo
  labels:
    app: order-service
spec:
  selector:
    app: order-service
  ports:
  - name: http
    port: 80
    targetPort: 80
  type: ClusterIP
---
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata:
  name: order-service-hpa
  namespace: microservices-demo
spec:
  scaleTargetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: order-service
  minReplicas: 2
  maxReplicas: 5
  metrics:
  - type: Resource
    resource:
      name: cpu
      target:
        type: Utilization
        averageUtilization: 70
```

**k8s/payment-service.yaml & k8s/inventory-service.yaml** (similar structure)

## Step 4: CI/CD Pipeline

**.github/workflows/ci-cd.yml:**
```yaml
name: Microservices CI/CD

on:
  push:
    branches: [ main, develop ]
  pull_request:
    branches: [ main ]

env:
  REGISTRY: ghcr.io
  IMAGE_PREFIX: ${{ github.repository }}

jobs:
  test:
    runs-on: ubuntu-latest
    strategy:
      matrix:
        service: [order-service, payment-service, inventory-service]
    
    steps:
    - uses: actions/checkout@v3
    
    - name: Setup .NET
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: '10.0.x'
    
    - name: Restore dependencies
      run: dotnet restore src/${{ matrix.service }}/${{ matrix.service }}.csproj
    
    - name: Run tests
      run: dotnet test src/${{ matrix.service }}/${{ matrix.service }}.csproj --verbosity normal

  build-and-push:
    needs: test
    runs-on: ubuntu-latest
    if: github.ref == 'refs/heads/main'
    strategy:
      matrix:
        service: [order-service, payment-service, inventory-service]
    
    steps:
    - uses: actions/checkout@v3
    
    - name: Log in to GitHub Container Registry
      uses: docker/login-action@v2
      with:
        registry: ${{ env.REGISTRY }}
        username: ${{ github.actor }}
        password: ${{ secrets.GITHUB_TOKEN }}
    
    - name: Extract metadata for Docker
      id: meta
      uses: docker/metadata-action@v4
      with:
        images: ${{ env.REGISTRY }}/${{ env.IMAGE_PREFIX }}-${{ matrix.service }}
        tags: |
          type=ref,event=branch
          type=ref,event=pr
          type=semver,pattern={{version}}
          type=semver,pattern={{major}}.{{minor}}
          type=sha,prefix={{abbrev(7,false)}}
    
    - name: Build and push Docker image
      uses: docker/build-push-action@v4
      with:
        context: .
        file: src/${{ matrix.service }}/Dockerfile
        push: true
        tags: ${{ steps.meta.outputs.tags }}
        labels: ${{ steps.meta.outputs.labels }}

  deploy-to-dev:
    needs: build-and-push
    runs-on: ubuntu-latest
    if: github.ref == 'refs/heads/develop'
    
    steps:
    - uses: actions/checkout@v3
    
    - name: Deploy to Minikube (Dev)
      run: |
        kubectl apply -f k8s/namespace.yaml
        kubectl apply -f k8s/configmaps.yaml
        kubectl apply -f k8s/order-service.yaml
        kubectl apply -f k8s/payment-service.yaml
        kubectl apply -f k8s/inventory-service.yaml
        kubectl apply -f k8s/monitoring/

  deploy-to-prod:
    needs: build-and-push
    runs-on: ubuntu-latest
    if: github.ref == 'refs/heads/main'
    
    steps:
    - uses: actions/checkout@v3
    
    - name: Deploy to Production
      run: |
        kubectl apply -f k8s/namespace.yaml
        kubectl apply -f k8s/configmaps.yaml
        kubectl apply -f k8s/order-service.yaml
        kubectl apply -f k8s/payment-service.yaml
        kubectl apply -f k8s/inventory-service.yaml
        kubectl apply -f k8s/monitoring/
```

## Step 5: Monitoring Stack

**k8s/monitoring/prometheus-config.yaml:**
```yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: prometheus-config
  namespace: microservices-demo
data:
  prometheus.yml: |
    global:
      scrape_interval: 15s
      evaluation_interval: 15s

    rule_files:
      - /etc/prometheus/rules.yml

    scrape_configs:
      - job_name: 'microservices'
        kubernetes_sd_configs:
        - role: endpoints
          namespaces:
            names:
              - microservices-demo
        relabel_configs:
        - source_labels: [__meta_kubernetes_service_annotation_prometheus_io_scrape]
          action: keep
          regex: true
        - source_labels: [__meta_kubernetes_service_annotation_prometheus_io_path]
          action: replace
          target_label: __metrics_path__
          regex: (.+)
        - source_labels: [__address__, __meta_kubernetes_service_annotation_prometheus_io_port]
          action: replace
          regex: ([^:]+)(?::\d+)?;(\d+)
          replacement: $1:$2
          target_label: __address__
        - action: labelmap
          regex: __meta_kubernetes_service_label_(.+)
        - source_labels: [__meta_kubernetes_namespace]
          action: replace
          target_label: kubernetes_namespace
        - source_labels: [__meta_kubernetes_service_name]
          action: replace
          target_label: service

  rules.yml: |
    groups:
    - name: microservices-rules
      rules:
      - alert: HighErrorRate
        expr: rate(http_requests_duration_seconds_count{code=~"5.."}[5m]) / rate(http_requests_duration_seconds_count[5m]) > 0.1
        for: 2m
        labels:
          severity: warning
        annotations:
          summary: "High error rate detected in {{ $labels.service }}"
          description: "Error rate is {{ $value }} for service {{ $labels.service }}"

      - alert: HighLatency
        expr: histogram_quantile(0.95, rate(http_requests_duration_seconds_bucket[5m])) > 1
        for: 2m
        labels:
          severity: warning
        annotations:
          summary: "High latency detected in {{ $labels.service }}"
          description: "95th percentile latency is {{ $value }} seconds for {{ $labels.service }}"

      - alert: PaymentFailureRateHigh
        expr: rate(payment_failures_total[5m]) / rate(payments_processed_total[5m]) > 0.2
        for: 3m
        labels:
          severity: critical
        annotations:
          summary: "High payment failure rate"
          description: "Payment failure rate is {{ $value }}"

      - alert: LowStockAlert
        expr: low_stock_items > 3
        for: 5m
        labels:
          severity: warning
        annotations:
          summary: "Multiple items have low stock"
          description: "{{ $value }} items have low stock levels"
```

## Step 6: Troubleshooting Scenarios

### Scenario 1: High Payment Failure Rate

**Symptoms:**
- Payment failure rate > 20%
- Increased order processing time
- Customer complaints about failed payments

**Investigation Steps:**

1. **Check Metrics in Prometheus:**
   ```promql
   # Payment failure rate
   rate(payment_failures_total[5m]) / rate(payments_processed_total[5m])
   
   # Breakdown by failure reason
   rate(payment_failures_total[5m])
   ```

2. **Check Logs:**
   ```bash
   # Get payment service logs
   kubectl logs -n microservices-demo -l app=payment-service --tail=100
   
   # Search for specific error patterns
   kubectl logs -n microservices-demo -l app=payment-service | grep -i "failed\|error\|timeout"
   ```

3. **Analyze Traces:**
   ```bash
   # Check order processing flow
   # Look for spans involving payment service with high duration or errors
   ```

### Scenario 2: Inventory Service Degradation

**Symptoms:**
- High latency in inventory operations
- Increased order failures due to inventory validation
- Inventory service health checks failing

**Investigation Steps:**

1. **Check Service Health:**
   ```bash
   kubectl get pods -n microservices-demo -l app=inventory-service
   kubectl describe pod -n microservices-demo <inventory-pod-name>
   ```

2. **Analyze Resource Usage:**
   ```bash
   kubectl top pods -n microservices-demo -l app=inventory-service
   ```

3. **Check Application Metrics:**
   ```promql
   # Inventory operation duration
   histogram_quantile(0.95, rate(inventory_operation_duration_seconds_bucket[5m]))
   
   # Error rate
   rate(inventory_validations_total{status!="success"}[5m])
   ```

### Scenario 3: Cascading Failure

**Symptoms:**
- Multiple services showing degraded performance
- Increased error rates across the system
- Resource exhaustion alerts

**Investigation Steps:**

1. **Check Dependency Map:**
   ```bash
   # Visualize service dependencies using tracing data
   # Identify the root cause service
   ```

2. **Analyze Circuit Breaker Patterns:**
   ```promql
   # Check for timeout patterns
   rate(http_request_duration_seconds_count{code="408"}[5m])
   
   # Check retry patterns
   rate(http_client_retries_total[5m])
   ```

3. **Check Resource Limits:**
   ```bash
   kubectl describe hpa -n microservices-demo
   kubectl get events -n microservices-demo --sort-by=.lastTimestamp
   ```

## Step 7: Dockerfile for Each Service

**OrderService/Dockerfile:**
```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["OrderService/OrderService.csproj", "OrderService/"]
RUN dotnet restore "OrderService/OrderService.csproj"

COPY . .
WORKDIR "/src/OrderService"
RUN dotnet build "OrderService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "OrderService.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
EXPOSE 80

COPY --from=publish /app/publish .

RUN groupadd -r appuser && useradd -r -g appuser appuser
USER appuser

ENTRYPOINT ["dotnet", "OrderService.dll"]
```

## Step 8: Load Testing and Chaos Engineering

**scripts/load-test.sh:**
```bash
#!/bin/bash

echo "Starting comprehensive load test..."

API_BASE="http://localhost:8080"
REQUEST_COUNT=0
ERROR_COUNT=0

generate_order() {
    local customer_id="customer-$((RANDOM % 1000))"
    local product_count=$((RANDOM % 3 + 1))
    
    local items="["
    for i in $(seq 1 $product_count); do
        local product_id=$((RANDOM % 5 + 1))
        local quantity=$((RANDOM % 2 + 1))
        if [ $i -gt 1 ]; then
            items+=","
        fi
        items+="{\"productId\": \"$product_id\", \"quantity\": $quantity, \"price\": $((RANDOM % 100 + 10))}"
    done
    items+="]"
    
    local payment_methods=("credit_card" "paypal" "bank_transfer")
    local payment_method=${payment_methods[$((RANDOM % 3))]}
    
    cat << EOF
{
    "customerId": "$customer_id",
    "items": $items,
    "paymentMethod": "$payment_method"
}
EOF
}

run_load_test() {
    local duration=$1
    local end_time=$((SECONDS + duration))
    
    while [ $SECONDS -lt $end_time ]; do
        local order_data=$(generate_order)
        
        local response=$(curl -s -o response.txt -w "%{http_code}" -X POST \
            -H "Content-Type: application/json" \
            -d "$order_data" \
            "$API_BASE/api/orders")
        
        REQUEST_COUNT=$((REQUEST_COUNT + 1))
        
        if [ "$response" -ne 200 ]; then
            ERROR_COUNT=$((ERROR_COUNT + 1))
            echo "Error: HTTP $response"
            cat response.txt
            echo
        else
            echo "Success: Order created"
        fi
        
        # Random delay between requests
        sleep $(echo "scale=2; $RANDOM/32767" | bc)
    done
}

# Run different load patterns
echo "Phase 1: Normal load (30 seconds)"
run_load_test 30

echo "Phase 2: High load (20 seconds)"
for i in {1..5}; do
    run_load_test 4 &
done
wait

echo "Phase 3: Spike load (10 seconds)"
for i in {1..10}; do
    run_load_test 1 &
done
wait

echo "Load test completed"
echo "Total requests: $REQUEST_COUNT"
echo "Total errors: $ERROR_COUNT"
echo "Error rate: $(echo "scale=2; $ERROR_COUNT*100/$REQUEST_COUNT" | bc)%"

rm -f response.txt
```

## Step 9: Running the Lab

### Setup Instructions

1. **Start Minikube:**
   ```bash
   minikube start --cpus=4 --memory=8g --addons=ingress,metrics-server
   eval $(minikube docker-env)
   ```

2. **Build and Deploy:**
   ```bash
   # Build images
   docker build -t order-service:1.0.0 -f src/OrderService/Dockerfile .
   docker build -t payment-service:1.0.0 -f src/PaymentService/Dockerfile .
   docker build -t inventory-service:1.0.0 -f src/InventoryService/Dockerfile .
   
   # Deploy to Kubernetes
   kubectl apply -f k8s/namespace.yaml
   kubectl apply -f k8s/configmaps.yaml
   kubectl apply -f k8s/order-service.yaml
   kubectl apply -f k8s/payment-service.yaml
   kubectl apply -f k8s/inventory-service.yaml
   kubectl apply -f k8s/monitoring/
   ```

3. **Access the Application:**
   ```bash
   # Port forward for testing
   kubectl port-forward -n microservices-demo svc/order-service 8080:80 &
   
   # Generate load
   chmod +x scripts/load-test.sh
   ./scripts/load-test.sh
   ```

4. **Monitor the System:**
   ```bash
   # Watch pods
   kubectl get pods -n microservices-demo -w
   
   # Check logs
   kubectl logs -n microservices-demo -l app=order-service -f
   
   # Monitor resources
   kubectl top pods -n microservices-demo
   ```

This comprehensive lab provides hands-on experience with microservices observability, troubleshooting, and CI/CD practices in a Kubernetes environment. The setup demonstrates real-world scenarios and provides practical skills for maintaining distributed systems.