# Kubernetes Deployment Strategies Hands-On Lab

A comprehensive hands-on lab to practice Rolling Updates, Blue-Green, and Canary deployments using a C# .NET Core API on Kubernetes.

## 📋 Lab Overview

This lab will guide you through implementing three essential Kubernetes deployment strategies:

- **Rolling Update**: Zero-downtime deployments with gradual pod replacement
- **Blue-Green**: Instant switch between two identical environments
- **Canary**: Gradual traffic shifting to new versions for risk mitigation

## 🎯 Learning Objectives

By completing this lab, you will be able to:
- Configure and execute Rolling Update deployments
- Implement manual Blue-Green deployment switches
- Set up basic Canary release patterns
- Monitor deployment progress and verify strategies
- Understand when to use each deployment strategy

## ⚙️ Prerequisites

### Required Tools
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop) or container runtime
- [kubectl](https://kubernetes.io/docs/tasks/tools/)
- Kubernetes cluster (Docker Desktop, Minikube, or cloud Kubernetes)
- [jq](https://stedolan.github.io/jq/download/) (for JSON parsing in scripts)

### Verify Your Environment
```bash
# Check .NET installation
dotnet --version

# Check Docker
docker --version

# Check Kubernetes
kubectl version --client
kubectl cluster-info
```

## 🚀 Lab Setup

### Step 1: Clone and Explore the Project
```bash
# Create project directory
mkdir k8s-deployment-lab
cd k8s-deployment-lab

# Create the .NET Core Web API project
dotnet new webapi -n K8sDeploymentDemo
cd K8sDeploymentDemo
```

### Step 2: Examine the Application Structure
The project contains:
- **Program.cs**: Main application configuration with health checks
- **WeatherForecastController.cs**: API controller with version tracking
- **Dockerfile**: Container definition
- **Kubernetes manifests**: Deployment configuration files

### Step 3: Build the Application
```bash
# Restore dependencies
dotnet restore

# Test locally
dotnet run
# Visit https://localhost:7001/weatherforecast in your browser
```

## 🐳 Containerization

### Step 4: Build Docker Images
```bash
# Build version 1.0.0
docker build -t k8s-deployment-demo:1.0.0 .

# Make a small change to the code (e.g., modify Summaries array in Controller)
# Then build version 2.0.0
docker build -t k8s-deployment-demo:2.0.0 .
```

### Step 5: Verify Docker Images
```bash
docker images | grep k8s-deployment-demo
```
You should see both versions listed.

## ☸️ Kubernetes Deployment

### Step 6: Create Kubernetes Namespace
```bash
kubectl apply -f namespace.yaml
```

### Step 7: Deploy Base Application
```bash
# Apply all base resources
kubectl apply -f deployment-base.yaml
kubectl apply -f service.yaml

# Verify deployment
kubectl get all -n deployment-lab
```

## 🔄 Strategy 1: Rolling Update

### Step 8: Configure Rolling Update
```bash
# Apply rolling update deployment
kubectl apply -f deployment-rolling-update.yaml
```

### Step 9: Observe Rolling Update
```bash
# Watch the rollout in real-time
kubectl rollout status deployment/weather-api-rolling -n deployment-lab -w

# In another terminal, watch pods
kubectl get pods -n deployment-lab -l app=weather-api-rolling -w
```

### Step 10: Test Rolling Update
```bash
# Port forward to service
kubectl port-forward -n deployment-lab service/weather-service 8080:80 &

# Test the API
curl http://localhost:8080/weatherforecast | jq

# Observe version changes during rollout
for i in {1..10}; do
  curl -s http://localhost:8080/weatherforecast | jq '.Version'
  sleep 2
done
```

## 🔵🔶 Strategy 2: Blue-Green Deployment

### Step 11: Deploy Blue Environment
```bash
# Deploy blue (v1.0.0) and green (v2.0.0) environments
kubectl apply -f deployment-blue.yaml
kubectl apply -f deployment-green.yaml
kubectl apply -f service-blue-green.yaml
```

### Step 12: Verify Blue Environment
```bash
# Check both deployments
kubectl get deployments -n deployment-lab -l "app in (weather-api-blue,weather-api-green)"

# Test blue environment (current active)
kubectl port-forward -n deployment-lab service/weather-service-bg 8081:80 &
curl http://localhost:8081/weatherforecast | jq '.Version'
# Should show "1.0.0-blue"
```

### Step 13: Execute Blue-Green Switch
```bash
# Switch traffic to green environment
kubectl patch service weather-service-bg -n deployment-lab -p '{"spec":{"selector":{"app":"weather-api-green"}}}'

# Verify green environment is now serving
curl http://localhost:8081/weatherforecast | jq '.Version'
# Should show "2.0.0-green"
```

### Step 14: Rollback to Blue (if needed)
```bash
# Switch back to blue environment
kubectl patch service weather-service-bg -n deployment-lab -p '{"spec":{"selector":{"app":"weather-api-blue"}}}'
```

## 🐦 Strategy 3: Canary Release

### Step 15: Deploy Stable and Canary
```bash
# Deploy stable (v1.0.0) and canary (v2.0.0) deployments
kubectl apply -f deployment-stable.yaml
kubectl apply -f deployment-canary.yaml
kubectl apply -f service-canary.yaml
```

### Step 16: Initial Canary Test (10% traffic)
```bash
# Port forward to canary service
kubectl port-forward -n deployment-lab service/weather-service-canary 8082:80 &

# Test traffic distribution (approximately 25% to canary initially)
echo "Testing traffic distribution:"
for i in {1..20}; do
  curl -s http://localhost:8082/weatherforecast | jq -r '"Version: " + .Version'
  sleep 1
done
```

### Step 17: Increase Canary Traffic
```bash
# Scale canary to increase traffic percentage
kubectl scale deployment/weather-api-canary -n deployment-lab --replicas=2

# Test again (now ~40% to canary)
echo "Testing after scaling canary:"
for i in {1..20}; do
  curl -s http://localhost:8082/weatherforecast | jq -r '"Version: " + .Version'
  sleep 1
done
```

### Step 18: Promote Canary to Full Deployment
```bash
# If canary performs well, promote to full deployment
kubectl scale deployment/weather-api-stable -n deployment-lab --replicas=0
kubectl scale deployment/weather-api-canary -n deployment-lab --replicas=4

# Verify all traffic now goes to canary version
curl http://localhost:8082/weatherforecast | jq '.Version'
# Should show only "2.0.0-canary"
```

## 📊 Monitoring and Verification

### Step 19: Use Provided Scripts
```bash
# Make scripts executable
chmod +x test-deployment.sh
chmod +x observe-rollout.sh

# Run comprehensive test
./test-deployment.sh

# Observe deployments in real-time
./observe-rollout.sh
```

### Step 20: Manual Verification Commands
```bash
# Check all resources
kubectl get all -n deployment-lab

# Check deployment status
kubectl rollout status deployment -n deployment-lab --watch

# View pod versions
kubectl get pods -n deployment-lab -o custom-columns="NAME:.metadata.name,VERSION:.metadata.labels.version"

# Check service endpoints
kubectl get endpoints -n deployment-lab
```

## 🧹 Cleanup

### Step 21: Clean Up Resources
```bash
# Delete entire namespace (removes everything)
kubectl delete namespace deployment-lab

# Or delete specific resources
kubectl delete -f deployment-rolling-update.yaml
kubectl delete -f deployment-blue.yaml
kubectl delete -f deployment-green.yaml
kubectl delete -f deployment-stable.yaml
kubectl delete -f deployment-canary.yaml

# Verify cleanup
kubectl get all -n deployment-lab
```

## 📝 Lab Completion Checklist

- [ ] Successfully built and containerized .NET Core application
- [ ] Deployed base application to Kubernetes
- [ ] Executed Rolling Update and observed pod transition
- [ ] Performed manual Blue-Green deployment switch
- [ ] Implemented and scaled Canary release
- [ ] Verified traffic distribution in Canary deployment
- [ ] Used monitoring scripts to observe deployments
- [ ] Cleaned up all resources

## 🎯 Key Observations

### Rolling Update
- Pods are replaced gradually
- Service remains available throughout
- Mixed versions during transition

### Blue-Green
- Instant switch between environments
- Easy rollback capability
- Requires double resources

### Canary
- Gradual risk mitigation
- Performance testing in production
- Flexible traffic control

## 🚀 Next Steps

After completing this lab, consider:
1. Implementing automated Blue-Green switches with CI/CD
2. Adding metrics collection and analysis
3. Implementing advanced Canary with service mesh (Istio/Linkerd)
4. Adding automated rollback based on metrics
5. Integrating with your existing CI/CD pipeline

## 🆘 Troubleshooting

### Common Issues

**Images not found:**
```bash
# Build images with correct tags
docker build -t k8s-deployment-demo:1.0.0 .
```

**Port forwarding fails:**
```bash
# Kill existing port-forward processes
pkill -f "kubectl port-forward"

# Use different ports
kubectl port-forward -n deployment-lab service/weather-service 8088:80
```

**Pods not starting:**
```bash
# Check pod events and logs
kubectl describe pod -n deployment-lab <pod-name>
kubectl logs -n deployment-lab <pod-name>
```

## 📚 Additional Resources

- [Kubernetes Deployment Strategies](https://kubernetes.io/docs/concepts/workloads/controllers/deployment/)
- [.NET Core Docker Images](https://hub.docker.com/_/microsoft-dotnet)
- [kubectl Cheat Sheet](https://kubernetes.io/docs/reference/kubectl/cheatsheet/)

---

**Happy Deploying! 🚀**
