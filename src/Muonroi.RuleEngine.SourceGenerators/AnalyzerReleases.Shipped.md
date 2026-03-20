## Release 0.1.1

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|--------------------
MRG001 | RuleGen | Error | Duplicate rule code
MRG002 | RuleGen | Error | Invalid hook point
MRG003 | RuleGen | Warning | Non-interface dependency
MRG004 | RuleGen | Warning | Helper method extraction failed
MRG005 | RuleGen | Warning | Missing DependsOn reference
MRG006 | RuleGen | Warning | Order without DependsOn
MRG007 | RuleGen | Warning | Fact consumption without dependency
MRG008 | RuleGen | Warning | Nullable assignment risk
MRG009 | RuleGen | Warning | Fact guard throws InvalidOperationException
MBB001 | Muonroi.Governance | Warning | Forbidden DateTime.Now/UtcNow
MBB002 | Muonroi.Governance | Warning | Forbidden direct JsonSerializer usage
MBB003 | Muonroi.Governance | Warning | Forbidden DbContext inheritance
MBB004 | Muonroi.Governance | Warning | Forbidden AsyncLocal usage
MBB005 | Muonroi.Governance | Warning | Abstractions infra dependency
MBB006 | Muonroi.Governance | Warning | Missing tier guard
