# Initial administrator bootstrap contract — increment 1

Command: `dotnet IdentityPlatform.Migrator.dll --Operation initialize-administrator`. Default/explicit `upgrade` remains schema upgrade. Unknown operation is invalid configuration. Bootstrap requires only the platform connection and existing current schema; it does not migrate, provision database roles, read admin-session storage or listen on HTTP. LocalDevelopment runtime-role provisioning is incompatible with bootstrap.

Input is one UTF-8 JSON object supplied through redirected standard input, ending at EOF, bounded to 4096 raw bytes (not characters), with invalid UTF-8 rejected, with no unknown or duplicate properties. Credentials are never accepted from arguments/configuration. Required exact fields:

| Field | Constraint |
| --- | --- |
| operationId | Nonempty canonical GUID (`D` format); durable idempotency identity, not a credential |
| operatorReference | 1–64 ASCII letters/digits/dot/underscore/hyphen; supplied audit reference, not externally verified operator authentication |
| userName | 1–128 ASCII letters/digits/dot/underscore/hyphen/@/+; normalization uses Identity's normalizer |
| password | 16–256 characters; no trimming; native Identity validators own creation: RequiredLength=16, RequiredUniqueChars=1, no digit/lowercase/uppercase/nonalphanumeric composition requirement; maximum length is 256 UTF-16 code units, no trimming or normalization. IdentityV3 PBKDF2 uses 210,000 iterations, matching the existing explicit pilot baseline |

No email is asserted verified. Input errors, duplicate/unknown fields, extra JSON, nonredirected stdin, excessive input, unsupported operation, local-role combination and invalid database configuration yield safe diagnostics without echoing data. Standard input is not logged or saved. Do not place real inputs in shell history, examples, test output or retained artifacts.

Outcomes:

- `0`: created, or matching durable receipt already initialized. Fixed output distinguishes these; no IDs/credentials are printed. An already-initialized result does not mean the account currently exists, is enabled or retains privilege.
- `2`: invalid operation/configuration/input.
- `1`: conflict, missing/current-schema failure, manager/persistence/audit failure, cancellation or deadline. Fixed diagnostic; raw exception/provider messages are not printed.

A three-minute operation deadline covers input/EOF and operation; provider commands retain the existing 30-second timeout. Missing EOF within the deadline is failure; no unbounded input wait is permitted. No implicit retries. On ambiguous completion, use the original operation ID/username to reconcile with the receipt. Matching replay ignores current password validity and never re-enables, resets, regrants or recreates state. A failed transaction can be explicitly retried after the cause is resolved. Bootstrap never serves as a recovery/reset command.

The installation's durable consumed flag and receipt must agree. Missing governance seed or inconsistent consumed/receipt state fails without repair. Creation-only checks for existing target user, existing reserved roles or incomplete enrollment run after the matching-receipt branch; later legitimate changes must not turn replay into repair. Unrelated accounts, pilot configuration and sessions remain unchanged.

The created user has IsEnabled=false, TwoFactorEnabled=false and LockoutEnabled=true, a manager-generated password hash and security/concurrency stamps. Enrollment is pending. No session/token/factor/recovery secret is created. Until increment 2 supplies its verified enrollment ceremony the account cannot sign in. Existing pilot denial rules remain mandatory.
