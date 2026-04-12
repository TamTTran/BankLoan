# Security Review Findings

Date: 2026-04-11
Scope: Static review of authentication, session handling, configuration, and deployment defaults.

## 1. Hardcoded production fallback secrets

Severity: High

### Description

The repository publishes known database passwords and a known JWT signing key, and `docker-compose.yml` uses them as production fallback values. Any deployment that starts without explicitly overriding these environment variables will run with attacker-known credentials.

### Evidence

- [docker-compose.yml](/D:/Project/BankLoan/docker-compose.yml:13)
- [docker-compose.yml](/D:/Project/BankLoan/docker-compose.yml:16)
- [docker-compose.yml](/D:/Project/BankLoan/docker-compose.yml:48)
- [.env.example](/D:/Project/BankLoan/.env.example:6)
- [.env.example](/D:/Project/BankLoan/.env.example:9)
- [.env.example](/D:/Project/BankLoan/.env.example:12)
- [README.md](/D:/Project/BankLoan/README.md:195)

### Impact

- Attackers can mint valid JWTs if the default JWT key is used.
- Attackers can access the database if default DB credentials are used.
- A configuration mistake becomes a full environment compromise instead of a startup failure.

### Recommendation

- Remove all insecure default values from `docker-compose.yml`.
- Fail startup when required secrets are missing.
- Keep example values obviously fake and non-functional.
- Load secrets from environment variables, a secrets manager, or platform secret storage.

## 2. Logout endpoint can revoke another user's refresh token

Severity: Medium

### Description

`POST /api/auth/logout` accepts a refresh token in the request body and revokes it without verifying that the token belongs to the authenticated caller. An authenticated user who obtains another user’s refresh token can revoke that other session.

### Evidence

- [src/LoanFlow.API/Controllers/AuthController.cs](/D:/Project/BankLoan/src/LoanFlow.API/Controllers/AuthController.cs:39)
- [src/LoanFlow.Application/Services/AuthService.cs](/D:/Project/BankLoan/src/LoanFlow.Application/Services/AuthService.cs:97)

### Impact

- Session denial-of-service against other users.
- Weak session ownership guarantees.

### Recommendation

- Bind logout to the current authenticated `UserId`.
- Revoke only the caller’s own token or session identifier.
- Prefer session revocation by server-side session ID rather than raw refresh-token value.

## 3. Refresh tokens stored and exposed in plaintext

Severity: Medium

### Description

Refresh tokens are returned to the frontend, stored in `localStorage`, and persisted in plaintext in the database. This creates a direct session hijack path if the browser is compromised by XSS or if database contents are exposed.

### Evidence

- [src/LoanFlow.Application/DTOs/Auth/AuthDtos.cs](/D:/Project/BankLoan/src/LoanFlow.Application/DTOs/Auth/AuthDtos.cs:5)
- [frontend/src/api/index.js](/D:/Project/BankLoan/frontend/src/api/index.js:6)
- [frontend/src/api/index.js](/D:/Project/BankLoan/frontend/src/api/index.js:8)
- [src/LoanFlow.Application/Services/AuthService.cs](/D:/Project/BankLoan/src/LoanFlow.Application/Services/AuthService.cs:155)
- [src/LoanFlow.Domain/Entities/RefreshToken.cs](/D:/Project/BankLoan/src/LoanFlow.Domain/Entities/RefreshToken.cs:7)

### Impact

- Any XSS can steal both access and refresh tokens.
- A read-only database leak gives reusable refresh tokens immediately.
- Session persistence risk remains even after access tokens expire.

### Recommendation

- Store refresh tokens in `HttpOnly`, `Secure`, `SameSite` cookies where possible.
- Hash refresh tokens before storing them in the database.
- Return minimal auth data to the client and avoid persisting the full login response in `localStorage`.
- Add token-family reuse detection if refresh rotation is retained.

## 4. Development seed credentials are weak and documented

Severity: Low

### Description

The project seeds predictable demo accounts with simple passwords and documents those credentials. The seeding is limited to Development, which reduces impact, but the risk returns immediately if a dev or staging environment is exposed externally.

### Evidence

- [src/LoanFlow.Infrastructure/Data/DbSeeder.cs](/D:/Project/BankLoan/src/LoanFlow.Infrastructure/Data/DbSeeder.cs:15)
- [README.md](/D:/Project/BankLoan/README.md:221)

### Impact

- Easy compromise of exposed non-production environments.
- Increased chance of password reuse in manual test environments.

### Recommendation

- Replace fixed demo passwords with environment-provided secrets or one-time randomized bootstrap credentials.
- Do not document reusable credentials for any environment that may be network-accessible.
- Block external exposure of development deployments.

## Notes

- This review was static only; no runtime validation or penetration testing was performed.
- Existing role-based authorization on controllers reduces some attack surface, but it does not mitigate the issues above.
