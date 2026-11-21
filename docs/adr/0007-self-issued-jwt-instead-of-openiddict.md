# ADR 0007: Self-issued JWTs and rotating refresh tokens instead of OpenIddict for MVP

**Status:** Accepted (2026-11-21)

## Context

The build prompt's Sprint 1 asks for "IssueToken (OpenIddict password + refresh + Google)". OpenIddict is a full
OAuth 2.0 / OpenID Connect server: it needs its own EF Core stores (applications, authorizations, scopes, tokens),
client registration, and a hosted authorization endpoint. The OAuth password grant it would expose is deprecated in
OAuth 2.1, and nothing in the MVP (first-party web and Android apps only) needs third-party clients, consent screens
or discovery documents. Pulling it in now would add a second persistence model and a lot of surface area before any
of it is used.

## Decision

Identity issues its own tokens through `ITokenService` in Infrastructure:

- **Access tokens** are HS256 JWTs (15 min) with `sub`, `tenant_id`, `role`, `org_id` and `amr` claims. The API
  validates them with the standard JWT bearer handler, so swapping the issuer later is a configuration change.
- **Refresh tokens** are opaque 256-bit values stored hashed in `identity_refresh_tokens` and rotated on every use;
  reuse of a rotated token revokes the user's whole chain.
- **MFA challenges** are 5-minute JWTs with `token_use=mfa_challenge`; the bearer handler rejects them for API calls.
- **Grants** at `POST /identity/auth/token`: `password` (email + password), `refresh_token`, and `google` (ID-token
  verification behind `IGoogleIdTokenVerifier`, a stub until a client id exists).
- Phone OTP sign-in is a separate flow (`/auth/otp/request`, `/auth/otp/verify`) and is the primary one.

Authorization policies (`Policies.*`) are role + MFA claims; resource ownership is checked by per-resource
`IAuthorizationHandler`s (e.g. `OrganisationRoleRequirement`).

## Consequences

- No OIDC discovery, no third-party client support, no SSO for staff yet. Staff SSO + hardware keys (blueprint §19)
  is a Phase 2 item that can be introduced by adding an external OIDC scheme next to the bearer scheme.
- The signing key is a shared secret from `Secrets:JwtSigningKey`; rotate to an asymmetric key (RS256/ES256) before
  any service other than the API needs to validate tokens.
- Password hashing uses ASP.NET Core Identity's PBKDF2 format (`IPasswordHasher`), TOTP is RFC 6238 in-house, and
  personal columns use AES-256-GCM with an HMAC blind index (`IFieldEncryptor`), all in Infrastructure/Security.
