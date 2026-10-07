# Phase 07 — Frontend foundations: permissions, working branch, navigation

**Status:** [x] complete
**Complexity:** M

## Objective

Teach the frontend about Round 1: a real typecheck gate; the permission codes and route rules; a working-branch store that sends `X-Branch-Id` on every API call, plus the shared API-error helpers; the branch context bootstrap (pages render only once the working branch is resolved) and the header branch switcher (only for `branches.access_all`); the "Kho" sidebar group and the new catalog and settings entries; the default branch on the admin user form; the new `inventory` permission module in the role matrix; and a service worker that never serves branch- or user-scoped data from its cache.

## Files

- `frontend/package.json` (modify — `typecheck` script)
- `frontend/src/lib/permissions.ts` (modify)
- `frontend/src/lib/route-permissions.ts`, `route-permissions.test.ts` (modify)
- `frontend/src/stores/branch-store.ts`, `branch-store.test.ts` (new)
- `frontend/src/lib/api-client.ts`, `api-client.test.ts` (modify — `X-Branch-Id`, `getApiError`, `formatApiErrorDetails`, clear the branch store on refresh failure)
- `frontend/src/features/admin-roles/types.ts`, `frontend/src/pages/admin/components/{role-matrix-table,role-create-dialog}.tsx`, `frontend/src/pages/admin/roles-matrix-page.test.tsx` (modify — Task 7.6)
- `frontend/src/sw.ts` (modify), `frontend/src/lib/sw-routes.ts`, `sw-routes.test.ts` (new — Task 7.7)
- `frontend/src/features/auth/hooks.ts` (modify — clear the branch store on logout)
- `frontend/src/features/branches/{types,api,keys,hooks}.ts` (new)
- `frontend/src/features/branches/use-branch-context.ts`, `use-branch-context.test.tsx` (new)
- `frontend/src/components/layout/header/header-branch-switcher.tsx`, `__tests__/header-branch-switcher.test.tsx` (new)
- `frontend/src/components/layout/header/app-header.tsx` (modify)
- `frontend/src/components/layout/nav-config.ts`, `nav-config.test.ts` (new)
- `frontend/src/components/layout/app-layout.tsx` (modify)
- `frontend/src/features/admin-users/types.ts` (modify)
- `frontend/src/pages/admin/components/user-form-dialog.tsx`, `user-form-dialog.test.tsx` (modify / new)
- `frontend/src/pages/admin/users-list-page.tsx` (modify — branch column)

## Reference files (read-only)

- `frontend/src/stores/ui-store.ts` (zustand), `frontend/src/stores/auth-store.ts`
- `frontend/src/features/product-groups/{api,hooks,keys}.ts` (feature module pattern)
- Phase 01 API: `GET /api/branches`, `GET /api/me/branches` (`MyBranchesDto { defaultBranchId, workingBranchId, canSwitch, branches[] }`), `PUT /api/branches/{id}/lock`

## Tasks

### Task 7.0 — Make `npm run typecheck` check something

`tsconfig.json` is a solution file (`"files": []` + references), so today's `tsc --noEmit` compiles nothing and always exits 0 (review M16). Change the script to `"typecheck": "tsc -p tsconfig.app.json"` (`tsconfig.app.json` already sets `noEmit: true`; it excludes test files, which Vitest transpiles without type-checking). On 2026-10-07 this real check passes on `main` in about 25 s.

1. **Check:** `cd frontend && npm run typecheck` → exit 0; introduce a deliberate type error in a scratch line, confirm it now fails, remove it.
2. **Commit:** `git commit -m "chore(frontend): typecheck the app project"`

From here on every frontend phase's Verification runs `npm run typecheck` **and** `npm run build`.

### Task 7.1 — Permission codes and route rules

New route → permission rules (keep the specific ones before the generic ones):

| Route pattern | Permission |
|---|---|
| `^/stock-in(/[^/]+)?$` | `stock_in.view` |
| `^/stock-out(/[^/]+)?$` | `stock_out.view` |
| `^/inventory/opening-stock$` | `inventory.opening_stock` |
| `^/inventory/(stock-on-hand\|stock-card)$` | `reports.inventory` |
| `^/suppliers(/[^/]+)?$` | `suppliers.view` |
| `^/(warehouses\|stock-reasons\|payment-methods)$` | `inventory.catalogs.manage` |
| `^/settings/branches$` | `branches.manage` |
| `^/settings/period-lock$` | `period_lock.manage` |
| `^/settings/(inventory\|numbering)$` | `inventory.settings` |
| `^/settings/recalc-cost$` | `inventory.recalc_cost` |

1. **Write the failing test:** append to `lib/route-permissions.test.ts` `describe('inventory routes')` with one `it` per row. Each asserts blocked without the permission and allowed with it. Also assert `/stock-in/new` follows the `stock_in.view` rule.
2. **Run the test to verify it fails:** `cd frontend && npx vitest run src/lib/route-permissions.test.ts`. Expected: FAIL (routes allowed without the permission, since no rule matches).
3. **Write the minimal implementation:** add every Phase 01 permission code to `PERMISSIONS` in `lib/permissions.ts`, in the same order as `Permissions.cs`. Add the rules to `RULES`.
4. **Run the tests to verify they pass:** same command, then `npm run typecheck`. Expected: PASS.
5. **Commit:** `git commit -m "feat(frontend): inventory permission codes and route rules"`

### Task 7.2 — Working-branch store and `X-Branch-Id` header

```ts
// stores/branch-store.ts
interface BranchState {
  workingBranchId: string | null;
  setWorkingBranch: (userId: string, branchId: string) => void; // also writes localStorage `working_branch_${userId}`
  restore: (userId: string, allowedIds: string[], defaultId: string) => string; // stored id if allowed, else default; sets state
  clear: () => void;
}
```

Wrap every `localStorage` access (read, write **and** remove) in try/catch — `use-quotation-draft.ts` guards reads and writes but not `removeItem`.

API-error helpers (moved here from Phase 09 because Phases 08 and 10 need them — review m4):

```ts
// lib/api-client.ts
export interface ApiErrorShape { code: string; message: string; details?: Record<string, string[]>; status?: number }
// Reads ApiCallError (2xx with success:false), an AxiosError carrying response.data.error (non-2xx ApiResponse),
// and ASP.NET ValidationProblemDetails (400 binding errors: { title, errors }). Detail keys are normalized to
// camelCase paths ('Lines[0].Quantity' → 'lines[0].quantity'). Returns undefined for anything else.
export function getApiError(error: unknown): ApiErrorShape | undefined;
// "message" plus the joined detail messages, for toasts.
export function formatApiErrorDetails(error: unknown): string;
```

1. **Write the failing tests:**
   - `stores/branch-store.test.ts`: `restore returns stored id when allowed`, `restore falls back to default when stored id not allowed`, `setWorkingBranch persists per user`, `clear resets state`.
   - `lib/api-client.test.ts`: `adds X-Branch-Id when a working branch is set` and `omits X-Branch-Id when none`. Capture the request by setting `api.defaults.adapter = async (config) => { captured = config; return { data: { success: true, data: null, timestamp: '' }, status: 200, statusText: 'OK', headers: {}, config }; }` and calling `apiGet('/x')`. Restore the adapter in `afterEach`.
   - `lib/api-client.test.ts`: `getApiError reads code and details from an axios 422 response`; `getApiError returns ApiCallError data`; `getApiError parses ValidationProblemDetails and camelCases keys`; `formatApiErrorDetails joins the detail messages`; `refresh failure clears the branch store`.
2. **Run the tests to verify they fail:** `npx vitest run src/stores/branch-store.test.ts src/lib/api-client.test.ts`. Expected: FAIL (module missing / header absent / helpers missing).
3. **Write the minimal implementation:** the store; in the `api-client.ts` request interceptor add `const branchId = useBranchStore.getState().workingBranchId; if (branchId) config.headers['X-Branch-Id'] = branchId;`; the two helpers; in `useLogout` → `onSettled` **and** in the api-client refresh-failure branch (next to `queryClient.clear()`), call `useBranchStore.getState().clear()` so the next user on the machine never sends the previous user's branch.
4. **Run the tests to verify they pass:** same command. Expected: PASS.
5. **Commit:** `git commit -m "feat(frontend): working branch store and X-Branch-Id header"`

### Task 7.3 — Branches feature, context bootstrap and header switcher

- `features/branches/types.ts`: `Branch { id, code, name, address?, lockedUntil? }`, `MyBranches { defaultBranchId, workingBranchId, canSwitch, branches: Branch[] }`, `CreateBranchRequest`, `UpdateBranchRequest`.
- `features/branches/api.ts`: `list`, `get`, `create`, `update`, `remove`, `setLock(id, lockedUntil: string | null)`, `me()`.
- `features/branches/keys.ts` and `hooks.ts`: `useBranches()`, `useMyBranches()`, `useCreateBranch`, `useUpdateBranch`, `useDeleteBranch`, `useSetPeriodLock` (each mutation invalidates the branch lists and `me`).
- `use-branch-context.ts`: `useBranchContext()` — calls `useMyBranches()`; when data arrives, `restore(user.id, branches.map(b => b.id), defaultBranchId)`. Returns `{ myBranches, workingBranch, ready }` where `ready` is true once `restore` has run (or `/me/branches` failed — then the backend default applies).
- **Gate (review M10):** `AppLayout` renders the header and sidebar immediately but renders `<Outlet />` only when `ready` (a `PageLoaderOverlay` meanwhile), so no page query is sent before `X-Branch-Id` is set. Without this, a reload under a stored non-default branch fetches the default branch's data and never refetches.
- `header-branch-switcher.tsx`: renders `null` unless `canSwitch`. Otherwise a compact `Select` (label `Chi nhánh làm việc`) of branches showing `code — name`. On change: `setWorkingBranch(user.id, id)`, then `await caches.delete('api-cache')` (guarded: `'caches' in window`), then `queryClient.resetQueries()` (drops inactive cached data too, unlike `invalidateQueries`), and, when the current route is a voucher detail/new page (`/stock-in/...`, `/stock-out/...`), navigate to that module's list.

1. **Write the failing tests:**
   - `features/branches/use-branch-context.test.tsx` (mock `./hooks` and the auth store): restores the stored allowed branch; falls back to the default; `ready` is false until the data arrives.
   - `components/layout/__tests__/app-layout-branch-gate.test.tsx`: the outlet content is not rendered while `ready` is false and appears once it is true.
   - `components/layout/header/__tests__/header-branch-switcher.test.tsx`: `renders nothing when user cannot switch`; `lists branches and switches working branch` (select an option → store updated; `resetQueries` spy called; on `/stock-in/123` navigate to `/stock-in`).
2. **Run the tests to verify they fail:** `npx vitest run src/features/branches src/components/layout`. Expected: FAIL (modules missing).
3. **Write the minimal implementation:** the feature module, the hook, the gate in `AppLayout`, the switcher. Call `useBranchContext()` once in `AppLayout` and render `<HeaderBranchSwitcher />` in `app-header.tsx` before `<HeaderNotifications />`.
4. **Run the tests to verify they pass:** same command. Expected: PASS. The existing `app-header.test.tsx` has no `vi.mock` (it uses a real `QueryClient`); add `vi.mock('@/features/branches/hooks')` there.
5. **Commit:** `git commit -m "feat(frontend): branch context and header working-branch switcher"`

### Task 7.4 — Navigation config with the "Kho" group

Move `navGroups` out of `app-layout.tsx` into `components/layout/nav-config.ts` and extend `NavItem` with `anyPermission?: Permission[]` (visible when the user has at least one).

```ts
export function visibleNavGroups(hasPermission: (p: Permission) => boolean, isInRole: (r: Role) => boolean): SidebarNavGroup[];
```

Groups (lucide icons in brackets):
- **Chức năng**: existing items, then `Nhà cung cấp` `/suppliers` [Truck] `suppliers.view`, `Kho` `/warehouses` [Warehouse] `inventory.catalogs.manage`, `Lý do nhập xuất` `/stock-reasons` [ListChecks] `inventory.catalogs.manage`, `Hình thức thanh toán` `/payment-methods` [Wallet] `inventory.catalogs.manage`.
- **Kho** (new, right after Chức năng): `Phiếu nhập kho` `/stock-in` [ArrowDownToLine] `stock_in.view`; `Phiếu xuất kho` `/stock-out` [ArrowUpFromLine] `stock_out.view`; `Tồn đầu kỳ` `/inventory/opening-stock` [ClipboardList] `inventory.opening_stock`; `Tồn kho` `/inventory/stock-on-hand` [Boxes] `reports.inventory`; `Thẻ kho` `/inventory/stock-card` [ScrollText] `reports.inventory`.
- **Setting**: `Cấu hình hệ thống` changes from `permission: 'system.manage_settings'` to `anyPermission: ['system.manage_settings', 'branches.manage', 'period_lock.manage', 'inventory.settings', 'inventory.recalc_cost']`.

1. **Write the failing test** `components/layout/nav-config.test.ts`:
   - `warehouse user sees Kho group with vouchers, opening stock and reports` (the D23 WAREHOUSE permission set)
   - `sales user sees no Kho group and no inventory catalogs`
   - `catalog manager sees warehouse, reason and payment method entries`
   - `settings entry is visible with inventory.settings only`
2. **Run the test to verify it fails:** `npx vitest run src/components/layout/nav-config.test.ts`. Expected: FAIL (module missing).
3. **Write the minimal implementation:** `nav-config.ts`; `AppLayout` uses `visibleNavGroups(hasPermission, isInRole)`.
4. **Run the tests to verify they pass:** same command plus `npx vitest run src/components/layout`. Expected: PASS.
5. **Commit:** `git commit -m "feat(frontend): Kho navigation group and inventory catalog entries"`

### Task 7.5 — Default branch on the admin user form

1. **Write the failing test** `pages/admin/components/user-form-dialog.test.tsx` (mock `@/features/admin-users/hooks` and `@/features/branches/hooks`):
   - `create form branch select is optional and omitted means the main branch` (`useBranches` first returns no data, then `[CN01, CN02]`; submitting without a choice sends no `defaultBranchId` — the backend applies `MainBranchId`, D15)
   - `submitting create with a chosen branch sends defaultBranchId`
   - `edit form preselects the user's default branch and sends changes`
2. **Run the test to verify it fails:** `npx vitest run src/pages/admin/components/user-form-dialog.test.tsx`. Expected: FAIL (no branch field).
3. **Write the minimal implementation:** `features/admin-users/types.ts` gets `defaultBranchId` / `defaultBranchName` on the detail and list item types and an optional `defaultBranchId` on create/update. `user-form-dialog.tsx` gets a `Chi nhánh mặc định` `Select` (`Controller`; zod `z.string().uuid().optional()` on create — the create dialog mounts before branches load, so a required field would block submit with "Invalid uuid"; required on edit, preselected) in both forms. `users-list-page.tsx` gets a `Chi nhánh` column.
4. **Run the tests to verify they pass:** same command plus `npx vitest run src/pages/admin`. Expected: PASS.
5. **Commit:** `git commit -m "feat(admin-users): pick default branch for users"`

### Task 7.6 — `inventory` permission module in the role matrix (review M3)

The backend puts 17 codes (`stock_in.*`, `stock_out.*`, `inventory.*`) in the new module `inventory` (Phase 01 Task 1.1). The role matrix renders only the modules in its hard-coded `MODULE_ORDER` (`role-matrix-table.tsx:15-22, 47-53`), so these codes would be invisible — admins could neither grant them to custom roles nor revoke the D23 grants before go-live (D39).

1. **Write the failing test:** add to `pages/admin/roles-matrix-page.test.tsx` a case `renders the Kho module and toggles stock_in.view` (mock data with a permission `{ code: 'stock_in.view', module: 'inventory' }`).
2. **Run the test to verify it fails:** `npx vitest run src/pages/admin/roles-matrix-page.test.tsx`. Expected: FAIL (row not rendered).
3. **Write the minimal implementation:** `PermissionModule` gains `'inventory'` (`features/admin-roles/types.ts`); `MODULE_LABEL` gains `inventory: 'Kho'` in `role-matrix-table.tsx` **and** `role-create-dialog.tsx`; `MODULE_ORDER` becomes `['system', 'catalog', 'sales', 'inventory', 'report']`. Also make the matrix append any unknown module at the end with its raw name as label, so a future module cannot disappear.
4. **Run the tests to verify they pass:** `npx vitest run src/pages/admin`. Expected: PASS.
5. **Commit:** `git commit -m "feat(admin-roles): show the inventory permission module"`

### Task 7.7 — Keep branch- and user-scoped API data out of the service worker cache (review M11)

`sw.ts` caches every `GET /api/*` with `NetworkFirst` (`networkTimeoutSeconds: 8`, key = URL). `X-Branch-Id` and `Authorization` are not part of the key, so on a slow or offline network it would serve another branch's — or another user's — data, including cost values and `canViewCost: true`.

```ts
// lib/sw-routes.ts (imported by sw.ts; testable without a service worker)
// Never cache: /api/stock-vouchers, /api/inventory, /api/reports, /api/warehouses, /api/branches,
// /api/me/, /api/suppliers, /api/stock-reasons, /api/payment-methods.
export function isCacheableApiPath(pathname: string): boolean;
```

1. **Write the failing test** `lib/sw-routes.test.ts`: the listed prefixes return false; `/api/products/search` and `/api/lookups/units` return true; non-API paths return false.
2. **Run the test to verify it fails:** `npx vitest run src/lib/sw-routes.test.ts`. Expected: FAIL (module missing).
3. **Write the minimal implementation:** the helper; in `sw.ts` register the `NetworkFirst` route only for `url.pathname.startsWith('/api/') && isCacheableApiPath(url.pathname)` (other API GETs go straight to the network). In `useLogout` → `onSettled` (and the refresh-failure branch), delete `api-cache` (`'caches' in window && caches.delete('api-cache')`); the switcher already does it (Task 7.3).
4. **Run the tests to verify they pass:** same command, then `npm run build` (the service worker is built by `vite-plugin-pwa` with `injectManifest`). Expected: PASS.
5. **Commit:** `git commit -m "fix(pwa): never serve branch- or user-scoped API data from the cache"`

## Verification

- `cd frontend && npm run typecheck` (real check after Task 7.0)
- `npm run lint`
- `npx vitest run src/lib src/stores src/features/branches src/components/layout src/pages/admin`
- `npm run test` (no new failures compared with the baseline in SUMMARY.md)
- `npm run build`

## Exit Criteria

- `npm run typecheck` type-checks the app.
- Every backend permission code exists in `PERMISSIONS`, and the route rules cover every new route.
- Pages render only after the working branch is resolved; API calls carry `X-Branch-Id`; only `branches.access_all` users see the switcher, and switching resets all cached data.
- The sidebar shows the "Kho" group and the new catalog entries by permission; the role matrix shows the "Kho" module.
- The service worker never caches branch- or user-scoped API responses.
- Admins can set a user's default branch (optional on create).
