# Research and implementation decisions

Verified starting main4814c3e. Existing BFF `/bff/session` returns authenticated plus optional opaque subject/UTC expiry; it is not API authority and false does not distinguish first visit/logout/expiry. Existing `/bff/login` always starts fresh password/MFA; callback currently returns to that form. Shell composition therefore needs a fixed local-only landing destination after successful publication, not an arbitrary return URL. Logout remains the existing form/issuer confirmation journey.

Native Aspire local-demo already packages issuer/BFF/migrator in disposable Linux containers. Serving prebuilt embedded React assets keeps ordinary IDE/dotnet launch and Docker graph intact. Generated assets are verified under a separate frontend CI gate, not trusted because committed. Runtime browser dependencies receive separate notices/license/advisory review from development-only contract tooling.

Current source has no React application. Design approval specifies React/TypeScript, Tailwind, shadcn/Radix-style accessible primitives and shared slate/indigo tokens. Use RadixSlot only where native controls suffice; avoid Radix positioned portals requiring inline styles under strict CSP. Native `select` remains the accessible theme control. Existing theme bootstrap reads storage before paint but its DOMContentLoaded binding can miss React mount; share bootstrap bytes and let React own the select event handler.

LocalDemo existing tests are Chromium only; retained WebKit needs separate generic HTTPS browser fixture opt-in. Existing native inventories must retain every prior name/type/trait; a new Node package is not covered by contract CI unless explicitly added.
