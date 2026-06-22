# Security Policy

## Reporting a vulnerability

If you discover a security vulnerability in this project, please report it
privately rather than opening a public issue.

- Use GitHub's [private vulnerability reporting](https://docs.github.com/en/code-security/security-advisories/guidance-on-reporting-and-writing-information-about-vulnerabilities/privately-reporting-a-security-vulnerability)
  (the **Security** tab → **Report a vulnerability**), or
- Email the maintainer at **kippolitov@gmail.com**.

Please include:

- A description of the issue and its potential impact
- Steps to reproduce (proof of concept if available)
- Affected component(s) and version/commit

You can expect an initial acknowledgement within a few days. Please give us a
reasonable opportunity to investigate and release a fix before any public
disclosure.

## Scope

This is a sample/demonstration application. It ships with predefined demo users
and seed data and is **not intended to handle real production data without
additional hardening** (authentication, authorization, secrets management, and
network controls).

## Secrets

No secrets are stored in this repository. Credentials (database passwords, Azure
credentials) are supplied at deploy time via GitHub Actions secrets and Azure
OIDC. If you believe a secret has been committed, please report it using the
process above.
