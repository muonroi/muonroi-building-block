# Tenant Namespaces

Each tenant should run in its own Kubernetes namespace to isolate resources and avoid noisy neighbor effects.

Apply `ResourceQuota` and `LimitRange` objects for each namespace to define hard limits and default container requests/limits. See `tenant-a.yaml` as an example that can be copied per tenant and adjusted to match required capacity.

Operational procedures for maintaining these quotas should be documented separately.
