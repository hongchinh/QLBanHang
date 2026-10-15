# Phase 01 — Help data layer, guard tests and the reference doc

**Status:** [ ] pending
**Complexity:** L

## Objective

Build everything the help UI reads, with no UI yet: the route tree that tests can walk, the Markdown parser, the registry, the empty `videos.json`, the lazy content loader, the `ui-store` fields, the first help file (`stock-out.form`) and the guard tests with `HELP_PENDING`. At the end `npm test` fails whenever a route has no help and is not pending, a help file is malformed, a related key is unknown or a video points at a missing step.

## Files

- `frontend/src/app-routes.tsx` (new) · `frontend/src/App.tsx` (modify)
- `frontend/src/features/help/types.ts` (new)
- `frontend/src/features/help/route-paths.ts` + `route-paths.test.tsx` (new)
- `frontend/src/features/help/parse-help.ts` + `parse-help.test.ts` (new)
- `frontend/src/features/help/registry.ts` + `registry.test.ts` (new)
- `frontend/src/features/help/videos.json` + `videos.ts` + `videos.test.ts` (new)
- `frontend/src/features/help/content/stock-out.form.md` (new)
- `frontend/src/features/help/content-index.ts` + `load-help.ts` + `load-help.test.ts` + `load-help-failure.test.ts` (new)
- `frontend/src/stores/ui-store.ts` + `ui-store.test.ts` (modify)
- `frontend/src/features/help/help-pending.ts` + `help-guard.test.ts` (new)

## Tasks

All commands run from `$WT/frontend`. "Run" means `npm run test -- --testTimeout=30000 --maxWorkers=3 <path>`.

### Task 1.1 — Export the route tree and flatten its paths

1. Write the failing test `src/features/help/route-paths.test.tsx`:
   ```tsx
   import { Navigate, Route, createRoutesFromChildren } from 'react-router-dom';
   import { describe, expect, it } from 'vitest';
   import { appRoutes } from '@/app-routes';
   import { ROUTES_WITHOUT_HELP, flattenRoutePaths } from './route-paths';

   const Page = () => null;
   const tree = (
     <>
       <Route path="/login" element={<Page />} />
       <Route path="/403" element={<Page />} />
       <Route element={<Page />}>
         <Route index element={<Page />} />
         <Route path="customers">
           <Route index element={<Page />} />
           <Route path="new" element={<Page />} />
           <Route path=":id" element={<Page />} />
         </Route>
         <Route path="debt/matching" element={<Page />} />
         <Route path="reports">
           <Route index element={<Navigate to="revenue" replace />} />
           <Route path="revenue" element={<Page />} />
           <Route path="sales-revenue/:saleUserId" element={<Page />} />
         </Route>
       </Route>
       <Route path="/404" element={<Page />} />
       <Route path="*" element={<Navigate to="/404" replace />} />
     </>
   );

   describe('flattenRoutePaths', () => {
     it('joins nested paths, maps index routes to their parent and skips layouts, redirects, * and pages without help', () => {
       expect(flattenRoutePaths(createRoutesFromChildren(tree))).toEqual([
         '/', '/customers', '/customers/:id', '/customers/new', '/debt/matching',
         '/reports/revenue', '/reports/sales-revenue/:saleUserId',
       ]);
     });
     it('reads the real app routes', () => {
       const paths = flattenRoutePaths(createRoutesFromChildren(appRoutes));
       expect(paths).toEqual(expect.arrayContaining([
         '/', '/stock-out/new', '/stock-out/:id', '/settings', '/admin/users/:userId/transfer-quotations',
       ]));
       expect(paths).not.toContain('/reports');
       for (const p of ROUTES_WITHOUT_HELP) expect(paths).not.toContain(p);
     });
   });
   ```
2. Run it → FAIL: `Failed to resolve import "@/app-routes"` (or `./route-paths`).
3. Implement:
   - `src/app-routes.tsx`: cut everything between `<Routes>` and `</Routes>` in `App.tsx` (lines 144–759 on the base: from `<Route path="/login" …/>` to `<Route path="*" …/>`) and paste it unchanged as
     ```tsx
     export const appRoutes = (
       <>
         {/* the cut <Route> elements, unchanged */}
       </>
     );
     ```
     Move with it every import those elements use (all `@/pages/...` imports, `ProtectedRoute`, `AppLayout`, `Navigate`, `Route`) and the `// Round 2A: …` comment lines. `App.tsx` keeps `<Routes>{appRoutes}</Routes>` and imports `appRoutes` from `@/app-routes`; delete the imports it no longer uses (`npm run typecheck` names them through `noUnusedLocals`).
   - `src/features/help/route-paths.ts`:
     ```ts
     import { isValidElement } from 'react';
     import { Navigate, type RouteObject } from 'react-router-dom';

     // Pages that are not screens of the app: no help file, no HELP_PENDING entry.
     export const ROUTES_WITHOUT_HELP: readonly string[] = ['/login', '/403', '/404'];

     function join(parent: string, path: string): string {
       if (path.startsWith('/')) return path;
       return `${parent === '/' ? '' : parent}/${path}`;
     }

     // Leaf routes as full paths. An index route takes its parent's path; a layout route (no path) adds nothing;
     // redirects (<Navigate>), '*' and ROUTES_WITHOUT_HELP are left out.
     export function flattenRoutePaths(routes: RouteObject[]): string[] {
       const out = new Set<string>();
       const walk = (list: RouteObject[], parent: string) => {
         for (const r of list) {
           const full = r.index ? parent || '/' : r.path === undefined ? parent : join(parent, r.path);
           const redirect = isValidElement(r.element) && r.element.type === Navigate;
           const leaf = !r.children?.length && (r.index || r.path !== undefined);
           if (leaf && r.path !== '*' && !redirect && !ROUTES_WITHOUT_HELP.includes(full)) out.add(full);
           if (r.children) walk(r.children, full);
         }
       };
       walk(routes, '');
       return [...out].sort();
     }
     ```
4. Run `src/features/help/route-paths.test.tsx src/App.routes.test.tsx src/App.shell.test.tsx` → PASS; `npm run typecheck` and `npm run lint` clean.
5. Commit: `feat(frontend): export the route tree and flatten its paths`.

### Task 1.2 — Parse a help file (frontmatter, sections, steps)

The format is the brainstorm's §2. Rules enforced (each failure adds one message to `errors`, prefixed with `<fileKey>: `):

| Part | Rule |
|---|---|
| Frontmatter | The file starts with a line `---` and a second `---` closes it. Lines are `name: value` or `name: [a, b]`. Allowed names: `key`, `title`, `routes`, `sources`, `related`, `updated`; anything else is an error. Required: `key`, `title`, `routes` (non-empty, each starts with `/`), `sources` (non-empty), `updated` (`YYYY-MM-DD`). `related` defaults to `[]`. |
| Key | Equals `fileKey`; matches `/^[a-z0-9]+(?:-[a-z0-9]+)*(?:\.(?:list|form))?$/`. |
| Sections | `## Dùng để` (required), `## Các bước` (required), `## Lưu ý` (optional), `## Phím tắt` (optional), in this order, each at most once. Any other `##` heading, or non-blank text before the first section, is an error. |
| Dùng để | Lines joined with one space; non-empty; 1 or 2 sentences (count of `/[.!?](?=\s|$)/g`). |
| Các bước | Every non-blank line is `N. {#id} text` with N = 1, 2, 3… in order; 3–7 items; `id` matches `/^[a-z][a-z0-9-]*$/` and is unique; `text` non-empty. |
| Lưu ý | Every non-blank line is `- text`; at least one. |
| Phím tắt | Every non-blank line is `` - `keys` label ``; at least one. |
| Inline (purpose, steps, notes, shortcut labels) | No Markdown link (`/\[[^\]]*\]\([^)]*\)/`); an even number of `**` and of `` ` ``. |

CR characters are stripped first. `parse-help.ts` may contain only `import type` lines (P7).

1. Write the failing test `src/features/help/parse-help.test.ts`. Fixture (`VALID`), built from lines so the test file has no trailing-space issues:
   ```ts
   const VALID = [
     '---',
     'key: stock-out.form',
     'title: Phiếu xuất kho',
     'routes: [/stock-out/new, /stock-out/:id]',
     'sources: [frontend/src/pages/stock-vouchers/**]',
     'related: [receipts.form]',
     'updated: 2026-10-15',
     '---',
     '## Dùng để',
     'Ghi nhận hàng xuất khỏi kho. Phiếu xuất bán trừ tồn kho.',
     '',
     '## Các bước',
     '1. {#reason} Chọn **Lý do xuất** và **Kho**.',
     '2. {#partner} Gõ mã khách ở **Mã đối tượng**.',
     '3. {#save} Bấm **Lưu tạm** (`Ctrl S`).',
     '',
     '## Lưu ý',
     '- Phải lưu trước khi in.',
     '',
     '## Phím tắt',
     '- `Ctrl S` Lưu',
   ].join('\n');
   ```
   Cases:
   - `parseHelpDoc(VALID, 'stock-out.form')` → `errors: []` and `doc` equal to `{ key: 'stock-out.form', title: 'Phiếu xuất kho', routes: ['/stock-out/new', '/stock-out/:id'], sources: ['frontend/src/pages/stock-vouchers/**'], related: ['receipts.form'], updated: '2026-10-15', purpose: 'Ghi nhận hàng xuất khỏi kho. Phiếu xuất bán trừ tồn kho.', steps: [{ id: 'reason', text: 'Chọn **Lý do xuất** và **Kho**.' }, …], notes: ['Phải lưu trước khi in.'], shortcuts: [{ keys: 'Ctrl S', label: 'Lưu' }] }`.
   - `VALID.replace(/\n/g, '\r\n')` → same doc.
   - Without the `related` line → `related: []`.
   - Each of these gives `doc: null` and an error containing the quoted text: no frontmatter (`frontmatter`); `title` line removed (`"title"`); `updated: 15/10/2026` (`updated`); extra line `owner: me` (`unknown field "owner"`); file key `stock-in.form` (`key`); `## Các bước` section removed (`Các bước`); `## Lưu ý` moved before `## Các bước` (`order`); a `## Ghi chú` section (`unknown section`); purpose `A. B. C.` (`sentences`); only 2 steps (`3–7`); 8 steps (`3–7`); duplicate `{#reason}` (`duplicate step id`); step `2.` renumbered `3.` (`numbering`); a step line without `{#id}` (`step id`); a note with `[xem](http://x)` (`link`); a shortcut line `- Ctrl S Lưu` (`shortcut`); a step with one `**` (`unbalanced`).
2. Run it → FAIL: `Failed to resolve import "./parse-help"`.
3. Implement `src/features/help/types.ts` (the "Data layer" contract of SUMMARY.md) and `parseHelpDoc` in `src/features/help/parse-help.ts` (`import type { HelpDoc, HelpStep, HelpShortcut } from './types';` only). Keep it a straight line-by-line reader: split frontmatter, then walk lines collecting sections by heading, then validate each section with the table above.
4. Run → PASS.
5. Commit: `feat(frontend): parse help files`.

### Task 1.3 — Inline markers, narration text, step hash

1. Add failing cases to `parse-help.test.ts`:
   ```ts
   expect(parseInline('Chọn **Lý do xuất** rồi `Ctrl S`.')).toEqual([
     { kind: 'text', text: 'Chọn ' }, { kind: 'strong', text: 'Lý do xuất' }, { kind: 'text', text: ' rồi ' },
     { kind: 'kbd', text: 'Ctrl S' }, { kind: 'text', text: '.' },
   ]);
   expect(parseInline('a ** b')).toEqual([{ kind: 'text', text: 'a ** b' }]);          // unterminated marker stays text
   expect(narrationText('{#save}  Bấm **Lưu**   (`Ctrl S`) ')).toBe('Bấm Lưu (Ctrl S)');
   expect(narrationText('Lu\u0301u')).toBe('L\u00fau');                                // NFD in, NFC out (type the escapes)
   expect(stepHash('a')).toBe('e40c292c');                                               // FNV-1a 32-bit of "a"
   expect(stepHash('**a**')).toBe('e40c292c');
   expect(stepHash('Bấm Lưu')).not.toBe(stepHash('Bấm Lưu.'));
   expect(stepHash('x')).toMatch(/^[0-9a-f]{8}$/);
   ```
2. Run → FAIL: `parseInline is not a function` (not exported yet).
3. Implement in `parse-help.ts`:
   - `parseInline`: scan left to right; `**` opens a strong run until the next `**`, a backtick opens a kbd run until the next backtick; an opener without its closer stays literal text; adjacent text merges.
   - `narrationText`: remove a leading `{#id}`, then `**` and backticks, collapse whitespace, trim, `normalize('NFC')`.
   - `stepHash`: FNV-1a 32-bit (offset `0x811c9dc5`, prime `0x01000193`, `Math.imul`, `>>> 0`) over `new TextEncoder().encode(narrationText(text))`, as 8 lowercase hex chars (`padStart(8, '0')`).
4. Run → PASS.
5. Commit: `feat(frontend): inline help markers and step hash`.

### Task 1.4 — Registry

1. Write the failing test `src/features/help/registry.test.ts` with a helper `doc(key, routes)` that returns a minimal `HelpDoc` (one-sentence purpose, three steps `a`/`b`/`c`):
   - docs `home ['/']`, `stock-out.list ['/stock-out']`, `stock-out.form ['/stock-out/new', '/stock-out/:id']`, `reports-sales-revenue ['/reports/sales-revenue', '/reports/sales-revenue/:saleUserId']`;
   - `resolve('/stock-out/new')` → `stock-out.form`; `resolve('/stock-out/0b9c')` → `stock-out.form`; `resolve('/stock-out')` and `resolve('/stock-out/')` → `stock-out.list`; `resolve('/')` → `home`; `resolve('/reports/sales-revenue/u1')` → `reports-sales-revenue`; `resolve('/products')` → `null`;
   - `docs` sorted by key; `byKey.get('home')` is the home doc;
   - `firstStaticPath(stock-out.form)` → `'/stock-out/new'`; `firstStaticPath(doc('x', ['/admin/user-settings/:userId']))` → `'/admin/user-settings/_'`.
2. Run → FAIL: `Failed to resolve import "./registry"`.
3. Implement `src/features/help/registry.ts`: sort the docs by key; build `RouteObject[]` with one `{ path, id: \`${key}|${path}\` }` per doc route; `resolve` calls `matchRoutes(routes, pathname)` and maps the last match's `route.id` back to the doc. `firstStaticPath` replaces every `:param` segment of `routes[0]` with `_`.
4. Run → PASS.
5. Commit: `feat(frontend): help registry keyed by route`.

### Task 1.5 — Video helpers and the empty `videos.json`

1. Write the failing test `src/features/help/videos.test.ts`:
   - `videoFor('k', { k: video })` → `video`; `videoFor('missing', { k: video })` → `undefined`; `videoFor('stock-out.form')` (default record) → `undefined` in round 1;
   - `youtubeUrl('abc')` → `'https://www.youtube.com/watch?v=abc'`; `youtubeUrl('abc', 21)` → `'https://www.youtube.com/watch?v=abc&t=21s'`; `youtubeUrl('abc', 0)` → `'https://www.youtube.com/watch?v=abc&t=0s'`;
   - `youtubeThumb('abc')` → `'https://i.ytimg.com/vi/abc/mqdefault.jpg'`;
   - `formatDuration(155)` → `'2:35'`, `formatDuration(8)` → `'0:08'`, `formatDuration(600)` → `'10:00'`.
2. Run → FAIL: `Failed to resolve import "./videos"`.
3. Implement: `src/features/help/videos.json` containing `{}` and a final newline; `src/features/help/videos.ts` with `import videosJson from './videos.json';` and `export const helpVideos = videosJson as Readonly<Record<string, HelpVideo>>;` plus the four functions of the contract.
4. Run → PASS.
5. Commit: `feat(frontend): help video helpers`.

### Task 1.6 — Reference help file `stock-out.form`

Content, not code: the guard test of Task 1.9 validates it, and Task 1.7 loads it. Labels are checked against the code (`STOCK_VOUCHER_LABELS.Out`, `stock-voucher-main-fields.tsx`, `stock-voucher-tabs.tsx`, `stock-voucher-payment-tab.tsx`, `stock-voucher-form-page.tsx`).

1. Create `src/features/help/content/stock-out.form.md`:
   ```md
   ---
   key: stock-out.form
   title: Phiếu xuất kho
   routes: [/stock-out/new, /stock-out/:id]
   sources: [frontend/src/pages/stock-vouchers/**, frontend/src/features/stock-vouchers/**, backend/src/OrderMgmt.Application/Inventory/StockVouchers/**]
   related: []
   updated: <today, YYYY-MM-DD>
   ---
   ## Dùng để
   Ghi nhận hàng xuất khỏi kho: bán cho khách, trả hàng nhà cung cấp hoặc xuất dùng nội bộ. Phiếu xuất bán trừ tồn kho và tăng công nợ phải thu của khách.

   ## Các bước
   1. {#reason} Chọn **Lý do xuất** (ví dụ Xuất bán hàng) và **Kho**.
   2. {#partner} Gõ mã hoặc tên khách ở **Mã đối tượng**, Enter để chọn.
   3. {#lines} Nhập hàng ở tab **Hàng hóa**: mã hàng, số lượng, đơn giá. Enter sang ô kế tiếp, `Insert` thêm dòng.
   4. {#totals} Xem tổng tiền và công nợ của khách ở thanh cuối phiếu.
   5. {#payment} Chọn **HTTT** và nhập **Số tiền thanh toán** ở tab **Thanh toán** nếu khách trả ngay, phiếu thu được tạo tự động.
   6. {#save} Bấm **Lưu tạm** hoặc **Lưu và thoát** (**Cập nhật** khi sửa phiếu), rồi in phiếu nếu cần.

   ## Lưu ý
   - Khách đang Ngừng sử dụng thì không chọn được (báo Đối tượng đã ngừng hoạt động); phiếu cũ của khách đó vẫn sửa được.
   - Vượt hạn mức nợ: hệ thống cảnh báo hoặc chặn, tùy cấu hình hạn mức nợ.
   - Phải lưu thay đổi trước khi in.

   ## Phím tắt
   - `Ctrl S` Lưu
   - `Alt 1–4` Đổi tab
   - `Insert` Thêm dòng
   - `Ctrl Del` Xóa dòng
   ```
   `related` stays empty for now: the intended links (`receipts.form`, `debt-matching`) have no file yet, and Task 1.9's related-key test would fail. Phase 04 Task 4.4 adds them once both files exist.
2. Check every bold label and the notes against the code; fix the text where the code says otherwise. Evidence found while reviewing the plan (recheck it on the worktree):
   - `git grep -n "Lưu tạm\|Lưu và thoát\|Cập nhật" -- src/pages/stock-vouchers/stock-voucher-form-page.tsx` (lines 735–746); `git grep -n "Số tiền thanh toán" -- src/pages/stock-vouchers` (payment tab); tab names and hotkeys in `stock-voucher-tabs.tsx` (`Alt+4` `Lịch sử` exists only when editing).
   - Inactive customer: status label `Ngừng sử dụng` (`src/pages/customers/customer-form-fields.tsx:37`), message `Đối tượng đã ngừng hoạt động.` and the "only a newly chosen partner" rule (`backend/src/OrderMgmt.Application/Inventory/StockVouchers/Services/StockVoucherService.cs:666-668`).
   - Credit limit: dialog title `Vượt hạn mức nợ` (`src/lib/confirm-required.ts:37-38`); the policy is the Round 2A bullet "credit-limit and negative-fund policies" under "Current Scope" in `docs/project-pdr/product-goals.md` and its section "Tiền & Công nợ".
   - Print: `Lưu thay đổi trước khi in` (`stock-voucher-form-page.tsx:729`). Delete-line hint on screen: `Ctrl`+`Del` (`stock-line-grid.tsx:320`).
3. Commit: `docs(help): reference help for the stock-out voucher form`.

### Task 1.7 — Content loader

1. Write the failing test `src/features/help/load-help.test.ts`:
   - `parseHelpFiles({ './content/stock-out.form.md': VALID, './content/bad.md': 'nope' })` (copy the `VALID` fixture of `parse-help.test.ts` into this file) (import `parseHelpFiles` from `./content-index`) returns one doc with key `stock-out.form` and calls `console.error` once with a message containing `bad` (spy with `vi.spyOn(console, 'error').mockImplementation(() => {})`).
   - `await loadHelpIndex()` resolves an index whose `resolve('/stock-out/new')?.key` is `'stock-out.form'` (the real file of Task 1.6); a second call returns the same promise (`toBe`).
   - `renderHook(() => useHelpIndex())`: `result.current` is `null` first, then (`waitFor`) an index; `renderHook(() => useHelpLoadFailed())` stays `false`. Call `resetHelpIndexCacheForTests()` in `beforeEach`.
   - Second file `src/features/help/load-help-failure.test.ts`, where the chunk cannot load: `vi.mock('./content-index', () => { throw new Error('offline'); });` at the top. `await expect(loadHelpIndex()).rejects.toThrow()`; then `renderHook(() => useHelpLoadFailed()).result.current` is `true`; a second `loadHelpIndex()` returns a **new** promise (`not.toBe` the first: the cache was cleared, so a retry is possible); `renderHook(() => useHelpIndex())` stays `null`.
2. Run → FAIL: `Failed to resolve import "./content-index"`.
3. Implement:
   - `src/features/help/content-index.ts`:
     ```ts
     import { parseHelpDoc } from './parse-help';
     import type { HelpDoc } from './types';

     // path -> raw text; the key is the file name without .md. Invalid files are logged and skipped
     // (help-guard.test.ts fails on them first).
     export function parseHelpFiles(files: Record<string, string>): HelpDoc[] {
       const docs: HelpDoc[] = [];
       for (const [path, raw] of Object.entries(files)) {
         const key = path.replace(/^.*\//, '').replace(/\.md$/, '');
         const { doc, errors } = parseHelpDoc(raw, key);
         if (doc) docs.push(doc);
         else console.error(`Help file ${key} is invalid:\n${errors.join('\n')}`);
       }
       return docs;
     }

     export const helpDocs: HelpDoc[] = parseHelpFiles(
       import.meta.glob<string>('./content/*.md', { query: '?raw', import: 'default', eager: true }),
     );
     ```
   - `src/features/help/load-help.ts`: a small module-level store, so a retry started anywhere (the panel's `Thử lại` button, Phase 02) reaches every reader:
     ```ts
     import { useEffect, useSyncExternalStore } from 'react';
     import { buildHelpIndex, type HelpIndex } from './registry';

     let cache: Promise<HelpIndex> | null = null;
     let loaded: HelpIndex | null = null;
     let failed = false;
     const listeners = new Set<() => void>();
     const emit = () => listeners.forEach((l) => l());
     const subscribe = (l: () => void) => { listeners.add(l); return () => { listeners.delete(l); }; };

     // One lazy chunk with every help file. A failed import (offline before the service worker cached it, or a chunk
     // removed by a deploy) clears the cache so the next call tries again.
     export function loadHelpIndex(): Promise<HelpIndex> {
       cache ??= import('./content-index').then(
         (m) => { loaded = buildHelpIndex(m.helpDocs); failed = false; emit(); return loaded; },
         (e: unknown) => { cache = null; failed = true; emit(); throw e; },
       );
       return cache;
     }

     export function useHelpIndex(): HelpIndex | null {
       useEffect(() => { loadHelpIndex().catch(() => {}); }, []);
       return useSyncExternalStore(subscribe, () => loaded, () => null);
     }

     export function useHelpLoadFailed(): boolean {
       return useSyncExternalStore(subscribe, () => failed, () => false);
     }

     export function resetHelpIndexCacheForTests(): void { cache = null; loaded = null; failed = false; }
     ```
     Nothing else imports `content-index.ts` statically (that keeps it a separate chunk).
4. Run → PASS.
5. Commit: `feat(frontend): lazy help content loader`.

### Task 1.8 — `ui-store` help fields

1. Add a failing `describe('useUiStore help')` to `src/stores/ui-store.test.ts` (reset with `localStorage.clear()` and `useUiStore.setState({ helpOpen: false, helpHintSeen: {}, helpHintsOff: {} })` in `beforeEach`):
   - `openHelp` / `closeHelp` / `toggleHelp` set `helpOpen` to true / false / the opposite;
   - `markHelpHintSeen('u1', 'stock-out.form')` twice → `helpHintSeen` is `{ u1: ['stock-out.form'] }`; another user keeps its own list;
   - `turnOffHelpHints('u1')` → `helpHintsOff.u1 === true`;
   - after `openHelp()` and `markHelpHintSeen(...)` and `turnOffHelpHints(...)`, `readPersisted()?.state` has `helpHintSeen` and `helpHintsOff` and no `helpOpen`.
2. Run `src/stores/ui-store.test.ts` → FAIL: `openHelp is not a function`.
3. Implement in `ui-store.ts`: the fields and actions of the contract (defaults `false`, `{}`, `{}`); add `helpHintSeen` and `helpHintsOff` to `partialize`; keep `version: 1` (zustand merges new fields over the stored state).
4. Run → PASS (the whole file).
5. Commit: `feat(frontend): help state in ui-store`.

### Task 1.9 — Guard tests and `HELP_PENDING`

1. Create `src/features/help/help-pending.ts` with an empty list and a comment, and write the failing test `src/features/help/help-guard.test.ts`:
   ```ts
   // help-pending.ts
   // App routes that have no help file yet. The list only shrinks: help-guard.test.ts fails when a route here
   // gets a help file, and when a new route has neither a file nor an entry here.
   export const HELP_PENDING: readonly string[] = [];
   ```
   ```ts
   // help-guard.test.ts
   import { createRoutesFromChildren } from 'react-router-dom';
   import { describe, expect, it } from 'vitest';
   import { appRoutes } from '@/app-routes';
   import { HELP_PENDING } from './help-pending';
   import { parseHelpDoc } from './parse-help';
   import { flattenRoutePaths } from './route-paths';
   import { helpVideos } from './videos';

   const raw = import.meta.glob<string>('./content/*.md', { query: '?raw', import: 'default', eager: true });
   const files = Object.entries(raw).map(([path, text]) => {
     const key = path.replace(/^.*\//, '').replace(/\.md$/, '');
     return { key, ...parseHelpDoc(text, key) };
   });
   const docs = files.flatMap((f) => (f.doc ? [f.doc] : []));
   const byKey = new Map(docs.map((d) => [d.key, d]));
   const appPaths = flattenRoutePaths(createRoutesFromChildren(appRoutes));
   const owners = (route: string) => docs.filter((d) => d.routes.includes(route)).map((d) => d.key);

   describe('help files', () => {
     it('are all valid', () => {
       expect(files.flatMap((f) => f.errors)).toEqual([]);
     });
     it('cover every app route once, or the route is pending', () => {
       const missing = appPaths.filter((r) => owners(r).length === 0 && !HELP_PENDING.includes(r));
       expect(missing, 'Thêm file hướng dẫn cho các route này, hoặc thêm chúng vào HELP_PENDING').toEqual([]);
       const shared = appPaths.filter((r) => owners(r).length > 1).map((r) => `${r}: ${owners(r).join(', ')}`);
       expect(shared, 'Một route chỉ được thuộc một file hướng dẫn').toEqual([]);
     });
     it('HELP_PENDING only shrinks', () => {
       const covered = HELP_PENDING.filter((r) => owners(r).length > 0);
       expect(covered, 'Xóa khỏi HELP_PENDING các route đã có hướng dẫn').toEqual([]);
       expect(HELP_PENDING.filter((r) => !appPaths.includes(r)), 'HELP_PENDING có route không tồn tại').toEqual([]);
       expect(HELP_PENDING.length, 'HELP_PENDING có route trùng').toBe(new Set(HELP_PENDING).size);
     });
     it('claim only app routes', () => {
       expect(docs.flatMap((d) => d.routes.filter((r) => !appPaths.includes(r)).map((r) => `${d.key}: ${r}`))).toEqual([]);
     });
     it('link only to other help files', () => {
       expect(docs.flatMap((d) => d.related.filter((k) => k === d.key || !byKey.has(k)).map((k) => `${d.key} -> ${k}`))).toEqual([]);
     });
   });

   describe('videos.json', () => {
     it('points at existing help files and steps', () => {
       const stale = Object.entries(helpVideos).flatMap(([key, video]) => {
         const doc = byKey.get(key);
         if (!doc) return [`${key}: không có file hướng dẫn`];
         return Object.keys(video.steps).filter((id) => !doc.steps.some((s) => s.id === id))
           .map((id) => `video cũ, cần quay lại: ${key} bước ${id}`);
       });
       expect(stale).toEqual([]);
     });
   });
   ```
2. Run `src/features/help/help-guard.test.ts` → FAIL: "cover every app route once…" lists every route except `/stock-out/new` and `/stock-out/:id`.
3. Paste that printed list into `HELP_PENDING` (one quoted route per line, sorted). Then prove the ratchet: add `'/stock-out/new'` to the list → the "only shrinks" test fails → remove it again. Record both RED lines in the commit body.
4. Run `src/features/help` → PASS.
5. Commit: `test(frontend): help guard tests and the pending route list`.

## Verification

```bash
cd "$WT/frontend"
npm run test -- --testTimeout=30000 --maxWorkers=3 src/features/help src/stores/ui-store.test.ts src/App.routes.test.tsx src/App.shell.test.tsx
npm run typecheck && npm run lint
```

## Exit Criteria

- `appRoutes` is the only route tree; `App.tsx` renders it.
- The parser, registry, video helpers, loader and `ui-store` fields match the SUMMARY contracts.
- `stock-out.form.md` exists and is valid; `HELP_PENDING` lists every other app route.
- The guard tests pass and fail when a route, file, related key or video step is wrong (RED lines recorded).
