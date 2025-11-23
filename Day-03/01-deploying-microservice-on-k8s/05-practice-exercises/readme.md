Deployment Exercise:

Create a Deployment for a custom application (you can use nginx:alpine)

Scale it to 4 replicas

Perform a rolling update to change the image tag

Rollback the update if something goes wrong

StatefulSet Exercise:

Create a StatefulSet with 2 replicas

Scale it up to 3 replicas and observe the order

Scale it down to 1 replica and observe the reverse order

DaemonSet Exercise:

Add a node selector to the DaemonSet to run only on nodes with a specific label

Create the label on one of your nodes and verify the DaemonSet behavior

Key kubectl Commands for Practice:

```
# Get resources
kubectl get deployments,statefulsets,daemonsets,pods

# Describe a resource for detailed info
kubectl describe deployment nginx-deployment

# Check rollout history
kubectl rollout history deployment/nginx-deployment

# Rollback to previous version
kubectl rollout undo deployment/nginx-deployment

# Check logs of a Pod
kubectl logs <pod-name>

# Execute command in a Pod
kubectl exec -it <pod-name> -- /bin/bash

```