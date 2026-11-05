# Local lifecycle mutation receipts

These sanitized runner receipts record the original comprehensive journey as the disabled-admission
mutation witness. PostgreSQL ran at `b7886ff6cf87d0cab541ecdc2e8412b8f4b8ea80`; SQL Server ran at
`2f852e2dff9230d93a94f0df00f654542097929f`. Each records four baseline passes, four demonstrated
behavioral kills, four restored passes, zero skips and exact production source restoration.
Independent review matched the actual methods, traits and original/mutant source hashes.

They are historical evidence, **not receipts for the subsequent focused admission witness**.
The original complete journey remains an ordinary required case. The subsequent focused witness has its own strict baseline/mutant/restored receipts below. The hosted SQL failure outside
the expected marker in run517 remains rejected; these local passes do not explain or erase it.

## Focused admission witness — a87a96d

`focused-admission-mutations-PostgreSql.json` and `focused-admission-mutations-SqlServer.json`
record the actual runner executions at `a87a96d3f7669fc6322e90827028c81dd2dc7508`.
Each provider has four baseline passes, four exact marked behavioral kills, four restored passes,
zero skips and restored source hashes. Independent review verified byte identity, the exact
provider/CriticalDecision metadata, manifest substitutions and original/mutant hashes.

SHA256: PostgreSQL `d8d98b04ffedf7b2a03c3a5abe416890eef869fdce6690eac3ad3c1a6a91ba06`;
SQL Server `b9795e78c314dc90d743a8aff4231b7801845ea8fb8243087264f5599e518c47`.
The independent review did not reparse ephemeral TRXs. These receipts bind to a87a96d, before the
separate live-clock browser fixture correction; later exact-head hosted verification remains required.
