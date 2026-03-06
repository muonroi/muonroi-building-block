# Rule Engine Upgrade Plan (Licensed + Enterprise)

## 1. Muc tieu

Nang cap Rule Engine theo huong:
- an toan artifact (integrity + storage hardening),
- governance tot hon (validation + versioning),
- deny-by-default cho tinh nang premium `rule-engine`,
- tiep can operational maturity tu cac he thong manh tren thi truong.

## 2. Benchmark he thong manh hien tai (baseline)

### 2.1. Drools
- Rule engine theo mo hinh working memory + agenda + conflict resolution.
- Co governance/deployment maturity cao cho KIE artifacts.
- Ref:
  - https://docs.drools.org/latest/drools-docs/drools/rule-engine/index.html
  - https://docs.jboss.org/drools/release/latest/drools-docs/drools/KIE/index.html

### 2.2. Camunda DMN
- Chuan DMN hit policy (`U`, `F`, `A`, `C`, ...), model governance ro rang.
- Ref:
  - https://docs.camunda.org/manual/latest/user-guide/process-engine/decisions/configuration/
  - https://docs.camunda.io/docs/components/best-practices/modeling/choosing-the-dmn-hit-policy/

### 2.3. OPA
- Policy bundle signed, chi activate bundle hop le.
- Ref:
  - https://www.openpolicyagent.org/docs/management-bundles
  - https://www.openpolicyagent.org/docs/policy-language

### 2.4. NRules (.NET)
- Rete-based, rich runtime/agenda model, testing ecosystem ro rang.
- Ref:
  - https://github.com/NRules/NRules
  - https://nrules.net/articles/getting-started.html
  - https://nrules.net/api/index.html

### 2.5. Microsoft RulesEngine / Rules Composer ecosystem
- JSON workflow + action output + custom type injection, huong externalized rules.
- Ref:
  - https://microsoft.github.io/RulesEngine/
  - https://github.com/microsoft/RulesEngine
  - https://learn.microsoft.com/en-us/azure/logic-apps/rules-engine/create-rules
  - https://learn.microsoft.com/en-us/azure/logic-apps/rules-engine/create-manage-vocabularies

### 2.6. OpenL Tablets
- BRMS + repository + version + consistency + test tooling cho business users.
- Ref:
  - https://openl-tablets.org/what-is-openl-tablets
  - https://openl-tablets.org/features
  - https://openl-tablets.org/documentation/user-guides

## 3. Doi chieu voi he thong hien tai

### 3.1. Diem manh hien co
- Co 2 tang rule:
  - typed rule orchestration (`RuleEngine<T>`, `RuleOrchestrator<TContext>`)
  - external JSON workflow (`RulesEngineService`, `FileRuleSetStore`)
- Co versioned ruleset file store + optional HMAC signer.
- Co tenant-aware ruleset path.
- Co telemetry/hook/listener trong `Muonroi.RuleEngine.Core`.

### 3.2. Diem yeu/rui ro can nang cap ngay
- Chua gate premium feature `rule-engine` theo license.
- `FileRuleSetStore` chua harden du path/workflow segment, de rui ro traversal va artifact poisoning.
- Parse version file chua defensive (`int.Parse`), de fail runtime khi file rac.
- Chua co guard kich thuoc artifact ruleset.
- `RulesEngineService.SaveRuleSetAsync` chua validate payload/workflow consistency.
- Signature verify flow can defensive hon voi malformed signature.

## 4. Plan chi tiet

### P0. Analysis lock + gap matrix
- [x] Hoan tat benchmark.
- [x] Chot gap matrix va muc uu tien.

### P1. Security hardening cho ruleset storage
- [x] Harden `FileRuleSetStore`:
  - validate tenant/workflow segment
  - chong path traversal
  - defensive version parsing (ignore malformed files)
  - gioi han kich thuoc ruleset
  - lock ghi version de tranh race
- [x] Mo rong `RuleStoreConfigs` cho security knobs.

### P2. Artifact integrity & governance
- [x] Strengthen signature flow:
  - option bat buoc signature (`RequireSignature`)
  - defensive verify khi signature malformed
- [x] Validate workflow document truoc khi save:
  - workflowName consistency
  - rules array khong rong
  - payload parseable

### P3. License-gated execution
- [x] Gate `FreeTierFeatures.Premium.RuleEngine` trong:
  - `RulesEngineService` (save/set-active/execute)
  - `RuleEngine<T>` (execute path)

### P4. Tests + docs
- [x] Unit tests cho storage hardening, validation, license gate.
- [x] Chay full test projects lien quan va fix fail.
- [x] Viet report + migration notes.

## 5. Tieu chi pass

- Hoan tat P1..P4.
- Tat ca unit tests lien quan pass.
- Co docs plan + report + migration notes ro rang.
