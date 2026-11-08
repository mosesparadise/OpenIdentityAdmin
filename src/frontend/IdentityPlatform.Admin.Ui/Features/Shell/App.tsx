import { CatalogueWorkspace } from "../Catalogue/CatalogueWorkspace";
import { ApplicationsWorkspace } from "../Applications/Editor/ApplicationsWorkspace";
import { useState } from "react";
import { Icon } from "../../Components/Icon";
import { ThemeControl } from "./ThemeControl";
import { SessionPanel } from "../Session/SessionPanel";
import { useSession } from "../Session/useSession";

/** Presents real session and application journeys through the protected BFF. */
export function App() {
  const applications = window.location.pathname === "/applications";
  const catalogue = window.location.pathname === "/scopes" ? "scope" : window.location.pathname === "/audiences" ? "audience" : undefined;
  const detail = window.location.pathname === "/session";
  const [menu, setMenu] = useState(false);
  const { state, check } = useSession();
  const page = catalogue ? catalogue === "scope" ? "Scopes" : "Audiences" : applications ? "Applications" : detail ? "Your session" : "Overview";
  document.title = `${page} · OpenIdentityAdmin`;
  return <div className="workspace">
    <a className="skip-link" href="#main">Skip to content</a>
    <aside className="sidebar">
      <a className="brand" href="/" aria-label="OpenIdentityAdmin home"><span className="brand-mark"><Icon name="shield" /></span><span>OpenIdentity<span className="brand-admin">Admin</span></span></a>
      <button className="menu-toggle" aria-expanded={menu} aria-controls="workspace-navigation" onClick={() => setMenu(!menu)}><Icon name="menu" /><span>Navigation</span></button>
      <nav id="workspace-navigation" aria-label="Workspace" className={menu ? "navigation navigation-open" : "navigation"}>
        <p className="nav-label">Workspace</p>
        <a href="/" aria-current={!detail && !applications && !catalogue ? "page" : undefined}><Icon name="grid" />Overview</a>
        <a href="/applications" aria-current={applications ? "page" : undefined}><Icon name="grid" />Applications</a>
        <a href="/scopes" aria-current={catalogue === "scope" ? "page" : undefined}><Icon name="grid" />Scopes</a>
        <a href="/audiences" aria-current={catalogue === "audience" ? "page" : undefined}><Icon name="grid" />Audiences</a>
        <a href="/session" aria-current={detail ? "page" : undefined}><Icon name="shield" />Your session</a>
      </nav>
      <div className="sidebar-footer"><span className="environment-dot" /><span>Local development</span></div>
    </aside>
    <div className="workspace-body">
      <header className="topbar"><div className="breadcrumb"><span>Workspace</span><span aria-hidden="true">/</span><strong>{page}</strong></div><ThemeControl /></header>
      <main id="main" tabIndex={-1}>
        <div className="page-heading"><p className="eyebrow">Your identity workspace</p><h1>{catalogue ? page : applications ? "Applications" : detail ? "Your session" : "Welcome to OpenIdentityAdmin"}</h1>
          <p>{catalogue ? catalogue === "scope" ? "Define access scopes and their consent information." : "Manage the resources that API scopes can address." : applications ? "Register applications and review their sign-in configuration." : detail ? "Review this browser’s session and choose when to sign out." : "Sign in to view your account and session details."}</p></div>
        {catalogue ? <CatalogueWorkspace kind={catalogue} session={state} /> : applications ? <ApplicationsWorkspace session={state} /> : <SessionPanel state={state} check={check} detail={detail} />}
        {!detail && !applications && !catalogue && <div className="overview-grid"><section className="info-card"><span className="card-icon"><Icon name="shield" /></span><h2>A protected sign-in</h2><p>Use your password and authenticator whenever you start a new sign-in.</p><a className="text-link" href="/session">Review your session <Icon name="arrow" /></a></section>
          <section className="info-card"><span className="card-icon"><Icon name="clock" /></span><h2>Your session, at a glance</h2><p>See the latest checked status and when your local session is due to end.</p><p className="card-footnote">Checking status does not extend your session.</p></section></div>}
        {detail && <section className="quiet-note"><Icon name="clock" /><div><h2>Session checks are read-only</h2><p>Checking your session does not extend it. When it ends, sign in again. Signing out offers separate local and issuer options.</p></div></section>}
        <footer className="page-footer"><a href="/admin-assets/notices.txt">OpenIdentityAdmin · Notices</a><span>Local development · Data is discarded when the run ends</span></footer>
      </main>
    </div>
  </div>;
}
