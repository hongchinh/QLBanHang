// Imported by sw.ts; kept free of DOM/service-worker APIs so it is testable.
//
// The service worker's NetworkFirst cache keys on the URL only. X-Branch-Id and
// Authorization are not part of the key, so a cached branch-, user- or
// permission-scoped response would be served to another branch or user on a
// slow or offline network. Only reference data that is the same for every
// signed-in user is cached, so this is an allow-list: a new endpoint is never
// cached unless it is added here.
const CACHEABLE_API_PREFIXES = [
  '/api/lookups/', // active product groups and units (LookupsController, [Authorize] only)
  '/api/banks', // VietQR bank list (BanksController, [Authorize] only)
  '/api/settings/branding', // logo / app branding meta (SettingsController GET, [Authorize] only)
];

export function isCacheableApiPath(pathname: string): boolean {
  return CACHEABLE_API_PREFIXES.some((prefix) => pathname.startsWith(prefix));
}
