# Special Talent Exam Application System

ASP.NET Core 8 web application for special talent exam candidate registration, preference selection, document verification, and exam-period administration.

Originally developed as an internship project. This public portfolio edition uses neutral branding.

## Highlights

- ASP.NET Core 8
- MSSQL + Dapper
- Transaction-safe `CandidateNo` allocation
- Security-focused architecture (roles, antiforgery, CSP helpers, masking)
- Automated regression suite (1218 tests)

## Features

- Candidate registration / profile / applications
- Exam period lifecycle (create, activate, close)
- Preference options and manager assignment
- Attendance / score evaluation and exports
- Document verification by code
- Audit logging and system settings (SuperAdmin)

## Architecture

Layered solution:

- `Domain` — entities, enums, constants
- `Application` — services, view models, validation
- `Infrastructure` — Dapper repositories, email, PDF/Excel export
- `Web` — MVC UI (Bootstrap 5.3.8 public shell)
- `Tests` — unit + HTTP/integration coverage

## Security Highlights

- Role separation: Candidate / ApplicationManager / SuperAdmin
- Exam-period scoped access checks for managers
- Antiforgery on mutating forms
- Soft-delete and parameterized SQL via Dapper
- Sensitive value masking in staff listings

## CandidateNo & Concurrency

Candidate numbers are allocated with transactional safeguards to avoid duplicates under concurrent applications. Automated tests cover numbering and write-result failure modes.

## Tech Stack

- .NET 8 / ASP.NET Core MVC
- MSSQL
- Dapper
- Bootstrap 5.3.8 (local MIT vendor)
- jQuery, Select2, DataTables, Toastr, SweetAlert2 (local OSS vendors)

## Project Structure

```
src/OzelYetenekSinavSistemi.Web
src/OzelYetenekSinavSistemi.Application
src/OzelYetenekSinavSistemi.Infrastructure
src/OzelYetenekSinavSistemi.Domain
tests/OzelYetenekSinavSistemi.Tests
database/
```

## Screenshots

Synthetic screenshots are not included in this edition.

## Prerequisites

- .NET 8 SDK
- SQL Server (LocalDB / Express / full) for live runs

## Configuration

1. Copy / create local `appsettings.Development.json` (gitignored).
2. Set `ConnectionStrings:DefaultConnection`.
3. Configure `Email`, `PublicUrl`, and `Seed` as needed.
4. Defaults in repo use neutral example addresses (not a real institution).

## Database Setup

Apply scripts under `database/` according to the project’s SQL baseline and migrations folder.

## Run Locally

```powershell
dotnet restore .\OzelYetenekSinavSistemi.sln
dotnet run --project .\src\OzelYetenekSinavSistemi.Web\OzelYetenekSinavSistemi.Web.csproj
```

## Run Tests

```powershell
dotnet test .\OzelYetenekSinavSistemi.sln -c Release
```

Current suite: **1218** tests.

## Health Checks

Use the application’s configured health endpoints / diagnostics when enabled in your environment.

## Deployment Notes

- Serve over HTTPS
- Store secrets outside source control
- Protect Data Protection keys in production
- Configure reverse-proxy forwarded headers only when required

## Public Portfolio Edition

- Commercial INSPINIA theme removed
- UI migrated to local Bootstrap 5.3.8 + OSS vendors
- Neutral product branding (`Özel Yetenek Sınavları Başvuru Sistemi`)
- Business/domain behavior preserved

## Third-party Components

See `THIRD_PARTY_NOTICES.md` for vendored OSS names, versions, licenses, and upstream sources.

## License

No license is currently granted for reuse of the first-party source code.
Third-party components retain their own licenses under `wwwroot/vendor/**/LICENSE*`.
