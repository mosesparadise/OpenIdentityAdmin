# Security Assurance and Certification Plan Draft

Release 1 will be engineered and tested for OpenID Foundation OpenID Connect **Basic OP and Config OP certification**, alongside independent application-security verification against **OWASP ASVS 5.0.0 Level 3 applicable requirements**. These targets address protocol interoperability and the security of a high-value identity system. Neither a library choice nor this plan guarantees certification.

Status: Version 0.2 for approval, 10 October 2026. The user requested recommended authentication/authorization assurance frameworks. This document selects the proposed baseline; no certification, security assessment or implementation test has yet been completed.

## Assurance tracks and release scope

| Track | R1 target and evidence | Meaning and limits |
| --- | --- | --- |
| OIDC interoperability | OpenID Foundation Basic OP and Config OP for the named release/configuration | Formal product protocol certification following the Foundation's process; not inherited from OpenIddict |
| OAuth security | RFC 9700 OAuth 2.0 Security Best Current Practice, plus applicable OIDC Core, PKCE, PAR, revocation, introspection and device-flow requirements | Requirement-level engineering and negative-test evidence; no generic RFC 9700 certificate is assumed |
| Application security | ASVS 5.0.0 Level 3 applicable controls across STS, BFF, Admin API, worker and browser surfaces; independent assessment | Includes applicable lower-level requirements; exclusions require evidence and security-owner approval, not silent downgrading |
| Authentication assurance | NIST SP 800-63B-4 AAL2-oriented administrator authentication and authenticator lifecycle; phishing-resistant passkey option | Actual AAL claims require complete control and deployment review. MFA alone does not establish AAL2; AAL3 and identity proofing are not promised |
| Federation assurance | Assess applicable SP 800-63C-4 controls and document federation trust, assertions and account linking | No blanket FAL claim; relying-party arrangements and deployment evidence matter |
| Secure delivery | NIST SSDF 1.1 practices, dependency/secret scanning, SBOM, signed release provenance, review and vulnerability response | Evidence for development and supply-chain controls; not an OAuth certificate |
| Organizational assurance | Map operational evidence to ISO/IEC 27001:2022 and SOC 2 Security criteria, with Availability/Confidentiality considered for hosting | An organization's ISMS and auditor examination require people, processes and operating evidence outside a repository. SOC 2 is an attestation, not a product certification |
| Privacy and accessibility | Data minimization, retention/deletion/access controls, privacy notices and WCAG 2.2 AA; deployment-specific legal review | GDPR/UK GDPR or other laws depend on actual deployment and jurisdiction; neither jurisdiction nor legal compliance is inferred |

Security and compliance owners are assigned at kickoff. The development team owns implementation evidence; an independent assessor reviews security; an authorized product representative owns external submissions and declarations. Deployment owners supply operational and jurisdictional evidence.

## Selected OpenID certification profiles

Basic OP is the authorization-code authentication profile; Config OP exercises discovery metadata. Static client registration is supported, so Dynamic OP certification and Dynamic Client Registration are not needed for R1. Implicit and Hybrid OP profiles remain out of scope because those flows are disabled.

R1 targets code responses with query response mode. If form_post is added as a supported product capability, add its applicable Form Post OP certification tests and browser cross-site cookie tests before advertising it. Do not broaden production capabilities solely to collect certificates. Discovery must accurately reflect the enabled configuration and supported client authentication methods.

The first postapproval protocol milestone has an explicit blocking exit criterion: resolve compatibility between the current certification suite and hardened product defaults before accepting the protocol baseline. The current Basic suite can construct authorization requests without PKCE, while this product requires PKCE; selected client authentication variants also need verification. Resolve the supported profile/configuration with the Foundation and preserve a truthful record of the configuration being certified. This is an open compatibility risk, not evidence of a passing profile. Verify PKCE/PAR policy, supported response modes and client-authentication registration requirements. The current OP setup describes two client_secret_basic clients and client_secret_post support for its client tests. Use dedicated synthetic clients, and assess compatibility before committing to a submission configuration. Never silently disable a security requirement to obtain a passing run. Any unavoidable incompatibility becomes a documented finding with a secure correction or an explicit amendment to the certification target.

R1 tests RP-initiated logout as a supported feature. The Foundation's logout submission rules require RP-Initiated Logout OP **and at least one** of Session Management, Front-Channel or Back-Channel Logout OP. Therefore a separate logout certificate is deferred until an approved release implements and verifies a qualifying combination; RP logout alone does not satisfy that submission plan.

## Conformance automation and certification workflow

1. After approval, pin the official conformance suite by release/commit and container digest. Add an optional isolated Aspire profile and CI runner. Its client redirects, issuer, TLS and network reachability must be exact; the hosted Foundation service cannot call an arbitrary localhost endpoint.
2. Run Basic OP and Config OP against the same hardened product configuration on both PostgreSQL and SQL Server. Record product commit, OpenIddict/runtime versions, deployment manifest, selected test-plan IDs, client settings and suite version. Confirm with the Foundation how provider variants are represented in the submission.
3. Review failures, warnings, skipped and manual-evidence cases. A feature is not covered merely because a suite skipped it. Block release on unresolved failures or incomplete applicable evidence. Retain product tests for device flow, PAR, administration, recovery and BFF behavior that these profiles do not establish.
4. Run the selected plans on release candidates and relevant changes to protocol processing, claims, login/consent, keys, client policy, reverse proxies and framework/library dependencies. Nightly automation provides early feedback; production readiness still needs independent penetration testing and operational exercises.
5. Produce the profile export packages and a human-reviewable submission record. The authorized representative handles the declaration, any fee or waiver and submission through the current Foundation process. External publication, payment and signing are separate explicit actions, beyond permission to develop the application.
6. Record submission status and the Foundation's resulting listing/mark authorization before describing a release as certified. Keep evidence tied to the declared product version and configuration; assess whether later changes require retesting or resubmission.

Certification evidence can be public and contain credentials or private test keys. Use only disposable test identities, clients and keys. Deactivate clients and revoke test credentials before publication. Preserve valid evidence according to the Foundation's instructions; do not upload production logs or silently edit required evidence into an unverifiable report.

## Security control register

At kickoff, import the pinned official ASVS control catalogue and record **every applicable Level 1–3 requirement**, with version-qualified identifiers. Do not invent numeric identifiers from memory. Add normative OAuth/OIDC and NIST control references alongside the PRD requirements. This draft defines the register schema and initial domains; the full implementation/evidence register is generated and maintained after approval.

Required fields: control source/version/identifier; applicability and rationale; threat or requirement; owner; feature/task; enforcing component; automated test IDs; manual assessment; evidence location and date; result; residual risk; exception approver/expiry; reassessment trigger. A pending or excluded control never appears as a pass.

| Control domain | Initial required evidence |
| --- | --- |
| Protocol/client trust | Exact redirects, PKCE/PAR, grant binding, client authentication, issuer/audience/type rejection, algorithm allowlists and downgrade resistance |
| Authentication/recovery | Enrollment, password/lockout policy, passkey verification, TOTP replay prevention, recovery-code single use, authenticator change and administrator recovery |
| Sessions and access control | BFF anti-forgery, fixation prevention, server-held tokens, distributed refresh coordination, current permissions, object-level access and last-admin races |
| Cryptography/secrets | Separate key purposes, access restrictions, encrypted persisted tickets, rotation/restore exercises, secret-free logs and controlled credential output |
| Input/output and browser security | Bounded requests, injection/SSRF defenses, CSP and encoding, safe error contracts, no open proxy/redirect and no mass assignment |
| Data/audit/privacy | Atomic audit intent, restricted reads/exports, retention, deletion consequences, lawful-retention decisions, tamper detection and incident evidence |
| Delivery/operations | Least-privilege DB roles, clean installs/upgrades, image/package scans, dependency inventory, backups, resilience, monitoring and release provenance |

Independent assessment includes authenticated testing with multiple administrative roles, protocol abuse and account-recovery scenarios. No unresolved exploitable critical/high finding is acceptable for R1; retest corrections. Track lesser residual risks with owners and due dates. Do not represent AI review, static analysis or coverage percentages as an independent penetration test.

## FAPI 2.0 roadmap

FAPI 2.0 Security Profile is final and certification is available. It is not included in R1. A later candidate is FAPI2SP OP with private_key_jwt and DPoP, plus the OpenID Connect extension where applicable. This needs a separate specification covering the authorization server, confidential BFF/client and resource servers, sender-constrained tokens, binding/replay checks and suite compatibility. Verify the selected OpenIddict release's support and required extensions first. FAPI message signing is additional scope.

The conventional R1 BFF uses rolling refresh tokens. A future FAPI profile cannot inherit that blanket rule: FAPI 2.0 generally prohibits refresh-token rotation except defined extraordinary circumstances with recovery provisions. Keep policy profile-specific and verify the normative requirements before implementation. PAR and PKCE alone do not establish FAPI conformance.

## Sources and acceptance

Authoritative references: [OP conformance testing](https://openid.net/certification/connect_op_testing/), [current Basic test plan](https://gitlab.com/openid/conformance-suite/-/blob/master/src/main/java/net/openid/conformance/openid/OIDCCBasicTestPlan.java), [suite request construction](https://gitlab.com/openid/conformance-suite/-/blob/master/src/main/java/net/openid/conformance/openid/AbstractOIDCCServerTest.java), [logout certification](https://openid.net/certification/connect_op_logout_testing/), [conformance suite](https://openid.net/certification/about-conformance-suite/), [submission process](https://openid.net/certification/how-to-submit-your-certification-request/), [published evidence precautions](https://openid.net/certification/fapi_op_submission/), [OAuth Security BCP](https://www.rfc-editor.org/rfc/rfc9700.html), [ASVS](https://owasp.org/projects/asvs), [NIST Digital Identity Guidelines](https://pages.nist.gov/800-63-4/), [NIST SSDF](https://csrc.nist.gov/Projects/ssdf), [ISO 27001](https://www.iso.org/standard/27001), [SOC 2](https://www.aicpa-cima.com/resources/landing/system-and-organization-controls-soc-suite-of-services), [FAPI 2.0 final](https://openid.net/specs/fapi-security-profile-2_0.html) and [FAPI certification availability](https://openid.net/fapi2-0-final-conformance-tests-available/).

R1 release evidence must include passing selected conformance plans, complete applicable ASVS evidence with independent review, threat-model closure, security regression results and a submission-ready package. A release may be described as certification-ready while the external process is pending, but cannot be marketed as certified before that process completes. The recommended public general-availability gate includes completed Basic OP/Config OP certification for the declared configuration. An internal release candidate may be submission-ready while the external process is pending. Budget the independent assessment, authorized signatory and Foundation process separately; do not silently waive this gate or label a different security configuration certified.
