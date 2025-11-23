#Step 1: Create the StatefulSet YAML File
Create a file named web-statefulset.yaml:

#Step 2: Create the Headless Service
Create a file named web-service.yaml

#Step 3: Apply the Manifests
```
kubectl apply -f web-service.yaml
kubectl apply -f web-statefulset.yaml
```

#Step 4: Observe StatefulSet Behavior
``` 
# Watch the Pods being created in order
kubectl get pods -w -l app=nginx-stateful

# Check the StatefulSet
kubectl get statefulsets

# Check the Persistent Volume Claims (PVCs)
kubectl get pvc
```

Expected Output:
``` 
NAME        READY   AGE
web         3/3     2m

NAME      STATUS    VOLUME                                     CAPACITY   ACCESS MODES   STORAGECLASS   AGE
www-web-0   Bound     pvc-abc123...   1Gi        RWO            standard       2m
www-web-1   Bound     pvc-def456...   1Gi        RWO            standard       1m
www-web-2   Bound     pvc-ghi789...   1Gi        RWO            standard       30s

NAME              READY   STATUS    RESTARTS   AGE
web-0             1/1     Running   0          2m
web-1             1/1     Running   0          1m
web-2             1/1     Running   0          30s
```

#Step 5: Test Stable Network Identity

``` 
# Execute a command inside web-0
kubectl exec web-0 -- hostname

# You can also access Pods via their stable DNS:
# web-0.web-service.default.svc.cluster.local
```

