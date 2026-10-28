/* Scalar bundles Zod, whose JIT probe invokes Function even when caught.
   Opt out before the bundle loads; preserve the CSP prohibition on unsafe-eval. */
globalThis.__zod_globalConfig ??= {};
globalThis.__zod_globalConfig.jitless = true;
document.documentElement.lang = 'en';
