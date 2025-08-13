# K8s Load Generator

App to generate synthetic load on a pod to test scaling capabilities in a K8s cluster.

## Prerequisites

This example requires [metrics server](https://github.com/kubernetes-sigs/metrics-server#deployment) running in the cluster.

To install it on Kubernetes v1.31+ use:

```sh
kubectl apply -f https://github.com/kubernetes-sigs/metrics-server/releases/latest/download/components.yaml
```

Installation instructions for previous releases can be found in [Metrics Server releases](https://github.com/kubernetes-sigs/metrics-server/releases).

## Deploy Resources

There are two main resources in this example. The Load Generation API is the one in charge of generating artificial load in a pod.

```sh
kubectl create ns load-gen
kubectl -n load-gen apply -f K8s/load-gen.yaml
```

The Horizontal Pod Autoscaler contains the configuration for scaling the deployment. There are two examples: `hpa-cpu.yaml` for CPU-based triggering, and `hpa-mem.yaml` for RAM-based triggering.

```sh
kubectl -n load-gen apply -f K8s/hpa-cpu.yaml
```

To access the API without publicly exposing it, you can use port-forwarding:

```sh
kubectl -n load-gen port-forward svc/load-gen 8080:80
```


Once the service can be accesed locally, use a tool like curl or Postman to create resource load. For example, for CPU consumption:

```sh
curl -X POST -H "Content-Type: application/json" -d '{ "threads": 4, "intensityPercent": 85, "durationSeconds": 120 }' http://localhost:8080/load/cpu -b
```

This will increase the CPU load to 85% for 2 minutes. Once executed, the HPA configuration will detect the load increase and trigger the configured rules. 

You can also use a pod for making requests within the cluster.

```sh
kubectl apply -f K8s/test.yaml
```

The curl request will be different since we'll be using the service `FQDN` instead of `localhost`. Here's an example for memory consumption:

```sh
curl -X POST -H "Content-Type: application/json" -d '{ "megabytes": 1024, "durationSeconds": 300, "holdUntilStopped": false }' http://load-gen/load/memory -v
```

> Warning: If `holdUntilStopped` is set to true, memory consumption won't go down. You'll need to kill the pod or call the /load/{jobId}/stop on the **POD IP**, where jobId is the guid returned when calling the API.

## Monitoring

You can monitor the increase in the replicas using:

```sh
kubectl -n load-gen get deployment load-gen --watch
```

Additionally, you can monitor the HPA using:

```sh
kubectl -n load-gen get hpa load-gen --watch  
```

To see resource usage on each pod, you can run:

```sh
kubectl -n load-gen top pod
```

Once the cooldown threshold is passed, the replicas will go back to the minimum configured.

## Combine with Cluster autoscaler

This example can also be used to test the cluster autoscaling capabilities. Once there's no more room for scheduling pods within the existing nodes, the process will allocate new nodes.

You can monitor the nodes state using:

```sh
kubectl top node
```
