# Data and lifecycle

No domain tables, provider schemas or migration histories change.

A **demo run** has a random ownership identifier, canonical issuer/BFF authorities, disposable database resources and seven distinct ephemeral certificate roles. The issuer owns three roles, BFF owns four; initializer receives only platform runtime credentials and public registration input. Schema migrator alone receives schema provisioning authority.

An **administrator** follows existing pending → password/enrollment → MFA-active transitions; initialization cannot create an already active account. The existing fixed client registration is initialized and upgraded once with expected revision 1 and its original operation identifiers. Partial failure has no automatic repair.

A run progresses configured → schema ready → runtime ready → explicitly initialized → enrolled → disposed. Runtime readiness alone does not mean an administrator or registration exists. Stop/restart of an individual service can invalidate protected state; restart the complete disposable graph and initialize anew for a clean run. Old cookies and previous-run data must not acquire new authority.
