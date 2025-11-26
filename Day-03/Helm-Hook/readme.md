# **Module 7: Advanced Helm Patterns - Hooks, Tests, and Library Charts**

## **Learning Objectives**

After completing this module, you will be able to:

- 🎯 Implement Helm hooks for lifecycle management
- 🎯 Create and use chart tests for validation
- 🎯 Design and use library charts for code reuse
- 🎯 Implement advanced templating patterns
- 🎯 Apply best practices for complex Helm deployments

## **1. Helm Hooks - Lifecycle Management**

### **1.1. Introduction to Helm Hooks**

Helm hooks allow you to intervene at specific points in a release lifecycle. They are regular Kubernetes manifests annotated to be executed at certain hooks points.

**Why Use Hooks?**
- Database migrations before application deployment
- Backup operations before upgrades
- Cleanup tasks after uninstall
- Notifications and monitoring setup
- Pre-installation checks and validation

### **1.2. Hook Types and Execution Order**

| Hook Type | Description | Execution Timing |
|-----------|-------------|------------------|
| `pre-install` | Executes after templates are rendered, before resources are created | After `helm install` |
| `post-install` | Executes after all resources are loaded into Kubernetes | After resources are created |
| `pre-upgrade` | Executes after templates are rendered, before resources are updated | After `helm upgrade` |
| `post-upgrade` | Executes after all resources have been updated | After upgrade is complete |
| `pre-rollback` | Executes after templates are rendered, before resources are rolled back | After `helm rollback` |
| `post-rollback` | Executes after all resources have been modified | After rollback is complete |
| `pre-delete` | Executes before any resources are deleted | Before `helm uninstall` |
| `post-delete` | Executes after all resources have been deleted | After all resources are removed |
| `test` | Executes when helm test is run | When `helm test` is executed |

### **1.3. Hook Implementation Examples**

#### **Database Migration Hook**
```yaml
# templates/job-db-migration.yaml
apiVersion: batch/v1
kind: Job
metadata:
  name: {{ include "app.fullname" . }}-db-migration
  annotations:
    "helm.sh/hook": pre-upgrade,pre-install
    "helm.sh/hook-weight": "-5"
    "helm.sh/hook-delete-policy": before-hook-creation,hook-succeeded
  labels:
    {{- include "app.labels" . | nindent 4 }}
    app.kubernetes.io/component: migration
spec:
  template:
    spec:
      restartPolicy: Never
      containers:
      - name: migrator
        image: "{{ .Values.image.repository }}:{{ .Values.image.tag }}"
        command: ["/bin/sh", "-c"]
        args:
          - |
            echo "Running database migrations..."
            npm run db:migrate
            echo "Migrations completed successfully!"
        env:
          - name: DATABASE_URL
            valueFrom:
              secretKeyRef:
                name: {{ include "app.fullname" . }}-db-secret
                key: connection-string
        resources:
          {{- toYaml .Values.resources | nindent 12 }}
```

#### **Pre-installation Validation Hook**
```yaml
# templates/job-preflight-check.yaml
apiVersion: batch/v1
kind: Job
metadata:
  name: {{ include "app.fullname" . }}-preflight-check
  annotations:
    "helm.sh/hook": pre-install
    "helm.sh/hook-weight": "-10"
    "helm.sh/hook-delete-policy": hook-succeeded
spec:
  template:
    spec:
      restartPolicy: Never
      containers:
      - name: preflight
        image: alpine/k8s:1.25.0
        command: ["/bin/sh", "-c"]
        args:
          - |
            echo "Running pre-flight checks..."
            
            # Check if required secrets exist
            if ! kubectl get secret {{ include "app.fullname" . }}-db-secret; then
              echo "ERROR: Database secret not found!"
              exit 1
            fi
            
            # Check database connectivity
            # Add your specific checks here
            
            echo "All pre-flight checks passed!"
        env:
          - name: NAMESPACE
            value: "{{ .Release.Namespace }}"
```

#### **Post-install Notification Hook**
```yaml
# templates/job-notification.yaml
apiVersion: batch/v1
kind: Job
metadata:
  name: {{ include "app.fullname" . }}-notification
  annotations:
    "helm.sh/hook": post-install,post-upgrade
    "helm.sh/hook-weight": "5"
    "helm.sh/hook-delete-policy": hook-succeeded
spec:
  template:
    spec:
      restartPolicy: Never
      containers:
      - name: notifier
        image: curlimages/curl:8.00.1
        command: ["/bin/sh", "-c"]
        args:
          - |
            # Send notification to Slack/Teams/Webhook
            curl -X POST \
              -H 'Content-Type: application/json' \
              -d '{
                "text": "Application {{ include "app.fullname" . }} ({{ .Release.Namespace }}) was successfully deployed!",
                "version": "{{ .Chart.AppVersion }}",
                "namespace": "{{ .Release.Namespace }}"
              }' \
              ${SLACK_WEBHOOK_URL}
        env:
          - name: SLACK_WEBHOOK_URL
            valueFrom:
              secretKeyRef:
                name: notification-secrets
                key: slack-webhook
```

### **1.4. Hook Weight and Execution Order**

Hook weights determine the execution order. Lower weights run first.

```yaml
apiVersion: batch/v1
kind: Job
metadata:
  name: {{ include "app.fullname" . }}-ordered-hook
  annotations:
    "helm.sh/hook": pre-install
    "helm.sh/hook-weight": "-5"  # This runs before weight 0
```

### **1.5. Hook Deletion Policies**

| Policy | Description |
|--------|-------------|
| `hook-succeeded` | Delete the hook after it succeeds |
| `hook-failed` | Delete the hook after it fails |
| `before-hook-creation` | Delete previous hook before new hook is created |

```yaml
annotations:
  "helm.sh/hook-delete-policy": hook-succeeded,before-hook-creation
```

## **2. Helm Tests - Application Validation**

### **2.1. Understanding Helm Tests**

Helm tests are Kubernetes manifests that validate your application is working correctly. They run in the cluster and can check:
- Service connectivity
- API endpoints
- Database connections
- Custom business logic

### **2.2. Test Manifests Structure**

Tests are placed in the `templates/tests/` directory and use the `helm.sh/hook: test` annotation.

```yaml
# templates/tests/test-connection.yaml
apiVersion: v1
kind: Pod
metadata:
  name: "{{ include "app.fullname" . }}-test-connection"
  annotations:
    "helm.sh/hook": test
    "helm.sh/hook-delete-policy": hook-succeeded
  labels:
    {{- include "app.labels" . | nindent 4 }}
    helm.sh/chart-test: "true"
spec:
  containers:
  - name: test-connection
    image: curlimages/curl:8.00.1
    command: ['sh', '-c']
    args:
      - |
        echo "Testing service connectivity..."
        
        # Test if the service is responding
        timeout 30s bash -c '
          until curl -f http://{{ include "app.fullname" . }}:{{ .Values.service.port }}/health; do
            echo "Waiting for service to be ready..."
            sleep 5
          done
        '
        
        if [ $? -eq 0 ]; then
          echo "✅ Service connectivity test passed!"
          exit 0
        else
          echo "❌ Service connectivity test failed!"
          exit 1
        fi
  restartPolicy: Never
```

### **2.3. Comprehensive Test Suite Examples**

#### **API Endpoint Test**
```yaml
# templates/tests/test-api.yaml
apiVersion: v1
kind: Pod
metadata:
  name: "{{ include "app.fullname" . }}-test-api"
  annotations:
    "helm.sh/hook": test
    "helm.sh/hook-delete-policy": hook-succeeded
  labels:
    {{- include "app.labels" . | nindent 4 }}
    helm.sh/chart-test: "true"
spec:
  containers:
  - name: test-api
    image: curlimages/curl:8.00.1
    command: ['sh', '-c']
    args:
      - |
        set -e
        
        SERVICE_URL="http://{{ include "app.fullname" . }}:{{ .Values.service.port }}"
        
        echo "Testing API endpoints..."
        
        # Test health endpoint
        echo "1. Testing health endpoint..."
        curl -f -s "${SERVICE_URL}/health" | grep -q '"status":"OK"' || {
          echo "Health endpoint failed"
          exit 1
        }
        
        # Test info endpoint
        echo "2. Testing info endpoint..."
        curl -f -s "${SERVICE_URL}/api/info" | grep -q '"service":"{{ .Values.app.name }}"' || {
          echo "Info endpoint failed"
          exit 1
        }
        
        # Test business logic endpoint
        echo "3. Testing business logic..."
        RESPONSE=$(curl -s "${SERVICE_URL}/api/greet/TestUser")
        echo "$RESPONSE" | grep -q '"message":"Hello, TestUser!"' || {
          echo "Business logic test failed"
          exit 1
        }
        
        echo "✅ All API tests passed!"
  restartPolicy: Never
```

#### **Database Connectivity Test**
```yaml
# templates/tests/test-database.yaml
apiVersion: batch/v1
kind: Job
metadata:
  name: "{{ include "app.fullname" . }}-test-database"
  annotations:
    "helm.sh/hook": test
    "helm.sh/hook-delete-policy": hook-succeeded
  labels:
    {{- include "app.labels" . | nindent 4 }}
    helm.sh/chart-test: "true"
spec:
  template:
    spec:
      containers:
      - name: test-db
        image: postgres:13-alpine
        command: ['sh', '-c']
        args:
          - |
            set -e
            
            echo "Testing database connectivity..."
            
            # Extract database connection details from secret
            PG_HOST=$(kubectl get secret {{ include "app.fullname" . }}-db-secret -o jsonpath='{.data.host}' | base64 -d)
            PG_USER=$(kubectl get secret {{ include "app.fullname" . }}-db-secret -o jsonpath='{.data.username}' | base64 -d)
            PG_PASS=$(kubectl get secret {{ include "app.fullname" . }}-db-secret -o jsonpath='{.data.password}' | base64 -d)
            PG_DB=$(kubectl get secret {{ include "app.fullname" . }}-db-secret -o jsonpath='{.data.database}' | base64 -d)
            
            # Test connection
            if PGPASSWORD="${PG_PASS}" psql -h "${PG_HOST}" -U "${PG_USER}" -d "${PG_DB}" -c "SELECT 1;" > /dev/null 2>&1; then
              echo "✅ Database connectivity test passed!"
              exit 0
            else
              echo "❌ Database connectivity test failed!"
              exit 1
            fi
        env:
          - name: NAMESPACE
            value: "{{ .Release.Namespace }}"
      restartPolicy: Never
```

### **2.4. Running and Managing Tests**

```bash
# Run tests for a release
helm test my-release

# Run tests with timeout
helm test my-release --timeout 300s

# View test results
kubectl get pods -l helm.sh/chart-test=true

# Debug test failures
kubectl logs <test-pod-name>

# Delete test pods
helm test my-release --cleanup
```

## **3. Library Charts - Code Reusability**

### **3.1. What are Library Charts?**

Library charts are Helm charts that define reusable template components that can be shared across multiple application charts. They cannot be installed directly.

**Use Cases:**
- Common application patterns
- Standardized monitoring setup
- Security baseline configurations
- Cross-cutting concerns

### **3.2. Creating a Library Chart**

#### **Library Chart Structure**
```
common-library/
├── Chart.yaml
├── README.md
├── values.yaml
└── templates/
    ├── _helpers.tpl
    ├── _configmap.tpl
    ├── _deployment.tpl
    ├── _service.tpl
    ├── _ingress.tpl
    └── _monitoring.tpl
```

#### **Library Chart.yaml**
```yaml
# Chart.yaml
apiVersion: v2
name: common-library
description: A library chart for common Kubernetes resources
type: library  # This makes it a library chart
version: 0.1.0

dependencies: []

keywords:
  - library
  - common
  - templates
```

### **3.3. Library Chart Template Examples**

#### **Standardized Deployment Template**
```tpl
# templates/_deployment.tpl
{{- define "common-library.deployment" -}}
apiVersion: apps/v1
kind: Deployment
metadata:
  name: {{ .name }}
  labels:
    {{- include "common-library.labels" . | nindent 4 }}
    {{- with .extraLabels }}
    {{ toYaml . | nindent 4 }}
    {{- end }}
  annotations:
    {{- with .annotations }}
    {{ toYaml . | nindent 4 }}
    {{- end }}
spec:
  replicas: {{ .replicas | default 1 }}
  selector:
    matchLabels:
      app.kubernetes.io/name: {{ .name }}
      app.kubernetes.io/instance: {{ .releaseName }}
  template:
    metadata:
      labels:
        app.kubernetes.io/name: {{ .name }}
        app.kubernetes.io/instance: {{ .releaseName }}
        {{- with .podLabels }}
        {{ toYaml . | nindent 8 }}
        {{- end }}
      annotations:
        {{- with .podAnnotations }}
        {{ toYaml . | nindent 8 }}
        {{- end }}
    spec:
      serviceAccountName: {{ .serviceAccountName | default (printf "%s-sa" .name) }}
      securityContext:
        {{- include "common-library.podSecurityContext" . | nindent 8 }}
      containers:
      - name: {{ .container.name | default "app" }}
        image: "{{ .container.image }}"
        imagePullPolicy: {{ .container.pullPolicy | default "IfNotPresent" }}
        {{- if .container.command }}
        command: {{ .container.command | toYaml | nindent 10 }}
        {{- end }}
        {{- if .container.args }}
        args: {{ .container.args | toYaml | nindent 10 }}
        {{- end }}
        ports:
        {{- range .container.ports }}
        - name: {{ .name }}
          containerPort: {{ .port }}
          protocol: {{ .protocol | default "TCP" }}
        {{- end }}
        env:
        {{- range .container.env }}
        - name: {{ .name }}
          {{- if .value }}
          value: {{ .value | quote }}
          {{- else if .valueFrom }}
          valueFrom: {{ .valueFrom | toYaml | nindent 12 }}
          {{- end }}
        {{- end }}
        resources:
          {{- .container.resources | toYaml | nindent 10 }}
        securityContext:
          {{- include "common-library.containerSecurityContext" . | nindent 10 }}
        {{- if .container.livenessProbe }}
        livenessProbe: {{ .container.livenessProbe | toYaml | nindent 10 }}
        {{- end }}
        {{- if .container.readinessProbe }}
        readinessProbe: {{ .container.readinessProbe | toYaml | nindent 10 }}
        {{- end }}
      {{- if .imagePullSecrets }}
      imagePullSecrets:
      {{- range .imagePullSecrets }}
      - name: {{ . }}
      {{- end }}
      {{- end }}
      {{- if .nodeSelector }}
      nodeSelector: {{ .nodeSelector | toYaml | nindent 8 }}
      {{- end }}
      {{- if .affinity }}
      affinity: {{ .affinity | toYaml | nindent 8 }}
      {{- end }}
      {{- if .tolerations }}
      tolerations: {{ .tolerations | toYaml | nindent 8 }}
      {{- end }}
{{- end }}
```

#### **Standardized Service Template**
```tpl
# templates/_service.tpl
{{- define "common-library.service" -}}
apiVersion: v1
kind: Service
metadata:
  name: {{ .name }}
  labels:
    {{- include "common-library.labels" . | nindent 4 }}
  annotations:
    {{- with .annotations }}
    {{ toYaml . | nindent 4 }}
    {{- end }}
spec:
  type: {{ .type | default "ClusterIP" }}
  ports:
  {{- range .ports }}
  - name: {{ .name }}
    port: {{ .port }}
    targetPort: {{ .targetPort | default .port }}
    protocol: {{ .protocol | default "TCP" }}
    {{- if .nodePort }}
    nodePort: {{ .nodePort }}
    {{- end }}
  {{- end }}
  selector:
    app.kubernetes.io/name: {{ .name }}
    app.kubernetes.io/instance: {{ .releaseName }}
{{- end }}
```

#### **Security Context Helpers**
```tpl
# templates/_helpers.tpl
{{- define "common-library.labels" -}}
helm.sh/chart: {{ .chartName | default "common-library" }}-{{ .chartVersion | default "0.1.0" }}
app.kubernetes.io/name: {{ .name }}
app.kubernetes.io/instance: {{ .releaseName }}
app.kubernetes.io/version: {{ .appVersion | default "1.0.0" | quote }}
app.kubernetes.io/managed-by: Helm
{{- end }}

{{- define "common-library.podSecurityContext" -}}
runAsNonRoot: true
runAsUser: 1000
fsGroup: 2000
{{- if .securityContext }}
{{ .securityContext.pod | toYaml }}
{{- end }}
{{- end }}

{{- define "common-library.containerSecurityContext" -}}
allowPrivilegeEscalation: false
readOnlyRootFilesystem: true
runAsNonRoot: true
runAsUser: 1000
capabilities:
  drop:
  - ALL
{{- if .securityContext }}
{{ .securityContext.container | toYaml }}
{{- end }}
{{- end }}
```

### **3.4. Using Library Charts in Application Charts**

#### **Application Chart.yaml with Dependency**
```yaml
# Chart.yaml
apiVersion: v2
name: my-microservice
description: A microservice using common library
type: application
version: 1.0.0

dependencies:
  - name: common-library
    version: "0.1.0"
    repository: "file://../common-library"  # Local path
    # repository: "https://my-chart-repo.com"  # Remote repository
```

#### **Using Library Templates in Application**
```yaml
# templates/deployment.yaml
{{- $deploymentConfig := dict }}
{{- $_ := set $deploymentConfig "name" (include "app.fullname" .) }}
{{- $_ := set $deploymentConfig "releaseName" .Release.Name }}
{{- $_ := set $deploymentConfig "chartName" .Chart.Name }}
{{- $_ := set $deploymentConfig "chartVersion" .Chart.Version }}
{{- $_ := set $deploymentConfig "appVersion" .Chart.AppVersion }}
{{- $_ := set $deploymentConfig "replicas" .Values.replicaCount }}
{{- $_ := set $deploymentConfig "serviceAccountName" (include "app.serviceAccountName" .) }}

{{- $containerConfig := dict }}
{{- $_ := set $containerConfig "image" (printf "%s:%s" .Values.image.repository .Values.image.tag) }}
{{- $_ := set $containerConfig "pullPolicy" .Values.image.pullPolicy }}
{{- $_ := set $containerConfig "ports" .Values.container.ports }}
{{- $_ := set $containerConfig "env" .Values.env }}
{{- $_ := set $containerConfig "resources" .Values.resources }}
{{- $_ := set $containerConfig "livenessProbe" .Values.livenessProbe }}
{{- $_ := set $containerConfig "readinessProbe" .Values.readinessProbe }}

{{- $_ := set $deploymentConfig "container" $containerConfig }}
{{- $_ := set $deploymentConfig "securityContext" .Values.securityContext }}
{{- $_ := set $deploymentConfig "imagePullSecrets" .Values.imagePullSecrets }}
{{- $_ := set $deploymentConfig "nodeSelector" .Values.nodeSelector }}
{{- $_ := set $deploymentConfig "affinity" .Values.affinity }}
{{- $_ := set $deploymentConfig "tolerations" .Values.tolerations }}

{{- include "common-library.deployment" $deploymentConfig }}
```

## **4. Advanced Templating Patterns**

### **4.1. Conditional Resource Creation**

```yaml
# templates/optional-resources.yaml
{{- if .Values.ingress.enabled }}
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: {{ include "app.fullname" . }}
  annotations:
    {{- with .Values.ingress.annotations }}
    {{ toYaml . | nindent 4 }}
    {{- end }}
spec:
  # ... ingress spec
{{- end }}

{{- if .Values.autoscaling.enabled }}
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata:
  name: {{ include "app.fullname" . }}
spec:
  # ... HPA spec
{{- end }}
```

### **4.2. Looping and Range Patterns**

```yaml
# templates/configmap-with-loops.yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: {{ include "app.fullname" . }}-config
data:
  # Simple key-value pairs
  application.yaml: |
    {{- range $key, $value := .Values.config }}
    {{ $key }}: {{ $value | quote }}
    {{- end }}
  
  # Complex nested structures
  services.yaml: |
    {{- range .Values.services }}
    - name: {{ .name }}
      url: {{ .url }}
      timeout: {{ .timeout }}
    {{- end }}
  
  # Conditional entries
  features.yaml: |
    {{- range .Values.features }}
    {{- if .enabled }}
    {{ .name }}: true
    {{- else }}
    {{ .name }}: false
    {{- end }}
    {{- end }}
```

### **4.3. Template Partials and Includes**

```tpl
# templates/_partials.tpl
{{- define "app.containerSpec" -}}
containers:
- name: {{ .Chart.Name }}
  image: "{{ .Values.image.repository }}:{{ .Values.image.tag }}"
  imagePullPolicy: {{ .Values.image.pullPolicy }}
  ports:
    - name: http
      containerPort: {{ .Values.service.port }}
      protocol: TCP
  env:
    {{- include "app.envVars" . | nindent 4 }}
  resources:
    {{- toYaml .Values.resources | nindent 4 }}
  {{- if .Values.livenessProbe }}
  livenessProbe:
    {{- toYaml .Values.livenessProbe | nindent 4 }}
  {{- end }}
  {{- if .Values.readinessProbe }}
  readinessProbe:
    {{- toYaml .Values.readinessProbe | nindent 4 }}
  {{- end }}
{{- end }}

{{- define "app.envVars" -}}
- name: ENVIRONMENT
  value: {{ .Values.environment | quote }}
- name: LOG_LEVEL
  value: {{ .Values.logLevel | quote }}
{{- range .Values.extraEnv }}
- name: {{ .name }}
  value: {{ .value | quote }}
{{- end }}
{{- end }}
```

## **5. Best Practices and Patterns**

### **5.1. Error Handling and Validation**

```tpl
# templates/_validation.tpl
{{- define "app.validateValues" -}}
{{- $requiredKeys := list "image.repository" "image.tag" "service.port" -}}
{{- range $requiredKeys }}
{{- if not (get $.Values (splitList "." .)) }}
{{- fail (printf "Required value '%s' is missing" .) }}
{{- end }}
{{- end }}

{{- if and .Values.ingress.enabled (not .Values.ingress.host) }}
{{- fail "Ingress host is required when ingress is enabled" }}
{{- end }}
{{- end }}

# Use in templates
{{- include "app.validateValues" . }}
```

### **5.2. Resource Naming and Labels**

```tpl
# templates/_naming.tpl
{{- define "app.standardLabels" -}}
app.kubernetes.io/name: {{ .Chart.Name }}
app.kubernetes.io/instance: {{ .Release.Name }}
app.kubernetes.io/version: {{ .Chart.AppVersion | quote }}
app.kubernetes.io/component: {{ .component | default "app" }}
app.kubernetes.io/part-of: {{ .Chart.Name }}
app.kubernetes.io/managed-by: {{ .Release.Service }}
helm.sh/chart: {{ .Chart.Name }}-{{ .Chart.Version | replace "+" "_" }}
{{- end }}

{{- define "app.selectorLabels" -}}
app.kubernetes.io/name: {{ .Chart.Name }}
app.kubernetes.io/instance: {{ .Release.Name }}
{{- end }}
```

### **5.3. Security Best Practices**

```yaml
# values.yaml - Security defaults
securityContext:
  pod:
    runAsNonRoot: true
    runAsUser: 1000
    fsGroup: 2000
  container:
    allowPrivilegeEscalation: false
    readOnlyRootFilesystem: true
    runAsNonRoot: true
    runAsUser: 1000
    capabilities:
      drop:
        - ALL

podSecurityPolicy:
  enabled: true
  annotations:
    seccomp.security.alpha.kubernetes.io/allowedProfileNames: 'runtime/default'
    apparmor.security.beta.kubernetes.io/allowedProfileNames: 'runtime/default'

networkPolicy:
  enabled: true
  ingress:
    - from:
        - namespaceSelector:
            matchLabels:
              name: {{ .Release.Namespace }}
```

## **6. Real-World Example: Complete Microservice Chart**

### **6.1. Chart Structure with Advanced Features**
```
advanced-microservice/
├── Chart.yaml
├── values.yaml
├── values-dev.yaml
├── values-prod.yaml
├── charts/                    # Dependencies
├── crds/                      # Custom Resource Definitions
│   └── my-crd.yaml
└── templates/
    ├── _helpers.tpl
    ├── _validation.tpl
    ├── _partials.tpl
    ├── deployment.yaml
    ├── service.yaml
    ├── ingress.yaml
    ├── configmap.yaml
    ├── secret.yaml
    ├── serviceaccount.yaml
    ├── networkpolicy.yaml
    ├── hpa.yaml
    ├── pdb.yaml
    ├── hooks/                 # Lifecycle hooks
    │   ├── pre-install/
    │   │   ├── job-db-migration.yaml
    │   │   └── job-preflight-check.yaml
    │   ├── post-install/
    │   │   └── job-notification.yaml
    │   └── pre-upgrade/
    │       └── job-backup.yaml
    ├── tests/                 # Test suites
    │   ├── test-connection.yaml
    │   ├── test-api.yaml
    │   ├── test-database.yaml
    │   └── test-integration.yaml
    └── monitoring/            # Monitoring resources
        ├── servicemonitor.yaml
        ├── podmonitor.yaml
        └── prometheusrule.yaml
```

### **6.2. Deployment Commands with Advanced Features**

```bash
# Install with hooks and tests
helm install my-app ./advanced-microservice \
  --namespace production \
  --values values-prod.yaml \
  --set image.tag="v1.2.3" \
  --timeout 15m

# Run tests after installation
helm test my-app --namespace production

# Upgrade with hooks
helm upgrade my-app ./advanced-microservice \
  --namespace production \
  --values values-prod.yaml \
  --set image.tag="v1.2.4" \
  --atomic \  # Automatically rollback on failure
  --timeout 10m

# Check hook status
kubectl get jobs -l helm.sh/hook -n production

# View test results
helm test my-app --namespace production
kubectl logs -l helm.sh/chart-test=true -n production
```

## **7. Troubleshooting Advanced Patterns**

### **7.1. Common Issues and Solutions**

```bash
# Debug hook failures
kubectl get jobs -n <namespace>
kubectl describe job <hook-job-name>
kubectl logs <hook-pod-name>

# Debug template rendering
helm template my-chart --debug --dry-run
helm get manifest my-release

# Check dependency issues
helm dependency list
helm dependency update

# Validate chart
helm lint my-chart
helm install my-chart --dry-run --debug
```

### **7.2. Performance Optimization**

- Use `--atomic` flag for automatic rollback on failure
- Set appropriate timeout values for hooks
- Use hook deletion policies to clean up resources
- Limit the number of parallel hook executions
- Use resource requests and limits in hook jobs

## **Summary**

In this advanced module, you've learned:

- ✅ **Helm Hooks**: Lifecycle management for complex deployment scenarios
- ✅ **Helm Tests**: Validation and testing strategies for reliable deployments
- ✅ **Library Charts**: Code reuse and standardization across multiple charts
- ✅ **Advanced Templating**: Conditional logic, loops, and partial templates
- ✅ **Best Practices**: Security, naming conventions, and error handling

## **Next Steps**

1. **Helm Security** - Implement security contexts and policies
2. **CI/CD Integration** - Automate Helm deployments in pipelines
3. **Chart Museum** - Set up private chart repositories
4. **Custom Plugins** - Extend Helm functionality with plugins

---

**Congratulations!** You've now mastered advanced Helm patterns that will enable you to manage complex Kubernetes applications with confidence and efficiency. 🚀