# Security Policy

## Supported Versions

| Version | Supported          |
| ------- | ------------------ |
| 0.x.x   | :white_check_mark: |

## Reporting a Vulnerability

We take security seriously. If you discover a security vulnerability, please report it responsibly.

### How to Report

1. **Do NOT** create a public GitHub issue for security vulnerabilities
2. Email the maintainers directly or use [GitHub's private vulnerability reporting](https://github.com/gbolabs/photos-index/security/advisories/new)
3. Include as much detail as possible:
   - Description of the vulnerability
   - Steps to reproduce
   - Potential impact
   - Suggested fix (if any)

### What to Expect

- **Acknowledgment**: Within 48 hours
- **Initial assessment**: Within 7 days
- **Resolution timeline**: Depends on severity, typically 30-90 days

### Scope

This security policy applies to:
- The Photos Index application code
- Container images published to ghcr.io
- Official deployment configurations

### Out of Scope

- Third-party dependencies (report to upstream maintainers)
- Self-hosted instances with custom modifications
- Issues in development/preview environments

## Security Best Practices for Deployment

When deploying Photos Index:

1. **Use HTTPS** in production (configure Traefik with TLS)
2. **Restrict network access** to the PostgreSQL database
3. **Keep images updated** to get security patches
4. **Use secrets management** for sensitive environment variables
5. **Enable container security features** (read-only root filesystem, non-root user)

## Acknowledgments

We appreciate responsible disclosure and will acknowledge security researchers who help improve our security.
