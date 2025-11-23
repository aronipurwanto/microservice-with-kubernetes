##Step 1: Create the Deployment YAML File
Create a file named nginx-deployment.yaml

##Step 2: Apply the Manifest

````
kubectl apply -f nginx-deployment.yaml
````

#Step 3: Verify the Deployment

```
# Check the Deployment
kubectl get deployments

# Check the ReplicaSet (the mechanism the Deployment uses)
kubectl get replicasets

# Check the Pods (you should see 3 Pods with random suffixes)
kubectl get pods
```

Expected Output:
```
NAME               READY   UP-TO-DATE   AVAILABLE   AGE
nginx-deployment   3/3     3            3           30s

NAME                          DESIRED   CURRENT   READY   AGE
nginx-deployment-789c55fffd   3         3         3       30s

NAME                                READY   STATUS    RESTARTS   AGE
nginx-deployment-789c55fffd-8xk2g   1/1     Running   0          30s
nginx-deployment-789c55fffd-j2q9v   1/1     Running   0          30s
nginx-deployment-789c55fffd-mp4f7   1/1     Running   0          30s
```

#Step 4: Test Scaling
```
kubectl scale deployment nginx-deployment --replicas=5
```

#Step 5: Test Rolling Update

Update the deployment to use a newer Nginx version:

```
kubectl set image deployment/nginx-deployment nginx=nginx:1.26
```

Watch the rolling update in action:

```
kubectl rollout status deployment nginx-deployment
```
You can see the new ReplicaSet being created and old Pods being gradually replaced.

#Step 6: Expose the Deployment
Create a Service to access your Nginx deployment:

```
kubectl expose deployment nginx-deployment --port=80 --type=NodePort
```
Find the URL to access it:

```
# If using Minikube
minikube service nginx-deployment --url  
# Or check the assigned port
kubectl get services

```