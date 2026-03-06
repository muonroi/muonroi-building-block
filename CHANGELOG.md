# Changelog

All notable changes to this project will be documented in this file.

## [1.9.1] - 2026-02-07
### Added
- **Tier 2 Security Enhancement**: Signed Policy System for Enterprise compliance.
- `LicensePolicy`: RSA-signed data structure defining enforcement rules, feature quotas, and rate limits.
- `PolicyVerifier`: Cryptographic verification of policy files using RSA-SHA256.
- `PolicyEnforcer`: Runtime enforcement of policy-defined rate limits and feature usage quotas.
- `PolicySigner` CLI Tool: Utility for generating and signing license policies for distribution.
- Support for `RequireSignedPolicy` in `LicenseConfigs` to mandate valid policies in production.

### Changed
- Integrated `PolicyEnforcer` into `LicenseGuard` to override configuration settings with signed policy rules.
- Enhanced `LicenseSaveChangesInterceptor` to respect policy-defined database enforcement rules.

## [1.9.0] - 2026-02-07
### Added
- **Tier 1 Security Enhancement**: Comprehensive client-side protection.
- `LicenseExecutionContext`: Prevents infinite recursion in database interceptors using `AsyncLocal`.
- `AntiTamperDetector`: Hardware breakpoint detection (DR0-DR7) and debugger sensing.
- `LicenseEnforcementMode`: Explicit control over security strictness (Free, Development, Production).
- Integrated `AntiTamperDetector` into `LicenseGuard` for hardened production checks.

### Fixed
- Fixed infinite recursion bug in `LicenseSaveChangesInterceptor` when recording actions during DB saves.
- Added missing `SavingChangesAsync` override in `LicenseSaveChangesInterceptor`.
- Improved tier-based enforcement: License tier is now the source of truth, reducing reliance on environment variables.

## [1.6.6] - 2025-08-17
### Added
- Rule Engine with DMN decision tables, complex event processing, feature flags, and governance capabilities.
- Multi-tenancy improvements including isolation, tenant-aware observability, and rate limiting.
- Authentication and authorization enhancements such as OIDC PKCE flow, WebAuthn MFA, DPoP token binding, and rule-driven login.
- OpenTelemetry instrumentation and ECS logging for MassTransit.

### Changed
- Refactored project structure and namespaces for clarity and maintainability.

### Fixed
- General bug fixes including improved null handling and error messages.

## [1.5.8] - 2025-02-11
### Added
- Logging enhancements and better token handling across services.
- Task management and file upload functionalities.

### Changed
- Updated repository interfaces and middleware behavior.
- Refactored serialization and repository layers.

### Fixed
- Resolved various issues in permission and authentication flows.
