# Go-Live Production Readiness Review

Date: 2026-04-23
Repository: LoanFlow / BankLoan

## Verdict

Not ready for production go-live until production environment values are supplied and verified.

The first remediation pass has addressed the repo-level issues that can be fixed safely in code and Compose defaults: production startup now rejects unsafe mock/default configuration, demo login shortcuts were removed, webhook audit actor handling was hardened, internal services are no longer published by default in Compose, and forwarded headers are limited to configured trusted proxies.

Remaining go-live work is environment-specific: real TLS termination, real provider credentials/endpoints, trusted proxy IPs, production host names, secret rotation, and staging verification.

## Remediation Applied

Applied on 2026-04-23:

- Added Production startup validation for mock integrations, placeholder provider secrets, localhost CORS origins, unsafe `AllowedHosts`, missing webhook secret, and missing webhook system actor.
- Added configured external certificate authority provider registration and kept mock CA available only for non-production/dev mode.
- Removed hard-coded demo quick-login accounts from the React login page.
- Changed bank-transfer webhook processing to resolve a configured `Webhook:SystemActorUsername` instead of hard-coding user id `1`.
- Changed webhook exception responses to generic client errors with `traceId`, while logging detailed exceptions server-side.
- Changed forwarded-header handling to trust only configured proxies/networks.
- Changed `docker-compose.yml` so DB, API, and Prometheus use internal `expose` instead of host `ports`.
- Added production integration and deployment variables to `.env.example`.
- Kept local full-stack development mock settings in `docker-compose.override.yml`.

## Critical Findings

### 1. Public exposure is too broad

Evidence:

- `docker-compose.yml:20` exposes MySQL on `3306:3306`.
- `docker-compose.yml:48` exposes API HTTP on `8080:8080`.
- `docker-compose.yml:49` exposes API HTTPS on `8443:8443`.
- `docker-compose.yml:96` exposes frontend on `80:80`.
- `docker-compose.yml:120` exposes Prometheus on `9090:9090`.

Risk:

Database, direct API ports, and metrics should not be reachable from the public network in production. Direct API exposure also bypasses the intended Nginx edge.

Status:

Partially remediated in repo. `docker-compose.yml` no longer publishes DB, API, or Prometheus host ports. Production infrastructure must still confirm firewall/security group restrictions.

### 2. Public TLS edge is not production configured

Evidence:

- `nginx.conf:2` listens on port `80`.
- `nginx.conf:3` uses `server_name localhost`.
- HSTS headers are configured, but the public Nginx server itself is HTTP-only.

Risk:

Production traffic may run over plaintext HTTP unless TLS is terminated elsewhere. If TLS is terminated upstream, the current repository does not make that production boundary explicit.

Required action:

Terminate TLS at the public edge with valid certificates, configure the real production host name, and document whether TLS terminates at Nginx or an upstream load balancer.

### 3. mTLS is documented but not enforced by the API

Evidence:

- `nginx.conf:75` and `nginx.conf:76` configure Nginx to present a client certificate.
- `docker-compose.yml:49` exposes API HTTPS port `8443` directly.
- No API-side client certificate authentication or Kestrel client certificate requirement was found.

Risk:

The comments describe mTLS, but the API does not appear to require or validate client certificates. Direct access to `8443` can bypass Nginx entirely.

Status:

Partially remediated in repo by removing direct API host publication in Compose. API-side client-certificate enforcement is still not implemented and must be addressed if mTLS remains a requirement.

### 4. Production defaults still use mock integrations

Evidence:

- `src/LoanFlow.API/appsettings.json:39` enables mock payment gateway.
- `src/LoanFlow.API/appsettings.json:40` enables mock CIC.
- `src/LoanFlow.API/appsettings.json:41` enables mock eKYC.
- `src/LoanFlow.API/appsettings.json:42` enables mock SMS.
- `src/LoanFlow.API/Program.cs:121` registers `MockPaymentGatewayService` when enabled.
- `src/LoanFlow.API/Program.cs:126` registers `NullSmsService` when enabled.
- `src/LoanFlow.API/Program.cs:146` registers mock CIC when enabled.
- `src/LoanFlow.API/Program.cs:151` registers mock eKYC when enabled.
- `src/LoanFlow.API/Program.cs:166` always registers `MockCertificateAuthorityProvider`.

Risk:

Production loan disbursement, SMS, CIC, eKYC, and e-contract behavior can silently run against non-real implementations.

Status:

Remediated for startup safety. Production now fails startup if mock providers are enabled or provider secrets/endpoints are still placeholders. Real provider values and operational runbooks are still required.

### 5. Demo login shortcuts ship in the frontend

Evidence:

- `frontend/src/pages/Login.jsx:7` defines quick-login demo accounts.
- `frontend/src/pages/Login.jsx:8` includes an admin demo password.
- `frontend/src/pages/Login.jsx:92` renders the quick-login buttons.

Risk:

Hard-coded demo credentials and quick-login affordances are not acceptable in a production banking or lending system.

Status:

Remediated. Demo quick-login UI and hard-coded demo passwords were removed from `frontend/src/pages/Login.jsx`.

### 6. Webhook audit and error behavior are not production-grade

Evidence:

- `src/LoanFlow.API/Controllers/WebhookController.cs:100` records webhook payments as user id `1`.
- `src/LoanFlow.API/Controllers/WebhookController.cs:115` returns exception messages to the caller.

Risk:

Payment audit trails depend on a magic user id. Raw exception messages can disclose internal implementation details.

Status:

Remediated in code. Webhooks now resolve a configured system actor username and return generic processing errors with trace ids.

### 7. Forwarded header trust is too broad

Evidence:

- `src/LoanFlow.API/Program.cs:204` clears known networks.
- `src/LoanFlow.API/Program.cs:205` clears known proxies.

Risk:

If the API is directly reachable, clients can spoof `X-Forwarded-For` and `X-Forwarded-Proto`, weakening rate limiting and scheme handling.

Status:

Remediated in code. Forwarded headers now use configured known proxies/networks. Production must set `TRUSTED_PROXY_IP` or `ForwardedHeaders` values.

### 8. Local secrets must be rotated before go-live

Evidence:

- `.env` is ignored and not tracked, which is good.
- Local `.env` contains concrete DB and JWT values.

Risk:

Any secret used during development, local testing, screen sharing, logs, or support should be treated as non-production.

Required action:

Generate new production secrets. Store them in the production secret manager, not in repository files or host-local `.env` files.

## Additional Concerns

- `docker-compose.yml` uses floating `latest` tags for internal and monitoring images. Pin immutable image digests or versioned tags for reproducible releases.
- API build succeeds but has nullable warnings in application and controller code. These are not all release blockers, but should be triaged before a regulated production launch.
- `AllowedHosts` defaults to `localhost` in `src/LoanFlow.API/appsettings.json:55`; production host configuration should be explicit.
- CORS values in `docker-compose.yml:65` and `docker-compose.yml:66` are localhost-oriented. Production origins must match the actual public frontend origin.
- Database migration is opt-in outside development, which is good. A go-live runbook must explicitly cover backup, migration execution, rollback, and post-migration verification.

## Verification Results

Commands run on 2026-04-23:

- API build: passed using isolated output directory `.buildtmp/LoanFlow.API`.
- Backend tests: passed, `24/24`.
- Frontend production build: passed.
- Frontend tests: passed, `14/14`.

Notes:

- A normal API build initially failed because `src/LoanFlow.Domain/obj/Debug/net8.0/LoanFlow.Domain.dll` was locked by an existing `.NET Host` process. Re-running with an isolated output directory passed.
- Frontend tests initially failed in the sandbox with Windows `spawn EPERM`; re-running with approved execution passed.

## Go-Live Gate Checklist

Before production launch:

- Restrict public network exposure to the intended HTTPS edge only.
- Configure production TLS and real host names.
- Enforce or remove the claimed internal mTLS design.
- Disable all mock providers in Production and fail startup if any are enabled.
- Remove demo login shortcuts and demo credentials from production frontend bundles.
- Replace webhook magic user id with a dedicated system actor.
- Stop returning raw webhook exception messages.
- Restrict forwarded headers to trusted proxies.
- Rotate all local/dev secrets and move production secrets to a secret manager.
- Pin production container images.
- Confirm production CORS and `AllowedHosts`.
- Execute backup, migration, smoke-test, and rollback runbooks in staging before launch.
