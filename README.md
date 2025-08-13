# K8s Load Generator

App to generate synthetic load on a pod to test scaling capabilities in a K8s cluster.

## Deploy Resources

There are two main resources in this example. The Load Generation API is the one in charge of generating artificial load in a pod.

```sh
kubectl create ns load-gen
kubectl -n load-gen apply -f K8s/load-gen.yaml
```

The Horizontal Pod Autoscaler contains the configuration for scaling the deployment.

```sh
kubectl -n load-gen apply -f hpa.yaml
```

To access the API without publicly exposing it, you can use port-forwarding:

```sh
kubectl -n load-gen port-forward svc/load-gen 8080:80
```

Once the service can be accesed locally, use a tool like curl or Postman to create resource load. For example:

```sh
curl -X POST -H "Content-Type: application/json" -d '{ "threads": 4, "intensityPercent": 85, "durationSeconds": 120 }' http://localhost:8080/load/cpu
```

Once executed, the HPA configuration will detect the load increase and trigger the configured rules. In this example, you can monitor the increase in the replicas using:

```sh
kubectl -n load-gen get deployment load-gen --watch
```

Additionally, you can monitor the HPA using:

```sh
kubectl -n load-gen get hpa load-gen --watch  
```

To see resource usage, you can run:

```sh
kubectl -n load-gen top pods 
```

Once the cooldown threshold is passed, the replicas will go back to the minimum configured.
