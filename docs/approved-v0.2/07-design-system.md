# React Admin and STS Design System Draft

Deliver a polished, coherent identity product across the React administration UI and the server-rendered STS Razor Pages. Both experiences use the same visual language, semantic design tokens, typography, spacing, color roles and accessibility standards, while retaining their own appropriate components and interaction models.

Status: Version 0.2 for approval. The user selected modern enterprise with slate surfaces, indigo accents and complete light/dark modes. Screens and theme code will be built after approval.

## Visual direction

Use calm surfaces, a strong typographic hierarchy, generous spacing, restrained elevation and clear status colors. Avoid dense, unstyled CRUD screens and decorative motion that interferes with administration. Keep primary actions visually consistent; destructive actions must state the consequence and use a distinct treatment. The identity experience should feel like the same product without inheriting the full administration navigation.

The React admin uses an accessible shadcn/ui and Radix-based component approach with Tailwind CSS, plus deliberate product styling. Razor Pages use semantic HTML, shared compiled CSS tokens and accessible server-rendered components/partials. Do not import React dependencies solely to style STS or copy React DOM assumptions into Razor. Pin the supported stable frontend dependency set at implementation kickoff and preserve third-party notices.

## Shared design contract

Maintain a small design-token package containing semantic colors, font families and sizes, spacing, radius, elevation, focus rings and motion preferences. Generate or distribute CSS custom properties usable by both UI stacks. Product assets, icons and font loading have an explicit owner and license inventory. Keep responsive breakpoints and form-state meaning consistent.

Theme selection follows system preference initially, with an explicit accessible light/dark control. Persistence stores only theme preference. It must not require a cross-domain authentication cookie, weaken host-cookie isolation or copy authentication state between Admin BFF and STS. If the hosts use separate origins, independently remember the preference or pass only an allowlisted non-sensitive theme hint. Test first paint to avoid unreadable flashes, CSP violations and lost focus.

## Screen acceptance

| Area | Required experience |
| --- | --- |
| Administration shell | Responsive sidebar/header, environment badge, breadcrumbs, current identity, accessible navigation and permission-aware actions |
| Dashboard | Meaningful summary cards, actionable findings and accessible charts with textual alternatives; useful empty and error states |
| Application wizard | Clear steps, safe profile defaults, inline validation, final configuration review and no exposed persistent secret |
| List and detail screens | Search/filter/sort, readable tables, consistent paging, skeleton/loading, empty, unavailable, forbidden and stale-write states |
| Destructive or sensitive changes | Explicit consequence text, recent-authentication feedback, safe keyboard focus and reliable cancellation |
| Login and external provider selection | Clear product identity, password-manager support, passkey-first option where available, accessible errors and safe recovery links |
| Consent and device verification | Legible client identity and scopes, clear approve/deny controls, expiry/retry states and no disguised permission requests |
| MFA and recovery | TOTP, recovery codes and passkey enrollment/removal with status, help and clear confirmation; sensitive material protected |
| STS account management | Consistent forms for profile, authenticators and own sessions/grants; mobile usability and clear feedback |
| Protocol and service errors | Safe, comprehensible messages and correlation reference without exposing internal exceptions, tokens or secrets |
| Scalar and Swagger UI | Branded documentation shell, matching logo/colors where supported, clear version selector and consistent access policy |

## Accessibility and security

Target WCAG 2.2 AA for primary journeys. Check contrast in both themes, visible keyboard focus, skip links, landmarks, headings, labels, validation summaries, accessible names, status announcements, reduced motion, zoom/reflow and touch targets. Password/paste/autofill behavior must support assistive technology and password managers. Color alone cannot communicate status.

Self-host or explicitly approve fonts/assets. Keep strict CSP and output encoding; themes must not require unsafe raw HTML, arbitrary remote scripts or unreviewed inline script allowances. Provide a manual fallback where a browser does not support passkeys; avoid hiding essential error or recovery actions behind animation or hover-only interactions.

## Design verification

Playwright visual regression covers representative React and Razor screens in light/dark modes at desktop, tablet and mobile sizes, including validation, loading, empty, failure, long text and one-time secret states. Fix viewport, fonts, time and fixture content; mask only genuinely nondeterministic nonessential regions. Snapshot changes require inspection, not blanket regeneration.

Run automated accessibility checks and manual keyboard/screen-reader reviews. Cover Chromium, Firefox and WebKit for critical login/admin journeys; platform-specific passkey capabilities require targeted browser/OS evidence where automation cannot faithfully reproduce them. Visual checks complement functional assertions.

Definition of done includes design-token usage, responsive behavior, all interaction states, accessibility results, reviewed snapshots and product review of the four PRD journeys. A theme is not complete merely because the landing/login screen looks attractive.
