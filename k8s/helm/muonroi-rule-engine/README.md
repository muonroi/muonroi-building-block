# muonroi-rule-engine Helm Chart

Production-ready Helm chart for deploying Muonroi Rule Engine with:

- Decision table and FEEL runtime
- Multi-tenant quota enforcement
- Optional PostgreSQL and Redis dependencies
- HPA, PDB, NetworkPolicy and ServiceMonitor

## Prerequisites

- Kubernetes 1.28+
- Helm 3.12+ (validated with Helm 4 as well)

## Install

```bash
helm install rule-engine ./k8s/helm/muonroi-rule-engine \
  --namespace rule-engine \
  --create-namespace
```

## Install with environment values

```bash
helm install rule-engine ./k8s/helm/muonroi-rule-engine \
  --namespace rule-engine \
  --create-namespace \
  -f ./k8s/helm/muonroi-rule-engine/values-production.yaml
```

## Upgrade

```bash
helm upgrade rule-engine ./k8s/helm/muonroi-rule-engine \
  --namespace rule-engine \
  -f ./k8s/helm/muonroi-rule-engine/values-production.yaml
```

## Uninstall

```bash
helm uninstall rule-engine --namespace rule-engine
```

## Important values

- `image.repository`, `image.tag`: application image source.
- `ingress.enabled`: expose public endpoint.
- `config.license.mode`, `config.license.tier`: license behavior.
- `config.quota.enabled`: enable runtime quota enforcement.
- `postgresql.enabled`, `redis.enabled`: enable dependency charts.
- `secrets.databaseConnectionString`, `secrets.redisPassword`: override generated secrets.

## Test and validate

```bash
helm lint ./k8s/helm/muonroi-rule-engine
helm template rule-engine ./k8s/helm/muonroi-rule-engine >/tmp/rule-engine.yaml
helm test rule-engine --namespace rule-engine
```

## Grafana dashboard

Dashboard file:

- `k8s/helm/muonroi-rule-engine/dashboards/rule-engine-dashboard.json`

Import this JSON into Grafana and connect to your Prometheus datasource.
