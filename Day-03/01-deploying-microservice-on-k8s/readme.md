# Kubernetes Workload Resources Hands-on Lab

## Overview

This lab provides practical exercises for working with Kubernetes workload resources: Deployments, StatefulSets, and DaemonSets. Complete these exercises to gain hands-on experience with each resource type.

## Prerequisites

- A running Kubernetes cluster (Minikube, Kind, or cloud-based)
- `kubectl` configured to communicate with your cluster
- Basic understanding of Kubernetes concepts (Pods, Services, YAML manifests)

---

## Exercise 1: Deployment

### Objectives
- Create a Deployment for a stateless application
- Scale the Deployment
- Perform rolling updates and rollbacks

### Steps

#### 1.1 Create the Deployment

Create a file named `myapp-deployment.yaml`:

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: myapp-deployment
  labels:
    app: myapp
spec:
  replicas: 2
  selector:
    matchLabels:
      app: myapp
  template:
    metadata:
      labels:
        app: myapp
    spec:
      containers:
      - name: myapp
        image: nginx:alpine
        ports:
        - containerPort: 80
```

Apply the deployment:
```bash
kubectl apply -f myapp-deployment.yaml
```

Verify:
```bash
kubectl get deployments
kubectl get pods -l app=myapp
```

#### 1.2 Scale the Deployment

Scale from 2 to 4 replicas:
```bash
kubectl scale deployment myapp-deployment --replicas=4
```

Verify scaling:
```bash
kubectl get pods -l app=myapp
# You should see 4 pods running
```

#### 1.3 Perform Rolling Update

Update the container image:
```bash
kubectl set image deployment/myapp-deployment myapp=nginx:1.25-alpine
```

Watch the rolling update:
```bash
kubectl rollout status deployment/myapp-deployment
```

Check the update history:
```bash
kubectl rollout history deployment/myapp-deployment
```

#### 1.4 Test Rollback

Simulate a bad update:
```bash
kubectl set image deployment/myapp-deployment myapp=nginx:invalid-tag
```

Watch the failed rollout:
```bash
kubectl rollout status deployment/myapp-deployment
```

Rollback to previous version:
```bash
kubectl rollout undo deployment/myapp-deployment
```

Verify rollback:
```bash
kubectl get pods -l app=myapp
kubectl describe deployment myapp-deployment
```

#### 1.5 Cleanup
```bash
kubectl delete deployment myapp-deployment
```

---

## Exercise 2: StatefulSet

### Objectives
- Create a StatefulSet with stable identity and storage
- Observe ordered scaling operations
- Understand StatefulSet behavior during scaling

### Steps

#### 2.1 Create the StatefulSet and Headless Service

Create `web-statefulset.yaml`:
```yaml
apiVersion: v1
kind: Service
metadata:
  name: web
  labels:
    app: web
spec:
  ports:
  - port: 80
    name: web
  clusterIP: None
  selector:
    app: web
---
apiVersion: apps/v1
kind: StatefulSet
metadata:
  name: web
spec:
  serviceName: "web"
  replicas: 2
  selector:
    matchLabels:
      app: web
  template:
    metadata:
      labels:
        app: web
    spec:
      containers:
      - name: nginx
        image: nginx:alpine
        ports:
        - containerPort: 80
          name: web
        volumeMounts:
        - name: www
          mountPath: /usr/share/nginx/html
  volumeClaimTemplates:
  - metadata:
      name: www
    spec:
      accessModes: [ "ReadWriteOnce" ]
      resources:
        requests:
          storage: 1Gi
```

Apply the manifest:
```bash
kubectl apply -f web-statefulset.yaml
```

#### 2.2 Observe Initial Creation

Watch the Pods being created in order:
```bash
kubectl get pods -l app=web -w
```

Check StatefulSet and PVCs:
```bash
kubectl get statefulset
kubectl get pods -l app=web
kubectl get pvc
```

Notice the predictable naming:
- Pods: `web-0`, `web-1`
- PVCs: `www-web-0`, `www-web-1`

#### 2.3 Scale Up to 3 Replicas

Scale up and observe the ordered creation:
```bash
kubectl scale statefulset web --replicas=3
```

Watch the scaling process:
```bash
kubectl get pods -l app=web -w
```

Verify the new Pod and PVC:
```bash
kubectl get pods -l app=web
kubectl get pvc
```

#### 2.4 Scale Down to 1 Replica

Scale down and observe the reverse order termination:
```bash
kubectl scale statefulset web --replicas=1
```

Watch the scaling process:
```bash
kubectl get pods -l app=web -w
```

Check that PVCs are preserved:
```bash
kubectl get pvc
# Notice PVCs for web-1 and web-2 are still there
```

#### 2.5 Test Stable Network Identity

Access Pods using their stable hostnames:
```bash
# Get into web-0 pod
kubectl exec -it web-0 -- sh

# Inside the pod, test DNS resolution
nslookup web-0.web
nslookup web-1.web
nslookup web-2.web

# Exit the pod
exit
```

#### 2.6 Cleanup
```bash
kubectl delete -f web-statefulset.yaml
# PVCs might remain, delete them manually if needed
kubectl delete pvc -l app=web
```

---

## Exercise 3: DaemonSet

### Objectives
- Create a DaemonSet with node selectors
- Use node labels to control Pod placement
- Verify DaemonSet behavior

### Steps

#### 3.1 Check Current Cluster Nodes

First, check your cluster nodes:
```bash
kubectl get nodes
kubectl get nodes --show-labels
```

#### 3.2 Create a Label on a Node

Choose one node and add a custom label:
```bash
# Replace <node-name> with your actual node name
kubectl label nodes <node-name> environment=production
```

Verify the label:
```bash
kubectl get nodes --show-labels | grep environment=production
```

#### 3.3 Create DaemonSet with Node Selector

Create `monitoring-daemonset.yaml`:
```yaml
apiVersion: apps/v1
kind: DaemonSet
metadata:
  name: monitoring-agent
  labels:
    app: monitoring-agent
spec:
  selector:
    matchLabels:
      name: monitoring-agent
  template:
    metadata:
      labels:
        name: monitoring-agent
    spec:
      nodeSelector:
        environment: production
      containers:
      - name: monitoring-agent
        image: nginx:alpine
        ports:
        - containerPort: 80
        resources:
          requests:
            memory: "64Mi"
            cpu: "50m"
          limits:
            memory: "128Mi"
            cpu: "100m"
```

Apply the DaemonSet:
```bash
kubectl apply -f monitoring-daemonset.yaml
```

#### 3.4 Verify DaemonSet Behavior

Check where the DaemonSet Pods are running:
```bash
kubectl get daemonsets
kubectl get pods -l name=monitoring-agent -o wide
```

The DaemonSet should only run on the node with the `environment=production` label.

#### 3.5 Test Node Selector Changes

Add the label to another node:
```bash
# Label a second node
kubectl label nodes <another-node-name> environment=production
```

Watch the DaemonSet automatically deploy to the newly labeled node:
```bash
kubectl get pods -l name=monitoring-agent -o wide -w
```

Remove the label from the first node:
```bash
kubectl label nodes <node-name> environment-
```

Verify the Pod is terminated on that node:
```bash
kubectl get pods -l name=monitoring-agent -o wide
```

#### 3.6 Modify DaemonSet to Run on All Nodes

Update the DaemonSet to remove the node selector:
```bash
kubectl edit daemonset monitoring-agent
```

Remove the entire `nodeSelector` section under `spec.template.spec`.

Verify it now runs on all nodes:
```bash
kubectl get pods -l name=monitoring-agent -o wide
```

#### 3.7 Cleanup
```bash
kubectl delete daemonset monitoring-agent
kubectl label nodes <node-name> environment-
```

---

## Verification Checklist

After completing all exercises, verify your understanding:

### Deployment
- [ ] Successfully created and scaled a Deployment
- [ ] Performed rolling update without downtime
- [ ] Successfully rolled back a failed update
- [ ] Understand how ReplicaSets manage Pod replicas

### StatefulSet
- [ ] Created StatefulSet with stable network identity
- [ ] Observed ordered Pod creation during scale-up
- [ ] Observed reverse-ordered Pod termination during scale-down
- [ ] Verified persistent storage with volumeClaimTemplates
- [ ] Understand when to use StatefulSet vs Deployment

### DaemonSet
- [ ] Created DaemonSet with node selector
- [ ] Controlled Pod placement using node labels
- [ ] Observed automatic Pod management when node labels change
- [ ] Understand use cases for DaemonSets

## Troubleshooting Tips

1. **Pod not starting**: Check `kubectl describe pod <pod-name>` for details
2. **Image pull errors**: Verify image name and tag are correct
3. **PVC pending**: Check available storage classes in your cluster
4. **DaemonSet not scheduling**: Verify node labels and selectors match

## Further Learning

- Experiment with different update strategies (RollingUpdate vs Recreate)
- Try resource limits and requests in your manifests
- Explore readiness and liveness probes
- Practice with different storage classes for StatefulSets

---

**Congratulations!** You've successfully practiced with all three main Kubernetes workload resources. You're now ready to deploy real applications using the appropriate workload controllers.