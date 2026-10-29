# Workspace boundary

GET/HEAD `/` and `/session` serve the same public static application with no-store, nosniff, strict CSP, no-referrer and frame denial. GET/HEAD exact `/admin-assets/*` allowlist serves only generated JavaScript/CSS/notices with exact content types; no file-system expansion, source maps or remote imports. Query/body/method substitutions reject; unknown routes continue404 and never swallow `/bff/*`, `/connect/*`, `/docs/*`, `/openapi/*`, `/api/*` or health.

Existing GET `/bff/session` and generated bff-v1.browser.d.ts are unchanged. Cookies remain HttpOnly; credentials same-origin/no-store, fetch redirects denied, no automatic auth challenge or mutation. Existing `/bff/login` and `/bff/logout` own antiforgery forms. Fixed workspace-return presentation is selected server-side only by explicit shell composition. No arbitrary returnTo parameter is accepted.
