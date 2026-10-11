# Security control register

This is the initial M01-C assurance inventory for the approved greenfield product. **345 controls imported; 345 applicability decisions pending; 345 results not assessed; zero passes, exclusions or exceptions.** The register is not an ASVS verification, certification or release approval. Existing foundation tests are not automatically control evidence.

Start with [controls.csv](controls.csv). Every row links an official requirement to a provisional owner, threat, enforcing boundary and [roadmap tasks](../roadmap.md). All 17 chapters and 80 sections are present, including conditional technologies. [roles.json](roles.json) defines seven responsibility roles; **all named human assignments are still pending**. The SEC role owns applicability and exceptions; delivery roles supply evidence; an independent assessor reviews release security. AI reviewers do not replace these people.

T029 adds **candidate implementation/test references to 12 rows** for the merged Development pilot: cookie/form protection, nonce CSP, explicit synthetic provisioning, session replacement, decision-time eligibility, code expiry/replay/S256 and scoped log/failure behavior. Their 11 October 2026 evidence date records the CI execution date, not an assessment date. Applicability, results, named assessor/review and exception fields are unchanged. The [pilot acceptance matrix](../../specs/004-first-party-login/acceptance-matrix.md) records PR #12/source/run scope and remaining T031/T032/manual gaps. In particular, candidate evidence for immediate authorization-change effects is only a partial decision-time subset, and native token-store revocation does not prove deployed resource-server enforcement. These references do not turn any control into a pass.

## Source and interpretation

The [unchanged official CSV](asvs-5.0.0.csv) comes from OWASP ASVS **5.0.0**, tag `v5.0.0_release`, commit `5cf9b032440be53ce345ab3c130fda46ba1ce7a2`. [catalogue.json](catalogue.json) records its immutable URL, SHA-256, counts and acquisition date. The checker independently pins that release, so editing the metadata and catalogue together is rejected. A deliberate catalogue upgrade needs reviewed code, source and assessment changes.

Official descriptions and levels remain byte-for-byte unchanged. Local IDs follow `v5.0.0-10.4.6`; the corresponding official CSV ID is `V10.4.6`. Section names are official; local descriptions, component boundaries and risk summaries are project interpretation. [Attribution and CC BY-SA 4.0 terms](../third-party/README.md) apply to the catalogue and its adaptations. There is no OWASP endorsement.

Source caveat: `v5.0.0-10.6.1` says “response mode” while listing response-type values. Preserve the source and interpret configuration through the normative [OIDC Core authentication request](https://openid.net/specs/openid-connect-core-1_0.html#AuthRequest) definitions. This is not authorization to enable implicit or hybrid flows, which the approved product excludes.

## Reading the register

`triage` is preliminary routing: **expected**, **conditional**, **mixed** or **deployment**. It does not decide applicability. Conditional technologies include GraphQL, WebSockets, WebRTC, upload/media handling, LDAP/XPath, SAML, dynamic registration and later logout propagation. Their presence or absence must be reviewed at the actual exposed component/dependency/deployment boundary. A conditional row does not add that capability to product scope. Managed .NET code does not automatically exclude native memory, range or resource-lifetime concerns. Operator certificate/key-ring files require separate review from public uploads.

[threats.json](threats.json) gives 17 initial threat statements, assets and trust boundaries, with role ownership. This is a routing inventory, not a completed deployment threat model. Every row has a PRD requirement and roadmap task; M01-C maintains the register and M09-D owns final assurance review. More precise feature/task/test links are added as each slice is designed and verified.

Allowed applicability/result pairs:

| Applicability | Allowed result | Evidence rule |
| --- | --- | --- |
| pending | not_assessed | Unresolved applicability stays visible |
| applicable | not_assessed | Applicable work is still unverified |
| applicable | pass | Existing implementation, dated/scoped evidence, assessor/review and automated or manual assessment required |
| applicable | fail | Dated/scoped evidence and assessor/review required |
| applicable | exception | Dated/scoped evidence, residual risk, explicit approver/reason and unexpired decision required; not a pass |
| not_applicable | not_applicable | Reviewed triggering condition/absence evidence, rationale and approver/reason required; not a pass |

An exception is a deviation, not ASVS conformance. Its scope and claim impact need qualified review. Exclusions are reassessed on the recorded trigger; they cannot carry an exception expiry. Other states must not retain exception metadata. A pending requirement cannot be excluded merely because its implementation is scheduled later.

## Maintaining evidence

1. Confirm the precise control wording, applicable level, affected component and configuration. Record role-to-person assignment through normal review; never invent an appointee.
2. Link the owning feature task and exact enforcing implementation. Record actual automated test IDs and/or manual procedure. Preserve the product revision, configuration, provider/browser scope in `evidence_scope` and the referenced evidence document.
3. Use repository-relative existing files in `implementation` and `evidence`, separated by `|` when needed. Keep private assessment material out of a public repository; link a sanitized local evidence index describing authorized restricted retention. Do not insert real credentials, tokens, recovery codes, keys or personal data.
4. Record canonical `YYYY-MM-DD` dates and the assessor. Review must follow or coincide with the evidence date. A role label or syntactically valid name does not prove actual independent review.
5. Run the two commands below. Review the diff and evidence quality independently. Changing a result is never automatic because a feature merged or a library supports a capability.

```sh
python3 scripts/check_assurance.py
python3 scripts/test_assurance.py
```

The CI job **Assurance register integrity** runs offline with a five-minute deadline. It rejects source changes, inventory loss/duplication, bad fields/references/dates, unsupported result combinations, missing evidence files, escaping paths and expired exceptions. Tests use temporary synthetic positive/negative records; they never establish product passes. The baseline assertion intentionally expects all controls unassessed; future verified assessment changes must update that assertion deliberately in the same reviewed PR.

This is **snapshot integrity validation**. It cannot authenticate an approver, establish assessor independence, prove a named test ran, evaluate evidence content or reconstruct historical transitions. Human review, retained CI evidence, independent assessment and external certification remain necessary. Successful validation must not be used as a compliance badge.

## Release/design dispositions still required

These requirements remain pending; none is waived by the internal login demonstrator or [A-001](../amendments/A-001-certification-sequencing.md).

| Official control | Pending interpretation/implementation |
| --- | --- |
| v5.0.0-6.3.3 | MFA and the L3 hardware-based phishing-resistant mechanism with user intent; password-only pilot does not satisfy this |
| v5.0.0-10.4.13 | PAR for every code flow, beyond the currently planned BFF policy |
| v5.0.0-10.3.5, v5.0.0-10.4.14 | Sender-constrained access tokens and resource-server validation |
| v5.0.0-10.4.16 | Confidential clients using strong public-key client authentication |
| v5.0.0-10.4.5 | L3 public-client refresh replay defense; this control's rotation alternative is for L1/L2 |
| v5.0.0-13.3.1, v5.0.0-13.3.3 | Hardware-backed secret management and cryptographic operations inside an isolated security module |
| v5.0.0-8.3.2 | Immediate authorization-change effects versus the product's five-second bound; evaluate mitigation limits including information leakage |
| v5.0.0-10.7.3 | User review/change/revocation of consent, still planned |

The security/product owners must bring concrete release-profile decisions and any material scope/security amendment for explicit approval when designed. This baseline does not pre-approve exceptions or weaken controls. These open release issues do not create a new approval prerequisite for the already approved internal demonstrator.

## Remaining M01-C and release work

Assign named owners; refine every applicability decision by component and deployment; expand the threat model; add pinned normative OAuth/OIDC and NIST crosswalks; supply exact feature/test/manual evidence and reviewed exceptions where justified; reassess on feature/dependency/policy/deployment/catalogue changes. Keep WCAG 2.2 AA and FR-19/NFR-09 accessibility acceptance separately; ASVS is not the accessibility catalogue.

Independent security assessment, actual Basic OP/Config OP certification, organizational ISO/SOC evidence and deployment privacy/legal facts remain distinct. A-001 only changes certification sequencing; it preserves mandatory PKCE and the public-release certification gate. No Foundation contact, submission, fee, production deployment or public claim is authorized by this register. See the [approved assurance plan](../approved-v0.2/09-security-certification.md) and [feature tasks](../../specs/003-assurance-register/tasks.md).
