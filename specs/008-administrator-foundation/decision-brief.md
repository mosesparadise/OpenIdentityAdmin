# M02-A delivery decisions

The user accepted these recommendations on 12 October 2026. They refine the approved v0.2 scope; they do not authorize production deployment or relax security/release gates.

| Increment | Concrete outcome | Remains unavailable |
| --- | --- | --- |
| 1 — bootstrap/storage | Explicit operator command creates one disabled, enrollment-pending administrator, initial permission bundles, permanent bootstrap receipt and transactional audit. Adds both-provider migrations and dedicated administrator tests. | Enrollment/login/recovery UI and all administrator API/BFF access |
| 2 — TOTP/recovery | Real protected enrollment, password+TOTP login and single-use restricted recovery; durable cross-node replay controls and secret protection. | Administrator BFF/API until M02-B–D; passkeys remain M05 |
| 3 — authority/governance | Current-permission policy and guarded last-administrator transitions with recent-MFA and distributed race evidence. | Unbuilt management operations and their later endpoint-specific acceptance |

Each implementation increment requires a reviewed PR, corrected findings, exact-head checks and a normal squash merge before the next starts. The first PR cannot be reported as all of M02-A complete.

Approved structure: existing grouped production projects; Migrator/Features/Bootstrap/Initialize; AuthServer/Features/Accounts; Core and Infrastructure/Features/Identity. Add tests/aspire/IdentityPlatform.Administrator.Tests under the Aspire solution folder, with Features/Bootstrap initially and shared Fixtures. Add later feature folders only when implementing their journeys.

The first command is a one-shot local process with a bounded standard-input JSON contract. Passwords never appear in arguments, environment-backed application settings or command output. The operator supplies the input from a protected secret source over a pipe; the deployment operator remains responsible for protecting that source and process access. This avoids introducing a second host or duplicating the AuthServer's private-file adapter. Linux/WSL2 and current containerized CI are the exercised operational targets; other-platform acceptance is not inferred.
