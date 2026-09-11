# Security Policy

## Supported Versions

| Version | Supported |
|---------|-----------|
| Public Portfolio Edition (main) | Yes |

This repository is a public portfolio edition. Security fixes are applied on a best-effort basis for the published branch.

## Reporting a Vulnerability

Please use **GitHub private vulnerability reporting** on this repository when available.

Do **not** open a public issue that includes:

- exploit details that could harm production deployments
- secrets, connection strings, tokens, or private keys
- personal data (T.C. numbers, real emails, phone numbers, student identifiers)

If private reporting is unavailable, open a minimal private channel via the repository maintainers without including secrets in the initial message.

## Responsible Disclosure

1. Report privately first.
2. Allow reasonable time for assessment and remediation.
3. Avoid public proof-of-concept exploitation against third-party deployments.
4. Coordinate disclosure timing when possible.

## Secret Disclosure Warning

If you accidentally commit secrets:

1. Rotate the credential immediately.
2. Remove the secret from history if required.
3. Notify maintainers via private vulnerability reporting.

## Security Architecture Summary

This application emphasizes:

- ASP.NET Core authentication/authorization with role separation (Candidate, ApplicationManager, SuperAdmin)
- Antiforgery protection on state-changing form posts
- Server-side validation and identity-document rules
- Soft-delete / access checks for exam-period scoped admin operations
- Transaction-safe candidate numbering (`CandidateNo`)
- Security response headers and content security policy helpers
- Masking of sensitive fields in admin listings where applicable

Deployers are responsible for hardening production configuration (HTTPS, secrets storage, SQL access, reverse proxy trust, Data Protection keys).
