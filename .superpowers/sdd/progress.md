# Muonroi.CodeStandards — SDD Progress

Branch: develop

- [x] Analyzer package MSTD0001/0002 + codefix (refined to ignore null!/default!), 13/13 tests
- [x] MSTD = 0 across FULL solution (verified --no-incremental: 0 MSTD, 0 errors). Tests 2210 passed.
      Custom exceptions (PdfException, Permission*) now derive from MException.
      Framework-contract throws (RpcException/HubException/SecurityTokenException) class-suppressed.
- [x] Warnings cleanup (agent ab3130, commits e5ce516e..8f57fa84): 0 actionable warnings, only RS1038 left.
- [x] MSTD0003 (log only via IMLog): descriptor + analyzer + 10 tests (23/23 pass). Forbids Console/Debug/Trace/Serilog
      + raw ILogger.Log* on non-IMLog receiver, in Muonroi.* non-test ns. Exempts Muonroi.Logging*, tests.
- [x] MSTD0003 blast radius (59 violations) resolved:
    - migrate ILogger->IMLog where MLog guaranteed: DefaultFontResolver (Pdf self-registers MLog).
    - Console->optional IMLog: DefaultAuthContextFactory, MRuleContextJsonRegistry.
    - revert-to-ILogger + [SuppressMessage] (IMLog NOT guaranteed in DI scope — proven by sample-app DI
      ValidateOnBuild failures): SiteProfileStartupValidator, EfColumnSyncHostedService, HttpConnector, DiagnosticsExtensions.
    - pragma/class-suppress (pre-DI bootstrap / static helper / internal pipeline): MSecureFileReader,
      HostRoleAndUserCreator, BackgroundJobHandler, AuthorizeInternal, ImagePipeline, FontPipeline, MEcosystemStartupFilter.
    - delete dead ctor Debug trace: MDbContext.
    - CLI tools (Console = product output): DecisionTableGen, RuleGen -> <NoWarn>MSTD0003</NoWarn>.
- [x] Ship gate (rule 2a): Directory.Build.props TreatWarningsAsErrors + MSBuildTreatWarningsAsErrors +
      WarningsNotAsErrors(RS1038;xUnit1030). RS1038 NoWarn moved to Directory.Build.targets (eval-order fix for IsRoslynComponent).
- [x] Full --no-incremental build WITH gate: 0 Warning, 0 Error, 0 MSTD0003.
- [x] Full test suite (sequential -m:1): 62 assemblies, 4132/4132 passed, 0 failed.
- [x] Committed: 8b1b4c0d (26 files) — CodeStandards + migrations/suppressions + Directory.Build.props/targets + CLI csproj + tests.
- [x] Publish wiring + analyzer pack fix (commit 29bda9de). User chose: publish CodeStandards first, then wire template.
    - publish-all.yml (active release workflow; publish-oss.yml is DEPRECATED): added src/Muonroi.CodeStandards to
      ALL_PROJECTS (OSS → nuget.org only, NOT commercial GitHub Packages). It was MISSING — a v* tag would have
      packed ~71 packages but silently omitted CodeStandards.
    - Directory.Build.targets (IsRoslynComponent group): SuppressDependenciesWhenPacking=true + NoWarn NU5128.
      Ship gate (TreatWarningsAsErrors) was promoting NU5128 to error at `dotnet pack` for ALL netstandard2.0
      analyzer packages (CodeStandards, RuleEngine.SourceGenerators, Tenancy.SiteProfile.SourceGenerators) —
      pack-time regression NOT caught by the earlier dotnet build/test verification. Fixed systemically.
    - Verified: full Release build 0/0 under ship gate; release pack sweep 71/71 projects + template = all .nupkg.
      CodeStandards nuspec has NO dependency group (SuppressDependenciesWhenPacking works) and packs
      analyzers/dotnet/cs + .xml. CI-faithful pack (-p:Version=1.0.0-alpha.16) green for Pdf/Pdf.Enterprise/CodeStandards.
    - FYI (pre-existing, NOT introduced here, NOT blocking): Muonroi.Pdf + Pdf.Enterprise carry inline <Version>1.0.7
      (deliberate independent release cadence v1.0.1..1.0.7, commits 68148f80/c96fd93e). Packed standalone (no
      -p:Version) under the ship gate they emit NU5104 (stable pkg → prerelease suite deps). Masked in tag-driven
      CI by -p:Version override. Side effect: scripts/pack-pdf-packages.ps1 (PKG-07 CPM gate that FORBIDS inline
      <Version> and expects alpha.15) is now stale/red vs current Pdf practice. Left for user decision.
- [x] Dry-run validation (user chose alpha.15 + dry-run-first). Pushed develop (commits 29bda9de + aef34587 + 713ff102).
      Three CI dry-run rounds, each surfacing a layer only the .NET 8 runner + fresh pack exposes:
        #1 FAIL validate: 19× CS1587 — local .NET 10 SDK masks doc-comment errors the CI .NET 8 SDK flags.
           Fix aef34587: relocate record-param /// docs to <param> tags; /// inside initializer -> //; /// moved
           before [SuppressMessage] (3 Pdf files). Validated locally by pinning global.json to SDK 9.0.311.
        #2 FAIL pack: Muonroi.Rules (the ONLY publish project NOT in the .sln, so never compiled by sln build /
           --no-build sweeps which reused a stale pre-ship-gate dll) hit 98 ship-gate errors on fresh compile
           (CS0618 ×70 self-obsolete refs, MSTD0001 ×2, MSTD0002 ×26). Fix 713ff102: scoped
           src/Muonroi.Rules/.editorconfig severity=none for CS0618 + MSTD0001/0002/0003 ([DEPRECATED] pkg, frozen).
        #3 SUCCESS (run 27860312245, SHA 713ff102): validate ✓ (CodeStandards.Tests 23/23) + pack ✓
           ("Successfully created package .../artifacts/all/Muonroi.CodeStandards.1.0.0-alpha.15.nupkg"), dry_run
           uploaded all-packages-dryrun, no nuget.org push. Pre-push gate run locally each round: 4132/4132.
      nuget.org state: Muonroi.Core.Abstractions alpha.15 already published; Muonroi.CodeStandards = 404 (new).
      => Real publish at alpha.15 with --skip-duplicate pushes ONLY Muonroi.CodeStandards.
- [x] Real publish DONE (run 27861133154, dry_run=false, version_override=1.0.0-alpha.15). Push log:
      "Pushing Muonroi.CodeStandards.1.0.0-alpha.15.nupkg ... Created ... Your package was pushed." (HTTP 201).
      ~70 existing alpha.15 packages skip-duplicated. nuget.org flatcontainer index = HTTP 200 (live).
- [x] Template wiring DONE (commit 586902be): templates/content/muonroi-service/Directory.Build.props adds a
      build-time-only (PrivateAssets=all) PackageReference to Muonroi.CodeStandards 1.0.0-alpha.15, applying the
      analyzer to every project in a generated service. Verified: dotnet new tenant-service -> Directory.Build.props
      emitted -> restore pulled alpha.15 from nuget.org -> dotnet build solution 0 errors (MSTD active, scaffold
      compliant). Static scan of template .cs = no MSTD0001/0002/0003 violations.
- [ ] FOLLOW-UPS (not blocking, user's call):
    - Template still pins other Muonroi pkgs at alpha.9 (vs alpha.15 suite) — run bump-version.ps1 or bump manually.
    - Template package (Muonroi.Templates) must be RE-PUBLISHED for `dotnet new install Muonroi.Templates`
      consumers to receive the new Directory.Build.props (next template release).
    - Parallel PDF work in tree (NOT mine, left untouched): M src/Muonroi.Pdf/Internal/Layout/PaginationEngine.cs,
      ?? tests/Muonroi.Pdf.Tests/Diagnostic/A5RenderProbe.cs.
- [ ] Template PackageReference (Task 7 step 4) — wire after CodeStandards is published at the chosen version.
