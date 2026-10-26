#!/bin/sh
# Public input only. Every operation retains its existing bounded redirected-stdin contract.
set -eu
printf '%s' "$LOCAL_DEMO_REGISTRATION" | dotnet /app/IdentityPlatform.Migrator.dll --Operation initialize-administrator-protocol
printf '%s' "$LOCAL_DEMO_UPGRADE" | dotnet /app/IdentityPlatform.Migrator.dll --Operation upgrade-administrator-browser-registration
