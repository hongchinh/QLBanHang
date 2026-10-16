# Phase 04 — Docs, real-browser check and help

**Status:** [ ] pending
**Complexity:** M

## Objective

Finish the work in a fixed commit order, because `npm run help:stale` compares commit times:

1. the docs: `conventions.md`, `directory-structure.md` and two rows of `docs/SUMMARY.md`;
2. the real-browser check on a backend with its own database: the 11 steps of mockup 03 on stock-out and stock-in, steps 1–8 on the quotation, the flash, the header contrast in light and dark mode, reduced motion, the scroll anchor and the quotation **Mã hàng** list. A defect found there is fixed with a RED test first;
3. last, the help files of the five guides this work touches, then the guides the base already flags, then the final checks;
4. after the final checks, the execution report and the archive of the plan folder (Task 4.16).

Phases 01–03 must be merged into `feat/dimension-columns` before this phase starts.

## Files

- `docs/code-standard/conventions.md` (modify line 85, add one sub-bullet after it)
- `docs/codebase/directory-structure.md` (modify the `lib/` list, lines 249–254)
- `docs/SUMMARY.md` (modify the descriptions of `codebase/directory-structure.md`, line 20, and `code-standard/conventions.md`, line 28; Task 4.16 also changes the row of this plan)
- `docs/plans/261016-0249-dimension-columns-by-pricing-mode/BROWSER-CHECK.md` (new)
- `docs/plans/261016-0249-dimension-columns-by-pricing-mode/browser-check/*.png` (new)
- `frontend/src/features/help/content/stock-in.form.md`, `stock-out.form.md`, `quotations.form.md`, `inventory-opening-stock.md`, `inventory-stock-card.md` (modify)
- `frontend/src/features/help/content/reports-revenue.md` (modify: `sources`, `updated`, any sentence that no longer matches; Task 4.14, always, in its own commit with the base's other `help:stale` guides)
- `frontend/src/features/help/content/admin-dashboard.md`, `home.md` (modify only if Task 4.14 finds it needed)
- Only when Task 4.8 runs: the test files and sources of Phases 01–03 that own the defect
- `docs/plans/261016-0249-dimension-columns-by-pricing-mode/SUMMARY.md` and this file (status lines, Task 4.15)
- `docs/plans/261016-0249-dimension-columns-by-pricing-mode/EXECUTION-REPORT.md` (new; created in Task 4.13 step 3 or Task 4.14 step 3 if a note needs it, completed in Task 4.16), then the plan folder is moved to `docs/plans/archived/`; `docs/brainstorms/261015-2311-dimension-columns-by-pricing-mode/SUMMARY.md` (links, Task 4.16)
- Not committed: `$BC` = `<absolute session scratchpad>/dimcols-browser` (browser scripts, logs, raw results)

**Shell convention** (Phase 00 owns the rule; it is repeated here). Shell variables and the cwd do not survive between Bash calls, and a Monitor command is a fresh shell too. Begin every Bash call that uses `$WT` with this line:

```bash
WT=/D/Projects/QLBanHang/.claude/worktrees/dimension-columns
```

Begin every call that uses `$BC` with `BC=<absolute session scratchpad path>/dimcols-browser`, with the path written out in full (Git Bash form, `/c/Users/admin/AppData/Local/Temp/claude/…/scratchpad`). Begin every call that runs `psql` with `export PGPASSWORD=1; PSQL="/c/Program Files/PostgreSQL/18/bin/psql"`. Chain every `cd` to the next command with `&&`. Every `git add` / `git commit` runs as one chained line starting `cd "$WT" &&`. PowerShell snippets use absolute paths, not `$WT`. A call that runs `git` or `npm` without naming `$WT` (the greps, `help:stale`, the test runs) still starts with the `WT=` line and `cd "$WT/frontend" &&` (or `cd "$WT" &&`), because the cwd of a new call is the main checkout. The blocks below show these lines only where a block needs them; the rule applies to all.

Commands run from `$WT/frontend` unless stated; `git add` paths are relative to `$WT`. Add a second `-m` with the Co-Authored-By trailer from the session's attribution instructions to every commit line below, as Phase 00 does. Line numbers below are at `e61e4f1b`. No phase before this one edits the docs or help files listed here, except Phase 00, which adds rows only to the "Other" table of `docs/SUMMARY.md` (below its line 38), so lines 20 and 28 do not move. They hold on the branch unless a merge of `qlbanhang/main` changed the file (re-read it then).

## Tasks

Before Task 4.1, in the worktree copy of this file (`$WT/docs/plans/261016-0249-dimension-columns-by-pricing-mode/phase-04-docs-browser-help.md`; SUMMARY → Execution notes), set `**Status:** [-] in progress` and commit it, so the clean-tree checks below hold:

```bash
WT=/D/Projects/QLBanHang/.claude/worktrees/dimension-columns
cd "$WT" && git add docs/plans/261016-0249-dimension-columns-by-pricing-mode/phase-04-docs-browser-help.md && git commit -m "docs(plan): phase 04 in progress"
```

### Task 4.1 — Docs: conventions, directory structure, docs index

Docs only, so there is no test. The three files are CRLF in the worktree (`core.autocrlf=true`): edit them with the Edit tool, never with Git Bash `sed -i`, which rewrites the line endings.

1. `docs/code-standard/conventions.md`, section "Inventory Patterns (Round 1)", sub-list "- Frontend:", line 85 (the bullet that starts "Grid keyboard rules live in pure functions in `pages/stock-vouchers/utils/line-navigation.ts`").
   - In that line replace
     `` and, if hideable, to `OPTIONAL_COLUMNS`. ``
     with
     `` and, if the user may hide it, to `OPTIONAL_COLUMNS`. ``
   - Insert this sub-bullet as a new line between line 85 and line 86 (line 86 is "  - Enter never submits the stock voucher form. Save is Ctrl+S or the buttons."). It is indented by four spaces, like the sub-bullets at lines 153–155:
     ```
         - On the stock and quotation line grids, **Số tấm**, **Dài**, **Rộng** and **Dày** follow the highest pricing mode of the lines. The rule lives in `lib/dimension-columns.ts`: `PRICING_MODES` in rank order (moved there from `line-navigation.ts`; import it from `@/lib/dimension-columns`), `normalizePricingMode` (a missing or unknown mode counts as `PerUnit`), `usesDimension` and `shownDimensions`. These four columns are not `OPTIONAL_COLUMNS` and have no per-user switch. A cell that its row does not use keeps its input id, is disabled, has an empty plain `value` (not `register`, so nothing is written back to the form) and shows "—" as its `placeholder`; the stored dimension is still saved and printed. On the stock grid a dimension error forces its column into view through `dimensionErrorFields` (`use-stock-grid-prefs.ts`), not `forcedColumns`. Columns that appear flash through `useAppearedKeys` (`lib/use-appeared-keys.ts`): class `col-appeared`, rules in `pages/quotations/components/line-items-grid.css`, header colour `--qldh-col-appeared-head-bg`, no animation under `prefers-reduced-motion`.
     ```
2. `docs/codebase/directory-structure.md`, the `lib/` entry of the frontend tree (lines 249–254). Replace line 254
   ```
   │                                # use-dialog-opening.ts (token per dialog opening)
   ```
   with these three lines (same prefix: `│` and 32 spaces):
   ```
   │                                # use-dialog-opening.ts (token per dialog opening),
   │                                # dimension-columns.ts (pricing-mode rank, shown dimension columns),
   │                                # use-appeared-keys.ts (flash of newly shown keys)
   ```
3. `docs/SUMMARY.md`, two descriptions in the "Codebase" and "Code Standard" tables:
   - row `codebase/directory-structure.md` (line 20): replace
     `` the shared partner field in `components/partner-autocomplete` and the product quick add files in `features/products`) ``
     with
     `` the shared partner field in `components/partner-autocomplete`, the product quick add files in `features/products` and the dimension column helpers `lib/dimension-columns.ts` and `lib/use-appeared-keys.ts`) ``
   - row `code-standard/conventions.md` (line 28): replace
     `` the product quick add rules (`ProductFormFields`, `allowQuickAdd`, `useSaveProduct`), and the user help rules ``
     with
     `` the product quick add rules (`ProductFormFields`, `allowQuickAdd`, `useSaveProduct`), the dimension columns that follow the highest pricing mode (`lib/dimension-columns.ts`, `dimensionErrorFields`, `useAppearedKeys` and the `col-appeared` flash), and the user help rules ``
4. Check from `$WT` (`cd "$WT" && git diff --stat`): it lists exactly the three files, and `git diff --numstat` gives the same counts as `git diff --numstat --ignore-cr-at-eol` (conventions.md 2 added, 1 removed; directory-structure.md 3 added, 1 removed; SUMMARY.md 2 added, 2 removed).
5. Commit:
   ```bash
   WT=/D/Projects/QLBanHang/.claude/worktrees/dimension-columns
   cd "$WT" && git add docs/code-standard/conventions.md docs/codebase/directory-structure.md docs/SUMMARY.md && git commit -m "docs: dimension columns in conventions, directory structure and docs index"
   ```

### Task 4.2 — Sync with `qlbanhang/main`, then start a scratch backend and Vite

No code change. The check runs in the feature worktree itself, so Vite reloads any fix from Task 4.8.

1. Sync, from `$WT`:
   ```bash
   WT=/D/Projects/QLBanHang/.claude/worktrees/dimension-columns
   cd "$WT" && git fetch qlbanhang     # "Could not resolve host": retry with the sandbox disabled
   git log --oneline HEAD..qlbanhang/main
   ```
   If it lists commits: `git merge --no-edit qlbanhang/main`, resolve any conflict keeping both sides, then from `$WT/frontend` run `npm run typecheck` and `npm run test -- --testTimeout=30000 --maxWorkers=3` (only the failures in `BASELINE.md` are allowed) and `npm run help:stale` (note any new warning for Task 4.14). `git status --short` must be clean before step 2 (restore a rewritten `frontend/tsconfig.app.tsbuildinfo` with `git checkout -- frontend/tsconfig.app.tsbuildinfo`).
2. Scratch folder and the state before the run:
   ```bash
   WT=/D/Projects/QLBanHang/.claude/worktrees/dimension-columns
   BC=<absolute session scratchpad path>/dimcols-browser; mkdir -p "$BC"
   mkdir -p "$WT/docs/plans/261016-0249-dimension-columns-by-pricing-mode/browser-check"
   export PGPASSWORD=1; PSQL="/c/Program Files/PostgreSQL/18/bin/psql"
   "$PSQL" -h localhost -U postgres -tAc "select datname from pg_database where datname like 'qldonhang%' order by 1" | tee "$BC/isolation-proof.txt"
   netstat -ano | grep LISTENING | grep -E ':(5050|5061|5191) ' | tee -a "$BC/isolation-proof.txt"
   ```
   - `qldonhang_integtest_dimcols` must not be listed. If an earlier run left it, drop it as in Task 4.9 step 3, and delete the old `$BC/setup.json` together with it (the next step 3 re-creates the database, and `setup.js` runs again on it).
   - Ports 5061 and 5191 must be free. If a port is taken by a leftover scratch server from an earlier run of this task (`dotnet` from `.claude/worktrees/dimension-columns`, or `vite` on 5191), stop it as in Task 4.9 step 2. Otherwise stop and report. Port 5050 may be the developer backend, which uses the developer database `qldonhang_test`: leave it running and never send it a request.
3. Create the database:
   ```bash
   export PGPASSWORD=1; PSQL="/c/Program Files/PostgreSQL/18/bin/psql"
   "$PSQL" -h localhost -U postgres -c 'create database "qldonhang_integtest_dimcols"'
   ```
   Not `qldonhang_test` (the dev database, `appsettings.Development.json:3`), not `qldonhang`, and not the shared `qldonhang_integtest`. The integration test fixture drops only `<base>_tpl` and its own clones (`PostgresFixture.cs:63-65`, `:81-96`), so a separate name is safe from other sessions' test runs.
4. Start the API with Bash, `run_in_background: true`. The app reads `ConnectionStrings:DefaultConnection`, else `ConnectionStrings:Default` (`DependencyInjection.cs:39-40`), and `appsettings.Development.json:3` points `Default` at `qldonhang_test`, so override both. `--no-launch-profile` skips `launchSettings.json`, whose URL is port 5050.
   ```bash
   WT=/D/Projects/QLBanHang/.claude/worktrees/dimension-columns; BC=<absolute session scratchpad path>/dimcols-browser
   cd "$WT/backend/src/OrderMgmt.WebApi" && CS="Host=localhost;Port=5432;Database=qldonhang_integtest_dimcols;Username=postgres;Password=1" && ConnectionStrings__Default="$CS" ConnectionStrings__DefaultConnection="$CS" ASPNETCORE_URLS=http://localhost:5061 ASPNETCORE_ENVIRONMENT=Development Database__AutoMigrateAndSeed=true RateLimiting__LoginPermitLimit=100 dotnet run --no-launch-profile > "$BC/api.log" 2>&1
   ```
   Wait with Monitor (`BC=<absolute session scratchpad path>/dimcols-browser; until grep -q "Now listening on: http://localhost:5061" "$BC/api.log"; do sleep 2; done`). `Database__AutoMigrateAndSeed=true` runs the migrations and the seed (`Program.cs:272-275`): user `admin` with password `Admin@123` (`appsettings.Development.json:26`), warehouse KHO01, and the stock reasons NKH "Nhập khác" and XKH "Xuất khác" (`DbSeeder.cs:348`, `:350`). `RateLimiting__LoginPermitLimit=100` lifts the limit of 5 logins per minute (`Program.cs:135`).
5. Start Vite with Bash, `run_in_background: true`:
   ```bash
   WT=/D/Projects/QLBanHang/.claude/worktrees/dimension-columns; BC=<absolute session scratchpad path>/dimcols-browser
   cd "$WT/frontend" && npx vite --port 5191 --strictPort > "$BC/vite.log" 2>&1
   ```
   Wait for `http://localhost:5191/` in `$BC/vite.log` (Monitor, starting with the same `BC=` line). Do not edit `vite.config.ts`. Its proxy sends `/api` and `/hubs` to `localhost:5050` (`vite.config.ts:56-58`), so the browser must rewrite those requests itself (step 6). The PWA service worker runs in dev (`vite.config.ts:47-48`) and fetches `/api/lookups/…` on its own, outside Playwright's routes, so it must be blocked.
6. Playwright, in one call that starts with the `BC=` line: `cd "$BC" && npm init -y && npm i playwright-core@1.64.1`. Write `$BC/lib.js` with:
   - `launch()`: `chromium.launch({ channel: 'msedge', headless: true })`;
   - `newContext(browser, extra)`: `browser.newContext({ serviceWorkers: 'block', locale: 'vi-VN', timezoneId: 'Asia/Ho_Chi_Minh', viewport: { width: 1366, height: 768 }, ...extra })`, then on the context:
     - `route((url) => url.pathname.startsWith('/api/'), (r) => r.continue({ url: r.request().url().replace('http://localhost:5191', 'http://localhost:5061') }))`;
     - `route((url) => url.pathname.startsWith('/hubs/'), (r) => r.abort())` and `routeWebSocket(/\/hubs\//, (ws) => ws.close())` (no check needs SignalR);
     - `on('request', …)`: append every URL, and the rewritten target of each `/api` call, to the run's request log;
   - `assertNo5050(log)`: throws when any logged URL contains `:5050`; every script calls it last;
   - `login(page)`: open `http://localhost:5191/login`, fill `#username` with `admin` and `#password` with `Admin@123`, click **Đăng nhập**, wait until the URL leaves `/login`;
   - `api(method, path, body)`: Node `fetch('http://localhost:5061/api' + path)` with the bearer token from `POST /api/auth/login` (`{ username: 'admin', password: 'Admin@123' }`, token in `data.accessToken`); every response is `{ success, data, error }`. Without `X-Branch-Id` the API uses the user's default branch.

### Task 4.3 — Test data through the real API

No code change. Write `$BC/setup.js` and run it as in "Running the scripts" of Task 4.4; it stores every id in `$BC/setup.json`. The data mirrors mockup 03 (`mockup-03-interactive.html:331-337`); demo sample data cannot be used, because every sample product is `PerUnit`.

1. Units: `POST /api/lookups/units` with `{ name }` for `cuộn`, `m`, `m²`, `m³`. Group: `POST /api/product-groups` with `{ code: 'CN', name: 'Cách nhiệt' }`.
2. Products: `POST /api/products` (`CreateProductRequest`, `ProductDto.cs:64-84`; enums are strings), each with `defaultTaxRate: 10`, `trackInventory: true`:

   | Code | Name | Unit | `pricingMode` | `length` | `width` | `thickness` | `defaultPrice` | `costPrice` |
   |---|---|---|---|---|---|---|---|---|
   | CN-BTT50 | Bông thủy tinh 50mm | cuộn | PerUnit | – | – | – | 465000 | 410000 |
   | CN-BK48 | Băng keo bạc 48mm | cuộn | PerUnit | – | – | – | 32000 | 25000 |
   | CN-EPS50 | Tôn xốp EPS 50mm | m | PerLinearMeter | 6000 | – | – | 235000 | 198000 |
   | CN-PE05 | Tấm cách nhiệt PE OPP 5mm | m² | PerSquareMeter | 2400 | 1200 | 5 | 18500 | 15200 |
   | CN-XPS50 | Xốp XPS cách nhiệt 50mm | m³ | PerCubicMeter | 1200 | 600 | 50 | 2150000 | 1820000 |
   | CN-EPS-NL | Tôn xốp EPS chưa phát sinh | m | PerLinearMeter | 6000 | – | – | 235000 | 198000 |

3. Stock for the vouchers saved in step 10: one `POST /api/stock-vouchers` with `type: 'In'`, `voucherAt` = now (ISO), `warehouseId` of KHO01 (`GET /api/warehouses`), `reasonId` of NKH (`GET /api/stock-reasons`), `acknowledgeNegativeStock: false`, and lines shaped like `toUpsertPayload` (`features/stock-vouchers/payload.ts:38-55`): `sortOrder` 0–4, `warehouseId` of KHO01, `unitPrice` = cost price, `discountRate: 0`, `discountManual: false`, `vatRate: 0`, `note: ''`, and
   - CN-BTT50 and CN-BK48: `quantity: 1000`;
   - CN-EPS50: `sheetCount: 100, length: 6000, quantity: 0`;
   - CN-PE05: `sheetCount: 1000, length: 2400, width: 1200, thickness: 5, quantity: 0`;
   - CN-XPS50: `sheetCount: 100, length: 1200, width: 600, thickness: 50, quantity: 0`.
4. **CN-EPS-NL never gets a saved voucher.** Step 9 changes its pricing mode, and `ProductService.cs:192-200` refuses that once the product has ledger rows ("Hàng hóa đã phát sinh kho: không được đổi cách tính, ĐVT, …").
5. Check: `GET /api/products/search?q=CN-` returns at least the six products of the table (a stock-out walk adds `CN-XPS51` or `CN-XPS51-<n>`), and the stock-in returned 200 (or `setup.json` already holds `stockInId`). `setup.js` is re-runnable: see "Every script is re-runnable" in Task 4.4.

### Task 4.4 — Stock-out walk: mockup 03 steps 1–11

No code change. Script `$BC/walk-stock.js Out`; results in `$BC/results-stock-out.json`; screenshots `browser-check/so-NN-<slug>.png` (one per row below).

Setup: a new context (empty `localStorage`, so the default grid settings), `login`, open `/stock-out/new`, set **Lý do xuất** to "Xuất khác". XKH has `PartnerType.Any` and no debt effect, so no **Đối tượng** is needed (`StockVoucherService.cs:867-879` asks for one only for customer or supplier reasons). **Kho** stays KHO01.

**New voucher = new context.** A new `/stock-out/new` or `/stock-in/new` page restores the unsaved draft of the same user, branch and type from `localStorage` at mount (key `stock_voucher_draft_<type>_<user>_<branch>`, `use-stock-voucher-draft.ts:26-28`, read in `stock-voucher-form-page.tsx:280-283`, written 1.5 s after a change). `/quotations/new` does the same (`quotation_draft_<user>`, `use-quotation-draft.ts:17-20`, read in `quotation-form-page.tsx:290-292`). So every step or probe below that opens a new voucher or quotation (stock steps 9 and 10, the quotation walk, P1–P7) starts with `newContext` and `login`; a new page in the same context would bring back the previous step's rows.

**Every script is re-runnable on the same database** (Task 4.8 step 4 re-runs walks, and scripts are re-run while being debugged):

- `setup.js` looks up before it creates. It reads the group with `GET /api/product-groups` and posts `{ code: 'CN', … }` only when no group has code `CN` (an explicit code that exists returns 409 "Mã nhóm 'CN' đã tồn tại.", `ProductGroupService.cs:104-108`). It reads each product with `GET /api/products/search?q=<code>` and posts it only when no item has exactly that code (`ProductService.cs:134-137` returns 409 "Mã hàng '<code>' đã tồn tại."). Units are get-or-create (`CatalogLookupService.cs:37-44`). It posts the stock-in only when `setup.json` has no `stockInId`. The products are then reused, and their ids are written to `setup.json` either way.
- Row 5 (stock-out) types `CN-XPS51` when `GET /api/products/search?q=CN-XPS51` has no item with that exact code. Otherwise it types `CN-XPS51-<n>`, with the first `n` = 2, 3, … that has no exact match. The name, fields and expected result do not change.
- Row 9 (stock-out and stock-in) starts with `GET /api/products/{id}` of CN-EPS-NL and, when its `pricingMode` is not `PerLinearMeter`, a `PUT` with `pricingMode: 'PerLinearMeter'`, before the pick. The script runs steps 9 and 9b inside `try { … } finally { PUT CN-EPS-NL back to PerLinearMeter }`, so a failed assertion cannot leave it in m².

**Running the scripts** (this applies to Tasks 4.4–4.7). Run every walk and probe script from `$BC` with Bash `run_in_background: true`, or with `timeout: 600000`; a walk takes longer than the 120 s default. In both cases the call starts with the `BC=` line and `cd "$BC" && node <script>`. Delete the script's results file first (`rm -f "$BC/results-stock-out.json"`, in the call that starts the script), or the wait of a re-run returns at once on the old file. Wait with Monitor until the script has exited and its results JSON exists (`until [ -s "$BC/results-stock-out.json" ]; do sleep 5; done`, with the `BC=` line first). Then read the JSON and the `$BC/*.log` files. Never judge a run from a truncated terminal tail. Re-run a script that died half way only after the data reset above.

"Headers" are the `th` texts of `table.stock-grid`; "width" is its inline `style.width`. Read every expected value from the page (`document.activeElement.id`, `input.disabled`, `input.value`, `input.placeholder`, `classList`, `getAnimations()`).

| # | Mockup step | Action | Expected |
|---|---|---|---|
| 1 | Phiếu mới | Click `stock-line-product-code-0`. | Headers include **SL**, not **Số tấm**, **Dài**, **Rộng**, **Dày**; width 1142px; no element has `col-appeared`. |
| 2 | Chọn hàng tính theo cái | Type `CN-BTT50`, click it in the list, type `12`. | Right after the pick the caret is on `stock-line-quantity-0`; headers unchanged; no `col-appeared`. |
| 3 | Thêm dòng, chọn hàng md (suggestion list) | `Insert`; type `CN-EPS50` in `stock-line-product-code-1`, click it. | **Số tấm** and **Dài** appear (not **Rộng**, **Dày**), their `th` and every `td` have `col-appeared`; caret on `stock-line-sheet-count-1`; width 1288px; `stock-line-sheet-count-0` and `stock-line-length-0` are disabled, empty, placeholder "—"; `stock-line-length-1` holds 6000. Then type `8`. |
| 4 | Thêm dòng, chọn hàng m² (full catalog) | `Insert`; type `CN-PE`, click **Xem danh mục đầy đủ**. The product catalog opens with the search "CN-PE" (`initialQuery`, `product-typeahead-cell.tsx:468`); click the CN-PE05 row, then **Chọn sản phẩm này** (`product-catalog-detail.tsx:112-114`). The product catalog has no ↑↓/Enter keys: a row click only selects it (`product-catalog-list.tsx:119`). | **Rộng** appears and flashes; after the dialog closes the caret is on `stock-line-sheet-count-2`; width 1364px; `stock-line-width-0` and `stock-line-width-1` show "—"; `stock-line-width-2` holds 1200. Then type `10`. |
| 5 | Thêm dòng, chọn hàng m³ (quick add) | `Insert`; type `CN-XPS51`, press `F4`. In **Thêm nhanh hàng hóa** fill **Tên hàng hóa** "Xốp XPS cách nhiệt 51mm", **Nhóm hàng hóa** "Cách nhiệt", **Đơn vị tính** "m³", **Loại giá** "Theo m³", **Dài** 1200, **Rộng** 600, **Dày** 50, then click **Lưu và chọn**. | **Dày** appears and flashes; the caret is on `stock-line-sheet-count-3`; width 1428px; `stock-line-thickness-2` (CN-PE05, catalog thickness 5) is disabled and shows "—"; `stock-line-thickness-3` holds 50. Then type `20`. |
| 6 | Ctrl+Delete dòng m³ (ô Dày) | Click `stock-line-thickness-3`, press `Ctrl+Delete`. | The caret is on `stock-line-product-code-2` (the m² row does not use **Dày**) with `aria-expanded="false"`; **Dày** is gone at once, **Rộng** stays; 3 rows; width 1364px; no `col-appeared`. |
| 7 | Đổi hàng dòng m² sang hàng cái | In `stock-line-product-code-2` select all and type `CN-BK`. While the list shows, the headers still include **Rộng**. Click CN-BK48. | **Rộng** is gone at once; the caret is on `stock-line-quantity-2`; **Số tấm** and **Dài** remain (the CN-EPS50 row, index 1, is md). |
| 8 | Xóa dòng md cuối cùng (nút thùng rác) | Click the button named "Xóa dòng 2" (the CN-EPS50 row). | The caret is on `stock-line-product-code-1` (the CN-BK48 row that moved up), list closed; **Số tấm** and **Dài** are gone; 2 rows; width 1142px. |
| 9 | Lỗi từ máy chủ ở cột đang ẩn | New context and `login`, open `/stock-out/new` ("Xuất khác"). Row 1: CN-BTT50, **SL** 12. `Insert`; row 2: CN-EPS-NL, **Số tấm** 8. Then by API: `GET /api/products/{id}` of CN-EPS-NL and `PUT` it back with `pricingMode: 'PerSquareMeter'` and the width still empty. Click `stock-line-vat-rate-1` and set `.stock-grid-wrap` `scrollLeft` to its maximum, so the grid is scrolled to its right end. Press `Ctrl+S`. | The save gets 400 with `lines[1].width` "Chiều rộng phải lớn hơn 0." (`StockVoucherService.cs:733-751`). Header **Rộng** appears (forced by the error) and flashes. `stock-line-width-1` is disabled, empty, placeholder "—", `aria-invalid="true"`, with the message under it; `stock-line-width-0` shows "—". The caret is on `stock-line-product-code-1`, list closed. The rect of `stock-line-width-1` and the rect of its message `stock-line-width-1-error` both lie to the right of the right edge of the pinned **Tên hàng** header and to the left of the left edge of the pinned **Số còn lại** header (the cell and its message are visible, not hidden under the pinned columns; the grid reveals them, Phase 02 Task 2.10 `unusedDimensionCellId`). The URL is still `/stock-out/new`. |
| 9b | Chọn lại hàng sau lỗi | In `stock-line-product-code-1` select all and type `EPS-NL` (not `CN-EPS-NL`). The cell searches on its debounced text (`useProductSearch`, `features/products/hooks.ts:67-74`, 200 ms debounce, key `productKeys.search(text)`). A query whose key does not change is never refetched, however stale. Retyping `CN-EPS-NL` would show the cached `PerLinearMeter` result with no request. `EPS-NL` was never searched in this context (step 9 opened a new one), so it fetches. Before typing, start `page.waitForResponse((r) => r.url().includes('/api/products/search') && new URL(r.url()).searchParams.get('q') === 'EPS-NL')`. Check that the response's CN-EPS-NL item has `pricingMode: 'PerSquareMeter'`, then click CN-EPS-NL in the list. | The message is gone and `stock-line-width-1` has no `aria-invalid`; **Rộng** is still shown (the m² row uses it), and `stock-line-width-1` is enabled and empty; the caret is on `stock-line-sheet-count-1`. Leave without saving, then `PUT` CN-EPS-NL back to `PerLinearMeter`. |
| 10 | Phiếu đã hủy (chỉ xem) | New context and `login`, open `/stock-out/new` ("Xuất khác"): CN-BTT50 **SL** 12; CN-EPS50 **Số tấm** 8; CN-PE05 **Số tấm** 10. `Ctrl+S`. Click **Hủy phiếu** and confirm **Hủy phiếu**. Reload the page. | The body of `POST /api/stock-vouchers` has `lines[2].width: 1200` and `lines[2].thickness: 5` although **Dày** is hidden (decision 4). After the reload the grid is read-only: no **Thêm dòng**, no trash column, every input disabled. Headers **Số tấm**, **Dài**, **Rộng**, no **Dày**. The CN-PE05 row shows 10, 2400, 1200; the CN-EPS50 row shows 8, 6000 and "—" in **Rộng**; the CN-BTT50 row shows "—" in all three. No element has `col-appeared` after the reload (no flash on mount). |
| 11 | Mở "Cột & phím Enter" | On the same voucher click **Cột & phím Enter**. | Under **Hiện cột** the only checkboxes are Kho, SL tồn, Số tiền, %CK, Tiền CK, %VAT, Tiền VAT, Ghi chú, followed by the muted line "Số tấm, Dài, Rộng, Dày tự hiện theo hàng tính md, m², m³ có trong phiếu.". The table **Phím Enter dừng ở (Mã hàng luôn là ô đầu)** still has the columns Kho, Tấm, Dài, Rộng, Dày, SL, Đơn giá, %CK, Tiền CK, %VAT, Ghi chú and the rows Cái, md, m², m³. |

End the script with `assertNo5050`.

### Task 4.5 — Stock-in walk: mockup 03 steps 1–11

No code change. Run `$BC/walk-stock.js In` on `/stock-in/new` with **Lý do nhập** "Nhập khác"; results in `$BC/results-stock-in.json`; screenshots `browser-check/si-NN-<slug>.png`. Every row of the Task 4.4 table applies, with `stock-in` in place of `stock-out`, and these differences:

- Steps 3, 4 and 5 pick by the suggestion list: `CN-EPS50`, `CN-PE05`, `CN-XPS50`. Step 5 expects `stock-line-thickness-3` to hold 50. (The full catalog and quick add paths are covered once, on stock-out.)
- Step 9 resets CN-EPS-NL to `PerLinearMeter` first and wraps steps 9 and 9b in the same `try`/`finally` as on stock-out; it changes the product to `PerSquareMeter` before `Ctrl+S` as on stock-out. Step 9b searches `EPS-NL` as on stock-out (Task 4.4 row 9b), and its row 9 scroll and rect expectations apply too.
- Step 10 saves a "Nhập khác" voucher and cancels it.

Run the script as in "Running the scripts" of Task 4.4 (results file `$BC/results-stock-in.json`); re-runs follow "Every script is re-runnable" there.

### Task 4.6 — Quotation walk: mockup 03 steps 1–8, and the Mã hàng list

No code change. Steps 9–11 do not apply to the quotation: it gets no forced column, has no read-only mode and no **Cột & phím Enter** button. Script `$BC/walk-quote.js`; results in `$BC/results-quote.json`; screenshots `browser-check/q-NN-<slug>.png`. Run it as in "Running the scripts" of Task 4.4.

Setup: a new context, `login` (`admin` has every permission, `quotations.view_cost` included, `DbSeeder.cs:194`), open `/quotations/new`; it starts with one empty line. Nothing is saved, so no **Khách hàng** is needed. "Headers" are the `th` texts of the `table.accounting-grid` under **Chi tiết hàng hóa**; "min-width" is its inline `style.minWidth`, which overrides the `min-width: 1360px` rule at `line-items-grid.css:16-18`.

| # | Mockup step | Action | Expected |
|---|---|---|---|
| 1 | Phiếu mới | Click `line-product-code-0`. | `aria-expanded="false"`; headers #, Mã hàng, Tên hàng, ĐVT, SL, Đơn giá bán, Thành tiền bán, Đơn giá nhập, Thành tiền nhập, Lợi nhuận and the empty action header, with no dimension header; min-width 1136px. |
| 2 | Chọn hàng tính theo cái | Type `CN-BTT50`, click it in the list. | The caret is on `line-name-0`; headers unchanged. |
| 3 | Thêm dòng, chọn hàng md | `Insert`; type `CN-EPS50` in `line-product-code-1`, click it. | **Số tấm** and **Dài** appear between **ĐVT** and **SL** and flash; the caret is on `line-name-1`; min-width 1282px; `line-sheet-count-0` and `line-length-0` are disabled, empty, placeholder "—". Then Enter from `line-name-1` walks `line-unit-1` → `line-sheet-count-1` → `line-length-1` → `line-unit-price-1` (**SL** is computed) → `line-unit-cost-1`. |
| 4 | Thêm dòng, chọn hàng m² | `Insert`; pick CN-PE05. | **Rộng** appears and flashes; the caret is on `line-name-2`; min-width 1358px; `line-width-0` and `line-width-1` show "—". |
| 5 | Thêm dòng, chọn hàng m³ | `Insert`; pick CN-XPS50. | **Dày** appears and flashes; the caret is on `line-name-3`; min-width 1422px; `line-thickness-2` is disabled, empty, "—"; `line-thickness-3` is enabled and empty, because a quotation pick does not copy the catalog dimensions (`line-items-grid.tsx:298-308`, unchanged by this work). Then type `50` in `line-thickness-3`. |
| 6 | Ctrl+Delete dòng m³ (ô Dày) | Click `line-thickness-3`, press `Ctrl+Delete`. Then press Enter. | Before the Enter: the caret is on `line-product-code-2`, `aria-expanded="false"` and no `[role="listbox"]`; **Dày** is gone; 3 rows; min-width 1358px. After the Enter: the caret is on `line-name-2`, and row 2 still holds CN-PE05, its **Tên hàng** and its **Đơn giá bán** (no re-pick). |
| 7 | Đổi hàng dòng m² sang hàng cái | In `line-product-code-2` select all and type `CN-BK48`: the list opens and the headers still include **Rộng**. Click CN-BK48. | **Rộng** is gone at once; the caret is on `line-name-2`; min-width 1282px. |
| 8 | Xóa dòng md cuối cùng (nút thùng rác) | Click the second button named exactly "Xóa dòng" (index 1, CN-EPS50). Then press Enter. | Before the Enter: the caret is on `line-product-code-1` (CN-BK48 moved up), list closed; **Số tấm** and **Dài** are gone; min-width 1136px. After the Enter: the caret is on `line-name-1`; row 1 keeps CN-BK48, its **Tên hàng**, **SL** and **Đơn giá bán**. |

Then the **Mã hàng** list (decision 11), on the same page:

| # | Action | Expected |
|---|---|---|
| M1 | Click into `line-product-code-0` (holds CN-BTT50); wait 600 ms. | `aria-expanded="false"`, no `[role="listbox"]`. Screenshot `q-code-click.png`. |
| M2 | Click `line-unit-cost-0` (the last field of row 0), press Enter, then Enter again. | After the first Enter the caret is on `line-product-code-1` with the list closed; after the second it is on `line-name-1`, and row 1 is unchanged. |
| M3 | In `line-product-code-1` select all and type `CN-BK48`; then press `Esc`. | Typing opens the list (`aria-expanded="true"`, a `listbox` that shows CN-BK48); `Esc` closes it; row 1 is unchanged. Screenshot `q-code-type.png`. |

End the script with `assertNo5050`.

### Task 4.7 — Probes: flash, header contrast, dark mode, reduced motion, scroll anchor

No code change. Script `$BC/probes.js`; results in `$BC/results-probes.json`; run it as in "Running the scripts" of Task 4.4. P1–P5 pick the first m² row on a unit-only voucher, as SUMMARY → Testing → Real browser asks.

- **P1 Flash, light, stock-out.** New context, `/stock-out/new` ("Xuất khác"). Row 1 CN-BTT50, `Insert`, row 2 CN-BK48, `Insert`, row 3: type `CN-PE05` and click it. Right after the click, wait until `document.activeElement.id === 'stock-line-sheet-count-2'` (`page.waitForFunction`; the grid moves the caret in a 0 ms timer, `stock-line-grid.tsx:121-125`), then in one `page.evaluate`:
  - the three new headers (**Số tấm**, **Dài**, **Rộng**) have `col-appeared` and `th.getAnimations().length === 1`;
  - rows 1 and 2 ("—" cells): the disabled input in each of the three new columns has `getAnimations().length === 1` (it paints over its `td`);
  - row 3: the `td` of `stock-line-length-2` and of `stock-line-width-2` (unfocused, enabled) has `getAnimations().length === 1`; `stock-line-sheet-count-2` has the focus and its computed `backgroundColor` is `rgb(255, 248, 220)` (`--qldh-cell-focus-bg` `#fff8dc`, `control-tokens.css:27`, applied by the focus style at `line-items-grid.css:92-95`; compare with the rgb literal, not with `getPropertyValue`, which returns the hex text);
  - record the computed background of one new header and one "—" input;
  - pause every animation at 200 ms (`document.getAnimations().forEach((a) => { a.currentTime = 200; a.pause(); })`), take `browser-check/flash-stock-light.png`, then `a.play()` each.
  1.5 s after the pick: no element inside `table.stock-grid` has `col-appeared` or a running animation, and the header background is `rgb(0, 91, 172)` (`--qldh-brand-header`, `control-tokens.css:25`). Count only the grid's animations (`document.getAnimations().filter((a) => a.effect?.target instanceof Element && a.effect.target.closest('table.stock-grid'))`): `getAnimations()` also returns CSS transitions, and the toolbar buttons have one on hover and focus (`stock-line-grid.css:83`).
- **P2 Header contrast.** Repeat P1 in a new context. From the pick, sample the computed `backgroundColor` and `color` of the new **Rộng** header on every animation frame for 1.3 s and compute the WCAG contrast ratio of each pair. Pass when the minimum is at least 4.5:1. Record the minimum: about 5.0:1 is expected at the start (white on `#b45309`), rising to about 6.8:1 at rest (`#005bac`). The text colour must stay white in every sample.
- **P3 Dark mode.** The app has no theme switch: Tailwind uses the `dark` class (`tailwind.config.ts:5`) and the dark tokens live under `.dark` (`index.css:61`), but no code adds that class. Repeat P1 and P2 in a new context, after `login`, `/stock-out/new` and `page.evaluate(() => document.documentElement.classList.add('dark'))`. Same expectations: the header colours are the same in both themes, and the cells flash from the dark `--nav-match` (`index.css:92`). Screenshot `flash-stock-dark.png`.
- **P4 Reduced motion.** New context with `reducedMotion: 'reduce'`; repeat the P1 pick. The new headers and cells still have `col-appeared`, but no animation targets an element inside `table.stock-grid` (the filter of P1) and the header background is `rgb(0, 91, 172)` right after the pick. Screenshot `flash-stock-reduced.png`.
- **P5 Flash, quotation.** New context, `/quotations/new`: rows CN-BTT50 and CN-BK48, `Insert`, pick CN-PE05. Same checks as P1 (animations filtered to `table.accounting-grid` under **Chi tiết hàng hóa**), except that the caret is on `line-name-2`, so the `td` of all three new cells of row 3 flash. Screenshot `flash-quote-light.png`.
- **P6 Scroll anchor, stock-out.** New context, `/stock-out/new`. Rows CN-BTT50, CN-EPS50 (**Số tấm** 8), CN-PE05 (10), CN-XPS50 (20); at 1366 px the 1428 px table scrolls. Click `stock-line-vat-rate-3`, record the `scrollLeft` of `.stock-grid-wrap`, press `Ctrl+Delete`. The caret is on `stock-line-vat-rate-2`; **Dày** is gone; the focused input's rect lies between the right edge of the pinned **Tên hàng** header and the left edge of the pinned **Số còn lại** header. Record `scrollLeft` after. Screenshot `scroll-stock.png`.
- **P7 Scroll anchor, quotation.** New context, `/quotations/new`. Rows CN-BTT50, CN-EPS50, CN-PE05, CN-XPS50 (min-width 1422px). Click `line-unit-cost-3`, press `Ctrl+Delete`. The caret is on `line-unit-cost-2`, and its rect lies inside the rect of `.accounting-grid-wrap`. Screenshot `scroll-quote.png`.

End the script with `assertNo5050`.

A row fails when the value measured on the page differs from its Expected cell. When a row fails, or the script throws, first re-run its segment from the segment's first step in a new context:

- stock walks: steps 1–8, steps 9–9b, steps 10–11;
- quotation: steps 1–8 followed by M1–M3;
- probes: each probe on its own.

Before a re-run, set up the data again (Task 4.4, "Every script is re-runnable"): on stock-out the script picks the next free quick add code itself (Task 4.4 row 5: `CN-XPS51-2`, `CN-XPS51-3`, …; a hand re-run uses the same scheme; `POST /api/products` refuses a used code with "Mã hàng '…' đã tồn tại.", `ProductService.cs:137`), and before a step 9 re-run `GET /api/products/{id}` of CN-EPS-NL and `PUT` it back to `PerLinearMeter` if it is not.

Read the request log of the re-run, then classify the failure:

- (a) The script or the test data is at fault (a selector, a wait, a missing row, a reused code, a product in the wrong mode). This is not a defect. Fix the script or the data, re-run, and record it under "Script and data corrections" in `$BC/defects.md` with the PASS re-run.
- (b) The Expected cell contradicts a sentence of the brainstorm, or a test or task of Phases 01–03, and the measured value matches that sentence or test. Correct the Expected cell in this file, and record the old value, the new value and the quote under "Plan corrections" in `$BC/defects.md` with the PASS re-run. If no such quote exists, it is not (b).
- (c) Anything else is a defect `D<n>` for Task 4.8. Write its repro, expected and actual result at once into `$BC/defects.md`.

### Task 4.8 — Fix a defect found in the browser (only when a check failed)

Run this task once per defect, that is, per failure classified (c) in Task 4.7 (a failure classified (a) or (b) is not a defect and does not come here). If every check passed or every failure was (a) or (b), skip it; Task 4.9 then writes "Defects: none" in `BROWSER-CHECK.md`. `BROWSER-CHECK.md` does not exist yet: this task writes only to `$BC/defects.md`, and Task 4.9 copies that file into the report.

1. **Write the failing test.** Put it in the test file that owns the behaviour, and use that file's harness:

   | Defect area | Test file | Harness to use |
   |---|---|---|
   | Stock grid: columns, caret after a pick or delete, "—" cells, flash class, `scrollIntoView` call, forced error column, error clearing on re-pick | `src/pages/stock-vouchers/components/stock-line-grid.test.tsx` | `renderGrid({ type, lines, readOnly })`, `line({...})`, `headers()`, `byId(id)`, `pickFromTypeahead(code)`, `outsideAct(steps)`, fixtures `sheet` (m²) and `listItem` (m³), the settable quick add product from Phase 02, `vi.spyOn(Element.prototype, 'scrollIntoView')` |
   | Stock form page: the focus after a failed save | `src/pages/stock-vouchers/stock-voucher-form-page.test.tsx` | the fixtures of the server-error tests near `:2566-2591`, with `updateMock` rejected |
   | Quotation grid | `src/pages/quotations/components/line-items-grid.test.tsx` | `renderGrid(lines)`, `setCostPermission(enabled)`, `cellProps.byId[...]`, fixtures with `_uiKey` (Phase 03) |
   | Quotation **Mã hàng** list or re-pick | `src/pages/quotations/components/line-items-grid.typeahead.test.tsx` | the real typeahead with mocked `@/features/products/hooks` (Phase 03) |
   | The rule or the flash hook | `src/lib/dimension-columns.test.ts`, `src/lib/use-appeared-keys.test.ts` | pure calls; fake timers as in `src/lib/use-debounced-value.test.ts` |

   Name the test `'<what the browser showed> (browser check D<n>)'`. It replays the failed browser steps (same row modes, same keys and clicks) and asserts the "Expected" cell of the walk table. On a pick or delete path, also assert the column headers: a caret-only assertion passes on a grid that shows the wrong columns. Do not switch on fake timers in the grid tests (RTL's `waitFor` does not advance them); when the flash must end, use `waitFor(…, { timeout: 2000 })`.
2. **Run it and check it fails:** `npx vitest run <test file> -t "browser check D<n>"`. Expected: FAIL on the assertion that matches the browser's wrong result (for example `expected 'stock-line-unit-price-3' to be 'stock-line-sheet-count-3'`). Copy the failure line under D<n> in `$BC/defects.md`. If the test passes, it does not reproduce the defect: change the test, for example run the steps with `outsideAct` as a browser does, and leave the code alone. If the test still passes when it replays the browser's exact steps with `outsideAct`, return to the Task 4.7 classification: the cause is (a) or (b), not a code defect.
   Exception: jsdom cannot show colours, the animation itself, reduced motion, measured layout or scroll position, or the real focus loss of an input that becomes disabled (vitest runs with `css: false`, `vitest.config.ts`). For such a defect no red test is possible: skip steps 1–2, say why in the commit body, and rely on the browser re-check in step 4.
3. **Write the minimal implementation** in the file that owns the behaviour (`stock-line-grid.tsx`, `stock-voucher-form-page.tsx`, `use-stock-grid-prefs.ts`, `line-items-grid.tsx`, `line-items-grid.css`, `lib/dimension-columns.ts`, `lib/use-appeared-keys.ts` or `styles/control-tokens.css`). Keep the names and signatures of the shared contract (plan SUMMARY, Phase 01).
4. **Run the tests and check they pass:** `npx vitest run src/lib src/pages/stock-vouchers src/pages/quotations`, then `npm run typecheck`. Re-run the failed segment of the walk or the probe in the browser, after the data reset of Task 4.7 (Vite has reloaded the change) and add a line "Re-check on `<sha>`: PASS" under D<n> in `$BC/defects.md`.
5. **Commit**, in one chained line: `cd "$WT" && git add <test file> <changed source files> && git commit -m "fix(stock-vouchers): <defect>"` (or `fix(quotations): …`, `fix(lib): …`). The `git add` paths are relative to `$WT`, so `frontend/src/…`.

### Task 4.9 — BROWSER-CHECK.md, cleanup and commit

No code change.

1. Write `docs/plans/261016-0249-dimension-columns-by-pricing-mode/BROWSER-CHECK.md`, modelled on `docs/plans/archived/261015-2038-product-quick-add/BROWSER-CHECK.md`:
   - header: date and time, the code under test (`git -C "$WT" rev-parse --short HEAD`, and that `git status --short` was clean at the start), browser (Edge headless, `playwright-core` 1.64.1), the context options (`serviceWorkers: 'block'`, locale, time zone, viewport 1366×768), the scripts in `$BC` (not committed);
   - a summary table: stock-out 1–11 and 9b, stock-in 1–11 and 9b, quotation 1–8, M1–M3, P1–P7, each PASS or FAIL;
   - isolation proof: the database list and listeners before the run, the API and Vite PIDs and ports, 0 occurrences of `:5050` in every request log, and that `qldonhang_test`, `qldonhang` and the shared `qldonhang_integtest*` were never connected to;
   - the data of Task 4.3;
   - one table per walk (step, action, expected, measured, evidence file), and the probe measurements (animation counts, contrast minimum per theme, `scrollLeft` before and after);
   - defects, copied from `$BC/defects.md`: repro, expected, actual, the failing test line, cause, fix commit, re-check (or "Defects: none" when that file does not exist);
   - script and data corrections and plan corrections, copied from `$BC/defects.md`, each with its PASS re-run (or "none");
   - cleanup.
2. Stop the API and Vite (PowerShell): `Get-NetTCPConnection -LocalPort 5061,5191 -State Listen | Select-Object -ExpandProperty OwningProcess -Unique | ForEach-Object { Stop-Process -Id $_ -Confirm:$false }`, then stop the two background shells (TaskStop). `netstat -ano | grep LISTENING | grep -E ':(5061|5191) '` prints nothing; port 5050 shows the same PID as in `isolation-proof.txt`, or nothing if it was free.
3. Drop the database (the call starts with `export PGPASSWORD=1; PSQL="/c/Program Files/PostgreSQL/18/bin/psql"`): `"$PSQL" -h localhost -U postgres -c 'drop database "qldonhang_integtest_dimcols" with (force)'`; then `"$PSQL" -h localhost -U postgres -tAc "select count(*) from pg_database where datname = 'qldonhang_integtest_dimcols'"` prints `0`.
4. `WT=/D/Projects/QLBanHang/.claude/worktrees/dimension-columns; git -C "$WT" status --short` lists only `BROWSER-CHECK.md` and `browser-check/` (backend `bin/` and `obj/` are ignored, `.gitignore:15-16`).
5. Commit:
   ```bash
   WT=/D/Projects/QLBanHang/.claude/worktrees/dimension-columns
   cd "$WT" && git add docs/plans/261016-0249-dimension-columns-by-pricing-mode/BROWSER-CHECK.md docs/plans/261016-0249-dimension-columns-by-pricing-mode/browser-check && git commit -m "docs(plan): dimension columns real-browser check"
   ```

### Task 4.10 — Last sync before the help commits

No code change. A merge commit made after the help commits can flag the guides again, so merge first.

1. From `$WT` (`WT=…; cd "$WT" && git fetch qlbanhang`, then `git log --oneline HEAD..qlbanhang/main`). If it prints nothing, go to Task 4.11.
2. Otherwise `git merge --no-edit qlbanhang/main` (same call, after the `cd`), then from `$WT/frontend` `npm run typecheck` and `npm run test -- --testTimeout=30000 --maxWorkers=3` (only the failures in `BASELINE.md`).
3. If the merge changed any file under `frontend/src/pages/stock-vouchers`, `frontend/src/pages/quotations`, `frontend/src/lib`, `frontend/src/styles`, `frontend/src/index.css` or `frontend/src/components/ui` (`git diff --stat HEAD^1 HEAD -- <those paths>`), redo Tasks 4.2–4.9 on the merged tip and add a section "Re-check after merge `<sha>`" to `BROWSER-CHECK.md`.
4. Run `npm run help:stale` and note its output for Tasks 4.11–4.14.

### Task 4.11 — Help: stock-in and stock-out

Help files are not code: no red test; the check is the help tests plus `help:stale`. Invoke the `help-content` skill first and follow it. `videos.json` is `{}`, and no step changes, so nothing needs re-recording.

1. Run `npm run help:stale`. Expected before the help commits: `stock-in.form`, `stock-out.form`, `quotations.form`, `inventory-opening-stock`, `inventory-stock-card`, plus the base's `admin-dashboard`, `home`, `reports-revenue` (`BASELINE.md`). Check that the two new lib files are tracked: `git ls-files -- ':(top)frontend/src/lib/dimension-columns.ts' ':(top)frontend/src/lib/use-appeared-keys.ts'` prints both.
2. `frontend/src/features/help/content/stock-in.form.md`:
   - line 5 (`sources`): replace the end `frontend/src/lib/use-dialog-opening.ts]` with `frontend/src/lib/use-dialog-opening.ts, frontend/src/lib/dimension-columns.ts, frontend/src/lib/use-appeared-keys.ts]`;
   - line 7: `updated: 2026-10-15` becomes `updated: <today>` (`date +%Y-%m-%d`);
   - keep line 33 ("- Hàng tính theo mét (m dài, m², m³) nhập **Số tấm** và **Dài**, **Rộng**, **Dày** theo loại hàng; **SL** tự tính, không nhập tay.") and insert right after it, before line 34 ("- Ở tab **Thanh toán**, …"):
     ```
     - Các cột **Số tấm**, **Dài**, **Rộng**, **Dày** chỉ hiện khi phiếu có hàng tính theo m dài, m² hoặc m³, theo loại cao nhất trong phiếu: m dài hiện **Số tấm** và **Dài**, m² thêm **Rộng**, m³ thêm **Dày**. Ô mà dòng không dùng hiện —. Nếu lúc lưu có lỗi ở một cột đang ẩn, cột đó hiện ra và lỗi ghi dưới ô.
     - Trong **Cột & phím Enter**, mục **Hiện cột** không có bốn cột này vì chúng tự hiện theo hàng có trong phiếu.
     ```
3. `frontend/src/features/help/content/stock-out.form.md`:
   - line 5 (`sources`): the same replacement as in stock-in;
   - line 7: `updated: 2026-10-15` becomes `updated: <today>`;
   - insert between line 31 ("- Vượt hạn mức nợ: …") and line 32 ("- **HTTT** không gắn với quỹ thì **Số tiền thanh toán** khóa ở 0 và không có phiếu thu."):
     ```
     - Hàng tính theo mét (m dài, m², m³) nhập **Số tấm** và **Dài**, **Rộng**, **Dày** theo loại hàng; **SL** tự tính, không nhập tay.
     - Các cột **Số tấm**, **Dài**, **Rộng**, **Dày** chỉ hiện khi phiếu có hàng tính theo m dài, m² hoặc m³, theo loại cao nhất trong phiếu: m dài hiện **Số tấm** và **Dài**, m² thêm **Rộng**, m³ thêm **Dày**. Ô mà dòng không dùng hiện —. Nếu lúc lưu có lỗi ở một cột đang ẩn, cột đó hiện ra và lỗi ghi dưới ô.
     - Trong **Cột & phím Enter**, mục **Hiện cột** không có bốn cột này vì chúng tự hiện theo hàng có trong phiếu.
     ```
   The "Các cột …" bullet is identical in both files (the stock-in and stock-out text must stay identical). Its last sentence covers the forced column: on the stock grid a dimension error forces its column into view even when the highest mode would hide it (`dimensionErrorFields`; the browser walk step 9). The quotation bullet of Task 4.12 has no such sentence, because the quotation has no forced columns. The new sentence adds no bold label, so the grep in step 4 does not change.
   Both files stay at 7 steps, the parser's maximum (`parse-help.ts:145`); all new text is in **Lưu ý**.
4. Every bold label is real screen text:
   ```bash
   for l in "header: 'Số tấm'" "header: 'Dài'" "header: 'Rộng'" "header: 'Dày'" "header: 'SL'"; do git grep -nF "$l" -- ':(top)frontend/src/pages/stock-vouchers/components/stock-line-grid.tsx'; done
   git grep -nF 'Cột &amp; phím Enter' -- ':(top)frontend/src/pages/stock-vouchers/components/stock-grid-settings-popover.tsx'
   git grep -nF 'Hiện cột' -- ':(top)frontend/src/pages/stock-vouchers/components/stock-grid-settings-popover.tsx'
   git grep -nF 'tự hiện theo hàng tính md, m², m³ có trong phiếu' -- ':(top)frontend/src/pages/stock-vouchers/components/stock-grid-settings-popover.tsx'
   ```
   Each command prints at least one line (at `e61e4f1b` the headers are at `stock-line-grid.tsx:72-76`, the button at `stock-grid-settings-popover.tsx:32`, **Hiện cột** at `:37`; the note line comes from Phase 02). The source writes the button's ampersand as `&amp;`, so grep the entity.
5. `npm run test -- --testTimeout=30000 --maxWorkers=3 src/features/help` passes.
6. Commit:
   ```bash
   WT=/D/Projects/QLBanHang/.claude/worktrees/dimension-columns
   cd "$WT" && git add frontend/src/features/help/content/stock-in.form.md frontend/src/features/help/content/stock-out.form.md && git commit -m "docs(help): dimension columns on the stock voucher screens"
   ```
7. `npm run help:stale` no longer lists `stock-in.form` or `stock-out.form`.

### Task 4.12 — Help: quotation form

Help file: no red test. Use the `help-content` skill.

1. `frontend/src/features/help/content/quotations.form.md`:
   - line 5 (`sources`): replace the end `frontend/src/lib/use-dialog-opening.ts]` with `frontend/src/lib/use-dialog-opening.ts, frontend/src/lib/dimension-columns.ts, frontend/src/lib/use-appeared-keys.ts]`;
   - line 7 is `updated: 2026-10-16`. If today (`date +%Y-%m-%d`) is 2026-10-16, leave it; otherwise set today;
   - first replace line 30 ("- Hàng tính theo m, m² hoặc m³: **SL** tự tính từ **D × R × Dày × Tấm**, các ô không dùng bị khóa.") with these two bullets:
     ```
     - Hàng tính theo m dài, m² hoặc m³: nhập **Số tấm** và **Dài**, **Rộng**, **Dày** theo loại hàng, **SL** tự tính. Các cột **Số tấm**, **Dài**, **Rộng**, **Dày** chỉ hiện khi báo giá có hàng tính theo m dài, m² hoặc m³, theo loại cao nhất trong báo giá (như phiếu nhập, xuất kho): m dài hiện **Số tấm** và **Dài**, m² thêm **Rộng**, m³ thêm **Dày**. Ô mà dòng không dùng hiện —.
     - Xóa dòng bằng `Ctrl Delete` đưa con trỏ sang dòng bên cạnh: cùng ô nếu dòng đó dùng ô ấy, nếu không thì ô **Mã hàng**; xóa bằng nút thùng rác đưa con trỏ về ô **Mã hàng** của dòng bên cạnh.
     ```
     The second bullet is the brief's sentence word for word (SUMMARY → Help and docs; section 02 V14): the trash button always sends the caret to **Mã hàng**, so it must not share the "cùng ô" rule of `Ctrl Delete`;
   - then insert after line 28 ("- Muốn nhập dòng hàng chưa có trong danh mục, gõ mã rồi bấm `Esc` để đóng danh sách; Enter sẽ sang **Tên hàng**.", which stays true; the replacement above is below it, so line 28 has not moved):
     ```
     - Con trỏ vào ô **Mã hàng** đã có mã không tự mở danh sách gợi ý; gõ để tìm lại.
     ```
   The trash button has only the aria-label "Xóa dòng", so the guide calls it "nút thùng rác" without bold. The shortcut line "`Ctrl Delete` Xóa dòng đang chọn" stays.
2. Every bold label is real screen text. In `line-items-grid.tsx` the four dimension labels must be column headers (the column list's header strings or `<th>` text), not only the `aria-label` the inputs already had at `e61e4f1b` (`:365`, `:379`, `:393`, `:407`):
   ```bash
   git grep -nE "'(Số tấm|Dài|Rộng|Dày|SL|Mã hàng)'|<th[^>]*>(Số tấm|Dài|Rộng|Dày|SL|Mã hàng)<" -- ':(top)frontend/src/pages/quotations/components/line-items-grid.tsx'
   ```
   Every one of the six labels appears in a header position. If Phase 03 wrote the headers another way, read the column list and confirm by eye.
3. `npm run test -- --testTimeout=30000 --maxWorkers=3 src/features/help` passes.
4. Commit:
   ```bash
   WT=/D/Projects/QLBanHang/.claude/worktrees/dimension-columns
   cd "$WT" && git add frontend/src/features/help/content/quotations.form.md && git commit -m "docs(help): quotation dimension columns, delete focus and code cell list"
   ```
5. `npm run help:stale` no longer lists `quotations.form`.

### Task 4.13 — Help: opening stock and stock card re-check

Help files: no red test. Both guides list `frontend/src/pages/quotations/components/product-typeahead-cell.tsx` in `sources`; this work changes only its prop comment at line 24, and their screens do not change.

1. Check what changed in their sources on this branch:
   ```bash
   WT=/D/Projects/QLBanHang/.claude/worktrees/dimension-columns
   cd "$WT" && B=$(git merge-base HEAD qlbanhang/main)
   git diff $B HEAD -- ':(top)frontend/src/pages/quotations/components/product-typeahead-cell.tsx'
   git diff --stat $B HEAD -- ':(top)frontend/src/pages/inventory/opening-stock-page.tsx' ':(top,glob)frontend/src/features/opening-stock/**' ':(top,glob)backend/src/OrderMgmt.Application/Inventory/OpeningStocks/**' ':(top)frontend/src/pages/stock-vouchers/components/negative-stock-dialog.tsx' ':(top)frontend/src/lib/use-dialog-opening.ts' ':(top)frontend/src/features/products/components/product-quick-add-dialog.tsx' ':(top)frontend/src/features/products/components/product-form-fields.tsx' ':(top)frontend/src/features/products/split-typed-text.ts' ':(top)frontend/src/pages/inventory/stock-card-page.tsx' ':(top,glob)frontend/src/features/inventory-reports/**' ':(top,glob)frontend/src/components/period-preset-select/**' ':(top,glob)frontend/src/components/tt133-export/**' ':(top)frontend/src/components/layout/back-link.tsx' ':(top,glob)backend/src/OrderMgmt.Application/Inventory/Reports/**'
   ```
   The second command lists every other `sources` entry of the two guides (their frontmatter line 5). Expected: the first diff changes only the comment "Mặc định true (báo giá giữ nguyên)" at line 24; the second prints nothing. The base no longer flags `inventory-stock-card` (PR #31 re-committed both guides in `eaddfa4c`, `updated: 2026-10-16`); `help:stale` lists the two guides only because of this branch's comment commit to `product-typeahead-cell.tsx`.
2. Re-read each guide against its screen. Since PR #31 (`438b0db4`) opening stock and the stock card pass `openOnFocus={false}`:
   - `inventory-opening-stock.md`: step 2 "Bấm **Thêm dòng**, rồi gõ mã hoặc tên hàng ở cột **Mã hàng** và chọn trong danh sách gợi ý." (`opening-stock-page.tsx:315`), the line-20 bullet "Bấm vào ô **Mã hàng** chưa mở danh sách gợi ý; …" (the page passes `openOnFocus={false}`, `:252`, from PR #31) and the `F4` bullet at line 28 (`allowQuickAdd`, `:251`).
   - `inventory-stock-card.md`: step 1 "Gõ mã hoặc tên vào ô **Hàng hóa** rồi chọn một mặt hàng trong danh sách gợi ý hiện ra." and the line-21 bullet "Bấm vào ô **Hàng hóa** đã có mã chưa mở danh sách gợi ý; …" (`stock-card-page.tsx:156-158`).
   If step 1 showed only the comment change, no sentence changes. If it showed more, rewrite each sentence that no longer matches the code and treat that file as changed in step 3.
3. Both files have `updated: 2026-10-16` (PR #31). Per file:
   - a sentence changed in step 2, or today is later than 2026-10-16: set `updated: <today>`;
   - the text matches and `updated` already equals today: do not touch the file (a dummy edit is not a fix, and `updated` cannot move twice on one day). Add it under "PR notes - Help" in `docs/plans/261016-0249-dimension-columns-by-pricing-mode/EXECUTION-REPORT.md` as "Help: `<key>` — flagged only by this branch's comment commit to `product-typeahead-cell.tsx`; text re-checked, still accurate; `updated` already today" (create the file if it does not exist; Task 4.14 step 3 explains where it is committed), and tell the user.
   If neither file changed, skip steps 4 and 5.
4. `npm run test -- --testTimeout=30000 --maxWorkers=3 src/features/help` passes.
5. Commit, in one chained line (only the files that changed):
   ```bash
   WT=/D/Projects/QLBanHang/.claude/worktrees/dimension-columns
   cd "$WT" && git add frontend/src/features/help/content/inventory-opening-stock.md frontend/src/features/help/content/inventory-stock-card.md && git commit -m "docs(help): re-check opening stock and stock card after the product cell change"
   ```
6. `npm run help:stale` lists none of the five guides of this work, except a guide left under step 3's same-day case. If it lists another guide because of this branch (for example `quotations.list` after a change under `frontend/src/features/quotations/**`), re-check and commit it the same way.

### Task 4.14 — help:stale: the guides the base already flags

Help files: no red test. `BASELINE.md` (Phase 00) records the base's 3 warnings: `admin-dashboard`, `home` and `reports-revenue` (from PR #30: merge `bb2af827` and fix `242a4baf`; `inventory-stock-card` is no longer flagged on `438b0db4`). These screens are not part of this work, but the SUMMARY scope names their guides. Task 4.13 handles the two inventory guides that this branch's own comment commit flags.

1. For each guide, diff its `sources` (its frontmatter line 5, each glob as `':(top,glob)<glob>'`) from its last help commit to `HEAD`, and read the diff:
   - `admin-dashboard` and `home` (help commit `33be6756`, `updated: 2026-10-16`): at `e61e4f1b` the diff is only `frontend/src/features/dashboard/use-dashboard-params.ts` and its test (`4678cd2c`, brought in by `bb2af827`): a period or sale change now replaces the history entry and keeps its state;
   - `reports-revenue` (help commit `cf1c9b68`, `updated: 2026-10-15`): at `e61e4f1b` the diff is `revenue-page.tsx`, `revenue-area-chart.tsx` and `features/dashboard/hooks.ts`. `revenue-page.tsx` now reads its own endpoints through `@/features/reports/revenue/hooks` (`700d1c26`; `revenue-page.tsx:22-26`), and `revenue-area-chart.tsx` no longer passes the count to the chart (`700d1c26`). `features/dashboard/hooks.ts` gained the live refresh and the 60 s fallback (`d63ba005`, `242a4baf`), but neither the page nor its components import it any more (`revenue-page.tsx:18-33` imports only components, `use-dashboard-params`, `types` and `format` from `features/dashboard`), so that refresh is not on this screen: do not describe it in this guide. `use-dashboard-params.ts` is not in this diff: `4678cd2c` is older than `cf1c9b68`.
   ```bash
   git diff 33be6756 HEAD -- ':(top)frontend/src/pages/admin/admin-dashboard-page.tsx' ':(top)frontend/src/pages/dashboard-page.tsx' ':(top,glob)frontend/src/features/dashboard/**' ':(top)frontend/src/hooks/use-dashboard-signal.ts' ':(top)backend/src/OrderMgmt.Application/Sales/Quotations/Services/DashboardService.cs' ':(top)backend/src/OrderMgmt.Application/Dashboards/Common/SalesRevenueSource.cs'
   git diff cf1c9b68 HEAD -- ':(top)frontend/src/pages/reports/revenue-page.tsx' ':(top)frontend/src/features/dashboard/components/range-picker.tsx' ':(top)frontend/src/features/dashboard/components/kpi-card.tsx' ':(top)frontend/src/features/dashboard/components/revenue-area-chart.tsx' ':(top)frontend/src/features/dashboard/components/top-list-card.tsx' ':(top)frontend/src/features/dashboard/use-dashboard-params.ts' ':(top,glob)frontend/src/features/reports/sales-revenue-detail/**' ':(top)frontend/src/components/layout/back-link.tsx' ':(top)frontend/src/features/dashboard/hooks.ts' ':(top,glob)backend/src/OrderMgmt.Application/Reports/SalesRevenue/**'
   ```
   Then read every sentence of the guide against the current page and its labels (help-content skill).
2. `reports-revenue.md` also lacks sources: its page reads `frontend/src/features/reports/revenue/**`, which calls `/reports/revenue/*` (`features/reports/revenue/api.ts:7-16`) served by `backend/src/OrderMgmt.Application/Reports/QuotationRevenue/**`. Add both globs to its `sources`, and remove `frontend/src/features/dashboard/hooks.ts`, which the screen no longer uses (step 1) and which would flag the guide on every overview dashboard change. Check that each glob matches tracked files (`git ls-files -- ':(top,glob)frontend/src/features/reports/revenue/**' ':(top,glob)backend/src/OrderMgmt.Application/Reports/QuotationRevenue/**'` prints 4 frontend and 3 backend files at `e61e4f1b`), fix any sentence that no longer matches, and set `updated` to today.
3. `admin-dashboard.md` and `home.md`:
   - a sentence that no longer matches: fix it and set `updated` to today;
   - the text still matches and `updated` is older than today: set `updated` to today (the re-check is the change);
   - the text still matches and `updated` already equals today: do not touch the file. A dummy edit is not a fix, and `updated` cannot move again on the same day. Add it under a heading "PR notes - Help" in `docs/plans/261016-0249-dimension-columns-by-pricing-mode/EXECUTION-REPORT.md` as "baseline warning from merge commit `bb2af827` (PR #30); text re-checked, still accurate". Create the file now if it does not exist; it is in the plan folder, and Task 4.15 step 6 allows it untracked. Commit it with the Task 4.15 step 8 commit, and tell the user. The PR body copies that section.
4. `npm run test -- --testTimeout=30000 --maxWorkers=3 src/features/help` passes.
5. Commit the guides you changed, separately from this work's guides. Always `reports-revenue.md`; add `admin-dashboard.md` and `home.md` only when step 3 changed them:
   ```bash
   WT=/D/Projects/QLBanHang/.claude/worktrees/dimension-columns
   cd "$WT" && git add frontend/src/features/help/content/reports-revenue.md && git add frontend/src/features/help/content/admin-dashboard.md frontend/src/features/help/content/home.md && git commit -m "docs(help): re-check guides flagged by the PR #30 merge"
   ```
   Drop the second `git add` from the chain when step 3 changed neither `admin-dashboard.md` nor `home.md` (add only the one it changed).
6. `npm run help:stale` prints `Không có cảnh báo.`, or lists only guides from step 3's last case (here or in Task 4.13 step 3), each named under "PR notes - Help" in `EXECUTION-REPORT.md`. Task 4.15 step 6 allows that untracked file until step 8.

### Task 4.15 — Final checks

No code change.

First, from `$WT`: `WT=…; cd "$WT" && git fetch qlbanhang`, then `git log --oneline HEAD..qlbanhang/main`. If `qlbanhang/main` moved after Task 4.10, merge it now (`git merge --no-edit qlbanhang/main`), run `npm run typecheck` and the full suite as in Task 4.10, redo the browser check if Task 4.10 step 3 says so, then run `npm run help:stale`: a merge made after the help commits can flag a guide again (as `bb2af827` did on the base). Re-check every guide it lists as in Tasks 4.11–4.14 and commit those help files again, after the merge (a guide whose text matches and whose `updated` already equals today is not touched; name it under "PR notes - Help" in `EXECUTION-REPORT.md` as in Task 4.13 step 3).

Then, from `$WT/frontend`:

```bash
WT=/D/Projects/QLBanHang/.claude/worktrees/dimension-columns
cd "$WT/frontend" && npm run help:stale
npm run test -- --testTimeout=30000 --maxWorkers=3 src/features/help
npm run typecheck
npm run build
git checkout -- tsconfig.app.tsbuildinfo
npm run lint
npm run test -- --testTimeout=30000 --maxWorkers=3
```

1. `help:stale`: as in Task 4.14 step 6 (it may list a guide left under Task 4.13 step 3's or Task 4.14 step 3's same-day case; every other warning is a failure).
2. The help tests pass.
3. `typecheck` passes. `build` succeeds (it is the only check that parses the CSS Phase 03 edited), and the `git checkout -- tsconfig.app.tsbuildinfo` after it restores the file `build` rewrites, before step 6.
4. `lint`: the baseline only (2 errors, `components/ui/table.tsx:75` and `product-catalog-list.tsx:51`, and the 15 warnings listed in `BASELINE.md`; compare by file and rule, not by count alone); no new error or warning.
5. Full suite: no failure beyond `BASELINE.md` (under load: bank-accounts-tab "adds a new account", useNotificationHub "invalidates unread-count", payment-qr-page "generates and displays a QR"). A test that times out counts only if it also fails alone.
6. From `$WT`: `git status --short` is empty, except the untracked `docs/plans/261016-0249-dimension-columns-by-pricing-mode/EXECUTION-REPORT.md` when Task 4.13 step 3 or Task 4.14 step 3 created it (restore a rewritten `frontend/tsconfig.app.tsbuildinfo` with `git checkout -- frontend/tsconfig.app.tsbuildinfo`); `git log --oneline $(git merge-base HEAD qlbanhang/main)..HEAD -- frontend/tsconfig.app.tsbuildinfo` prints nothing; `git diff --numstat $(git merge-base HEAD qlbanhang/main)` and the same with `--ignore-cr-at-eol` give the same counts.
7. Commit order: after the last merge on the branch, no code commit follows the first help commit. Check it by path, not by message prefix (a code commit may also be a `chore`, as `231dfd58` was). From `$WT`:
   ```bash
   WT=/D/Projects/QLBanHang/.claude/worktrees/dimension-columns
   cd "$WT" && B=$(git merge-base HEAD qlbanhang/main)
   M=$(git log -1 --merges --format=%H $B..HEAD); [ -n "$M" ] || M=$B
   H=$(git log --reverse --format=%h $M..HEAD -- frontend/src/features/help/content | head -1)
   git log --format= --name-only $H..HEAD | sort -u
   ```
   The last command lists only files under `frontend/src/features/help/content/` and `docs/plans/261016-0249-dimension-columns-by-pricing-mode/`. `H` is empty only when the merge in the first paragraph above came after every help commit and `help:stale` then flagged no guide; in that case skip the last command, because step 1 is the check.
8. Set **Status:** of this file to `[x] done (<today>)`, tick all five lines (Phases 00–04) of the plan SUMMARY's Phases list (the only place that list is ticked; SUMMARY → Execution notes), and commit both, in the worktree copy, as one chained line:
   ```bash
   WT=/D/Projects/QLBanHang/.claude/worktrees/dimension-columns
   cd "$WT" && git add docs/plans/261016-0249-dimension-columns-by-pricing-mode/phase-04-docs-browser-help.md docs/plans/261016-0249-dimension-columns-by-pricing-mode/SUMMARY.md && git commit -m "docs(plan): phase 04 done"
   ```
   If Task 4.13 step 3 or Task 4.14 step 3 created `EXECUTION-REPORT.md`, add `docs/plans/261016-0249-dimension-columns-by-pricing-mode/EXECUTION-REPORT.md` to the same `git add` (the file holds the "PR notes - Help" section so far). These plan files are in no guide's `sources`, so the commit does not flag a guide.

### Task 4.16 — Execution report and archive

No code change. Run after Task 4.15 steps 7 and 8, because the step 7 check accepts only help content and `docs/plans/261016-0249-dimension-columns-by-pricing-mode/` paths, and archiving moves that folder.

1. Write `docs/plans/261016-0249-dimension-columns-by-pricing-mode/EXECUTION-REPORT.md` (complete the file if Task 4.13 or 4.14 already created it; keep its "PR notes - Help" section) in the format of `docs/plans/archived/261015-2114-dashboard-branch-revenue/EXECUTION-REPORT.md`, with: a link to the plan, each phase with its commits and result, the files changed, the verification commands and their outcomes against `BASELINE.md`, the browser check result (`BROWSER-CHECK.md`, including its script and data corrections and plan corrections), and the deviations with reasons. The section headed "PR notes - Help" lists: (a) each guide left untouched under Task 4.13 step 3 or Task 4.14 step 3, with its line; (b) the help files changed in Tasks 4.11–4.14, and the guides flagged only by comment changes; (c) "Follow-ups to file separately": the five items of the brainstorm SUMMARY → Open questions (sample data with metre goods, cleanup of stale quotation dimensions, the revenue report showing unused dimensions, **Tấm** vs **Số tấm** in the Enter-stop table, the quotation **Xóa tất cả dòng** focus loss) and the product limitations of the brainstorm Non-goals and the plan SUMMARY → Risks. The PR body copies this section.
2. From `$WT`, in one chained line: `cd "$WT" && git add docs/plans/261016-0249-dimension-columns-by-pricing-mode/EXECUTION-REPORT.md && git mv docs/plans/261016-0249-dimension-columns-by-pricing-mode docs/plans/archived/261016-0249-dimension-columns-by-pricing-mode` (with the `WT=` line first).
3. Fix the links with the Edit tool (`docs/SUMMARY.md` is CRLF, the two copied `SUMMARY.md` files are LF; edit one line at a time, never `sed -i`):
   - in `docs/plans/archived/261016-0249-dimension-columns-by-pricing-mode/SUMMARY.md`, in the header line near the top (line 3), change each of the four `](../../brainstorms/` to `](../../../brainstorms/`;
   - in `docs/brainstorms/261015-2311-dimension-columns-by-pricing-mode/SUMMARY.md` (lines 3 and 256 at the time of review; find them with `git grep -n 'plans/261016-0249'`), change `../../plans/261016-0249-dimension-columns-by-pricing-mode/` to `../../plans/archived/261016-0249-dimension-columns-by-pricing-mode/`;
   - in `docs/SUMMARY.md`, change the plan row's two paths to `plans/archived/261016-0249-dimension-columns-by-pricing-mode/SUMMARY.md` and start its description with "Archived plan (executed <today>; report in `EXECUTION-REPORT.md`, browser check in `BROWSER-CHECK.md`) for the dimension columns that follow the highest pricing mode — …".
   Check, from `$WT`: `git grep -n 'plans/261016-0249' -- docs/SUMMARY.md docs/brainstorms | grep -v 'plans/archived/'` prints nothing (the archived phase files and `BASELINE.md` still name their own old paths; they are not links), and `git diff --numstat` matches the same command with `--ignore-cr-at-eol`.
4. Commit, in one chained line: `cd "$WT" && git add docs/SUMMARY.md docs/brainstorms/261015-2311-dimension-columns-by-pricing-mode/SUMMARY.md docs/plans/archived/261016-0249-dimension-columns-by-pricing-mode && git commit -m "docs(plan): dimension columns execution report, archive plan"`. These docs are in no guide's `sources`. Then run `npm run help:stale` again from `$WT/frontend`: its output must equal the output of Task 4.15 step 1.

## Verification

- `npm run help:stale`: `Không có cảnh báo.`, or only base guides that Task 4.14 re-checked and left untouched, or the two inventory guides left under Task 4.13 step 3's same-day case (each named under "PR notes - Help" in `EXECUTION-REPORT.md`).
- `npm run test -- --testTimeout=30000 --maxWorkers=3 src/features/help`: pass.
- `npm run typecheck`: pass. `npm run build`: succeeds, then `git checkout -- tsconfig.app.tsbuildinfo` (`git status --short` empty). `npm run lint`: baseline only (2 errors, the 15 warnings of `BASELINE.md`).
- `npm run test -- --testTimeout=30000 --maxWorkers=3`: baseline failures only.
- `BROWSER-CHECK.md`: every row PASS (on a re-run after any listed script, data or plan correction), or FAIL followed by a fix commit and a PASS re-check.
- `qldonhang_integtest_dimcols` dropped; ports 5061 and 5191 free; the developer backend on 5050 untouched.
- Task 4.15 step 8 committed the status lines and the five ticks of the plan SUMMARY; Task 4.16 committed `EXECUTION-REPORT.md` in the archived plan folder.

## Exit Criteria

- `conventions.md`, `directory-structure.md` and `docs/SUMMARY.md` describe the dimension columns, and were committed before the browser check.
- The real-browser check passed on stock-out (steps 1–11), stock-in (steps 1–11) and the quotation (steps 1–8 and the **Mã hàng** list): the flash shows on the header, on the picked row's unfocused new cells and on every "—" cell; the header text stays readable in light and dark mode; nothing animates under reduced motion; the caret stays visible when columns hide; a dimension error on a hidden column forces it into view and its "—" cell and message are visible beside the pinned columns even when the grid is scrolled right (step 9). `BROWSER-CHECK.md` and its screenshots are committed.
- Each defect `D<n>` has a fix commit with a RED test first, or a stated reason why jsdom cannot show it. Each script, data or plan correction is listed in `BROWSER-CHECK.md` with its PASS re-run, and each plan correction quotes the brainstorm sentence or Phase 01–03 test that supports it.
- The help files of `stock-in.form`, `stock-out.form`, `quotations.form`, `inventory-opening-stock` and `inventory-stock-card` are committed after every code commit and every merge (except an inventory guide left under Task 4.13 step 3's same-day case), list the two new lib files where required, and match the screens.
- `help:stale` flags none of this work's guides, except an inventory guide left under Task 4.13 step 3's same-day case; every remaining warning is listed under "PR notes - Help" in `EXECUTION-REPORT.md` and reported to the user.
- `EXECUTION-REPORT.md` is in the archived plan folder `docs/plans/archived/261016-0249-dimension-columns-by-pricing-mode/`, and no link in `docs/` points at `docs/plans/261016-0249-…` (Task 4.16).
