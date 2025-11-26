# **Lab 6: Packaging and Deploying Microservices with Helm**

## **Overview**

This hands-on lab will guide you through creating, packaging, and deploying a microservice using Helm charts. You'll learn how to transform traditional Kubernetes manifests into reusable Helm packages and manage the complete application lifecycle.

**Estimated Time:** 60-75 minutes

## **Prerequisites**

Before starting this lab, ensure you have:

- ✅ Kubernetes cluster running (Minikube, Kind, or cloud cluster)
- ✅ Helm CLI installed (v3.8+)
- ✅ kubectl configured to communicate with your cluster
- ✅ Basic understanding of Kubernetes concepts (from Module 5)
- ✅ Docker installed (for building container images)

## **Lab Objectives**

By the end of this lab, you will be able to:

- 🎯 Create and customize Helm charts from scratch
- 🎯 Use Helm templates and values for configuration management
- 🎯 Package and install Helm charts
- 🎯 Manage application releases (upgrade, rollback)
- 🎯 Work with multiple environments using value files

## **Lab Architecture**

```
Lab Setup:
├── Kubernetes Cluster
├── Sample Microservice (Node.js/Express API)
└── Helm Chart Structure:
    ├── Chart.yaml
    ├── values.yaml
    ├── templates/
    │   ├── deployment.yaml
    │   ├── service.yaml
    │   ├── configmap.yaml
    │   └── ingress.yaml
    └── values-dev.yaml & values-prod.yaml
```

## **Exercise 1: Setting Up the Environment**

### **Step 1.1: Verify Your Environment**

```bash
# Check Kubernetes cluster
kubectl cluster-info
kubectl get nodes

# Check Helm installation
helm version

# Create lab namespace
kubectl create namespace helm-lab
kubectl config set-context --current --namespace=helm-lab
```

### **Step 1.2: Prepare Sample Application**

Create a simple Node.js application for testing:

```bash
# Create project directory
mkdir helm-microservice-lab && cd helm-microservice-lab

# Create sample app structure
mkdir app && cd app
```

Create `app/app.js`:
```javascript
const express = require('express');
const app = express();
const port = process.env.PORT || 8080;

app.use(express.json());

app.get('/health', (req, res) => {
    res.status(200).json({ status: 'OK', timestamp: new Date().toISOString() });
});

app.get('/api/info', (req, res) => {
    res.json({
        service: process.env.SERVICE_NAME || 'helm-microservice',
        version: process.env.SERVICE_VERSION || '1.0.0',
        environment: process.env.ENVIRONMENT || 'development',
        host: process.env.HOSTNAME || 'unknown'
    });
});

app.get('/api/greet/:name', (req, res) => {
    const name = req.params.name;
    res.json({ 
        message: `Hello, ${name}!`,
        timestamp: new Date().toISOString()
    });
});

app.listen(port, () => {
    console.log(`Microservice running on port ${port}`);
    console.log(`Environment: ${process.env.ENVIRONMENT || 'development'}`);
});
```

Create `app/package.json`:
```json
{
  "name": "helm-microservice",
  "version": "1.0.0",
  "description": "Sample microservice for Helm lab",
  "main": "app.js",
  "scripts": {
    "start": "node app.js"
  },
  "dependencies": {
    "express": "^4.18.0"
  }
}
```

Create `app/Dockerfile`:
```dockerfile
FROM node:18-alpine

WORKDIR /app

COPY package*.json ./
RUN npm install --production

COPY . .

EXPOSE 8080

USER node

CMD ["npm", "start"]
```

## **Exercise 2: Creating Your First Helm Chart**

### **Step 2.1: Generate Chart Structure**

```bash
# Navigate back to project root
cd ..

# Create Helm chart
helm create helm-microservice

# Explore the generated structure
tree helm-microservice
```

### **Step 2.2: Customize Chart Metadata**

Edit `helm-microservice/Chart.yaml`:

```yaml
apiVersion: v2
name: helm-microservice
description: A Helm chart for Kubernetes lab microservice
type: application
version: 0.1.0
appVersion: "1.0.0"

maintainers:
  - name: Your Name
    email: your.email@example.com

keywords:
  - microservice
  - api
  - express
  - nodejs
```

### **Step 2.3: Clean Up Templates**

Remove unnecessary generated files and keep only what we need:

```bash
cd helm-microservice/templates
rm -rf hpa.yaml ingress.yaml serviceaccount.yaml pdb.yaml
cd ../..
```

## **Exercise 3: Customizing Templates and Values**

### **Step 3.1: Customize values.yaml**

Replace the content of `helm-microservice/values.yaml`:

```yaml
# Default values for helm-microservice
replicaCount: 2

image:
  repository: helm-lab/microservice
  pullPolicy: IfNotPresent
  tag: "latest"

nameOverride: ""
fullnameOverride: ""

service:
  type: ClusterIP
  port: 8080
  targetPort: 8080

ingress:
  enabled: false
  className: ""
  hosts:
    - host: microservice.local
      paths:
        - path: /
          pathType: Prefix
  tls: []

resources:
  limits:
    cpu: 500m
    memory: 256Mi
  requests:
    cpu: 200m
    memory: 128Mi

autoscaling:
  enabled: false
  minReplicas: 1
  maxReplicas: 5
  targetCPUUtilizationPercentage: 80

nodeSelector: {}

tolerations: []

affinity: {}

# Application specific configuration
app:
  name: "helm-microservice"
  version: "1.0.0"
  environment: "development"
  
env:
  - name: ENVIRONMENT
    value: "development"
  - name: SERVICE_NAME
    value: "helm-microservice"
  - name: SERVICE_VERSION
    value: "1.0.0"
  - name: LOG_LEVEL
    value: "INFO"

configMap:
  enabled: true
  data:
    app.config: |
      welcome.message: "Hello from Helm!"
      api.timeout: "30s"
```

### **Step 3.2: Customize Deployment Template**

Edit `helm-microservice/templates/deployment.yaml`:

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: {{ include "helm-microservice.fullname" . }}
  labels:
    {{- include "helm-microservice.labels" . | nindent 4 }}
    app.kubernetes.io/component: api
spec:
  {{- if not .Values.autoscaling.enabled }}
  replicas: {{ .Values.replicaCount }}
  {{- end }}
  selector:
    matchLabels:
      {{- include "helm-microservice.selectorLabels" . | nindent 6 }}
  template:
    metadata:
      labels:
        {{- include "helm-microservice.selectorLabels" . | nindent 8 }}
        version: {{ .Values.app.version | quote }}
      annotations:
        config.checksum: {{ include (print $.Template.BasePath "/configmap.yaml") . | sha256sum }}
    spec:
      containers:
        - name: {{ .Chart.Name }}
          image: "{{ .Values.image.repository }}:{{ .Values.image.tag | default .Chart.AppVersion }}"
          imagePullPolicy: {{ .Values.image.pullPolicy }}
          ports:
            - name: http
              containerPort: {{ .Values.service.targetPort }}
              protocol: TCP
          livenessProbe:
            httpGet:
              path: /health
              port: http
            initialDelaySeconds: 30
            periodSeconds: 10
            timeoutSeconds: 5
          readinessProbe:
            httpGet:
              path: /health
              port: http
            initialDelaySeconds: 5
            periodSeconds: 5
            timeoutSeconds: 3
          resources:
            {{- toYaml .Values.resources | nindent 12 }}
          env:
            {{- range .Values.env }}
            - name: {{ .name }}
              value: {{ .value | quote }}
            {{- end }}
            - name: PORT
              value: {{ .Values.service.targetPort | quote }}
          {{- if .Values.configMap.enabled }}
          volumeMounts:
            - name: config-volume
              mountPath: /app/config
          {{- end }}
      {{- if .Values.configMap.enabled }}
      volumes:
        - name: config-volume
          configMap:
            name: {{ include "helm-microservice.fullname" . }}-config
      {{- end }}
      {{- with .Values.nodeSelector }}
      nodeSelector:
        {{- toYaml . | nindent 8 }}
      {{- end }}
      {{- with .Values.affinity }}
      affinity:
        {{- toYaml . | nindent 8 }}
      {{- end }}
      {{- with .Values.tolerations }}
      tolerations:
        {{- toYaml . | nindent 8 }}
      {{- end }}
```

### **Step 3.3: Create ConfigMap Template**

Create `helm-microservice/templates/configmap.yaml`:

```yaml
{{- if .Values.configMap.enabled }}
apiVersion: v1
kind: ConfigMap
metadata:
  name: {{ include "helm-microservice.fullname" . }}-config
  labels:
    {{- include "helm-microservice.labels" . | nindent 4 }}
data:
  {{- range $key, $value := .Values.configMap.data }}
  {{ $key }}: |
    {{- $value | nindent 4 }}
  {{- end }}
{{- end }}
```

### **Step 3.4: Customize Service Template**

Edit `helm-microservice/templates/service.yaml`:

```yaml
apiVersion: v1
kind: Service
metadata:
  name: {{ include "helm-microservice.fullname" . }}
  labels:
    {{- include "helm-microservice.labels" . | nindent 4 }}
spec:
  type: {{ .Values.service.type }}
  ports:
    - port: {{ .Values.service.port }}
      targetPort: {{ .Values.service.targetPort }}
      protocol: TCP
      name: http
  selector:
    {{- include "helm-microservice.selectorLabels" . | nindent 4 }}
```

## **Exercise 4: Building and Testing the Application**

### **Step 4.1: Build Docker Image**

```bash
# Build the application image
docker build -t helm-lab/microservice:latest ./app

# If using Minikube, build inside Minikube environment
# minikube docker-env
# eval $(minikube -p minikube docker-env)
# docker build -t helm-lab/microservice:latest ./app
```

### **Step 4.2: Template and Validate Chart**

```bash
# Test template rendering
helm template helm-microservice ./helm-microservice --debug

# Validate chart syntax
helm lint ./helm-microservice

# Dry-run installation
helm install my-microservice ./helm-microservice --dry-run --namespace helm-lab
```

## **Exercise 5: Deploying to Different Environments**

### **Step 5.1: Create Environment-specific Value Files**

Create `helm-microservice/values-dev.yaml`:

```yaml
replicaCount: 1

image:
  tag: "dev-latest"

resources:
  requests:
    cpu: 100m
    memory: 128Mi
  limits:
    cpu: 200m
    memory: 256Mi

app:
  environment: "development"

env:
  - name: ENVIRONMENT
    value: "development"
  - name: LOG_LEVEL
    value: "DEBUG"

configMap:
  enabled: true
  data:
    app.config: |
      welcome.message: "Hello from Development!"
      api.timeout: "30s"
      debug.enabled: "true"
```

Create `helm-microservice/values-prod.yaml`:

```yaml
replicaCount: 3

image:
  tag: "v1.0.0"

resources:
  requests:
    cpu: 300m
    memory: 256Mi
  limits:
    cpu: 500m
    memory: 512Mi

app:
  environment: "production"

env:
  - name: ENVIRONMENT
    value: "production"
  - name: LOG_LEVEL
    value: "WARN"

autoscaling:
  enabled: true
  minReplicas: 2
  maxReplicas: 5
  targetCPUUtilizationPercentage: 80

configMap:
  enabled: true
  data:
    app.config: |
      welcome.message: "Hello from Production!"
      api.timeout: "10s"
      debug.enabled: "false"
```

### **Step 5.2: Deploy to Development**

```bash
# Install development release
helm install my-microservice-dev ./helm-microservice \
  --namespace helm-lab \
  --values ./helm-microservice/values-dev.yaml

# Verify deployment
helm list --namespace helm-lab
kubectl get all --namespace helm-lab

# Test the application
kubectl port-forward svc/helm-microservice-dev 8080:8080 --namespace helm-lab

# In another terminal, test the API
curl http://localhost:8080/health
curl http://localhost:8080/api/info
curl http://localhost:8080/api/greet/Student
```

### **Step 5.3: Deploy to Production (Simulated)**

```bash
# Install production release
helm install my-microservice-prod ./helm-microservice \
  --namespace helm-lab \
  --values ./helm-microservice/values-prod.yaml

# Verify both deployments
kubectl get deployments --namespace helm-lab
kubectl get services --namespace helm-lab
```

## **Exercise 6: Release Management**

### **Step 6.1: Upgrade Release**

```bash
# Let's upgrade our development release
helm upgrade my-microservice-dev ./helm-microservice \
  --namespace helm-lab \
  --values ./helm-microservice/values-dev.yaml \
  --set replicaCount=2 \
  --set app.version="1.1.0"

# Check upgrade status
helm history my-microservice-dev --namespace helm-lab
helm status my-microservice-dev --namespace helm-lab
```

### **Step 6.2: Test Rollback**

```bash
# Intentionally make a bad upgrade
helm upgrade my-microservice-dev ./helm-microservice \
  --namespace helm-lab \
  --set replicaCount=0  # This will break the application

# Check the failed deployment
kubectl get pods --namespace helm-lab

# Rollback to previous version
helm rollback my-microservice-dev 1 --namespace helm-lab

# Verify rollback
helm history my-microservice-dev --namespace helm-lab
kubectl get pods --namespace helm-lab
```

### **Step 6.3: Package the Chart**

```bash
# Package the chart for distribution
helm package ./helm-microservice

# List the packaged chart
ls -la helm-microservice-0.1.0.tgz

# Install from packaged chart
helm install from-package helm-microservice-0.1.0.tgz \
  --namespace helm-lab \
  --values ./helm-microservice/values-dev.yaml
```

## **Exercise 7: Advanced Features**

### **Step 7.1: Add Ingress Configuration**

Edit `helm-microservice/templates/ingress.yaml` (create new file):

```yaml
{{- if .Values.ingress.enabled -}}
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: {{ include "helm-microservice.fullname" . }}
  labels:
    {{- include "helm-microservice.labels" . | nindent 4 }}
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
                name: {{ include "helm-microservice.fullname" $ }}
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

Update `values.yaml` to include ingress annotations:

```yaml
ingress:
  enabled: false
  className: "nginx"
  annotations:
    nginx.ingress.kubernetes.io/rewrite-target: /
  hosts:
    - host: microservice.local
      paths:
        - path: /
          pathType: Prefix
  tls: []
```

### **Step 7.2: Test with Ingress Enabled**

```bash
# Upgrade with ingress enabled
helm upgrade my-microservice-dev ./helm-microservice \
  --namespace helm-lab \
  --values ./helm-microservice/values-dev.yaml \
  --set ingress.enabled=true

# Check ingress resource
kubectl get ingress --namespace helm-lab
```

## **Challenge Exercises**

### **Challenge 1: Add Database Dependency**

Add a Redis dependency to your chart:

1. Modify `Chart.yaml` to include Redis dependency
2. Create templates for Redis deployment and service
3. Update the microservice to connect to Redis
4. Add configuration values for Redis connection

### **Challenge 2: Implement Horizontal Pod Autoscaler**

Create HPA template and enable autoscaling in production values.

### **Challenge 3: Add Custom Health Checks**

Implement custom liveness and readiness probes based on your application endpoints.

## **Clean Up**

```bash
# List all releases
helm list --all-namespaces

# Uninstall releases
helm uninstall my-microservice-dev --namespace helm-lab
helm uninstall my-microservice-prod --namespace helm-lab
helm uninstall from-package --namespace helm-lab

# Delete namespace
kubectl delete namespace helm-lab

# Clean up local files
rm -f helm-microservice-0.1.0.tgz
```

## **Troubleshooting**

### **Common Issues:**

1. **Image Pull Errors**: Ensure your Docker image is built and available
2. **Template Rendering Errors**: Use `helm template --debug` to debug
3. **Resource Constraints**: Adjust resource requests/limits in values.yaml
4. **Helm Version Mismatch**: Ensure you're using Helm v3+

### **Debug Commands:**

```bash
# Debug template rendering
helm template my-release ./chart --debug --values values.yaml

# Check release status
helm status my-release

# View release manifest
helm get manifest my-release

# Check release history
helm history my-release
```

## **Summary**

In this lab, you've learned:

- ✅ Creating and customizing Helm charts
- ✅ Using templates and values for configuration management
- ✅ Deploying to multiple environments
- ✅ Managing releases (install, upgrade, rollback)
- ✅ Packaging charts for distribution

## **Next Steps**

1. **Advanced Helm Patterns** - Explore hooks, tests, and library charts
2. **Helm in CI/CD** - Integrate Helm into your deployment pipeline
3. **Chart Security** - Implement security best practices
4. **Chart Museum** - Set up private chart repository

---

**Congratulations!** You've successfully completed the Helm lab. You're now ready to package and deploy your own microservices using Helm! 🎉