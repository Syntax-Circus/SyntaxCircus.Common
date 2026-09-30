# Deferred: web-neutral contracts

Status: proposal only; not part of the GAT PKI implementation milestone (2026-09-30).

## Motivation

Result/Result<T>, pagination, claims helpers and rate limiting can be useful in self-contained command-line applications. The current package references Microsoft.AspNetCore.App because current-user registration uses HTTP context. That forces consumers needing only results to carry unrelated web framework dependencies.

## Future design to evaluate

- Extract non-HTTP types to a web-neutral package (working name SyntaxCircus.Common.Abstractions); depend only on required BCL/Microsoft.Extensions abstractions.
- Keep HTTP context integration in the existing package; preserve names and compatibility through type forwarding where supported rather than creating duplicate Result type identities.
- Keep ApiResult compatibility for existing consumers; separately decide whether its transport-specific contract belongs in the HTTP-facing package during the future design review.
- Preserve semantic error rules, public signatures and behavior. Moving assembly identity is not presumed non-breaking: test binary/source compatibility, serializer behavior and existing consuming applications.
- Test plain CLI and worker publishing on Windows/Linux without an ASP.NET runtime installation. Do not add a background scheduler merely to justify reuse.

This extraction requires its own approved compatibility/release plan and consumer migration tests. Until then GAT PKI keeps its small domain-specific outcome model locally. No package code has been modularized by this proposal.
