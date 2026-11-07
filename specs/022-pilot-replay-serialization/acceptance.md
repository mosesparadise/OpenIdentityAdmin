# Pilot replay correction delivery

Feature022 T001–T009 is complete for internal development. The initial pilot replay/late-issuance correction is accepted; historical, manual and public-release gates remain open.

## Accepted source and delivery

- Reviewed source: `0be053ea3ee09086634ecf981f3bfb1f01917608`.
- Tested PR merge: `922fca6f51cb22d6ebcba940dbc7292db95e9c01`; tree `1302f34ea93a66344490e5eaf2ff685855fdac5a` equals the reviewed source tree.
- [CI553, attempt 1](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/42399708731): all 126 jobs successful, no failed/skipped required job.
- [PR126](https://github.com/mosesparadise/OpenIdentityAdmin/pull/126) normal squash: `c2e41521ece542a92fa7ddb8867834b3d81b9ee3`.
- This later Markdown delivery record documents the accepted runtime source. Its own prose-only checks do not claim another full runtime execution.

## Verified execution

| Evidence | Actual result |
| --- | --- |
| Ordinary native reports | 103 reports; 5,899 passing executions; no failures or skips |
| Mutation controls | 120 demonstrated behavioral kills, with source-bound baseline/restored controls |
| UI and Node | 83 UI cases and 44 Node contract cases |
| Original coverage | 100 reports and complete before/after build bindings; 15 unchanged floors |
| Retained run evidence | 120 digest-verified archives and 126 complete timestamped job logs |

Expected mutant failures are not ordinary test failures. Native execution counts include provider/browser repetitions and are not a unique-test count. Focused contrast is retained separately: one passing native execution.

The focused SQL conformance run passed 27/27. Both synthetic controls explicitly asserted NativeTokenReadMatched; an ordinary primary-to-secondary flow also used that path, and the opposite ordinary flow used GateWait. The deliberate extra lock belongs only to the conformance fixture and does not modify native values or product isolation.

## Original coverage bounds

Each module retains 90% line and 80% branch floors. Branch coverage below is a conservative lower bound computed from original method conditions; merged branch summaries are not authoritative. All 100 archived before/after manifests, source/tree/run/attempt, DLL/PDB hashes, collector bytes/settings, owning jobs and step times are reconciled.

Observed SDK: `10.0.403`; collector: `18.12.0+e67518936dc54d459b37375c5eef5dc89f05d535`.

Unchanged coverage policy SHA-256: `0e26c85af81345cfa6f339a6bb6342a5697ec8c413c53f57ab1c8e5aa6587020`. Collector settings SHA-256: `6f7e7be9c12ecf74a7c7fa196f5aeaf15eb86975fb66cad29e025c3e255ce1ae`.

| Module | Lines covered/total | Branch lower bound/total |
| --- | ---: | ---: |
| IdentityPlatform.Core | 342/343 | 265/275 |
| IdentityPlatform.Infrastructure | 8431/9024 | 4099/4837 |
| IdentityPlatform.ServiceDefaults | 70/70 | 43/44 |
| IdentityPlatform.Migrator | 608/616 | 319/366 |
| IdentityPlatform.AuthServer | 6895/7363 | 5363/6700 |
| IdentityPlatform.Admin.Api | 1191/1274 | 694/843 |
| IdentityPlatform.Admin.Bff | 4039/4274 | 2900/3455 |
| IdentityPlatform.Worker | 60/61 | 4/4 |
| IdentityPlatform.AppHost | 531/547 | 220/250 |
| IdentityPlatform.Database.Abstractions | 111/112 | 71/78 |
| IdentityPlatform.Database.PostgreSql | 96/96 | 12/12 |
| IdentityPlatform.Database.SqlServer | 93/93 | 16/16 |
| IdentityPlatform.Database.Hosting | 42/44 | 14/16 |
| IdentityPlatform.Database.PostgreSql.Hosting | 13/13 | 0/0 |
| IdentityPlatform.Database.SqlServer.Hosting | 13/13 | 0/0 |

## Mutation controls

| Suite | Provider | Baseline | Behavioral kills | Restored |
| --- | --- | ---: | ---: | ---: |
| issuer | PostgreSql | 7 | 7 | 7 |
| issuer | SqlServer | 7 | 7 | 7 |
| administrator | PostgreSql | 8 | 8 | 8 |
| administrator | SqlServer | 8 | 8 | 8 |
| governance | PostgreSql | 6 | 6 | 6 |
| governance | SqlServer | 6 | 6 | 6 |
| administrator-protocol | PostgreSql | 3 | 3 | 3 |
| administrator-protocol | SqlServer | 3 | 3 | 3 |
| pilot-replay | PostgreSql | 5 | 5 | 5 |
| pilot-replay | SqlServer | 5 | 5 | 5 |
| applications | PostgreSql | 8 | 8 | 8 |
| application-grants | PostgreSql | 6 | 6 | 6 |
| application-lifecycle | PostgreSql | 4 | 4 | 4 |
| applications | SqlServer | 8 | 8 | 8 |
| application-grants | SqlServer | 6 | 6 | 6 |
| application-lifecycle | SqlServer | 4 | 4 | 4 |
| managed-applications | PostgreSql | 5 | 5 | 5 |
| managed-applications | SqlServer | 5 | 5 | 5 |
| browser-session | PostgreSql | 8 | 8 | 8 |
| browser-session | SqlServer | 8 | 8 | 8 |

The reviewed native-replay manifest transition is `038172eda99e1c5d526ac56e211d3ac8018a4333543a76803e0b85e4c3781989` → `b05efab35c7f2c72478115814abc920e6ff3524a8d3c47d1b55889ffc7758350`. Only its path/before/after changed; identity, marker, kill count and every other original mutation remained fixed.

## Discovery and review bindings

| Project | Discovered cases | Discovery SHA-256 |
| --- | ---: | --- |
| Core | 41 | `a467fc0c871a7e7592ed01e2b7630a40b6ea7a706e970d4314f9ebfa1ad1b17f` |
| Host | 1874 | `db90a7a401aca086775f4fef3649d10deba4e805559a41e8e188ad346c8078f1` |
| Architecture | 5 | `30024ff7410c47e7aa818186b8b4c438187605c7acaf922aa730212f583e0289` |
| AppHost | 152 | `949503be7e6606359f7130523d6ec9457740e2c482d0f1cf38390f7a84d16675` |
| Issuer | 646 | `b640cbce8d9c1e26f830462205f69860c0da4b7270b281f0d9ee691d496e09b6` |
| Issuer.Lifecycle | 88 | `9bcd753876e248fb963ea7e8bf415d929d4fc95ad62a421aaf7590ec4f2f3300` |
| Administrator | 2771 | `ce4c70b2f7432e8ad983679455df353cb81155a7fe30658518828470c8c5f131` |
| Persistence | 58 | `577b3f40527b4fc8ae48046c0e7a06316c676d7e221d3747dfdc38b068b0f3f7` |
| Contracts | 51 | `c438e6fc70bfc28f167a86b0c2cc7318fb94a5908e9d991ff2ca54f4948b7fdd` |

The final solution build passed with zero warnings/errors; fresh discovery ran for all nine projects and style verification passed. Existing lifecycle identities were preserved, with only two SQL conformance additions. Independent architecture/security/provider and test/evidence reviews accepted the scoped implementation and actual SQL lock proof. Separate NuGet and frontend advisory/license reviews assess the retained payloads and their documented exceptions.

| Retained receipt | SHA-256 |
| --- | --- |
| `reconciled.json` | `4761325e11ceb4a071fd1f5859e1b8d0d6bda16e3b1e46ebdabd20eaddd61859` |
| `observations.json` | `0c3c34e9dd96b31a919b3d33c214651f6462d44066720252debcb2cff12934d1` |
| `pilot-t008-obligations-0be053e.json` | `311d3dee99fa7af2189f70e53b732a943233def1e0ecdfd69bf2c8c2420107be` |
| `independent-final-review.json` | `5fae83b56bedffb9e1491465851cced7e66af46ffaaaeb0f68c663e60644b809` |
| `independent-retention-review.json` | `b09d95118701496934f7d1e02fffcbd65dfaf26d5f1d87e943b87c45a6ff2de7` |
| `source-discovery-review.json` | `7d1c5912a197e45472e2f4ec8b22a749d2b6de7cd0b98dd8386c12008e25be08` |
| `supply-review.json` | `4ac8bb356ca48cb95b87fd29a9cc38063e5bcd7297f786a578e5faa3ff434dce` |
| `frontend-supply-chain-review.json` | `2487f071256a29ec2c33011b81eb39d0c13e291d1fa6c7fa6dfe12cd1142121b` |
| `frozen-producer-preservation.json` | `514864b3846141a85c87b9fb9c61676469673c90f6c01240008d41f66b8a4d16` |
| `delivery-facts.json` | `7684f70a6f12ccd81ab33c837024810c2435c393fa0188de5d0e3eeaa3a37b15` |

## Dependency review limits

The retained NuGet feed reports no vulnerabilities across 31 projects; license
review matches 288 locked package/license pairs with five existing exceptions.
The private UI and contract npm audits report no advisories in their queried
payloads. Their graphs contain 104 and 76 lock entries respectively (180 entries
across two graphs, not 180 unique packages); the UI runner installed 49 compatible
packages. npm 11.9.0 was selected before strict installation.

The dormant contract viewer's current query contains 14 reviewed advisories across
seven packages: one critical, four high, four moderate and five low. The reviewed
policy inventory contains 15; the absent qs advisory 1243452 is not declared fixed.
Viewer activation remains blocked and its inventory remains incomplete. Existing
TypeScript 5.9.3, OpenAPI 2.13 and Playwright 1.63 compatibility exceptions remain.
These scoped feed/license observations are not legal clearance, a complete SBOM,
or a guarantee about future advisories.

## Delivery limits and coordinated upgrade

Stop/drain every old pilot issuer, apply the additive PostgreSQL `20261107135812_PilotProtocolGate` and SQL Server `20261107140026_PilotProtocolGate` migrations through the normal Migrator, then start only corrected binaries. Native data and existing migration histories are preserved. HTTP never repairs missing coordination state. Ordinary disposable Aspire launch remains unchanged; no production migration is authorized by this record.

CI551 and CI552 remain failed attempts; the failed first SQL diagnostic and the limited first focused pass remain retained in [verification.md](verification.md). This successful run does not relabel them or renew expired approvals.

All six historical findings, refresh activation, Windows/manual/Firefox/A-009/vendor/production/public-release gates remain open. The two exact deferred issuer WebKit certificate cases are not counted as passed. The dormant contract-viewer advisory disposition and incomplete inventory remain explicit; no blanket vulnerability-free, legal-clearance, SBOM, certification or independent-assurance claim is made.

Evidence binding relies on trusted runner metadata and build copies, not cryptographic loaded-module attestation. Host display-name truncation collisions are bound by exact multiplicities and portable metadata; native discovery UID-to-TRX-ID equality is not attested. Raw machine receipts and complete archives/logs are retained in the session workspace; GitHub artifacts use the configured seven-day retention. This Markdown preserves reviewed facts and digests, but hashes alone do not guarantee permanent retrieval of expired or session-local bytes.

## Original coverage report and manifest bindings

| Cell | Report SHA-256 | After-manifest SHA-256 |
| --- | --- | --- |
| unit | `e62f2948dae6c3f9afad6379fac3a0778d8225f8fead871b9e1589cc9ea2029b` | `c29b1e0e00dbd4cab25628bf28a01fcc91c3a3bb8ef20b61e1c83a9f0f522bcb` |
| PostgreSql | `17349c4ad935702252804cb9161d2b494776a3e8a525ab9552646dcb072e568a` | `43ca7ea8907d717f93cb9c30b44a2f41bf3203b3a439785348d9aa9c399b914a` |
| SqlServer | `344f414cbcfe96151b5f525f6217897810011272ca07c5647f113ba2a47c0c01` | `c38058b6efe1d73b37ea18c87ad335257e5d1f3e0c08859968eae5a7385e26e4` |
| PostgreSql-apphost-foundation | `a3f42544b8c3e01e5d2a3426bfb97fd32e7b56885a1e6bc4a5cb7bc9ed289d15` | `b451338833a82570d20f623037f01839d68100cdd2b94d17bd95fff1d7f02ad0` |
| SqlServer-apphost-foundation | `af37413c27b721ccf67b6f60d4bf1ee48222faa094a79f5c6aeccf781706831f` | `1f20cb32f16868b55f479ed80dc9d762cdcfa6a11a6e9cc9f3138cec5a765727` |
| PostgreSql-apphost-bffsessions | `6e2eff44bfc81addeac30c15a8c63ea2a52aea61ac7fd1c275dcab758b84addc` | `42fdc32eca8cdfaf03d68bc3d441dc287a35559f148dea0cd6f860036ae6b48d` |
| SqlServer-apphost-bffsessions | `a108ff4c1d1e7143b950e8f6546c86534ed77640c536add14bfb6e2397a99596` | `57cdf1be2e840c402b881afe5030bc6634b9128365b8cc69bfd3dee354ad1f45` |
| PostgreSql-apphost-localdemo | `927e59201ddd28483612fa5112e0d27b3729d7625757dc9f1ae9f707615544ab` | `825824bd7d73c66ee208241d1efdd1894bca1eff3ae7d9aecf81e31082a82d6d` |
| SqlServer-apphost-localdemo | `01b4553f103423cceba427a34fa8e964731276450363cbbb5a8e4ab9331f8187` | `fa690216822a86c7336c31672faca9f8f3ebfa822e9012f6f332b6aff84f24aa` |
| issuer-PostgreSql-flows | `2fa9d22984e78e89fa1d9a8d5332e4ed2f9d62ec1d2bf075a44fe32a0c263f60` | `4c8901f7ac61a484bd1871c972f0516e36f444686d5753af2221dda211cad7b7` |
| issuer-SqlServer-flows | `75293bbd343e02f4db8920c098d8a59597d6daa66d7ee929aa8082ca77cff1c8` | `23605bf39a75d73d19ae4c3ce0fc887b1180dc763e8748d4956dcf5efa885c2c` |
| issuer-PostgreSql-boundaries | `ce46c615be5c79b2483c68f21441964d8264c02cb0579fd815395af97742ddac` | `18beec1880fbf928c60ec6b9c1cc6af1596306d0c773fab36909dbc6f52b5dfe` |
| issuer-SqlServer-boundaries | `57d738258b77fc1a23a6b5061ae7b0d75c29b620b6ae8601c29968bf26c06fbf` | `687eacd5e02bfd4282385f285e4eafd17a30f3f7029bc680c72d1303808ac16a` |
| administrator-PostgreSql-bootstrap | `f525245cb452222e250afdbc74d6ff96cd5bdc2c0c47d66ce5a0b2fc3838763e` | `362fe6eefefb8c4ab70c327e9aff4c916ca9e0efd62a4c0213a1e785f947c3f2` |
| administrator-PostgreSql-factors | `722150d0219008652153b02c87aaa7f4aa6326a300def54b760bccf9098c0f6e` | `88a78df7ccf1fa224622834dc7b019a9683b09f671190e41ed90bcf76cc23cef` |
| administrator-SqlServer-bootstrap | `bdf05de0410f723b2342267769cd88ba1101fc90fbb0b183cf318c15a56d86d4` | `9ef38557bba40031df42e4a6d6489a0f31acb07509665fd12ef4c269527be0ca` |
| administrator-SqlServer-factors | `bc12a6cae9e6e87955907c322a7d91489bf8d00c405ef3819a2d713c20085c89` | `e5fb08466f6669b9d42abfb2314469967b80519fce741e6f7d46e0b548240168` |
| administrator-PostgreSql-recovery | `5fda30c3a83069eb7572609e6b50d7ee40f655b7de1f00c65b252b411bb5f224` | `4b84294d7bd5369d116aa221ea9657cb0645af29999226458fe7090717180eea` |
| administrator-SqlServer-recovery | `e6151e19e12c29b573476acf798b97bd3487782716383a38147c45da92a2e8d5` | `59bbf925306d6fcaed9e358010727f0b1023edc2747b402ae9711ce1d44e9ae0` |
| administrator-PostgreSql-security | `fe3f1c1c12c267c28db9b245cf2dc652727a483853cc93582b252c32f94743fc` | `23a6e2d8e301704c8c152b14601c42d235baa2ae05b5dc2daba77d0f0523dd9a` |
| administrator-SqlServer-security | `8b43ca674eb61f34f50b1d0703c6a7bb6802b57c1df48d027c2031f886bb515b` | `b16b8979e423de13d93ac8b485d92131f2ec7ef824b547f039a42fa5bd06382f` |
| administrator-PostgreSql-accounts | `23c23c76aed706d4b8afe3e2e09e2f0472796b3fd4cf80ae593ce22d4289d19b` | `6a2928a2b9734b27e61d70368fac92136d15d403ef06049107b3e90c4776f602` |
| administrator-SqlServer-accounts | `0e235a9f602b9bac3d2fbf5bc9053178335c33f58b2ef45f8314b8f9cf5fe41e` | `edce4a740ca3f2a75b26b4db1dd9315741b7beaa46d875ead41a2700f2735825` |
| administrator-PostgreSql-ceremonies | `9930d7815cc2e3b8e0b772e6f47567ddc2d49cc774e95760d983282f7176cb62` | `8b2b0b5fe7ff6d8550facd0cc40e911db45e1987986229abe9f2144f76baf796` |
| administrator-SqlServer-ceremonies | `2ae2443f720b330d922c931aa60c9bed5c7f589e94879a62dc6b0d646140a4b0` | `1fd914fce1e068d138435f2c2ad685c2a59a91054c64f9f4c5fe465788b825ba` |
| administrator-PostgreSql-governance-policy | `0383589ee78d4b76b8a8be3dbae5693d0510d7d474d80277ca58258bb751a5b0` | `c9c13cb450035a86fa9cf0834275d96de599bfa67af23d62da4739ec0cac677b` |
| administrator-PostgreSql-governance-commands | `01b70c510dd59669b6f4f6e415085474b74f5c2da50be8f2a8fc28ba85979815` | `69450b014c349e47c4fd09383046644e7a142e090c6bf0d27ae6fc6e89079453` |
| administrator-PostgreSql-governance-failures | `34692642857d1a0d80b304f400627610260f0342ade00d25717dc73e6c350ec4` | `b9726f05154478952b411e5ba8e4f69ce96f847d2a3125de1684886138a4f5f1` |
| administrator-SqlServer-governance-policy | `c7675a4adca7a6dd3d97de214c5e36082e8af121506b753de444c9205d1f93b8` | `02c6a872dc996764d55cced0461fed7bc8973b5502585f09284113a521e459c3` |
| administrator-SqlServer-governance-commands | `3a091a2be25e962f52c4af2d79701f02abd8a10a07dc2daa028e59a1cfa55fcc` | `7c98ee47843ff5ccfd6593a0d74919812f8983e4fbac65446fc43e141dcb9266` |
| administrator-SqlServer-governance-failures | `dbf3b0079df3ce4f653c5c119382ff9804c86b94eaf0f1776d890725c211653f` | `7429a9e513f6ca944c73a18f41adbc8b896918b9cdfc04a756ee7f1060a3974e` |
| administrator-PostgreSql-managedapplications-admission | `e69b983163d48bb2600de7dcd375aab037a8b5f9d29a5dd8975402f1b571864a` | `7d2c8df8d2879b9555be7d0cf8a3d8ca5b0b97d74c3004c588b5ed951c63e859` |
| administrator-PostgreSql-managedapplications-issuance | `0b2cbe746ba4e087760b35f680ec72f398443eec6529e180dedc8ed4574b3589` | `1d4891a23beda3fb93d942aec906a971505a71ba1e91f8a4cab4dd311622f583` |
| administrator-SqlServer-managedapplications-admission | `e6585acdf2f31eb8b4aabe727ffbad0fafd0fc7133d7248c59c94465d9ed9f24` | `edfc2cc39cd9f23f9abdb341e492c89672e526a3ec2116cab3374a22b8c33955` |
| administrator-SqlServer-managedapplications-issuance | `35c7824a255360cdc629259e8735126044458e2c446bb2ea73dcebf4c6f0faa4` | `406cde8177c75e9742ec1b13fbacc1b2cb2d1e5aba2002f60acd248bec9cccc0` |
| administrator-PostgreSql-applicationgrants | `58f27c71318523146343511ee8bc446165b6214f901ada461b5c72119f00a724` | `35459d3acd44729feccd505d88e9695f813ff8dbeb6209ddeb823d63a1ac148a` |
| administrator-SqlServer-applicationgrants | `07a93dfb0c07a9148008a730c18453606d3da2158099587f4e1c2720356ebefd` | `e40512e6ed6ad3666171fe729ee47707f7f3f808a53e073499a8514177c4704a` |
| administrator-PostgreSql-applications | `5a2014103cb0e4cf955a8ec462aa70d3340783f8502dd9754084dd9c60b5bc18` | `6ea197516404aa24e9d4fc7d3d945f37d9ad141176454ab8566bf1f2f4a25eab` |
| administrator-SqlServer-applications | `857ced42ea042b90e0cfde8f683be22923a2b02a6324c006210b08f5a23f58c6` | `3db96de42f5123500108e6d1bb47da1cbeb829e23e80cb9f93cb673e5895f7a8` |
| administrator-PostgreSql-sessionrefresh | `0550df2bdfdc7c12cc30a4afd4dc33429945eef27debe767af7291bd090950b5` | `9b97dab689ed302605a65036273f04340f5eb0727ea12182487249fea5453fc1` |
| administrator-SqlServer-sessionrefresh | `6a604b1884e39fcf0b3ff7cf4a6919270b1217b8da1e7a2ab828be3b65c08470` | `e116a265a7c6fa95229c0c47de3a9a45b670ea2277e3acb7f0298e1c9c974c6e` |
| administrator-PostgreSql-protocoladmission | `94768be248e3fd1273ec4116ce6d466c96e96c404465bb5ccd4c9c48a25c2195` | `38aa512cfbd7f9c82b2f1e78be192fa27e01bd4a35cf173bb8f38e034d58698c` |
| administrator-SqlServer-protocoladmission | `de190bbc1f96a3e769a12a75cfcccd9677e3666ab14159708859bb39de6dcca0` | `5de215adea4cb017c5c33ae92ff5efb212a95b506b3cce715a436018ae919acf` |
| administrator-PostgreSql-browsersessions-foundations | `d10cd9207d85723f01cb67e7309c00ab8cd14df20d49e9fc4af7183fc8e81905` | `601eb0d3f20d36061b77fa49861951e8dae1f4b60bb034be928e2e07bd861e4b` |
| administrator-PostgreSql-browsersessions-product | `8fb896b271ba5e25e309d85cb44e5f1de116966d3f777ded00b4ced1fe6f00c8` | `66b1e8a7fbd1048a3ac278a06d21a926a7ba114555cc1accb401eb9d80673bfa` |
| administrator-PostgreSql-browsersessions-lifecycle | `0d2fb716c19dd792b3e3f9cf649b6375e6cafeecd297475f8c7dff8d0abe4ffe` | `91ca16d4dfdc0e827b0f8371a6410be20014c3d24acc619467aacecb276dd422` |
| administrator-SqlServer-browsersessions-foundations | `b4e52a93673246adba5ce19290e3ea46d8dc924124af728c515b485125c0922c` | `d7369981fe3dd5227038d2215e797ce402eb70d49549fcb1490a3af159532c51` |
| administrator-SqlServer-browsersessions-product | `9b17c9b5fe0375b9d4cf431c9376279afd6040b2a6a62a70e1d05226d1e71da2` | `8f0493eb6dcf6e17f82ff552be87a04c0b0e4bb205752b275621ad771afa2dcf` |
| administrator-SqlServer-browsersessions-lifecycle | `a67f30da4ea384b07b5637ddf8857d22b73ef388c92ad2b5cd8ad34ddc7580d9` | `40d7c1ad4c5922cf143dd5aee28a3432097af4122cef5b558d9d19eaf0af5317` |
| PostgreSql-lifecycle-restart | `86806da7a8129da972826f17759402bce174edfe3da2e3a681a77d7e06099f16` | `ec40feb9f0dae22d5380f7f598ac9b32c53cc548d3bb8bc0be55b4e27662c837` |
| SqlServer-lifecycle-restart | `f9c12538afa915de91584ec76e1d560b5c6cb69a15d0f00037048615501bb28a` | `d60a69837e516bcb532829002910dfd5d344e89174d41df62a85fa1bf53c9ed6` |
| PostgreSql-lifecycle-distribution | `46b72d954622bfd88d63dbbf8aa9c99e4d5be50a40534d545dbe9c33a37c0075` | `f9ccdfdd5377225326048a97ab26d754deb229f1533cd39e62a44fd5e56ac465` |
| SqlServer-lifecycle-distribution | `df70e20b2741d7d02d754c4fbcc491db9b81d9ee838f97fa9e8bbc6d5203fa48` | `8c21028f2a8ce77cd00aca08f1ad6e758ad8d14a2564a63907d27a4d678a4584` |
| PostgreSql-lifecycle-keyrotation-activation | `28633f1c5abb5ea46d01966768458d8a7a720bd1d333254ce6144ad8409b8159` | `b280af8a2c1b71fb1fe73bcd42f233ae914b98047191fdf5a72547bd05cd670b` |
| SqlServer-lifecycle-keyrotation-activation | `ed57cd80a3993d11cea1c3eba8d24c8f18382aeaf4a4ab90c2dff6457e8e844c` | `96c71b35721bf7c7e395c2b1994a263a479b7a5c9f244d639f74fd0f829282cc` |
| PostgreSql-lifecycle-keyrotation-retirement | `7f2707c64b4abe57f106b1a6b2eef1309d9667a9763da6a9a263410b32a034c7` | `c8170b53bbb7e4dda9d569b5d211b20e547be45d7298324b4675445919519016` |
| SqlServer-lifecycle-keyrotation-retirement | `cf53460e62e90da294003781c0f9b287ea9e73d8c3f29ba87f3f77fae538ddec` | `774d4fce0b37ea3e707571d8e3c6786c26510d2663932380a3dd582661db155c` |
| PostgreSql-chromium | `19863e7b18a0dfea8924a0f8368c7878f6e6d905b63e78ca4e24ea35f54ed42a` | `61bf6602f8e1e6f05bd26057a2156a2bcf08382016c8bb8a5efbd8b717038971` |
| administrator-PostgreSql-chromium-accounts | `9e7274dd91fce2ada9cc6aadfde0d0e208e19d89d67b5a24e30ea8b39be1e8cc` | `4e9eb7aa50c018fb160a2b8461e9b8f95d799f69a79d6cfad3e42a6cac20ad05` |
| administrator-PostgreSql-chromium-security | `274594a4387a52a74ee54a4b59293674172577f9da75d7b1ee3bedf788d954b1` | `fbefe0b5fcacac3feace27b5e587daa9b57382cbe25b7a75293f34284ba2c1fc` |
| administrator-PostgreSql-chromium-browsersessions | `cafe2d6bf2eb0b0da6a03b018b5ef5d7c60568ab7779b2f4d2a3457efa0d5f4c` | `f4a4cf925620f9ce47d5bc0cf629d361c149d359d5d4ca6135afd41244b4ba31` |
| administrator-PostgreSql-chromium-browserlogin | `d3657d2ef1e4b83e6bb9433f490bab50ad51c249cee131577f7d82ad6af7cb21` | `408c03ea110a646ef19bec23ecc110f39b9892dc14442fe40fae7d38895c5671` |
| administrator-PostgreSql-chromium-browserprotection | `c796f1cd0aece95aece6e1d16fbd7e3860e43909131a3e2550fe769c3f940377` | `f8c7671a99e7b27bfe792da12561a9a87ce26d10b6b58e5bea732326cfccfd9e` |
| administrator-PostgreSql-chromium-applicationeditor | `e1f7dff588f86f57d76b08f6073809ad468b11444eeb198a9ec56df95f40fb6f` | `5d2cb358da58a383765ea9b60f1d4a23a012c257dd5bef2565c6566c8f211ff7` |
| administrator-PostgreSql-chromium-applicationauthority | `8ff12387338a2e78bc5fce7d82cfb7e3400e3d2dc35ee2499c0be52c3c221fd1` | `024b0af6c15a0b5e1722860a0d8c38336d4d1c65a2953043d16d36d9b547ffbb` |
| administrator-PostgreSql-chromium-applicationlifecycle | `212b095e85edee56835914ec53713726efb608c99145d46c78a1cb1125a4c455` | `cec0d8169cc9080f50eb1d0a682c0a920b0fbed7e35c17737c02296ce33f2749` |
| administrator-PostgreSql-chromium-managedconsent | `c6d54138a7fa9430e0afd09be09b1d589acd5b4c05fce70c8f58e501e54b9e44` | `3a59305799ea8a2910db5c2bcbe33bd09e8df6bd16563c9e93f6da33f8a4d596` |
| SqlServer-chromium | `a2b395570d17ae9e02ca07e49fd6392debd4858f3f7f0c1872a937aa9f134644` | `f8eb5b170d416e17f5180bba7766a9369e786c3076f416146f0161ee520fa805` |
| administrator-SqlServer-chromium-accounts | `0458a21761045981df5ece931cb0c67ee2942b22767a319e168089cfa6bf8263` | `39fafd96676e398af3253e07ae4e9333c1cb48c0700ce743b4c25464c13cedbf` |
| administrator-SqlServer-chromium-security | `3440192eab9d3b2d0a3611182c9f1d874abae1085043b36849e172f3a66664a9` | `53754f651472df4762cc7894e422f9b7fe972121fad32fdb00e0430e9ec86d27` |
| administrator-SqlServer-chromium-browsersessions | `0edc97177b7f3392ee22c7bfb874ef01b4abcfb2c03992795731fec4444eed9b` | `1e3da42e9e0f71d6acd8b4d6d3dc89184dcf225c99c2a05432a84d25b649ee13` |
| administrator-SqlServer-chromium-browserlogin | `bba18f312ff5e326d08017df3313af582ac6148bf7762b358f7c5202159110c1` | `f698873c5d1febaa0e52c5d12f60652bc0795e616b9e88f7da564722fe6276e4` |
| administrator-SqlServer-chromium-browserprotection | `fc50ea5ec4ccef2f0fff887fd169c0f68eb2b481000cc1f91fddd7412cad23ff` | `0187797e6871689d60d3669d1e18d7a8a0e4fec3492e5c75c2da98db71277315` |
| administrator-SqlServer-chromium-applicationeditor | `8b4bc897c03b273ec462ccab0bcc364df8745a7bc280ed98d98c9f8b92f6ecbb` | `41f91e0cd556845796c4c7e3dfa1c0d2041a26213e4d7b072690c724b4fc3747` |
| administrator-SqlServer-chromium-applicationauthority | `4f761dc7c2f6a08ff34cd942bacb04d32cb42169b871bd0d7cdee41490f5794e` | `6205783653cca099f567e819a615a6acbdfb8fed290f87a26e8d6892556e779b` |
| administrator-SqlServer-chromium-applicationlifecycle | `d5e75ed7e0f4236f06196c8337a705f6e31755860f556678950814d5e69efab2` | `b8cc72c3a5792d34c77e945f8d1b3ee0787a67f94a0bc218a709cf5184eddb2f` |
| administrator-SqlServer-chromium-managedconsent | `d93d0dd766e251ebd31e591eb60d76134b783e9c3c72de8825d8f107e546a4fe` | `be42fbae2cf82060b86636a4e101a5245edb8dc7c3e05a2ea381764836715e62` |
| PostgreSql-webkit | `4df49cb23febf5815c1145822e8a263db49d3f1b9307478fa973ee192af6c57d` | `7efec5bc7f9a1b1c0dc2a4258cefc1bc6588515f9571bffdddce43d7cbbabfc1` |
| administrator-PostgreSql-webkit-accounts | `9ccabf3a523302858859f8b3a152ca330abeee76e7d1a6eaec34fd16b8f3ce5d` | `af66f675f1126485a97c4b6f0c0c294305bf06d2b609de8c97f829a54b6a3ed9` |
| administrator-PostgreSql-webkit-security | `93aec40c8e491e5794ad24caf7f98830ebac9ba76bbb62bc62026444e18b6f4d` | `131a868e9a1913f023382405e80ed834167cd9cb0bacdd60c43e2ab9467bbab6` |
| administrator-PostgreSql-webkit-browsersessions | `f826adb77c882ca538e1d9923c83a51a55a89c8231500f270da17d03cc018e9e` | `9ba39b521c56158612fb00386fd6c25017d74d394f8629ff36818dd6c8ef47a9` |
| administrator-PostgreSql-webkit-browserlogin | `8574c2d7ded980c56251738018adbf69545bc304348692968b0650fa41aa44d6` | `f24ad7a39730aca8ca4389915aa02c5d1cd707aba025f6eb3cdee0c6ae6fd45f` |
| administrator-PostgreSql-webkit-browserprotection | `277e29f96b845c8e18c4830b1686368a6e6303a2345f2f5d0b4e40ba9ffc49ce` | `0d5cbb94202d74f156af835a0f613ea2ddef8960754c7918d6aaa565d8b848cb` |
| administrator-PostgreSql-webkit-applicationeditor | `2673d4971ecaa12e7eadf7d80f0d8b6a4c95bfe0b33f0d4a3a75fe211d7d5b20` | `98b87e043ab541bc7fcd1ffef25f7fb1a79fdb628c1b88ba1b27b572a321bfa1` |
| administrator-PostgreSql-webkit-applicationauthority | `ba23f01e802f77c7d55f38e51803abad6a4ce64f1dda062039f3ccef6e26e326` | `3f6a574615e656e65c4073fa32d4690b68f46ec1c117a7470f90e387817aca67` |
| administrator-PostgreSql-webkit-applicationlifecycle | `8da585d02947c9b265d95a23ebab4b50cdf03896cc7e72dbe521b5f21fb5986c` | `8e8e043d1ca4b53c9ee3add148363f8538f7179451aaf8ae4b309ca8334ee9f1` |
| administrator-PostgreSql-webkit-managedconsent | `b4d46b4a1c97d91ab0a7fef82d09a9a8a267e14dbfcbb454a1b748b95ad2c4e5` | `e1add6450969b75efa46a6baf43c2c01d832dd2bbcabfe3ef5916858ae35c9a4` |
| SqlServer-webkit | `35a8a4ac62cd16d6d7b8e929cbcb1c822be91ab07e11c2506d1ae3ae8fa02aac` | `cbfb18208c2dac960fc3b9150fd57899ae44d2591516cf45108ce01d66af6a14` |
| administrator-SqlServer-webkit-accounts | `64ef00988573ca8b23248cc44d4e790f22dfc3bcf048067f5bc22ca0ab95408b` | `379d613206977d864c8b2519a38c9f47423718b740cc060b5a6a20e142d5735c` |
| administrator-SqlServer-webkit-security | `4e6cac43da79326d2e9916783fe57ca7f0cf32c8c29f2c35cee237d35a88709c` | `3132f71d0e852a1ce9e614e8c8cf305923d59e76cc6474b5bc24a808631494fd` |
| administrator-SqlServer-webkit-browsersessions | `83678e8d25e847c3a6383d50aa1ae8fd2ed7395218cdf2e7c7c96e193fb95801` | `59fe62214f4f8e8ea1f7a91e244ed4164c309f24e41b4d06cde124c6ca984cd0` |
| administrator-SqlServer-webkit-browserlogin | `f8b1bd07d1f180e201b6547d9949752ed1395cfa47da128fafef60724dd53295` | `35c4d9385c43d4bb925870a54b24aadd4ce0d707dabb3c5d22a850421949de98` |
| administrator-SqlServer-webkit-browserprotection | `58178bfe43b2782cbe39b7765bbdb33fd315ba02c6952ac74553ecd877ab0c85` | `34de9290b2b0f7558e2332bb0c63fc33855f6bd29e2e08912ff53c9ea9b81eaa` |
| administrator-SqlServer-webkit-applicationeditor | `62a94d63bd6d4d5cf9ff1e3fea93e77cd2bb130d8d3c708a56e4beebd4ed23aa` | `1ddf0848a2aff58e3bc23fe5fe9c498a626815c39dbe24c3b8609a9693366fab` |
| administrator-SqlServer-webkit-applicationauthority | `dab3fdcf619447f57998d9a2409633fdbc8488f77c7f6ee05234d8ae93dc153c` | `a01d93d5bfa806b374564df9c185148e66a62896a84afabfcd6fa46874ec8afc` |
| administrator-SqlServer-webkit-applicationlifecycle | `c11733d221b2d22b5f748c5ad7eed5aac4dfc446f14240b93af22f6a5ee0f12f` | `350c7beb340aa735a38aad05c0de8e742804bf29985b64d8487d042f2937bc46` |
| administrator-SqlServer-webkit-managedconsent | `4c6db302a9838efa3ef6b84b443e34c8d753b156b34eac4cd616742e43516d49` | `a0636cb7abaacaad46ae3fcc5bcd5f49819cbad35505a153233376a8eb2b7b25` |
| contracts-core | `6546c5934a50b119a960a94e64a7abe26a37b870487bc222e50343b993f86f9b` | `a2f58d437a348cb3e6d09640bb3a1769db47bcc5081b9cd12654f02f43003dd8` |
| contracts-chromium | `9d397c0e16aaaec5ae1ebec837bf7b8695296bfc47249b23a637509d6a2671a1` | `e56c65ba5d6b9e9c222e0654f0973efad653b8a47fceb647ce7ed1d4dda20e5d` |
| contracts-webkit | `161c4ef06beec448bd6575f59e83c823626cc330f3069cf3e517b0160850adc7` | `c46c1382dac1bb6c2a426d06613ddb0056e9d2a2256c8a85f64d5e6e2efa51f9` |

## Artifact archive bindings

| Artifact | GitHub ID | Bytes | Archive SHA-256 |
| --- | ---: | ---: | --- |
| administrator-mutations-PostgreSql | 13585422504 | 1946 | `e7db75c9c0286ea31c3aaad76baf3b3789b87e0cb21ef37ea09125b9e713d5a4` |
| administrator-mutations-SqlServer | 13585407736 | 1947 | `4d10ca0c02524a07b5a4255c8127fc6cb96f641c8bee785d2b818f2d7bc0e764` |
| administrator-protocol-mutations-PostgreSql | 13584962773 | 1103 | `7c640217d3becb877e27d30ec6465352387b9907e32dba445fbc5cfb6300e486` |
| administrator-protocol-mutations-SqlServer | 13584094869 | 1101 | `06d21fc2d7e4e181b35bc4d995d0a6bf346f3058b4965c090797ed4bdbb04901` |
| administrator-ui | 13584459916 | 19633 | `0eced7c29bd1c22399faddc0ee715a78a27fb47999e64bea03f073b7b80496dc` |
| application-mutations-PostgreSql | 13585269758 | 4845 | `a92e646bf2ef11857949b7cfcf66aeb87052ba2e0d40fa06f6a3190592bdf262` |
| application-mutations-SqlServer | 13585279806 | 4828 | `f5d4ff9f20e694e49f2a5e2cfc6bc109419c94642ab5756e5d5e861027c4e75a` |
| browser-session-mutations-PostgreSql | 13584798908 | 1872 | `64261d6b69e32529da5fce24afcba3fff3159bd74225c15bd7ecdc9b57c9bf63` |
| browser-session-mutations-SqlServer | 13585565578 | 1871 | `cacf585c54234b6cb957d598769af0b75adb70f9e9554e58c3bfdcc8335b61f6` |
| critical-mutations-PostgreSql | 13585620097 | 1681 | `0e5dc0cb1c6f460378e7dd5a52205595351ed1265f324aba7ee07f77ad481cce` |
| critical-mutations-SqlServer | 13585153524 | 1677 | `2c58de7cddc4c3654695841127f9995a7c65eb100562554adb2783cadec559a9` |
| focused-webkit-contrast | 13584289311 | 4857 | `12b8f1442ac6e2abf018ee95bdbf3e042a43c41919846184e654f451036953bc` |
| foundation-PostgreSql | 13585455638 | 255558 | `f6336d3dc56aed46f80b2aef6e3e1c18796689bdb49b12ef2993e5ca0c78df20` |
| foundation-PostgreSql-apphost-bffsessions | 13585059061 | 263106 | `cdcffb546875f19c4ab503a1cb428fe78823ccd3a77c38c9038c7eafd1abe8df` |
| foundation-PostgreSql-apphost-foundation | 13585411936 | 263112 | `4b5eba94b9abd421b25c14f75f082883632c6f6c9629e4a3a40b13608f7a20f0` |
| foundation-PostgreSql-apphost-localdemo | 13585426695 | 20909 | `b29375459a61a37eed82a8835d5e5e6254b5e3e2c977d4d72b9a21597af99c74` |
| foundation-PostgreSql-chromium | 13585795416 | 636735 | `6fd6d598ac90500350d6a4113ea90ee28af8ee0530d45592a58a119692879f45` |
| foundation-PostgreSql-lifecycle-distribution | 13585645521 | 270223 | `578a0eecced7b00b97467e90dbeb9b7f62776caf298bf4059fa0e3d8e10783f5` |
| foundation-PostgreSql-lifecycle-keyrotation-activation | 13585517694 | 267137 | `3008ae7b4b52c2a02f68fb565c287e94d6eaf12bd3fbc7392d1d6b7a1659b07a` |
| foundation-PostgreSql-lifecycle-keyrotation-retirement | 13585234806 | 267183 | `09e6dd37fdb2e291ee46ef758254cd3bc93fc770bebb6e7414fcb7cf04568b76` |
| foundation-PostgreSql-lifecycle-restart | 13584878672 | 268636 | `a9b00d903c652ba024945079a2dc98590c3c1a05abafd5e4e43bcbb763bfd21f` |
| foundation-PostgreSql-webkit | 13585842265 | 682468 | `079a4f6b8d881c7ab8684bd5c7eb098f29424a6d469289eb5da7b4fe7102a971` |
| foundation-SqlServer | 13584479711 | 255499 | `ee95259caa8be64dde6aaaa6b551659416b40914059a0367fd7908e240b3ccc0` |
| foundation-SqlServer-apphost-bffsessions | 13585565472 | 263082 | `d3e9961ff2b699bf165bbba515f4ec387b8dece48584f8f691674be86e1573ea` |
| foundation-SqlServer-apphost-foundation | 13584369774 | 263200 | `8c887bd6e5d20b7561d269f7fa605596f9f76dcc11b0ec71b1ca21048d1215db` |
| foundation-SqlServer-apphost-localdemo | 13584754405 | 20879 | `cfbb9907e76cbbdcf7e887758d413b1003e101f41ffdae9bf3501ae6b3c4c194` |
| foundation-SqlServer-chromium | 13585915086 | 635777 | `48e6cb7c4ead7e0c799fa799d798bca4d150499973fbeadbe97addb645cfc7d4` |
| foundation-SqlServer-lifecycle-distribution | 13584934461 | 270619 | `fc2837cb848edbfb00f73fcc7681394468fa1071e16d3caec1479f15ac653b09` |
| foundation-SqlServer-lifecycle-keyrotation-activation | 13584703316 | 267135 | `5591e8521847447b17fa8b9f8be44946c87144e4a18d24546e7d22de61dd0230` |
| foundation-SqlServer-lifecycle-keyrotation-retirement | 13585731757 | 267073 | `504556673c85d759083f20e4e272002323a3481368a081a74917106dd2b321f9` |
| foundation-SqlServer-lifecycle-restart | 13585443110 | 268615 | `4aed5c41821ccb74b0652f18b17b78246f16c2ec6c033daf27203f1671c42ccc` |
| foundation-SqlServer-webkit | 13586020470 | 682395 | `35e6bc94e931915810735bb5a3e00c59aa611aca71bef5af7c2ac44aab11ea97` |
| foundation-administrator-PostgreSql-accounts | 13585761041 | 282482 | `8480fda5819ad70639b469184dea675d97e6864be328efeb9f0bcbe995098cf2` |
| foundation-administrator-PostgreSql-applicationgrants | 13585462132 | 316047 | `82500a206c12e7d19e46291981166a53b73592065dc6adb287d9c25e35d740b1` |
| foundation-administrator-PostgreSql-applications | 13584823451 | 311945 | `c66f84ff4be3214a945edf0be1065598b90a30c53b4827d4238dc09db2b329d2` |
| foundation-administrator-PostgreSql-bootstrap | 13585830377 | 276432 | `3e9e01a56d83ac8015355ac8bed6bffb69e0104dbfd6f6103109efe5b4210ec1` |
| foundation-administrator-PostgreSql-browsersessions-foundations | 13585064573 | 327236 | `2dc799d8b80e44b7271171aba8958100a963cdb9eb8dd553145d1af69bd7635b` |
| foundation-administrator-PostgreSql-browsersessions-lifecycle | 13585980084 | 305501 | `b66517cbb37762446b5130b0b95f5b5fb7c7919621ca2b806f7397ad62519452` |
| foundation-administrator-PostgreSql-browsersessions-product | 13585229438 | 311967 | `0003602e96e9c420d66fa96bcb594014f352cd1a8e472601f018662c6e739e3c` |
| foundation-administrator-PostgreSql-ceremonies | 13585133151 | 275820 | `349e36ba622406069a20e20ebe3c0c62f85cfffe652ee12f62506345a3814164` |
| foundation-administrator-PostgreSql-chromium-accounts | 13584794212 | 269251 | `33d7929830bf6b6bba8720f3c97f661a5c7d3077863e2b5653934fde73cc8b13` |
| foundation-administrator-PostgreSql-chromium-applicationauthority | 13585631559 | 289365 | `33836e56bc42f7bcbee0f7767489a63e572509a2c4ef143a7bbdd8b38f3684e9` |
| foundation-administrator-PostgreSql-chromium-applicationeditor | 13585642883 | 289525 | `69a79075e63b798c1c42dbcfa6b5d8dc623a2f6e6a51732cbe6b9936de976ac0` |
| foundation-administrator-PostgreSql-chromium-applicationlifecycle | 13585059961 | 289620 | `ec23624eca1e31dcaa7ce3766aa03b1a3320fc7d63cc614d6af276705f6c998d` |
| foundation-administrator-PostgreSql-chromium-browserlogin | 13585586081 | 289454 | `3f9d3a201980ff18eda722d9df9cb61094e61da9e31a46c05e7b9001036f39cd` |
| foundation-administrator-PostgreSql-chromium-browserprotection | 13584909231 | 758145 | `671e3976ec498d0f8298c44555aedc4d5bc7976052c71972f0106c8a9dc68ef6` |
| foundation-administrator-PostgreSql-chromium-browsersessions | 13584679251 | 1199141 | `50d712d405d1463ac2b3314faddd3e15571080a4ffe990aefe5558c25b18b0b1` |
| foundation-administrator-PostgreSql-chromium-managedconsent | 13585601855 | 292187 | `a388488a991106047bfa9fc4536b81c8f8b4e59f036a6dd5ae41570af3b47cdf` |
| foundation-administrator-PostgreSql-chromium-security | 13584649610 | 268313 | `7d2455f96431f400a447bd1eb540527415dae316d285108d347940cf7bd8b784` |
| foundation-administrator-PostgreSql-factors | 13584896751 | 274966 | `0d444c2b8a6ff7879bd2b39616ae7ceaf717cb704dab7b109d684318eec54af6` |
| foundation-administrator-PostgreSql-governance-commands | 13585187982 | 283379 | `dcef210c903ef5f6e4c72fe02f72fd6e754f8cc19711afa06a7c2d90f2465d76` |
| foundation-administrator-PostgreSql-governance-failures | 13585537385 | 291406 | `1a443c6291dc1cd178a2aad34e241a2db1174842b1f6fce1fc68202d84ee1292` |
| foundation-administrator-PostgreSql-governance-policy | 13585183300 | 282393 | `091493f1aff7b640a73d99a4e257c22d9fabebbdc9a52335f2efdb4c7a8982c8` |
| foundation-administrator-PostgreSql-managedapplications-admission | 13585502316 | 305975 | `13c0e57371eeacc46c5bf63a18a77a82a51ce6502f4a7d0cd54cdb1d20680c1e` |
| foundation-administrator-PostgreSql-managedapplications-issuance | 13585122653 | 307426 | `8b833090fffde67d9645cce1ce4dc36f30298ec7ef11f4d8f4395d6b14122aa0` |
| foundation-administrator-PostgreSql-protocoladmission | 13584804206 | 330095 | `29991bdc249feec9298b505828b283fd99fceb30685493c857a0cb7fd5e7ba5f` |
| foundation-administrator-PostgreSql-recovery | 13584978893 | 271211 | `b957b51b0156eb39e6b7e12ffda59e88bd24f9846b0d9be39daef3b794e9a51f` |
| foundation-administrator-PostgreSql-security | 13585277163 | 271358 | `9c8ef456a011933ebdaf6d71a096f92b5023f1dce072a7dde6f28c0ce941a7f1` |
| foundation-administrator-PostgreSql-sessionrefresh | 13584971975 | 264591 | `c4d400baadfc815f1bb709d7e1c06d70b3bcf7d45947c1176bd292ee52553949` |
| foundation-administrator-PostgreSql-webkit-accounts | 13585284703 | 269216 | `4f775f780f6bc157476656e1afe6f7307b9d84ebbf66651f6d91eac0bdaa733b` |
| foundation-administrator-PostgreSql-webkit-applicationauthority | 13585008586 | 289366 | `ec5212e88840dffb99efc28bed9fb716aa81e03356beeb797cbd6efdfe5c8d8e` |
| foundation-administrator-PostgreSql-webkit-applicationeditor | 13586125700 | 289566 | `73b389b8b94f0d56dea6a333fd1ce81b78e8a872e8aa773fe68075fd3499e42a` |
| foundation-administrator-PostgreSql-webkit-applicationlifecycle | 13586225003 | 289793 | `90e14881d3e446d725af0245b58159ec56fb2751f6023582b089371918f71e77` |
| foundation-administrator-PostgreSql-webkit-browserlogin | 13585645957 | 289434 | `ad65ce82ab61c62c5bc41217c142bd0e805936a2fbe4606e578396fc415f255c` |
| foundation-administrator-PostgreSql-webkit-browserprotection | 13584789573 | 803848 | `a3840e2de4936b8cc8359048129e3bb827174d8a44883c92de8ca70b49a9c86c` |
| foundation-administrator-PostgreSql-webkit-browsersessions | 13585409775 | 1307599 | `dab92a8d89c1b0368996260b4c33c058e4c2e12b2b3cb045f59f9f2f7d85523d` |
| foundation-administrator-PostgreSql-webkit-managedconsent | 13585642684 | 292152 | `36fa25036c28dcda778fb3d085bc25bc1f8670fa18b9475fbfd93ccabca8c94e` |
| foundation-administrator-PostgreSql-webkit-security | 13585124484 | 268530 | `f5880532626b68f466359940fc59d63bf2cc77c1ba9fa2df7e99335640776f14` |
| foundation-administrator-SqlServer-accounts | 13585093559 | 282266 | `e94f2bee1feb08f20e80e29081b158cf6aadd9614a1d438cb58bf3745b1059ae` |
| foundation-administrator-SqlServer-applicationgrants | 13585721213 | 315904 | `094c95331c49b09101ef726513bd07b8893905801d16c6e8681192f30074f30d` |
| foundation-administrator-SqlServer-applications | 13585278631 | 311950 | `f93eb9b8bf25173dc065d4467b52558e29a323995f77da0ce7141956ad57c805` |
| foundation-administrator-SqlServer-bootstrap | 13584869017 | 276274 | `c1ee5558ed529d7829325741c7e1024a1685fcbe829d703a097536e186fd0203` |
| foundation-administrator-SqlServer-browsersessions-foundations | 13584334881 | 326990 | `61a6b2781a6038ffb9ee95c4783e335996aa15761bd5d70d66581851c6023da5` |
| foundation-administrator-SqlServer-browsersessions-lifecycle | 13584808487 | 305354 | `1ce4f99706cb860b7735555e43fb9fa46a98bc7d3470721341f27fe086cc5892` |
| foundation-administrator-SqlServer-browsersessions-product | 13585328606 | 311925 | `f34a664600b2fd5aa5939c40be56d4f28d6ef9de297ca80b2bfb0a872c13ea65` |
| foundation-administrator-SqlServer-ceremonies | 13585351597 | 275808 | `ae98784e3e22a2e54239669d0e00e5d970e880fdd0ef1fcd772495fcb1e974c0` |
| foundation-administrator-SqlServer-chromium-accounts | 13586320164 | 269170 | `a0b122ca64de022f7b89e2a01ab82f917cfe01e4832c3b89028fdae66bf3664e` |
| foundation-administrator-SqlServer-chromium-applicationauthority | 13586025115 | 289165 | `8cb6a770e8665db8ae6719c10efa5d8c6e394d1424f9660ed21f0de7e762447e` |
| foundation-administrator-SqlServer-chromium-applicationeditor | 13584939112 | 289497 | `1c6305f25edca88849699566a554d18e79570771a1d22d8fc18139641757eccb` |
| foundation-administrator-SqlServer-chromium-applicationlifecycle | 13585900790 | 289654 | `455fc1442eafdc32ac2f263642834e90dfbd6131065196c60a21e86559aca2a4` |
| foundation-administrator-SqlServer-chromium-browserlogin | 13585745647 | 289300 | `f89a3c4004a14677bb623cc3cb9d0eb66bbdd5bd6ec6a48d1aeb2158549f6f84` |
| foundation-administrator-SqlServer-chromium-browserprotection | 13586015513 | 757865 | `52a55495509d946e8b01dcca254d2ce327c3a7921521a00855c04959efc02491` |
| foundation-administrator-SqlServer-chromium-browsersessions | 13585094869 | 1198882 | `b57f10cf211de6271b6ae9fb1d46b12bee6e5a443570ec366517e86de27dfbac` |
| foundation-administrator-SqlServer-chromium-managedconsent | 13585194581 | 291942 | `92404d190b921bc826046de5527faa60250910bdc876b4f1a20ebd1ce681f7ca` |
| foundation-administrator-SqlServer-chromium-security | 13585805864 | 268283 | `767d1bfadb4f11e281abf6df8fefd3fb46e7eaec2d31dec2284a18cce962dc87` |
| foundation-administrator-SqlServer-factors | 13584972540 | 275022 | `81b91bcc2c1b081666ea5ca025268d36ef44088cb049e23ff123e9e9237b2837` |
| foundation-administrator-SqlServer-governance-commands | 13585497067 | 283310 | `3b7449a4fb4d8e63cc63e29c3b5934ca86c604306b471517e8831c30eebd7ff9` |
| foundation-administrator-SqlServer-governance-failures | 13585641039 | 310863 | `4575c7ded728b1ec278b5138d18d70b68120c82413436a6dcaaf0aff5eb9cc6a` |
| foundation-administrator-SqlServer-governance-policy | 13585037755 | 282239 | `d990245b6470dcf1a9d213cc8577246e6bfe3d216b359f6e1076e95b0e5fbc53` |
| foundation-administrator-SqlServer-managedapplications-admission | 13584973284 | 305725 | `34d8c2bac9578c2aa9c55441849aff72197a1ec5afdc8e24e2f21242e3ca7f56` |
| foundation-administrator-SqlServer-managedapplications-issuance | 13585770462 | 307124 | `c0d0076d8500115fa20ef9da7b72f0284014a69c3731f487e78aa09b95bf6f32` |
| foundation-administrator-SqlServer-protocoladmission | 13585394328 | 330146 | `ecddf4b24949d2f406ff9bcf544afd71bc27feeaf973cdfc66e7584175d18217` |
| foundation-administrator-SqlServer-recovery | 13585485887 | 271073 | `116feedc568682b9905f666c4ddff22632a0ffe56c6f387cdf700e804093a226` |
| foundation-administrator-SqlServer-security | 13584359374 | 271108 | `3625c96d08dd060cfc517724e32d7c38b133d20e9ee1a3517ba73d7fa683083b` |
| foundation-administrator-SqlServer-sessionrefresh | 13585565402 | 264653 | `12d10ed5728007ce6803c7de767ed7dbcec2af5307cc20bfe42c4a8fd460e299` |
| foundation-administrator-SqlServer-webkit-accounts | 13585129806 | 269000 | `222233071bd215bcb8bb3b7f96c645cbc783bb03d6c09f577c5cf0f65b4dc33f` |
| foundation-administrator-SqlServer-webkit-applicationauthority | 13585980407 | 289054 | `e12bb3f45636664b6976c54c2367e33e94bbf79064102a4bc717494c458deb65` |
| foundation-administrator-SqlServer-webkit-applicationeditor | 13585244938 | 289264 | `db10ee340339296cbf87769cf4a5851ce08de41c03d869b45608e4ef9a6f6841` |
| foundation-administrator-SqlServer-webkit-applicationlifecycle | 13584743882 | 289592 | `f194a1a5286a66c836530e89dbc1d40e1b6ff187b5a8e3771e3cd048eb565a1a` |
| foundation-administrator-SqlServer-webkit-browserlogin | 13585453701 | 289269 | `e91b97841a90f5172b36cc91cc5a3828160f713f65edbf5485a6be518b8552d5` |
| foundation-administrator-SqlServer-webkit-browserprotection | 13586081419 | 803573 | `ad646e8612be2f41ec956fd642156358a0feb5e4c4f8d88b94770da6dc57e1bc` |
| foundation-administrator-SqlServer-webkit-browsersessions | 13584694637 | 1307351 | `bc50ce8f6c11b0a5eeaa36c4e9ca3bf5eee44b620b74b248616e253b4ca88acf` |
| foundation-administrator-SqlServer-webkit-managedconsent | 13585094713 | 291825 | `c68040c99e78a24f10a277137e71e3851e5165f06463c6d71114ff0591c009d6` |
| foundation-administrator-SqlServer-webkit-security | 13585696269 | 268323 | `eb3505e38293e6ce5f8a3de8d428540bdd6e86c1071aece8d6a8e3e489883bc5` |
| foundation-contracts-chromium | 13584873132 | 2576789 | `a1e28cd16b7958e899ddd58932727831dec96c5e1a73f09c08d02bac8c72835e` |
| foundation-contracts-core | 13585775432 | 189633 | `253c8f9f7c7c4e2af45dc4f904caf6688256c6079a303b7bbd70e48233ba3d21` |
| foundation-contracts-webkit | 13585034430 | 2736427 | `d09f7dfcf30411c75cb52c27cd7ced3ee33f2ca424c2ae2dc9914d2d0620d35e` |
| foundation-coverage | 13586211414 | 237968 | `4e2dd1af77a7d0a24b333304e9d1ef87ce75d79d5a024036ac52ef2548a3a2e2` |
| foundation-issuer-PostgreSql-boundaries | 13585097714 | 300088 | `d16eb5a8fba374bf4bcccb562623d1598ae5f4874cfd17fec745f7a7d5d81aad` |
| foundation-issuer-PostgreSql-flows | 13584843491 | 298522 | `b2c066d1a267b4f93a1dbc9c144533a98474096395307467c3a7938a1b152a14` |
| foundation-issuer-SqlServer-boundaries | 13584777620 | 300035 | `412ad1a0f64ad6496ae73ceccbf990f69aabcf81894b28ea6159f98653620d06` |
| foundation-issuer-SqlServer-flows | 13585232665 | 298508 | `231389b25b176f56eea1b8f88b5e5c959980f0df05fd3565d9007a24e19fccc2` |
| foundation-supply-chain | 13584647336 | 28069 | `df077a02ed7f0943897d26ad49a170cb7a2f8243b49618f6d0f35cc5c222fdce` |
| foundation-unit | 13585302997 | 712316 | `bc1e97cd066c733856a97a9bd6b7450ed82f1947db7ad9b0ec918760abde0b2c` |
| governance-mutations-PostgreSql | 13584722923 | 1674 | `c7adb61f1b925b00f35f9b694bd3c4c3bf1a5658444f15e16e1e7fe556a6e598` |
| governance-mutations-SqlServer | 13584978664 | 1668 | `ce0363a666d97f00f0dd518ac8c219b94b99ebbc9171077d2de1fa6b454ec0c0` |
| managed-application-mutations-PostgreSql | 13584634767 | 1482 | `087115137a3f11100f0c935d900bf8bc61e7f8f033b8d9f355d32e4b03251e9f` |
| managed-application-mutations-SqlServer | 13585540666 | 1476 | `32be7acae02a96cb470ed13ea47f61b307699496772eaafa792d2eb50b9cf850` |
| pilot-replay-mutations-PostgreSql | 13585555727 | 1255 | `7441d3d992e7cd33da13770248609a4f0ded02180cb64887415b67a13fce02d9` |
| pilot-replay-mutations-SqlServer | 13585620201 | 1248 | `73324976456ae613924cd209743c8965803bd3a21a0e6f3149346a54d3f85da8` |
