# Phase 06 — Cleanup, docs, real-browser checks, final verification

**Status:** [ ] pending
**Complexity:** L

## Objective

Remove the reason-based search mode that Phases 02–05 kept alive (the brainstorm's "bỏ `requireReason`"), update the project docs, write the go-live checklist, and check the three screens in a real browser at three widths, with a mocked API and against a real backend on a scratch database.

## Files

- `frontend/src/components/partner-autocomplete/partner-autocomplete.tsx` + `.test.tsx` (modify)
- `frontend/src/components/partner-autocomplete/partner-code-name-field.tsx` (modify)
- `frontend/src/features/cash-vouchers/{api.ts,keys.ts,hooks.ts,hooks.test.tsx}` (modify)
- `frontend/src/features/stock-vouchers/{api.ts,keys.ts,hooks.ts,hooks.test.tsx}` (modify)
- `frontend/src/pages/cash-vouchers/components/cash-voucher-header.tsx`, `cash-voucher-form-page.test.tsx` (modify)
- `frontend/src/pages/stock-vouchers/stock-voucher-list-page.tsx`, `frontend/src/pages/debt/opening-debt-page.tsx`, `frontend/src/pages/debt/opening-debt-form-dialog.tsx` (modify: drop `requireReason={false}`)
- `docs/code-standard/conventions.md`, `docs/codebase/directory-structure.md`, `docs/architecture/system-architecture.md`, `docs/SUMMARY.md` (modify)
- `docs/plans/261015-0926-cash-voucher-form-redesign/GO-LIVE-CHECKLIST.md`, `BROWSER-CHECK.md` (create)

## Tasks

Commands run from `$WT/frontend`; `T` is `npx vitest run --config vitest.config.ts`.

### Task 6.1 — Remove the reason-based search mode

The backend keeps its `reasonId` parameter (compatibility); only the frontend stops using it.

1. **Update the tests first** so they fail for the right reason:
   - `partner-autocomplete.test.tsx`: delete `is disabled until a reason is chosen` and `filter mode (requireReason=false) is enabled without a reason`; the lock test keeps `partnerType="None"` (no `requireReason`); every render drops `reasonId` / `requireReason`; a custom `useSearch` is called with `(keyword, activeOnly)`; the default stock search is called with `(type, keyword, activeOnly)` — no `reasonId` anywhere.
   - `features/cash-vouchers/hooks.test.tsx`, `features/stock-vouchers/hooks.test.tsx`: `useCashPartnerSearch(type, keyword, activeOnly?)`, `usePartnerSearch(type, keyword, activeOnly?)`, `cashVoucherKeys.partners(type, keyword, activeOnly?)`, `stockVoucherKeys.partners(type, keyword, activeOnly?)`, `…Api.partners(type, keyword, activeOnly?)`; the placeholder rule compares `type` and `activeOnly`.
   - `cash-voucher-form-page.test.tsx`, `cash-voucher-list-page.test.tsx`, `stock-voucher-form-page.test.tsx`, `stock-voucher-list-page.test.tsx` and the debt page tests: mock signatures `(type, keyword, activeOnly)` / `(keyword, activeOnly)`; no `reasonId` assertions.
   - `partner-code-name-field.test.tsx`: the mocked search no longer receives a reason.
2. **Run, expect FAIL.** `T src/components/partner-autocomplete src/features/cash-vouchers src/features/stock-vouchers src/pages/cash-vouchers src/pages/stock-vouchers src/pages/debt` — argument lists differ by one.
3. **Implement.**
   - `PartnerAutocomplete`: remove the `requireReason` and `reasonId` props and the `Chọn lý do trước` branch; `PartnerSearchHook = (keyword: string, activeOnly?: boolean) => …`; the default search is `usePartnerSearch(type ?? 'In', keyword, activeOnly)`; `blocked = partnerType === 'None'`.
   - `PartnerCodeNameField`: stop passing `requireReason`.
   - hooks, api and keys of both features: drop `reasonId` (parameter, query key part `reasonId`, and the `sameRoleFilter` comparison keeps `type` + `activeOnly`).
   - `cash-voucher-header.tsx`: the `PARTNER_SEARCH` wrappers become `(keyword, activeOnly) => useCashPartnerSearch('Receipt' | 'Payment', keyword, activeOnly)`.
   - `stock-voucher-list-page.tsx`, `opening-debt-page.tsx`, `opening-debt-form-dialog.tsx`: delete `requireReason={false}`.
   - `git grep -n "requireReason\|reasonId" -- 'src/components/partner-autocomplete' 'src/features/*/api.ts' 'src/features/*/keys.ts'` shows no partner-search use of `reasonId`.
4. **Run, expect PASS.** `T src` (the whole frontend suite, `--testTimeout=30000 --maxWorkers=3`), `npm run typecheck && npm run lint && npm run build`.
5. **Commit.** `refactor(frontend): remove the reason-based partner search mode`

### Task 6.2 — Docs and go-live checklist

Docs are English (Vietnamese UI strings quoted). Write only what is true of the merged code; read the code before each statement.

1. **`docs/code-standard/conventions.md`** — append a section `## Partner Field And Keyboard Entry` with these points (adjust to the final code):
   - Vouchers and quotations use `PartnerCodeNameField` (`components/partner-autocomplete`); filters use `PartnerAutocomplete` alone. The field owns the quick add and catalog dialogs, decides `+` / F4 / the rows from `customers|suppliers.create|view` (`usePartnerPermissions`), and moves the focus to the name after a pick; a cancelled dialog returns the focus to the code input.
   - Form searches pass `activeOnly` and no reason (`useCashPartnerSearch(type, keyword, activeOnly)`, `usePartnerSearch`); list filters pass nothing and still see inactive partners. A new search surface needs its own query key part, never the form's.
   - Enter keys: `PartnerAutocomplete` keeps Enter only for an open list, a pending result or typed-but-not-picked text; every other Enter is the form's. A form handler ignores Enter with Ctrl/Alt/Meta, during IME composition (`isComposing`), when `defaultPrevented`, and for targets inside `DIALOG_SELECTOR`. Walking is `focusNextInOrder` (`lib/enter-focus.ts`) over an id list; the cash voucher has `CASH_HEADER_FIELD_IDS` and `cashLineEnterTarget`, the stock voucher keeps its own `MAIN_FIELD_IDS` loop.
   - A dialog rendered inside a voucher `<form>` is wrapped in `StopFormEvents` (React events cross portals), and its mutation hooks live in a component inside the content so a closed dialog needs no providers. A test that opens the dialog inside a `<form onSubmit>` must assert the voucher is not saved.
   - Reason match: `features/cash-common/reason-match.ts` mirrors `ValidatePartner` of both services (`requiredRole`, `partnerFitsReason`, `matchReasonForPartner`). Picking a partner switches the reason when it does not fit (yellow notice, 1.6 s flash through `useReasonSwitchNotice`); changing the reason keeps the partner and shows a derived red line; only a "no partner" reason clears it.
   - Cash voucher auto text: `useCashAutoText` is event-driven and never writes when a saved voucher opens or is reset (the Print and QR buttons depend on `isDirty`); the payload carries no `partnerTaxCode` (the server reads the catalog).
   - F4 opens the quick add; the `+` button is Tab-reachable. New partner-related permission grants of the seed reach running databases only when an admin enables them.
2. **`docs/codebase/directory-structure.md`**: list the new files of `components/partner-autocomplete/`, `features/cash-common/` (`reason-match.ts`, `use-reason-notice.ts`), `features/cash-vouchers/` (`auto-text.ts`, `use-cash-auto-text.ts`), `pages/cash-vouchers/utils/cash-navigation.ts`, `lib/enter-focus.ts`, `features/customers/` (`partner-search-item.ts`, `use-partner-permissions.ts`, `use-quotation-customer-search.ts`); remove `components/customer-autocomplete/` and `features/customers/components/customer-catalog-*` if listed (`grep -n "customer-autocomplete\|customer-catalog" docs/codebase/directory-structure.md`).
3. **`docs/architecture/system-architecture.md`**: the file has no entry for the two partner search endpoints yet. Add `activeOnly` on `GET /api/stock-vouchers/partners` to "### Inventory (Round 1)" (line ~109) and on `GET /api/cash-vouchers/partners` to "### Cash & Debt (Round 2A)" (line ~152): default = a reason means active only, no reason means every partner including inactive; `reasonId` is kept for compatibility and the frontend no longer sends it. Add the default grants (ACCOUNTANT `customers.create`, `suppliers.view`, `suppliers.create`; WAREHOUSE `customers.create`, `suppliers.create`) to "## Role And Permission Management" (lines ~51-63), noting that they reach only new databases (line ~60 explains why).
4. **`docs/SUMMARY.md`**: the plan row already exists from Phase 00; check the brainstorm and BD rows; the archive step of `subagent-driven-development` moves the plan row to `plans/archived/`.
5. **`GO-LIVE-CHECKLIST.md`** (in the plan folder), with checkboxes:
   1. Deploy the backend before the frontend (the new frontend sends `activeOnly`; an old backend ignores it and returns inactive partners to the forms).
   2. On every running database an admin enables in the permission matrix: **Kế toán** `customers.create`, `suppliers.view`, `suppliers.create`; **Kho / giao hàng** `customers.create`, `suppliers.create`. (The seeder grants these only to new databases.) Without them the `+`, F4 and the "Thêm mới" row are hidden for that role.
   3. Sign in as an Accountant and a Warehouse user: F4 opens the quick add on receipts, payments, stock-in and stock-out; roles preselected by the reason; a role without permission is disabled.
   4. Type Vietnamese with Telex and VNI in the partner code, the name and the reason fields of the three forms: the Enter that commits a composition must not move the focus.
   5. F4 on laptops may need Fn: tell users the `+` button does the same.
   6. Tell users what changed: Enter now walks the receipt/payment form and the line grid; Lý do nộp and line descriptions fill themselves ("Tên lý do - Tên đối tượng") and follow until edited by hand; the MST box is gone (the catalog MST is used); a partner picked first sets the reason.
   7. Known limit: the quotation search covers customers only; typing a supplier's name there offers "Thêm mới" instead of turning the supplier into a customer.
   8. If the payment QR plan is not merged yet, nothing else to do; if it is, open a receipt and check its QR button still follows the Print rule (disabled while the form is dirty).

   Commit: `docs: partner field conventions, structure and go-live checklist`.

### Task 6.3 — Real-browser checks

Evidence over opinion: this catches layout and focus problems unit tests cannot (the icon registry plan found a wrap at 1440 px this way).

Setup (scratchpad, nothing committed): `mkdir "$SCRATCH/browser-check" && cd "$SCRATCH/browser-check" && npm init -y && npm i playwright-core`; launch Edge with `chromium.launch({ channel: 'msedge' })` (or `executablePath: 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe'`).

**Isolation rules (both modes).** A developer backend usually listens on `localhost:5050` against the dev database `qldonhang_test`, and `frontend/vite.config.ts` proxies `/api` and `/hubs` to 5050. Never send that process a request, never stop it, never edit `vite.config.ts`. Start **your own** Vite from the worktree on a free port: `cd "$WT/frontend" && npx vite --port 5181 --strictPort` (pick another 51xx if taken; check with `netstat -ano | grep :51`), run in the background. At the end kill your Vite (and your backend in mode B) and check with `netstat` that your ports are free and 5050 still listens.

**Mode A — mocked API.** `page.route('**/api/**', …)` and `page.route('**/hubs/**', route => route.abort())`; nothing may reach 5050 (log every request URL and assert it). Mock every call the pages make, starting with the app boot: `POST /api/auth/refresh` (token pair), `GET /api/auth/me` (user with the permissions of the case), then branches, funds, cash and stock reasons, warehouses, payment methods, settings/defaults, partner search with an optional delay, customer search, and the create endpoints returning ids. Answer any unmocked `/api` call with 501 and list it in the notes, so a missing mock is visible. Use this mode for layout and timing (a 600 ms search delay).

**Mode B — real backend on a scratch database.**
1. `PGPASSWORD=1 "/c/Program Files/PostgreSQL/18/bin/psql" -h localhost -U postgres -c "create database qldonhang_cvf_browser"`. Never use `qldonhang_test`, `qldonhang` or any `qldonhang_integtest*` database.
2. Run the API from the worktree on port 5055 with every override set (`DefaultConnection` wins over `Default`, so set both):
   ```bash
   CS="Host=localhost;Port=5432;Database=qldonhang_cvf_browser;Username=postgres;Password=1"
   ConnectionStrings__Default="$CS" ConnectionStrings__DefaultConnection="$CS" \
   ASPNETCORE_URLS=http://localhost:5055 ASPNETCORE_ENVIRONMENT=Development Database__AutoMigrateAndSeed=true \
   dotnet run --project "$WT/backend/src/OrderMgmt.WebApi" --no-launch-profile
   ```
   (background). Before the first request, **prove** the override took effect: `psql … -d qldonhang_cvf_browser -c "\dt"` lists the tables, and nothing you started listens on 5050. If isolation cannot be proven, stop and report.
3. Start your own Vite as above and route the browser to your backend: `page.route('**/api/**', r => r.continue({ url: r.request().url().replace(/^http:\/\/localhost:\d+/, 'http://localhost:5055') }))`, the same for `**/hubs/**`. Assert in the script that no request goes to `:5050`.
4. Sign in through the login page as `admin` / `Admin@123`; through the real API (as admin) create an Accountant user, a few customers, suppliers and a partner with both roles (the seed already has the `QTM` fund, the reasons, `KHO01` and payment methods). Record every request you rely on.
5. Afterwards: kill your backend and Vite, check the ports, and `psql … -c "drop database qldonhang_cvf_browser"` (report it if the drop fails).

Scenarios (each recorded in `BROWSER-CHECK.md` with pass / fail and, for a fail, the cause):

1. **Receipt, keyboard only**: the page opens with the focus in the partner code. Type a customer code and Enter → picked, focus on the name, `Lý do nộp` and the first line say `Thu tiền khách hàng - <name>`. Enter walks name → reason → person → address → description → attached → note → the line description (text selected) → amount → note → a new row with the description filled. Ctrl+S saves; reopen it: the Print button is enabled and nothing is dirty.
2. **Reason match**: on a payment pick a customer-only partner → the reason switches, the yellow line shows and the select flashes; change the reason back → the red line shows.
3. **Quick add**: type a new name, F4, Enter through the fields (name preselected), Ctrl+Enter → the partner is selected, the focus is on the name, **no voucher was saved** (URL still `/new`, no `Đã tạo phiếu thu` / `Đã tạo phiếu chi` toast — the quick add's own `Đã tạo khách hàng` toast is expected — and no `POST /api/cash-vouchers` in the request log). Repeat with Esc: the dialog closes and the typed text and the focus are back in the code input.
4. **Fast Enter** (mode A, 600 ms delay): type a known code and press Enter at once → that partner is picked; type an unknown code and Enter → the list shows "Thêm mới" selected and **no dialog opens**; a second Enter opens it.
5. **Stock voucher** (in and out): row 2 is `Đối tượng [mã][+] [tên] · Đ.chỉ`; the search shows customers and suppliers; picking a supplier-only partner on a sale switches the reason; Enter order unchanged; a mocked `400 { partnerId: [...] }` shows under the partner row.
6. **Quotation**: `Khách hàng*` row; F4 needs `customers.create`; Enter chain `customerId → customerName → deliveryAddress`; typed-but-not-picked text blocks saving with the message.
7. **Permissions** (mode B): the Accountant sees `+` and F4 on receipts/payments; remove `customers.create` in the matrix and the `+` disappears after a reload.
8. **Widths**: screenshots of the receipt header, the stock voucher main fields and the quotation customer row at 1440×900, 1100×800 and 800×900 (compare with `mockup-01-cash-voucher-form.html`, `mockup-02-shared-partner-field.html`). Check `document.documentElement.scrollWidth <= window.innerWidth`, the name input drops below the code input on narrow widths without falling into the label column, the error line sits under the partner container, the reason notice does not overlap the next row.
9. **Filters unchanged**: stock voucher list partner filter and the opening-debt dialog picker: no `+`, no extra rows, a `SĐT` column, selecting works.
10. **IME**: not scriptable reliably; dispatch `compositionstart` / `compositionend` and an Enter with `isComposing: true` through CDP (`Input.imeSetComposition` + `Input.dispatchKeyEvent`) on the partner code and assert the focus did not move; the real Telex run is on the go-live checklist.

Fix every real defect with a failing test first (separate commits), then rerun the scenario. Commit the notes: `docs(plan): browser check notes for the cash voucher form redesign`.

### Task 6.4 — Final verification

1. Run the **Final Verification** block of `SUMMARY.md` in full.
2. Hygiene, run **from `$WT`** (git pathspecs are relative to the current folder; from `frontend/` the `docs/` paths would match nothing): `git diff <base>..HEAD --stat | grep -E "tsbuildinfo|TestResults|\.trx"` is empty; line-ending check from "Executor conventions"; `git grep -n "lucide-react" -- frontend/src/components/partner-autocomplete frontend/src/pages/cash-vouchers frontend/src/pages/stock-vouchers` is empty (the icon guard test also covers it); `git grep -n "requireReason\|customer-autocomplete\|CustomerAutocomplete\|form-cols-stock-main-2" -- frontend/src docs/code-standard docs/codebase` is empty (`docs/codebase/directory-structure.md:134` lists `customer-autocomplete/` today; Task 6.2 removes it).
3. Re-check the base: `git fetch qlbanhang main` (retry with the sandbox disabled if it fails; a silent failure leaves the ref stale), then `git log --oneline HEAD..qlbanhang/main`. `qlbanhang/main` already moved to `1a87763` (docs only) after the plan was written. If it has commits the branch lacks, `git merge qlbanhang/main` into the branch, rerun the cash, stock and quotation page suites, and resolve conflicts by keeping both sides (the QR plan's button row, other plans' doc rows).
4. Tick the `Status` of every phase file and the checkboxes of `SUMMARY.md`.
5. Report: what is done, the baseline-only failures, the browser-check result, the go-live checklist, the merge-order note for the QR plan. Push and PR are the user's call (`finishing-a-development-branch`).

## Verification

- Everything in Task 6.4.

## Exit Criteria

- No frontend code path searches partners by reason; the forms and filters behave as the browser checks recorded.
- Docs describe the new field, keys and permissions; the go-live checklist exists.
- The final verification passes with only baseline failures.
