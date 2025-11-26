# **Hands-on Lab: Configuring Auto-scaling for .NET Core Services**

## **Lab Overview**
This lab guides you through configuring horizontal and vertical pod autoscaling for a .NET Core 10.0 microservice running on Minikube. You'll learn to set up autoscaling based on CPU and memory metrics.

## **Prerequisites**
- Minikube installed and running
- kubectl configured
- .NET Core 10.0 SDK
- Docker installed

---

## **1. Environment Setup**

### **1.1 Start Minikube with Required Components**
```bash
# Start Minikube with metrics server enabled
minikube start --memory=4096 --cpus=4
minikube addons enable metrics-server

# Verify metrics server is running
kubectl get pods -n kube-system | grep metrics-server

# Enable ingress if needed
minikube addons enable ingress
```

### **1.2 Verify Cluster Status**
```bash
# Check node resources
kubectl top nodes

# Verify metrics server is collecting data
kubectl get --raw /apis/metrics.k8s.io/v1beta1/nodes
```

---

## **2. Create .NET Core 10.0 Application**

### **2.1 Create Project Structure**
```bash
# Create project directory
mkdir dotnet-autoscaling-demo
cd dotnet-autoscaling-demo

# Create new web API project
dotnet new webapi -n AutoscaleApi -f net8.0
cd AutoscaleApi

# Add required packages
dotnet add package Prometheus.Net.AspNetCore
dotnet add package Microsoft.AspNetCore.HttpOverrides
```

### **2.2 Modify Program.cs**
Replace the content of `Program.cs` with:

```csharp
using Prometheus;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure health checks
builder.Services.AddHealthChecks()
    .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy());

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Use forwarding headers for Kubernetes
app.UseForwardedHeaders();

// Use HTTP metrics
app.UseHttpMetrics();

app.UseAuthorization();

app.MapControllers();

// Add health check endpoint
app.MapHealthChecks("/health");

// Add metrics endpoint
app.MapMetrics("/metrics");

// Add CPU/Memory load simulation endpoints
app.MapGet("/load/cpu/{seconds}", async (int seconds) =>
{
    var startTime = DateTime.UtcNow;
    var endTime = startTime.AddSeconds(seconds);
    
    // Simulate CPU load
    while (DateTime.UtcNow < endTime)
    {
        // Perform some calculations to use CPU
        var result = 0.0;
        for (int i = 0; i < 1000000; i++)
        {
            result += Math.Sqrt(i) * Math.Sin(i);
        }
    }
    
    return Results.Ok(new { 
        message = $"CPU load simulated for {seconds} seconds",
        timestamp = DateTime.UtcNow
    });
});

app.MapPost("/load/memory/{mb}", (int mb) =>
{
    try
    {
        // Simulate memory allocation
        var buffer = new byte[mb * 1024 * 1024];
        Array.Fill(buffer, (byte)1);
        
        return Results.Ok(new {
            message = $"Allocated {mb} MB of memory",
            allocatedMB = mb,
            timestamp = DateTime.UtcNow
        });
    }
    catch (OutOfMemoryException)
    {
        return Results.Problem("Out of memory");
    }
});

app.MapGet("/info", () =>
{
    var process = System.Diagnostics.Process.GetCurrentProcess();
    return Results.Ok(new {
        machineName = Environment.MachineName,
        processId = process.Id,
        processName = process.ProcessName,
        workingSetMB = process.WorkingSet64 / 1024 / 1024,
        privateMemoryMB = process.PrivateMemorySize64 / 1024 / 1024,
        startTime = process.StartTime,
        threadCount = process.Threads.Count,
        timestamp = DateTime.UtcNow
    });
});

app.Run();
```

### **2.3 Update Controllers/WeatherForecastController.cs**
Replace with enhanced controller:

```csharp
using Microsoft.AspNetCore.Mvc;

namespace AutoscaleApi.Controllers;

[ApiController]
[Route("[controller]")]
public class WeatherForecastController : ControllerBase
{
    private static readonly string[] Summaries = new[]
    {
        "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
    };

    private readonly ILogger<WeatherForecastController> _logger;

    public WeatherForecastController(ILogger<WeatherForecastController> logger)
    {
        _logger = logger;
    }

    [HttpGet(Name = "GetWeatherForecast")]
    public IEnumerable<WeatherForecast> Get()
    {
        _logger.LogInformation("Getting weather forecast - {Timestamp}", DateTime.UtcNow);
        
        return Enumerable.Range(1, 5).Select(index => new WeatherForecast
        {
            Date = DateTime.Now.AddDays(index),
            TemperatureC = Random.Shared.Next(-20, 55),
            Summary = Summaries[Random.Shared.Next(Summaries.Length)]
        })
        .ToArray();
    }

    [HttpGet("heavy")]
    public async Task<ActionResult<IEnumerable<WeatherForecast>>> GetHeavy()
    {
        _logger.LogInformation("Heavy operation started - {Timestamp}", DateTime.UtcNow);
        
        // Simulate heavy processing
        await Task.Delay(TimeSpan.FromMilliseconds(500));
        
        var forecasts = Enumerable.Range(1, 100).Select(index => new WeatherForecast
        {
            Date = DateTime.Now.AddDays(index),
            TemperatureC = Random.Shared.Next(-20, 55),
            Summary = Summaries[Random.Shared.Next(Summaries.Length)]
        })
        .ToArray();

        _logger.LogInformation("Heavy operation completed - {Timestamp}", DateTime.UtcNow);
        
        return Ok(forecasts);
    }
}
```

### **2.4 Update WeatherForecast Model**
Update `Models/WeatherForecast.cs`:

```csharp
namespace AutoscaleApi;

public class WeatherForecast
{
    public DateTime Date { get; set; }
    public int TemperatureC { get; set; }
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
    public string? Summary { get; set; }
    public string? GeneratedBy { get; set; } = Environment.MachineName;
}
```

---

## **3. Docker Configuration**

### **3.1 Create Dockerfile**
Create `Dockerfile` in the project root:

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["AutoscaleApi.csproj", "."]
RUN dotnet restore "AutoscaleApi.csproj"
COPY . .
WORKDIR "/src"
RUN dotnet build "AutoscaleApi.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "AutoscaleApi.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Install curl for health checks
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

ENTRYPOINT ["dotnet", "AutoscaleApi.dll"]
```

### **3.2 Create .dockerignore**
```
**/.dockerignore
**/.env
**/.git
**/.gitignore
**/.project
**/.settings
**/.toolstarget
**/.vs
**/.vscode
**/*.*proj.user
**/azds.yaml
**/bin
**/charts
**/docker-compose*
**/Dockerfile*
**/node_modules
**/npm-debug.log
**/obj
**/secrets.dev.yaml
**/values.dev.yaml
LICENSE
README.md
```

---

## **4. Kubernetes Manifests**

### **4.1 Create Namespace**
Create `namespace.yaml`:

```yaml
apiVersion: v1
kind: Namespace
metadata:
  name: autoscale-demo
  labels:
    name: autoscale-demo
```

### **4.2 Create Deployment**
Create `deployment.yaml`:

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: autoscale-api
  namespace: autoscale-demo
  labels:
    app: autoscale-api
spec:
  replicas: 2
  selector:
    matchLabels:
      app: autoscale-api
  template:
    metadata:
      labels:
        app: autoscale-api
    spec:
      containers:
      - name: autoscale-api
        image: autoscale-api:latest
        ports:
        - containerPort: 80
        env:
        - name: ASPNETCORE_ENVIRONMENT
          value: "Development"
        resources:
          requests:
            cpu: "100m"
            memory: "128Mi"
          limits:
            cpu: "500m"
            memory: "512Mi"
        livenessProbe:
          httpGet:
            path: /health
            port: 80
          initialDelaySeconds: 10
          periodSeconds: 10
        readinessProbe:
          httpGet:
            path: /health
            port: 80
          initialDelaySeconds: 5
          periodSeconds: 5
        startupProbe:
          httpGet:
            path: /health
            port: 80
          failureThreshold: 30
          periodSeconds: 10
---
apiVersion: v1
kind: Service
metadata:
  name: autoscale-api-service
  namespace: autoscale-demo
spec:
  selector:
    app: autoscale-api
  ports:
  - port: 80
    targetPort: 80
  type: ClusterIP
```

### **4.3 Create Horizontal Pod Autoscaler**
Create `hpa.yaml`:

```yaml
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata:
  name: autoscale-api-hpa
  namespace: autoscale-demo
spec:
  scaleTargetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: autoscale-api
  minReplicas: 2
  maxReplicas: 10
  metrics:
  - type: Resource
    resource:
      name: cpu
      target:
        type: Utilization
        averageUtilization: 50
  - type: Resource
    resource:
      name: memory
      target:
        type: Utilization
        averageUtilization: 70
  behavior:
    scaleDown:
      stabilizationWindowSeconds: 300
      policies:
      - type: Percent
        value: 50
        periodSeconds: 60
    scaleUp:
      stabilizationWindowSeconds: 60
      policies:
      - type: Percent
        value: 100
        periodSeconds: 30
```

### **4.4 Create Load Generator Job**
Create `load-generator.yaml`:

```yaml
apiVersion: batch/v1
kind: Job
metadata:
  name: load-generator
  namespace: autoscale-demo
spec:
  template:
    spec:
      containers:
      - name: load-generator
        image: curlimages/curl:latest
        command: ["/bin/sh"]
        args:
        - -c
        - |
          echo "Starting load test..."
          API_URL="http://autoscale-api-service.autoscale-demo.svc.cluster.local"
          
          # Generate continuous load
          while true; do
            # Mix of normal and heavy requests
            curl -s "${API_URL}/WeatherForecast" > /dev/null
            curl -s "${API_URL}/WeatherForecast/heavy" > /dev/null
            curl -s "${API_URL}/info" > /dev/null
            
            # Every 10th request, generate CPU load
            if [ $((RANDOM % 10)) -eq 0 ]; then
              curl -s "${API_URL}/load/cpu/5" > /dev/null
            fi
            
            # Every 20th request, generate memory load  
            if [ $((RANDOM % 20)) -eq 0 ]; then
              curl -s -X POST "${API_URL}/load/memory/50" > /dev/null
            fi
            
            sleep 0.5
          done
      restartPolicy: Never
  backoffLimit: 4
```

---

## **5. Deployment and Testing**

### **5.1 Build and Deploy Application**
```bash
# Build the Docker image
docker build -t autoscale-api .

# Load image into Minikube
minikube image load autoscale-api:latest

# Verify image is loaded
minikube image list | grep autoscale-api

# Apply Kubernetes manifests
kubectl apply -f namespace.yaml
kubectl apply -f deployment.yaml
kubectl apply -f hpa.yaml

# Wait for deployment to be ready
kubectl rollout status deployment/autoscale-api -n autoscale-demo
```

### **5.2 Verify Initial Setup**
```bash
# Check pods
kubectl get pods -n autoscale-demo

# Check services
kubectl get services -n autoscale-demo

# Check HPA status
kubectl get hpa -n autoscale-demo -w
```

### **5.3 Test the Application**
```bash
# Port forward to access the service
kubectl port-forward -n autoscale-demo svc/autoscale-api-service 8080:80 &

# Test basic endpoints
curl http://localhost:8080/WeatherForecast
curl http://localhost:8080/info
curl http://localhost:8080/health
curl http://localhost:8080/metrics

# Test load generation endpoints
curl http://localhost:8080/load/cpu/10
curl -X POST http://localhost:8080/load/memory/100
```

### **5.4 Generate Load and Monitor Autoscaling**
```bash
# Start load generator
kubectl apply -f load-generator.yaml

# Monitor HPA in real-time
kubectl get hpa -n autoscale-demo -w

# Monitor pods scaling
kubectl get pods -n autoscale-demo -w

# Check resource usage
kubectl top pods -n autoscale-demo

# Check detailed HPA information
kubectl describe hpa autoscale-api-hpa -n autoscale-demo
```

### **5.5 Monitor with Real-time Dashboard**
```bash
# Open Kubernetes dashboard
minikube dashboard

# Or use terminal-based monitoring
watch "kubectl get pods,hpa -n autoscale-demo"
```

---

## **6. Advanced Autoscaling Scenarios**

### **6.1 Custom Metrics Setup (Optional)**
```bash
# Install Prometheus for custom metrics
helm repo add prometheus-community https://prometheus-community.github.io/helm-charts
helm install prometheus prometheus-community/kube-prometheus-stack -n monitoring --create-namespace

# Verify Prometheus is collecting metrics
kubectl get pods -n monitoring
```

### **6.2 Stress Testing with Different Patterns**
```bash
# Create burst load test
kubectl run -it --rm burst-test --image=curlimages/curl -n autoscale-demo -- sh

# Inside the pod, run burst load
for i in {1..100}; do
  curl -s "http://autoscale-api-service.autoscale-demo/load/cpu/30" &
done
wait
```

---

## **7. Monitoring and Analysis**

### **7.1 Useful Monitoring Commands**
```bash
# Watch HPA events
kubectl get events -n autoscale-demo --field-selector involvedObject.kind=HorizontalPodAutoscaler --sort-by=.lastTimestamp

# Check pod resource usage over time
kubectl top pods -n autoscale-demo --containers

# Get detailed HPA description
kubectl describe hpa autoscale-api-hpa -n autoscale-demo

# Check deployment status
kubectl describe deployment autoscale-api -n autoscale-demo
```

### **7.2 Performance Metrics Collection**
```bash
# Collect metrics during load test
while true; do
  kubectl get hpa -n autoscale-demo
  kubectl top pods -n autoscale-demo
  echo "---"
  sleep 10
done
```

---

## **8. Cleanup**

### **8.1 Remove Resources**
```bash
# Stop port-forwarding if running
pkill -f "kubectl port-forward"

# Delete all resources
kubectl delete -f load-generator.yaml
kubectl delete -f hpa.yaml
kubectl delete -f deployment.yaml
kubectl delete -f namespace.yaml

# Remove Docker images
docker rmi autoscale-api:latest

# Stop Minikube (optional)
minikube stop
```

---

## **Troubleshooting**

### **Common Issues and Solutions**

**1. HPA shows "unknown" for metrics:**
```bash
# Check metrics server
kubectl get apiservices | grep metrics
kubectl top nodes

# Restart metrics server if needed
minikube addons disable metrics-server
minikube addons enable metrics-server
```

**2. Pods not scaling:**
```bash
# Check resource requests are set
kubectl describe deployment autoscale-api -n autoscale-demo

# Verify HPA configuration
kubectl describe hpa autoscale-api-hpa -n autoscale-demo
```

**3. Application not responding:**
```bash
# Check pod logs
kubectl logs -n autoscale-demo deployment/autoscale-api

# Check service endpoints
kubectl get endpoints -n autoscale-demo
```

**4. Memory issues:**
```bash
# Check memory usage
kubectl top pods -n autoscale-demo --containers

# Adjust memory limits if needed
kubectl patch deployment autoscale-api -n autoscale-demo -p '{"spec":{"template":{"spec":{"containers":[{"name":"autoscale-api","resources":{"limits":{"memory":"1Gi"}}}]}}}}'
```

---

## **Learning Objectives Achieved**

✅ Built and containerized .NET Core 10.0 application  
✅ Configured Kubernetes deployments with resource limits  
✅ Implemented Horizontal Pod Autoscaling with CPU and memory metrics  
✅ Tested autoscaling behavior under load  
✅ Monitored scaling events and performance  
✅ Applied best practices for production-ready autoscaling

This lab provides practical experience with autoscaling in Kubernetes using real .NET Core applications and realistic load patterns.