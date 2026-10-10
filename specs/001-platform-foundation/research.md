# Foundation Research

Decision: SDK 10.0.401/runtime 10.0.12 and Spec Kit 1.1.3. Its annotated tag object bea765ce48b383c811872bfa2761a1e9600b7f32 peels to source commit 0282d03eb6fc3091b3deaa3a855d4030dc40cd6c; record both, rather than calling the tag object a source commit. Initialization uses bundled Codex skill templates.

Decision: native Identity/OpenIddict EF model in PlatformDbContext and separate BFF context/schema. Use native manager writes and provider-specific migrations for both contexts. SQL Server protocol identifier columns use binary collation; verify PostgreSQL semantics with real queries. Reject provider-neutral migration assumptions and mocked database equivalents.

Decision: Aspire 13.6.1 uses explicit provider selection, Migrator completion before dependent services, health readiness and isolated resources. Testcontainers 4.16.0 verifies native persistence; distributed tests verify process boundaries. No generic security-mutation retries.

Decision: safe configuration errors identify fields without echoing credentials. Only explicit known providers and canonical HTTPS issuers are accepted. Runtime hosts never migrate automatically.

Decision: mandatory PKCE remains. Official suite release-v5.3.2 source confirms Basic OP usually omits PKCE with no global setting. Evidence and proposed sequencing are in docs/amendments/A-001-certification-sequencing.md. No suite was executed and no external communication sent. Infrastructure work does not depend on issuer acceptance.
