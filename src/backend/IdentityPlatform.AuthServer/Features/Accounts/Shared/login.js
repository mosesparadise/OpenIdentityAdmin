(() => {
  "use strict";
  const password = document.getElementById("password");
  const toggle = document.getElementById("show-password");
  if (password && toggle) {
    toggle.hidden = false;
    toggle.addEventListener("click", () => {
      const show = password.type === "password";
      password.type = show ? "text" : "password";
      toggle.textContent = show ? "Hide password" : "Show password";
    });
  }
  document.getElementById("error-summary")?.focus();
  const form = document.getElementById("login-form");
  const submit = document.getElementById("submit");
  form?.addEventListener("submit", () => { submit.textContent = "Signing in…"; });
  window.addEventListener("pageshow", () => { if (form && submit) submit.textContent = "Sign in →"; });
  const sensitiveAccountPage = document.querySelector("[data-account-secret]") !== null;
  window.addEventListener("pagehide", () => {
    document.querySelectorAll("[data-account-secret]").forEach(element => element.remove());
  });
  window.addEventListener("pageshow", event => {
    if (event.persisted && sensitiveAccountPage) location.replace("/account/administrator/security");
  });
  const continuation = document.getElementById("continue-link");
  if (continuation && continuation.origin === location.origin) location.replace(continuation.href);
})();
