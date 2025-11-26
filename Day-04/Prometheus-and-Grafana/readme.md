# Monitoring Demo with Prometheus and Grafana on Kubernetes

A comprehensive demo showing how to implement monitoring in .NET Core 10.0 using Prometheus and Grafana deployed on Minikube/Kubernetes.

## Prerequisites

- Minikube
- kubectl
- .NET 10.0 SDK
- Docker

## Quick Start

### 1. Start Minikube and Enable Required Addons

```bash
# Start Minikube with sufficient resources
minikube start --cpus=4 --memory=8g --disk-size=20g

# Enable metrics server and ingress
minikube addons enable metrics-server
minikube addons enable ingress

# Set up Docker environment to use Minikube's Docker daemon
eval $(minikube docker-env)

# Check cluster status
kubectl cluster-info
kubectl get nodes
```

### 2. Build the Application Docker Image

```bash
# Navigate to the project root
cd monitoring-demo-k8s

# Build the Docker image
docker build -t monitoring-demo-api:1.0.0 -f Dockerfile .

# Verify the image was built
docker images | grep monitoring-demo-api
```

### 3. Deploy the Application to Kubernetes

```bash
# Create namespace
kubectl apply -f k8s/namespace.yaml

# Deploy the application
kubectl apply -f k8s/deployment.yaml
kubectl apply -f k8s/service.yaml

# Wait for pods to be ready
kubectl get pods -n monitoring-demo --watch
```

### 4. Deploy the Monitoring Stack

```bash
# Create ConfigMap for Prometheus configuration
kubectl apply -f k8s/prometheus-config.yaml

# Deploy Prometheus
kubectl apply -f k8s/prometheus-deployment.yaml

# Deploy Grafana
kubectl apply -f k8s/grafana-deployment.yaml

# Check all resources
kubectl get all -n monitoring-demo
```

### 5. Access the Application and Monitoring Tools

```bash
# Get the Minikube IP
minikube ip

# Create temporary port forwarding for testing
kubectl port-forward -n monitoring-demo svc/monitoring-demo-service 8080:80 &
kubectl port-forward -n monitoring-demo svc/prometheus-service 9090:9090 &
kubectl port-forward -n monitoring-demo svc/grafana-service 3000:3000 &
```

Now you can access:
- **API**: http://localhost:8080
- **Prometheus**: http://localhost:9090
- **Grafana**: http://localhost:3000 (admin/admin)

### 6. Generate Load and Test Monitoring

```bash
# Generate load to see metrics in action
./scripts/generate-load.sh

# Or manually call endpoints
curl "http://localhost:8080/api/products"
curl "http://localhost:8080/api/system/load?durationMs=500"
curl "http://localhost:8080/health"
curl "http://localhost:8080/metrics"
```

## Kubernetes Manifest Files

### k8s/namespace.yaml
```yaml
apiVersion: v1
kind: Namespace
metadata:
  name: monitoring-demo
  labels:
    name: monitoring-demo
```

### k8s/deployment.yaml
```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: monitoring-demo-api
  namespace: monitoring-demo
  labels:
    app: monitoring-demo-api
spec:
  replicas: 2
  selector:
    matchLabels:
      app: monitoring-demo-api
  template:
    metadata:
      labels:
        app: monitoring-demo-api
      annotations:
        prometheus.io/scrape: "true"
        prometheus.io/port: "80"
        prometheus.io/path: "/metrics"
    spec:
      containers:
      - name: monitoring-demo-api
        image: monitoring-demo-api:1.0.0
        imagePullPolicy: IfNotPresent
        ports:
        - containerPort: 80
        env:
        - name: ASPNETCORE_ENVIRONMENT
          value: "Production"
        - name: ASPNETCORE_URLS
          value: "http://*:80"
        resources:
          requests:
            memory: "256Mi"
            cpu: "100m"
          limits:
            memory: "512Mi"
            cpu: "500m"
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
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata:
  name: monitoring-demo-api-hpa
  namespace: monitoring-demo
spec:
  scaleTargetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: monitoring-demo-api
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

### k8s/service.yaml
```yaml
apiVersion: v1
kind: Service
metadata:
  name: monitoring-demo-service
  namespace: monitoring-demo
  labels:
    app: monitoring-demo-api
spec:
  selector:
    app: monitoring-demo-api
  ports:
  - name: http
    port: 80
    targetPort: 80
  type: ClusterIP
```

### k8s/prometheus-config.yaml
```yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: prometheus-config
  namespace: monitoring-demo
data:
  prometheus.yml: |
    global:
      scrape_interval: 15s
      evaluation_interval: 15s

    rule_files:
      - /etc/prometheus/rules.yml

    scrape_configs:
      - job_name: 'monitoring-demo-api'
        kubernetes_sd_configs:
        - role: endpoints
          namespaces:
            names:
              - monitoring-demo
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
          target_label: kubernetes_name

      - job_name: 'kubernetes-apiservers'
        kubernetes_sd_configs:
        - role: endpoints
        scheme: https
        tls_config:
          ca_file: /var/run/secrets/kubernetes.io/serviceaccount/ca.crt
        bearer_token_file: /var/run/secrets/kubernetes.io/serviceaccount/token
        relabel_configs:
        - source_labels: [__meta_kubernetes_namespace, __meta_kubernetes_service_name, __meta_kubernetes_endpoint_port_name]
          action: keep
          regex: default;kubernetes;https

  rules.yml: |
    groups:
    - name: monitoring-demo-rules
      rules:
      - alert: HighErrorRate
        expr: rate(api_errors_total[5m]) > 0.1
        for: 2m
        labels:
          severity: warning
        annotations:
          summary: "High error rate detected"
          description: "Error rate is {{ $value }} errors per second"

      - alert: HighLatency
        expr: histogram_quantile(0.95, rate(http_requests_duration_seconds_bucket[5m])) > 1
        for: 2m
        labels:
          severity: warning
        annotations:
          summary: "High latency detected"
          description: "95th percentile latency is {{ $value }} seconds"
```

### k8s/prometheus-deployment.yaml
```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: prometheus
  namespace: monitoring-demo
  labels:
    app: prometheus
spec:
  replicas: 1
  selector:
    matchLabels:
      app: prometheus
  template:
    metadata:
      labels:
        app: prometheus
    spec:
      containers:
      - name: prometheus
        image: prom/prometheus:latest
        args:
          - '--config.file=/etc/prometheus/prometheus.yml'
          - '--storage.tsdb.path=/prometheus'
          - '--web.console.libraries=/etc/prometheus/console_libraries'
          - '--web.console.templates=/etc/prometheus/consoles'
          - '--storage.tsdb.retention.time=200h'
          - '--web.enable-lifecycle'
          - '--web.enable-admin-api'
        ports:
        - containerPort: 9090
        volumeMounts:
        - name: prometheus-config-volume
          mountPath: /etc/prometheus/
        - name: prometheus-storage-volume
          mountPath: /prometheus/
        resources:
          requests:
            memory: "512Mi"
            cpu: "300m"
          limits:
            memory: "1Gi"
            cpu: "500m"
      volumes:
      - name: prometheus-config-volume
        configMap:
          name: prometheus-config
      - name: prometheus-storage-volume
        emptyDir: {}
---
apiVersion: v1
kind: Service
metadata:
  name: prometheus-service
  namespace: monitoring-demo
  labels:
    app: prometheus
spec:
  selector:
    app: prometheus
  ports:
  - name: web
    port: 9090
    targetPort: 9090
  type: NodePort
```

### k8s/grafana-deployment.yaml
```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: grafana
  namespace: monitoring-demo
  labels:
    app: grafana
spec:
  replicas: 1
  selector:
    matchLabels:
      app: grafana
  template:
    metadata:
      labels:
        app: grafana
    spec:
      containers:
      - name: grafana
        image: grafana/grafana:latest
        ports:
        - containerPort: 3000
        env:
        - name: GF_SECURITY_ADMIN_USER
          value: admin
        - name: GF_SECURITY_ADMIN_PASSWORD
          value: admin
        - name: GF_INSTALL_PLUGINS
          value: "grafana-piechart-panel"
        volumeMounts:
        - name: grafana-storage-volume
          mountPath: /var/lib/grafana
        - name: grafana-datasource-volume
          mountPath: /etc/grafana/provisioning/datasources
        - name: grafana-dashboard-volume
          mountPath: /etc/grafana/provisioning/dashboards
        resources:
          requests:
            memory: "256Mi"
            cpu: "100m"
          limits:
            memory: "512Mi"
            cpu: "300m"
      volumes:
      - name: grafana-storage-volume
        emptyDir: {}
      - name: grafana-datasource-volume
        configMap:
          name: grafana-datasource-config
      - name: grafana-dashboard-volume
        configMap:
          name: grafana-dashboard-config
---
apiVersion: v1
kind: ConfigMap
metadata:
  name: grafana-datasource-config
  namespace: monitoring-demo
data:
  prometheus.yaml: |
    apiVersion: 1
    datasources:
    - name: Prometheus
      type: prometheus
      access: proxy
      url: http://prometheus-service:9090
      isDefault: true
      editable: true
---
apiVersion: v1
kind: ConfigMap
metadata:
  name: grafana-dashboard-config
  namespace: monitoring-demo
data:
  dashboard-provider.yaml: |
    apiVersion: 1
    providers:
    - name: 'default'
      orgId: 1
      folder: ''
      type: file
      disableDeletion: false
      editable: true
      options:
        path: /etc/grafana/provisioning/dashboards
  monitoring-demo-dashboard.json: |
    {
      "dashboard": {
        "id": null,
        "title": "Monitoring Demo Dashboard",
        "tags": [ "monitoring-demo" ],
        "timezone": "browser",
        "panels": [
          {
            "id": 1,
            "title": "HTTP Requests Rate",
            "type": "graph",
            "targets": [
              {
                "expr": "rate(http_requests_received_total[5m])",
                "legendFormat": "{{method}} {{code}}",
                "refId": "A"
              }
            ],
            "gridPos": { "h": 8, "w": 12, "x": 0, "y": 0 }
          },
          {
            "id": 2,
            "title": "Error Rate",
            "type": "graph",
            "targets": [
              {
                "expr": "rate(api_errors_total[5m])",
                "legendFormat": "{{controller}} {{method}}",
                "refId": "A"
              }
            ],
            "gridPos": { "h": 8, "w": 12, "x": 12, "y": 0 }
          }
        ],
        "time": { "from": "now-1h", "to": "now" }
      }
    }
---
apiVersion: v1
kind: Service
metadata:
  name: grafana-service
  namespace: monitoring-demo
  labels:
    app: grafana
spec:
  selector:
    app: grafana
  ports:
  - name: web
    port: 3000
    targetPort: 3000
  type: NodePort
```

## Dockerfile

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project file and restore as distinct layers
COPY ["MonitoringDemo.API.csproj", "."]
RUN dotnet restore "MonitoringDemo.API.csproj"

# Copy everything else and build
COPY . .
RUN dotnet build "MonitoringDemo.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "MonitoringDemo.API.csproj" -c Release -o /app/publish

# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
EXPOSE 80

COPY --from=publish /app/publish .

# Create a non-root user
RUN groupadd -r appuser && useradd -r -g appuser appuser
USER appuser

ENTRYPOINT ["dotnet", "MonitoringDemo.API.dll"]
```

## Load Generation Script

**scripts/generate-load.sh**
```bash
#!/bin/bash

echo "Starting load generation for monitoring demo..."

API_URL="http://localhost:8080"

# Function to make API calls with random delays
make_api_call() {
    local endpoint=$1
    local method=$2
    local data=$3
    
    echo "Calling: $method $endpoint"
    
    if [ "$method" = "GET" ]; then
        curl -s -o /dev/null -w "%{http_code}" "$API_URL$endpoint"
    elif [ "$method" = "POST" ]; then
        curl -s -o /dev/null -w "%{http_code}" -X POST -H "Content-Type: application/json" -d "$data" "$API_URL$endpoint"
    else
        curl -s -o /dev/null -w "%{http_code}" -X $method "$API_URL$endpoint"
    fi
    echo " - $endpoint"
}

# Generate load for 5 minutes
end=$((SECONDS+300))
request_count=0

echo "Load generation started. Will run for 5 minutes..."

while [ $SECONDS -lt $end ]; do
    # Randomly choose an endpoint to call
    case $((RANDOM % 10)) in
        0|1|2|3)
            make_api_call "/api/products" "GET"
            ;;
        4|5)
            make_api_call "/api/products/1" "GET"
            ;;
        6)
            make_api_call "/api/system/info" "GET"
            ;;
        7|8)
            duration=$((RANDOM % 1000 + 100))
            make_api_call "/api/system/load?durationMs=$duration" "GET"
            ;;
        9)
            product_data='{"name": "Test Product", "price": 99.99, "stock": 10}'
            make_api_call "/api/products" "POST" "$product_data"
            ;;
    esac
    
    request_count=$((request_count + 1))
    
    # Random delay between requests (0.1 - 1 second)
    sleep $(bc <<< "scale=2; $RANDOM/32767")
done

echo "Load generation completed. Total requests: $request_count"
```

## Key Features Demonstrated

### 1. Kubernetes Native Deployment
- Multi-container deployment with proper resource limits
- Horizontal Pod Autoscaler configuration
- Liveness and readiness probes
- Service discovery and networking

### 2. Prometheus Configuration
- Kubernetes service discovery
- Custom alerting rules
- ConfigMap for configuration management
- Proper resource allocation

### 3. Grafana Setup
- Data source provisioning
- Dashboard configuration via ConfigMap
- Persistent storage configuration

### 4. Application Features
- Health checks integrated with Kubernetes probes
- Metrics endpoint for Prometheus scraping
- Structured logging
- Custom business metrics

## Monitoring and Observability

### Exposed Metrics
- **HTTP Metrics**: Request duration, status codes, throughput
- **Business Metrics**: Product operations, error rates
- **System Metrics**: CPU, memory, GC collections
- **Custom Metrics**: Operation durations, background tasks

### Kubernetes Monitoring
- Pod resource usage
- Horizontal scaling based on CPU
- Service discovery and scraping
- Cluster-level metrics

## Useful Commands

### Monitoring Commands
```bash
# Watch pods
kubectl get pods -n monitoring-demo -w

# Check logs
kubectl logs -n monitoring-demo -l app=monitoring-demo-api --tail=50

# Check HPA status
kubectl get hpa -n monitoring-demo

# Check services
kubectl get svc -n monitoring-demo

# Describe resources for debugging
kubectl describe pod -n monitoring-demo <pod-name>
```

### Port Forwarding for Access
```bash
# API access
kubectl port-forward -n monitoring-demo svc/monitoring-demo-service 8080:80

# Prometheus access
kubectl port-forward -n monitoring-demo svc/prometheus-service 9090:9090

# Grafana access
kubectl port-forward -n monitoring-demo svc/grafana-service 3000:3000
```

### Load Testing
```bash
# Make script executable
chmod +x scripts/generate-load.sh

# Run load generation
./scripts/generate-load.sh
```

## Troubleshooting

### Common Issues

1. **Image Pull Errors**
   ```bash
   # Make sure you're using Minikube's Docker daemon
   eval $(minikube docker-env)
   docker images | grep monitoring-demo-api
   ```

2. **Pod CrashLoopBackOff**
   ```bash
   # Check pod logs
   kubectl logs -n monitoring-demo <pod-name>
   
   # Describe pod for events
   kubectl describe pod -n monitoring-demo <pod-name>
   ```

3. **Metrics Not Scraping**
   ```bash
   # Check Prometheus targets
   # Access Prometheus UI and go to Status -> Targets
   
   # Check service discovery
   kubectl get endpoints -n monitoring-demo
   ```

4. **Resource Issues**
   ```bash
   # Check resource usage
   kubectl top pods -n monitoring-demo
   kubectl top nodes
   ```

## Cleanup

```bash
# Delete all resources
kubectl delete -f k8s/ --recursive

# Delete namespace
kubectl delete namespace monitoring-demo

# Stop Minikube
minikube stop

# Delete Minikube cluster (optional)
minikube delete
```

## Learning Objectives

- Containerize .NET applications for Kubernetes
- Implement comprehensive monitoring in microservices
- Configure Prometheus for Kubernetes service discovery
- Set up Grafana with provisioning
- Implement proper health checks and resource management
- Understand horizontal pod autoscaling
- Practice Kubernetes deployment patterns

This demo provides a complete, production-ready monitoring setup that demonstrates modern cloud-native observability practices on Kubernetes.