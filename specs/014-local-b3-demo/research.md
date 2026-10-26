# Design research

## Windows and credential storage

Decision: Windows orchestrates Linux containers; private role files and protection rings live in app-owned Linux tmpfs. Existing private-storage checks remain intact. Windows bind mounts and a new Windows ACL adapter were rejected because they enlarge the security boundary unnecessarily. Disposable lifecycle was explicitly selected by the user.

## Canonical TLS

Decision: use `issuer.dev.localhost` and `bff.dev.localhost`, fixed distinct HTTPS ports and matching Docker DNS aliases. Host and container target ports agree. Aspire 13.6.1 supplies documented development-certificate/trust hooks; ASP.NET 10.0.12 development certificates can include `*.dev.localhost`. Actual certificates and trust must be validated. No host rewrite, remote-certificate callback or ignore flag. Existing certificates may lack the required SAN; fail with actionable guidance rather than regenerate/remove unrelated credentials.

## Material ownership

Decision: seven distinct RSA3072 certificates (issuer signing/encryption/wrapping, client state signing/encryption/assertion, BFF wrapping), freshly generated for each graph. Only owning services receive private roles via secret parameters. Linux adapter creates bounded encrypted PFX files and private rings before existing loaders. Docker-authorized operators can inspect environment metadata even after stop; secret masking is not confidentiality or secure erasure. Dispose consumers before keys and remove owned containers at completion.

## Initialization

Decision: reuse existing separate schema/role, pending-administrator, public registration and B3 upgrade commands. Bootstrap remains bounded redirected stdin; no password in environment, settings or argv. A Windows helper identifies exactly one run-owned initializer container and writes prompted JSON to `docker exec -i` stdin. Public registration fields can be environment configuration; private assertion keys never reach the initializer. Interrupted/ambiguous operations fail without automatic retry or repair.

## Runtime activation

Decision: existing AuthServer startup owns B3 account/protocol/logout configuration. BFF uses an explicit supported owned pipeline, retaining the existing rejection of direct automatic minimal-host composition. Independent architecture review selected generic BFF hosting for both default and demo modes, with no global authentication middleware. Explicit health routes and inert unknown paths cannot dispatch native work. Default health-only configuration remains tested. The existing guard against direct WebApplication composition remains in place.

## Evidence limits

Pinned API/source inspection establishes feasibility, not Windows execution. Actual Windows Docker Desktop trust/routing requires its own evidence. Required Linux CI verifies normal runtime hosts, both providers and retained browsers. M02-C refresh remains separate.

## Pinned certificate API diagnostic

Aspire 13.6.1 marks its development HTTPS certificate APIs `ASPIRECERTIFICATES001` (experimental). The single `WithDemoHttps` method opts into these documented public APIs with a local pragma; no security analyzer is disabled. Owner: local demo maintainers; expiry: next Aspire version change, when API shape and trust/routing tests must be reviewed again. This is a Development-only composition decision, not a production stability claim.

## Runtime image pins

The Microsoft registry manifest responses were fetched over verified HTTPS and their response bytes matched their `Docker-Content-Digest` values: SDK 10.0.401 `sha256:e70cdb7f80b0348f5cb85f19a8f670fca061f033d57eed12fa003d58b0e06317`; ASP.NET 10.0.12 `sha256:222759b391a1aaf241166672c8f99b2d4ada452e7b5319f3c6e8f265a37b5ad4`. The Dockerfile pins both tags and digests. This registry metadata check is not an image build or SQL execution.
