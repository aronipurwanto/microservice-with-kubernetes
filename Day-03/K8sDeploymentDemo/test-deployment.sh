#!/bin/bash
# test-deployment.sh

echo "Testing deployment strategies..."

echo "1. Checking all pods:"
kubectl get pods -n deployment-lab

echo "2. Testing service endpoints:"
SERVICES=("weather-service" "weather-service-bg" "weather-service-canary")
for service in "${SERVICES[@]}"; do
  echo "Testing $service:"
  kubectl port-forward -n deployment-lab service/$service 8080:80 &
  PF_PID=$!
  sleep 2
  curl -s http://localhost:8080/weatherforecast | jq '{Version: .Version, Server: .Server}'
  kill $PF_PID
  wait $PF_PID 2>/dev/null
  echo "---"
done

echo "3. Deployment status:"
kubectl get deployments -n deployment-lab