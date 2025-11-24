#!/bin/bash
# observe-rollout.sh

echo "Observing rollout in real-time..."

# Watch pods with color coding
kubectl get pods -n deployment-lab -w --color | while read line; do
  if [[ $line == *"Running"* ]]; then
    echo -e "\e[32m$line\e[0m"  # Green for running
  elif [[ $line == *"Pending"* ]]; then
    echo -e "\e[33m$line\e[0m"  # Yellow for pending
  elif [[ $line == *"Terminating"* ]]; then
    echo -e "\e[31m$line\e[0m"  # Red for terminating
  else
    echo "$line"
  fi
done