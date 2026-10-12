# Changelog

## Release 1 readiness pass — 2026-10-12
- Fixed SQL connection-string fallback so an empty `ConnectionStrings:DefaultConnection` does not prevent use of `CAKESHOP_SQL_CONNECTION`.
- Fixed product creation to preserve the requested `isAvailable` value from the admin form/API.
- Added a Release 1 launch checklist covering shop details, builds, smoke tests, security and known limitations.
- Updated README to reflect the included admin dashboard and the actual validation status.

Validation performed in this environment: JSON configuration files parse successfully; ZIP integrity can be checked. Angular package installation timed out and the .NET SDK is unavailable, so compilation and end-to-end execution remain unverified.
