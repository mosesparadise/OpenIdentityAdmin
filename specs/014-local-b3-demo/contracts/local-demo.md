# Local demo contract

`LocalDemo:Enabled` is disabled by default, accepts only explicit boolean spelling and requires Development. Publish mode is forbidden. Fixed local canonical authorities must match native issuer, registration callback, browser destinations, Docker aliases and TLS SANs.

Bootstrap invokes the existing `initialize-administrator` command with UTF-8 JSON over redirected stdin, max 4096 bytes, exact documented fields and native password validation. Public registration/upgrade uses its existing max 12288-byte stdin contract and expected revision 1. The Windows helper requires exactly one matching running, run-labelled initializer; it never lists or prints environment values. Process invocation uses argument lists and a bounded wait, closes stdin and disposes process state. Failure stops the sequence without retry.

Private files are created only in an owned private Linux directory, with CreateNew, no existing/symlink targets, bounded input and fixed role names; temporary decoded bytes are zeroed and owned partial files removed. Existing loaders validate encrypted certificates, role separation and protected rings. Docker metadata remains within the privileged local Docker trust boundary.

BFF owns request scopes before native client dispatch. Health/unknown routes must not accidentally invoke native authentication without an owner. The issuer preserves routing, account protection, protocol ownership, native authentication and authorization order. Existing exact-origin/antiforgery/fresh ceremony/publication invariants are unchanged.
