# Browser dependency provisioning correction

22 October 2026. [PR #71](https://github.com/mosesparadise/OpenIdentityAdmin/pull/71) source `c861d845fbf91a67a5a5c861dac63acea3e9282d` passes the implementation's native provider tests and all eight security mutation jobs. Its [workflow 39862791710](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/39862791710) nevertheless cannot complete acceptance because required browser evidence is missing.

## Observed failures

Attempt 1 has 63 successful jobs, two provisioning cancellations, failed acceptance and skipped aggregate coverage. Contracts WebKit job `120118222262` and administrator Accounts/PostgreSQL/WebKit job `120118222567` reached their unchanged 15/20-minute job deadlines downloading Ubuntu libraries from `http://azure.archive.ubuntu.com/ubuntu`. Neither reached browser tests. The failed evidence uploads reflect absent test artifacts, not failed product assertions.

One targeted failed-jobs retry retained the successful original jobs. PostgreSQL/WebKit Accounts job `120129042256` then passed all 8 required cases, zero skips, at the same tested merge `f14e051fa709be1f140392f94dacbf0f617d1e7f`. Artifact `12463404304` has SHA-256 `1f3b3ac0bd1c69b89174f74c013ccfafc7a327da457d4e6461ae983865fbb576`. This later pass does not erase the original cancellation.

Contracts WebKit retry `120129041127` again exhausted its 15-minute job budget before tests. Its finite log shows the same Azure archive, with the 13.6 MB `libflite1` download beginning at 22:50:29 UTC and cancellation at 22:52:18 UTC. No further identical retry is proposed. These observations establish the immediate provisioning boundary; they do not explain the remote mirror's underlying throughput.

## Bounded correction

`scripts/prepare_browser_archive.py` selects Ubuntu's official `archive.ubuntu.com` origin instead of `azure.archive.ubuntu.com` on the disposable Ubuntu 24.04 runner. It changes only validated archive URI tokens in deb822 or legacy repository declarations. Scheme, `/ubuntu` path, suites, components, signed-key configuration, disabled flags, comments and other repositories remain unchanged. It rejects credentials, ports, lookalike hosts, query/fragment/path substitutions and regional references outside supported URI fields. All files are validated before writes; symbolic source files and missing source inventories fail closed.

After a replacement, one bounded `apt-get update` refreshes the signed indexes. Failure propagates without retry. The workflow invokes this shared setup before the first apt operation in browser, contrast and contracts jobs, including contracts' earlier `strace` installation. The pinned Playwright 1.63.0 installer/browser revisions, existing native package commands, TLS trust, assertions, coverage inputs, security mutations and job/test deadlines are unchanged. This selects another official Ubuntu mirror; it does not claim guaranteed network reliability or a closed Playwright defect.

Six synthetic controls pass without modifying this workspace's package repositories or contacting a package server. They cover preserved repository bytes, legacy and multiline deb822 forms, ambiguous origins, validation before writes, symbolic files, index-update failure propagation, unchanged repeat execution and placement before every first apt command. Seven existing native-image preparation controls also pass. An initial test expectation assumed a 15-minute contrast budget; inspection corrected it to the existing 20-minute budget without changing the workflow deadline. Corrected-source hosted execution remains pending.

This infrastructure correction is separate from A-009's existing browser deferrals. No additional browser case or coverage input is deferred. All six historical findings, human assessments and production/public-release gates remain open.
