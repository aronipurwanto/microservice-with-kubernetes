Clean up all the resources we created:

```
kubectl delete deployment nginx-deployment
kubectl delete statefulset web
kubectl delete daemonset fluentd
kubectl delete service nginx-deployment web-service

```