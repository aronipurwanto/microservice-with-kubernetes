#Step 1: Create the DaemonSet YAML File
Create a file named fluentd-daemonset.yaml


Step 2: Apply the Manifest

``` 
kubectl apply -f fluentd-daemonset.yaml
```

Step 3: Observe DaemonSet Behavior

```
# Check the DaemonSet
kubectl get daemonsets

# Check the Pods - you should see one Pod per node
kubectl get pods -l name=fluentd -o wide
```

Expected Output:
``` 
NAME      DESIRED   CURRENT   READY   UP-TO-DATE   AVAILABLE   NODE SELECTOR   AGE
fluentd   1         1         1       1            1           <none>          1m

NAME            READY   STATUS    RESTARTS   AGE   IP           NODE
fluentd-abc12   1/1     Running   0          1m    10.244.1.5   node-1
```
