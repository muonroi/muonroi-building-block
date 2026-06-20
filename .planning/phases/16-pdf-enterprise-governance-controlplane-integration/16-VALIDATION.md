---
phase: 16
slug: pdf-enterprise-governance-controlplane-integration
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-06-20
---

# Phase 16 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit (.NET) |
| **Config file** | none — solution-level test projects already exist |
| **Quick run command** | `dotnet test tests/Muonroi.Pdf.Tests/Muonroi.Pdf.Tests.csproj` |
| **Full suite command** | `dotnet test` (solution root) |
| **Estimated runtime** | ~60–120 seconds (full solution) |

---

## Sampling Rate

- **After every task commit:** Run the relevant project test command (e.g. `dotnet test tests/Muonroi.Pdf.Tests/...`)
- **After every plan wave:** Run `dotnet test` (full solution)
- **Before `/gsd:verify-work`:** Full suite must be green (0 failed — Pre-Push Test Gate)
- **Max feedback latency:** 120 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| _to be filled by planner / nyquist-auditor_ | | | | | | | | | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

*To be filled by planner — new test files for the LicenseFeatureGate, Quota metering, compliance adapter, and canary rollback.*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| _to be filled by planner_ | | | |

*If none: "All phase behaviors have automated verification."*

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 120s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
