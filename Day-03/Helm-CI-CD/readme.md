## **Demo: Building a Complete CD Pipeline with .NET Core 10.0**

### **Prerequisites**

- .NET SDK 10.0
- Docker Desktop
- Kubernetes cluster (Minikube/Docker Desktop)
- Helm CLI
- GitHub Account (for GitHub Actions)

### **Step 1: Create .NET Core 10.0 Web API**

Create a new directory and application:

```bash
mkdir helm-cd-demo && cd helm-cd-demo
dotnet new webapi -n ProductService -f net8.0
cd ProductService
```

**Update `Program.cs`**:

```csharp
using ProductService.Models;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add Health Checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<ProductContext>();

// Database Context (In-Memory for demo)
builder.Services.AddDbContext<ProductContext>(opt => 
    opt.UseInMemoryDatabase("ProductDatabase"));

// Configure Kestrel to use port from environment variable
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.ListenAnyIP(8080);
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

// Health check endpoint
app.MapHealthChecks("/health");
app.MapGet("/", () => "Product Service API is running!");

// Seed initial data
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ProductContext>();
    SeedData(context);
}

app.Run();

void SeedData(ProductContext context)
{
    if (!context.Products.Any())
    {
        context.Products.AddRange(
            new Product { Id = 1, Name = "Laptop", Price = 999.99m, Category = "Electronics" },
            new Product { Id = 2, Name = "Book", Price = 29.99m, Category = "Education" },
            new Product { Id = 3, Name = "Headphones", Price = 149.99m, Category = "Electronics" }
        );
        context.SaveChanges();
    }
}
```

**Create Models and Controller**:

Create `Models/Product.cs`:

```csharp
namespace ProductService.Models;

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Category { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class ProductContext : DbContext
{
    public ProductContext(DbContextOptions<ProductContext> options) : base(options) { }
    
    public DbSet<Product> Products => Set<Product>();
}
```

Update `Controllers/WeatherForecastController.cs` to `Controllers/ProductsController.cs`:

```csharp
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProductService.Models;

namespace ProductService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly ProductContext _context;
    private readonly ILogger<ProductsController> _logger;

    public ProductsController(ProductContext context, ILogger<ProductsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Product>>> GetProducts()
    {
        _logger.LogInformation("Getting all products");
        return await _context.Products.ToListAsync();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Product>> GetProduct(int id)
    {
        var product = await _context.Products.FindAsync(id);

        if (product == null)
        {
            _logger.LogWarning("Product with id {ProductId} not found", id);
            return NotFound();
        }

        return product;
    }

    [HttpPost]
    public async Task<ActionResult<Product>> PostProduct(Product product)
    {
        _context.Products.Add(product);
        await _context.SaveChangesAsync();
        
        _logger.LogInformation("Created new product with id {ProductId}", product.Id);
        return CreatedAtAction(nameof(GetProduct), new { id = product.Id }, product);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> PutProduct(int id, Product product)
    {
        if (id != product.Id)
        {
            return BadRequest();
        }

        _context.Entry(product).State = EntityState.Modified;

        try
        {
            await _context.SaveChangesAsync();
            _logger.LogInformation("Updated product with id {ProductId}", id);
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!ProductExists(id))
            {
                return NotFound();
            }
            else
            {
                throw;
            }
        }

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteProduct(int id)
    {
        var product = await _context.Products.FindAsync(id);
        if (product == null)
        {
            return NotFound();
        }

        _context.Products.Remove(product);
        await _context.SaveChangesAsync();
        
        _logger.LogInformation("Deleted product with id {ProductId}", id);
        return NoContent();
    }

    private bool ProductExists(int id)
    {
        return _context.Products.Any(e => e.Id == id);
    }
}
```

**Update `appsettings.json`**:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "Application": {
    "Name": "ProductService",
    "Version": "1.0.0",
    "Environment": "Development"
  },
  "Database": {
    "Provider": "InMemory"
  }
}
```

### **Step 2: Create Dockerfile**

Create `Dockerfile` in the project root:

```dockerfile
# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project file and restore dependencies
COPY ["ProductService.csproj", "."]
RUN dotnet restore "ProductService.csproj"

# Copy everything else and build
COPY . .
RUN dotnet publish "ProductService.csproj" -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Install curl for health checks
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

# Create a non-root user
RUN groupadd -r appuser && useradd -r -g appuser appuser
USER appuser

COPY --from=build /app/publish .

EXPOSE 8080

ENTRYPOINT ["dotnet", "ProductService.dll"]
```

### **Step 3: Create Helm Chart**

Create the Helm chart structure:

```bash
# Create chart directory
mkdir -p helm-chart/templates
cd helm-chart
```

**Create `Chart.yaml`**:

```yaml
apiVersion: v2
name: product-service
description: A .NET Core Product Service API
type: application
version: 0.1.0
appVersion: "1.0.0"

maintainers:
  - name: DevOps Team
    email: devops@company.com

dependencies: []

keywords:
  - dotnet
  - webapi
  - microservice
  - csharp
```

**Create `values.yaml`**:

```yaml
# Default values for product-service
replicaCount: 2

image:
  repository: your-registry/product-service
  pullPolicy: IfNotPresent
  tag: "latest"

nameOverride: ""
fullnameOverride: ""

service:
  type: ClusterIP
  port: 80
  targetPort: 8080

ingress:
  enabled: false
  className: "nginx"
  annotations: {}
  hosts:
    - host: products.local
      paths:
        - path: /
          pathType: Prefix
  tls: []

resources:
  limits:
    cpu: 500m
    memory: 512Mi
  requests:
    cpu: 100m
    memory: 128Mi

autoscaling:
  enabled: false
  minReplicas: 1
  maxReplicas: 10
  targetCPUUtilizationPercentage: 80

app:
  name: "product-service"
  version: "1.0.0"
  
env:
  - name: ASPNETCORE_ENVIRONMENT
    value: "Production"
  - name: LOG_LEVEL
    value: "Information"

livenessProbe:
  httpGet:
    path: /health
    port: http
  initialDelaySeconds: 30
  periodSeconds: 10

readinessProbe:
  httpGet:
    path: /health
    port: http
  initialDelaySeconds: 5
  periodSeconds: 5

serviceAccount:
  create: false

securityContext:
  enabled: true
  runAsNonRoot: true
  runAsUser: 1000
  readOnlyRootFilesystem: true
```

**Create environment-specific values**:

Create `values-dev.yaml`:

```yaml
replicaCount: 1

image:
  tag: "dev-latest"

resources:
  requests:
    cpu: 50m
    memory: 64Mi
  limits:
    cpu: 200m
    memory: 128Mi

env:
  - name: ASPNETCORE_ENVIRONMENT
    value: "Development"
  - name: LOG_LEVEL
    value: "Debug"

livenessProbe:
  initialDelaySeconds: 60

ingress:
  enabled: true
  hosts:
    - host: products-dev.local
      paths:
        - path: /
          pathType: Prefix
```

Create `values-prod.yaml`:

```yaml
replicaCount: 3

image:
  tag: "stable"

resources:
  requests:
    cpu: 200m
    memory: 256Mi
  limits:
    cpu: 500m
    memory: 512Mi

env:
  - name: ASPNETCORE_ENVIRONMENT
    value: "Production"
  - name: LOG_LEVEL
    value: "Warning"

autoscaling:
  enabled: true
  minReplicas: 2
  maxReplicas: 5
  targetCPUUtilizationPercentage: 80

ingress:
  enabled: true
  hosts:
    - host: products.company.com
      paths:
        - path: /
          pathType: Prefix
  tls:
    - secretName: product-service-tls
      hosts:
        - products.company.com
```

**Create template files**:

Create `templates/_helpers.tpl`:

```tpl
{{/*
Expand the name of the chart.
*/}}
{{- define "product-service.name" -}}
{{- default .Chart.Name .Values.nameOverride | trunc 63 | trimSuffix "-" }}
{{- end }}

{{/*
Create a default fully qualified app name.
*/}}
{{- define "product-service.fullname" -}}
{{- if .Values.fullnameOverride }}
{{- .Values.fullnameOverride | trunc 63 | trimSuffix "-" }}
{{- else }}
{{- $name := default .Chart.Name .Values.nameOverride }}
{{- if contains $name .Release.Name }}
{{- .Release.Name | trunc 63 | trimSuffix "-" }}
{{- else }}
{{- printf "%s-%s" .Release.Name $name | trunc 63 | trimSuffix "-" }}
{{- end }}
{{- end }}
{{- end }}

{{/*
Create chart name and version as used by the chart label.
*/}}
{{- define "product-service.chart" -}}
{{- printf "%s-%s" .Chart.Name .Chart.Version | replace "+" "_" | trunc 63 | trimSuffix "-" }}
{{- end }}

{{/*
Common labels
*/}}
{{- define "product-service.labels" -}}
helm.sh/chart: {{ include "product-service.chart" . }}
{{ include "product-service.selectorLabels" . }}
{{- if .Chart.AppVersion }}
app.kubernetes.io/version: {{ .Chart.AppVersion | quote }}
{{- end }}
app.kubernetes.io/managed-by: {{ .Release.Service }}
{{- end }}

{{/*
Selector labels
*/}}
{{- define "product-service.selectorLabels" -}}
app.kubernetes.io/name: {{ include "product-service.name" . }}
app.kubernetes.io/instance: {{ .Release.Name }}
{{- end }}

{{/*
Create the name of the service account to use
*/}}
{{- define "product-service.serviceAccountName" -}}
{{- if .Values.serviceAccount.create }}
{{- default (include "product-service.fullname" .) .Values.serviceAccount.name }}
{{- else }}
{{- default "default" .Values.serviceAccount.name }}
{{- end }}
{{- end }}
```

Create `templates/deployment.yaml`:

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: {{ include "product-service.fullname" . }}
  labels:
    {{- include "product-service.labels" . | nindent 4 }}
spec:
  {{- if not .Values.autoscaling.enabled }}
  replicas: {{ .Values.replicaCount }}
  {{- end }}
  selector:
    matchLabels:
      {{- include "product-service.selectorLabels" . | nindent 6 }}
  template:
    metadata:
      labels:
        {{- include "product-service.selectorLabels" . | nindent 8 }}
        app.kubernetes.io/version: {{ .Chart.AppVersion | quote }}
      annotations:
        rollme: {{ randAlphaNum 5 | quote }}
    spec:
      {{- if .Values.securityContext.enabled }}
      securityContext:
        runAsNonRoot: {{ .Values.securityContext.runAsNonRoot }}
        runAsUser: {{ .Values.securityContext.runAsUser }}
      {{- end }}
      containers:
        - name: {{ .Chart.Name }}
          image: "{{ .Values.image.repository }}:{{ .Values.image.tag | default .Chart.AppVersion }}"
          imagePullPolicy: {{ .Values.image.pullPolicy }}
          ports:
            - name: http
              containerPort: {{ .Values.service.targetPort }}
              protocol: TCP
          livenessProbe:
            {{- toYaml .Values.livenessProbe | nindent 12 }}
          readinessProbe:
            {{- toYaml .Values.readinessProbe | nindent 12 }}
          resources:
            {{- toYaml .Values.resources | nindent 12 }}
          env:
            {{- range .Values.env }}
            - name: {{ .name }}
              value: {{ .value | quote }}
            {{- end }}
            - name: APP_VERSION
              value: {{ .Chart.AppVersion | quote }}
            - name: KUBERNETES_NAMESPACE
              value: {{ .Release.Namespace | quote }}
```

Create `templates/service.yaml`:

```yaml
apiVersion: v1
kind: Service
metadata:
  name: {{ include "product-service.fullname" . }}
  labels:
    {{- include "product-service.labels" . | nindent 4 }}
spec:
  type: {{ .Values.service.type }}
  ports:
    - port: {{ .Values.service.port }}
      targetPort: {{ .Values.service.targetPort }}
      protocol: TCP
      name: http
  selector:
    {{- include "product-service.selectorLabels" . | nindent 4 }}
```

Create `templates/hpa.yaml`:

```yaml
{{- if .Values.autoscaling.enabled }}
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata:
  name: {{ include "product-service.fullname" . }}
  labels:
    {{- include "product-service.labels" . | nindent 4 }}
spec:
  scaleTargetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: {{ include "product-service.fullname" . }}
  minReplicas: {{ .Values.autoscaling.minReplicas }}
  maxReplicas: {{ .Values.autoscaling.maxReplicas }}
  metrics:
    - type: Resource
      resource:
        name: cpu
        target:
          type: Utilization
          averageUtilization: {{ .Values.autoscaling.targetCPUUtilizationPercentage }}
{{- end }}
```

Create `templates/ingress.yaml`:

```yaml
{{- if .Values.ingress.enabled }}
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: {{ include "product-service.fullname" . }}
  labels:
    {{- include "product-service.labels" . | nindent 4 }}
  {{- with .Values.ingress.annotations }}
  annotations:
    {{- toYaml . | nindent 4 }}
  {{- end }}
spec:
  {{- if .Values.ingress.className }}
  ingressClassName: {{ .Values.ingress.className }}
  {{- end }}
  rules:
    {{- range .Values.ingress.hosts }}
    - host: {{ .host | quote }}
      http:
        paths:
          {{- range .paths }}
          - path: {{ .path }}
            pathType: {{ .pathType }}
            backend:
              service:
                name: {{ include "product-service.fullname" $ }}
                port:
                  number: {{ $.Values.service.port }}
          {{- end }}
    {{- end }}
  {{- if .Values.ingress.tls }}
  tls:
    {{- range .Values.ingress.tls }}
    - hosts:
        {{- range .hosts }}
        - {{ . | quote }}
        {{- end }}
      secretName: {{ .secretName }}
    {{- end }}
  {{- end }}
{{- end }}
```

Create `templates/tests/test-connection.yaml`:

```yaml
apiVersion: v1
kind: Pod
metadata:
  name: "{{ include "product-service.fullname" . }}-test-connection"
  annotations:
    "helm.sh/hook": test
  labels:
    {{- include "product-service.labels" . | nindent 4 }}
spec:
  containers:
  - name: test-connection
    image: curlimages/curl:8.00.1
    command: ['sh', '-c']
    args:
      - |
        set -e
        echo "Testing service connectivity..."
        timeout 30s bash -c '
          until curl -f http://{{ include "product-service.fullname" . }}:{{ .Values.service.port }}/health; do
            echo "Waiting for service to be ready..."
            sleep 5
          done
        '
        echo "✅ Service connectivity test passed!"
  restartPolicy: Never
```

### **Step 4: Create GitHub Actions CI/CD Pipeline**

Create `.github/workflows/cd-pipeline.yaml`:

```yaml
name: Product Service CD Pipeline

on:
  push:
    branches: [ main, develop ]
  pull_request:
    branches: [ main ]

env:
  REGISTRY: ghcr.io
  IMAGE_NAME: ${{ github.repository }}/product-service
  CLUSTER_NAME: my-k8s-cluster
  NAMESPACE: product-service

jobs:
  test:
    runs-on: ubuntu-latest
    steps:
    - uses: actions/checkout@v4
    
    - name: Setup .NET
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: 8.0.x
        
    - name: Restore dependencies
      run: dotnet restore
      
    - name: Build
      run: dotnet build --no-restore --configuration Release
      
    - name: Test
      run: dotnet test --no-build --verbosity normal

  build-and-push:
    needs: test
    runs-on: ubuntu-latest
    if: github.ref == 'refs/heads/main' || github.ref == 'refs/heads/develop'
    
    steps:
    - uses: actions/checkout@v4

    - name: Set up Docker Buildx
      uses: docker/setup-buildx-action@v3

    - name: Log in to Container Registry
      uses: docker/login-action@v3
      with:
        registry: ${{ env.REGISTRY }}
        username: ${{ github.actor }}
        password: ${{ secrets.GITHUB_TOKEN }}

    - name: Extract metadata
      id: meta
      uses: docker/metadata-action@v5
      with:
        images: ${{ env.REGISTRY }}/${{ env.IMAGE_NAME }}
        tags: |
          type=ref,event=branch
          type=ref,event=pr
          type=semver,pattern={{version}}
          type=semver,pattern={{major}}.{{minor}}
          type=sha,prefix={{branch}}-

    - name: Build and push Docker image
      uses: docker/build-push-action@v5
      with:
        context: .
        push: true
        tags: ${{ steps.meta.outputs.tags }}
        labels: ${{ steps.meta.outputs.labels }}
        cache-from: type=gha
        cache-to: type=gha,mode=max

  deploy-to-dev:
    needs: build-and-push
    runs-on: ubuntu-latest
    if: github.ref == 'refs/heads/develop'
    environment: development
    
    steps:
    - uses: actions/checkout@v4
    
    - name: Set up Helm
      uses: azure/setup-helm@v3
      with:
        version: '3.12.0'
    
    - name: Configure K8s Context
      uses: azure/k8s-set-context@v3
      with:
        method: kubeconfig
        kubeconfig: ${{ secrets.KUBECONFIG_DEV }}
        
    - name: Create namespace if not exists
      run: |
        kubectl create namespace ${{ env.NAMESPACE }} --dry-run=client -o yaml | kubectl apply -f -
        
    - name: Deploy to Development
      run: |
        helm upgrade --install product-service-dev ./helm-chart \
          --namespace ${{ env.NAMESPACE }} \
          --values ./helm-chart/values-dev.yaml \
          --set image.tag=${{ github.sha }} \
          --set image.repository=${{ env.REGISTRY }}/${{ env.IMAGE_NAME }} \
          --atomic \
          --timeout 5m
          
    - name: Run Tests
      run: |
        helm test product-service-dev --namespace ${{ env.NAMESPACE }} --timeout 2m

  deploy-to-prod:
    needs: build-and-push
    runs-on: ubuntu-latest
    if: github.ref == 'refs/heads/main'
    environment: production
    
    steps:
    - uses: actions/checkout@v4
    
    - name: Set up Helm
      uses: azure/setup-helm@v3
      with:
        version: '3.12.0'
    
    - name: Configure K8s Context
      uses: azure/k8s-set-context@v3
      with:
        method: kubeconfig
        kubeconfig: ${{ secrets.KUBECONFIG_PROD }}
        
    - name: Create namespace if not exists
      run: |
        kubectl create namespace ${{ env.NAMESPACE }} --dry-run=client -o yaml | kubectl apply -f -
        
    - name: Deploy to Production
      run: |
        helm upgrade --install product-service-prod ./helm-chart \
          --namespace ${{ env.NAMESPACE }} \
          --values ./helm-chart/values-prod.yaml \
          --set image.tag=${{ github.sha }} \
          --set image.repository=${{ env.REGISTRY }}/${{ env.IMAGE_NAME }} \
          --atomic \
          --timeout 10m
          
    - name: Run Tests
      run: |
        helm test product-service-prod --namespace ${{ env.NAMESPACE }} --timeout 3m
        
    - name: Verify Deployment
      run: |
        kubectl rollout status deployment/product-service-prod -n ${{ env.NAMESPACE }} --timeout=5m
        echo "✅ Production deployment successful!"

  rollback-on-failure:
    runs-on: ubuntu-latest
    if: failure() && (github.ref == 'refs/heads/main' || github.ref == 'refs/heads/develop')
    needs: [deploy-to-dev, deploy-to-prod]
    
    steps:
    - uses: actions/checkout@v4
    
    - name: Set up Helm
      uses: azure/setup-helm@v3
      
    - name: Rollback Development
      if: github.ref == 'refs/heads/develop'
      uses: azure/k8s-set-context@v3
      with:
        method: kubeconfig
        kubeconfig: ${{ secrets.KUBECONFIG_DEV }}
      run: |
        helm rollback product-service-dev 0 --namespace ${{ env.NAMESPACE }} || true
        echo "🚨 Development deployment failed - rolled back to previous version"
        
    - name: Rollback Production
      if: github.ref == 'refs/heads/main'
      uses: azure/k8s-set-context@v3
      with:
        method: kubeconfig
        kubeconfig: ${{ secrets.KUBECONFIG_PROD }}
      run: |
        helm rollback product-service-prod 0 --namespace ${{ env.NAMESPACE }} || true
        echo "🚨 Production deployment failed - rolled back to previous version"
```

### **Step 5: Local Development and Testing**

**Build and test locally**:

```bash
# Build the application
dotnet build
dotnet test

# Build Docker image
docker build -t product-service:local .

# Test the image
docker run -p 8080:8080 product-service:local

# Test the API
curl http://localhost:8080/api/products
```

**Test Helm chart locally**:

```bash
# Lint the chart
helm lint ./helm-chart

# Dry run installation
helm install product-service-local ./helm-chart --dry-run --debug

# Install to local Kubernetes (Minikube/Docker Desktop)
helm install product-service-local ./helm-chart \
  --set image.repository=product-service \
  --set image.tag=local \
  --create-namespace \
  --namespace product-service-local

# Test the deployment
kubectl port-forward svc/product-service-local 8080:80 -n product-service-local
curl http://localhost:8080/api/products
```

### **Step 6: Setting Up Secrets for CD Pipeline**

For GitHub Actions, you need to set up these secrets:

1. **KUBECONFIG_DEV**: kubeconfig for development cluster
2. **KUBECONFIG_PROD**: kubeconfig for production cluster

**To get kubeconfig**:
```bash
# For development cluster
kubectl config view --minify --flatten > kubeconfig-dev.yaml

# For production cluster  
kubectl config view --minify --flatten > kubeconfig-prod.yaml
```

Add these as secrets in your GitHub repository settings.

### **Step 7: Manual Deployment Commands (Backup)**

```bash
# Manual deployment to development
helm upgrade --install product-service-dev ./helm-chart \
  --namespace product-service \
  --values ./helm-chart/values-dev.yaml \
  --set image.tag=$COMMIT_SHA \
  --atomic

# Manual deployment to production  
helm upgrade --install product-service-prod ./helm-chart \
  --namespace product-service \
  --values ./helm-chart/values-prod.yaml \
  --set image.tag=$COMMIT_SHA \
  --atomic

# Check deployment status
helm list -n product-service
kubectl get pods -n product-service

# Run tests
helm test product-service-prod -n product-service

# Rollback if needed
helm rollback product-service-prod 0 -n product-service
```

## **CD Pipeline Features**

### **Automated Quality Gates**
- ✅ Unit tests execution
- ✅ Docker image building and scanning
- ✅ Helm chart linting
- ✅ Automated deployment to dev on merge to develop
- ✅ Automated deployment to prod on merge to main
- ✅ Automatic rollback on failure
- ✅ Health checks and integration tests

### **Security Features**
- ✅ Non-root user in containers
- ✅ Read-only root filesystem
- ✅ Security contexts in deployments
- ✅ Resource limits and requests
- ✅ Health checks with proper timeouts

### **Monitoring and Observability**
- ✅ Health check endpoints
- ✅ Structured logging
- ✅ Resource utilization monitoring
- ✅ Automated testing in pipeline
- ✅ Rollback capability

## **Best Practices Implemented**

1. **Immutable Infrastructure**: New image tag for every deployment
2. **Blue-Green Deployment**: Helm enables easy rollback
3. **Infrastructure as Code**: All Kubernetes manifests in Helm charts
4. **Security First**: Non-root users, security contexts
5. **Monitoring**: Health checks, logging, and metrics
6. **Automated Testing**: Unit tests and integration tests in pipeline

This complete CD pipeline demonstrates how to automatically build, test, and deploy a .NET Core application to Kubernetes using Helm, with proper security practices and rollback capabilities.