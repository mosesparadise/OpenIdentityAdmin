(() => {
  "use strict";
  let preference = "system";
  try { preference = localStorage.getItem("identity-theme") || "system"; } catch { /* Storage is optional. */ }
  const allowed = new Set(["system", "light", "dark"]);
  if (!allowed.has(preference)) preference = "system";
  const apply = value => {
    if (value === "system") document.documentElement.removeAttribute("data-theme");
    else document.documentElement.dataset.theme = value;
  };
  apply(preference);
  document.addEventListener("DOMContentLoaded", () => {
    const control = document.getElementById("theme");
    if (!control) return;
    control.value = preference;
    document.getElementById("theme-control").hidden = false;
    control.addEventListener("change", () => {
      const value = allowed.has(control.value) ? control.value : "system";
      apply(value);
      try { localStorage.setItem("identity-theme", value); } catch { /* Keep rendering functional. */ }
    });
  });
})();
