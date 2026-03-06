# Dynamic Business Logic Cloud Roadmap

## Vision

Cho phep cau hinh business rules tu mot UI tap trung va day xuong cac app dang chay theo co che hot-reload, khong can redeploy.

## Kien truc de xuat

1. Rule Control Plane
- UI + API quan tri ruleset.
- Rule registry/versioning/audit.
- Rule validation + signing truoc khi publish.

2. Rule Distribution Plane
- Phat su kien thay doi rules qua message bus (Redis PubSub/Kafka/RabbitMQ).
- Co metadata tenant/workflow/version.

3. Rule Runtime Plane (trong tung app)
- Runtime cache cho ruleset payload.
- Nhan su kien thay doi va invalidate cache ngay lap tuc.
- Lazy reload + compile lai rules trong request tiep theo.

## Da hoan thanh trong dot nay (foundation)

- Runtime cache + hot invalidation:
  - `IRuleSetRuntimeCache`, `RuleSetRuntimeCache`
  - `IRuleSetChangeNotifier`, `InMemoryRuleSetChangeNotifier`, `RedisRuleSetChangeNotifier`
- `RulesEngineService` da:
  - publish su kien khi save/activate
  - invalidate cache local
  - reload theo cache invalidation (hot-reload behavior)
- `RuleStoreConfigs` bo sung knobs:
  - `EnableRuntimeCache`, `RuntimeCacheMinutes`, `RuleChangeChannel`

## Pha tiep theo (khi xay UI)

### Phase 1 - Control API
- CRUD workflow/ruleset
- schema validation + dry-run test endpoint
- sign artifact + publish version

### Phase 2 - Safe rollout
- activate by tenant
- canary rollout theo tenant/pham vi % request
- rollback 1 click ve version truoc

### Phase 3 - Governance & Compliance
- approval flow (maker-checker)
- immutable audit trail
- diff view + blast radius analysis truoc deploy

### Phase 4 - Enterprise runtime
- policy guardrail (deny unsafe expressions)
- compile cache warm-up job
- SLO dashboard cho rule latency/error rate/reload lag
