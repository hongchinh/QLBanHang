# Browser check: cash voucher form redesign (Task 6.3)

- Date: 2026-10-15, 15:04–16:02 (Asia/Ho_Chi_Minh)
- Code under test: detached checkout `%TEMP%\cvf-browser` at `b4aaeb56` (merge of Phases 00–05). Read-only run: no source edits, commits or pushes. `git status` in the checkout was clean afterwards.
- Browser: Microsoft Edge (headless), driven by `playwright-core` 1.64.1 (`chromium.launch({ channel: 'msedge' })`). Contexts used `serviceWorkers: 'block'`, locale `vi-VN` and time zone `Asia/Ho_Chi_Minh`.
- Scripts in this folder: `lib.js` (helpers, routing, request log), `setup-b.js` and `setup-b2.js` (data setup through the real API), `scen-b.js` (S1–S3), `scen-b2.js` (S5, S6, S7, S9, S10, catalog checks), `scen-qa-focus.js` (focus after a quick add), `record-a.js` (records the Mode A fixtures), `scen-a.js` (S4, S8, mockup screenshots), `probe-ime.js` (key events during IME composition, lifetime of the yellow notice), `probe-s7.js` / `probe-sales.js` (fixed a race in my S7 script).
- Raw results: `results-*.json`. Each file includes the full request log of its run.

## Summary

| # | Scenario | Mode | Result |
|---|---|---|---|
| 1 | Receipt, keyboard only | B | PASS |
| 2 | Reason match on a payment | B | PASS |
| 3 | Quick add never saves the voucher, plus the Esc path | B | PASS |
| 4 | Fast Enter with a 600 ms search delay | A | PASS |
| 5 | Stock voucher in/out (400 `partnerId` mocked) | B | PASS |
| 6 | Quotation customer row | B | PASS |
| 7 | Permissions (Accountant, matrix) | B | PASS (the app follows spec §6.2; the plan's expected result was wrong, see S7) |
| 8 | Widths 1440 / 1100 / 800 | A | **FAIL**: stock row 1 drops when the reason notice shows (D1); cosmetic label alignment (D4) |
| 9 | Filters unchanged | B | PASS |
| 10 | IME (CDP composition plus an `isComposing` Enter) | B (+A probe) | **FAIL** on the quotation (D2); cash and stock pass |
| extra | Focus after a pick (list, quick add, catalog), catalog timing and tabs | B | Focus: PASS. Catalog tabs: **FAIL, minor** (D3) |

Defects: D1 Important, D2 Important, D3 Minor, D4 Minor (details below).

## Isolation proof (`isolation-proof.txt`, `cleanup.txt`)

- Nothing was listening on `:5050` at any point (no developer backend was running). No script sent anything to `:5050`. Every request URL was logged (`page.on('request')`, plus the target of every rewrite and every node `fetch`), and each script ends with `assertNo5050()`. Every `results-*.json` contains 0 occurrences of `:5050`.
- Scratch DB `qldonhang_cvf_browser`: created at 15:05. The API was started from `cvf-browser/backend/src/OrderMgmt.WebApi` with `ConnectionStrings__Default` and `ConnectionStrings__DefaultConnection` both set to the scratch DB, plus `ASPNETCORE_URLS=http://localhost:5055`, `ASPNETCORE_ENVIRONMENT=Development`, `Database__AutoMigrateAndSeed=true` and `--no-launch-profile`. The log shows `Now listening on: http://localhost:5055`. Before the first request, the scratch DB had 50 public tables and the seeded user `admin`. Listeners: 5055 (PID 17124, `OrderMgmt.WebApi.exe`) and 5181 (PID 27036, my Vite). 5050: none.
- My Vite ran from `cvf-browser/frontend` with `npx vite --port 5181 --strictPort`. I did not edit `vite.config.ts`.
- Mode B routing: `/api/*` used `route.continue({ url: … → http://localhost:5055 })`. **Deviation:** I aborted `/hubs/*` in both modes, and the WebSocket upgrade was closed through `context.routeWebSocket`. `page.route` cannot redirect a WebSocket, and a hub socket on `:5181` would have gone through the Vite proxy to `:5050`. The SignalR notification hub was therefore offline in Mode B. No scenario needs it.
- Cleanup: I stopped PIDs 17124 (API), 23548 (`dotnet run`), 27036 (Vite node) and 17456 (its cmd). At 16:02 nothing listened on 5181, 5055 or 5050. `drop database qldonhang_cvf_browser` returned `DROP DATABASE`, and `pg_database` now has 0 rows with that name. No other database was touched (`qldonhang_test`, `qldonhang` and `qldonhang_integtest*` were only listed, never connected to).

## Mode B data (real API on :5055, all logged in `setup-b.json`)

Created as admin: customers KH0001 "Công ty TNHH Xây dựng An Phát", KH0002 "Công ty CP Minh Long" and KH0003 "Cửa hàng Vật liệu Bình Minh"; suppliers NCC001 "Công ty TNHH Cách nhiệt Đông Á" and NCC002; the two-role partner KNB01 "Công ty TNHH Song Vai"; product SP001; the Accountant user `ketoan`; the Sales user `kinhdoanh`. I also set the branch opening date to 2026-01-01 (`PUT /api/branches/{id}/opening-date`) so the opening-debt "Thêm" button is enabled. The seed provided QTM, the cash and stock reasons, KHO01 and the payment methods. The login endpoint allows 5 calls per minute (`RateLimiting:LoginPermitLimit`), so the scripts cache the admin token and wait 61 s after a 429.

## Mode A (mocked API)

- `record-a.js` loaded the three forms and the two lists on the scratch backend and stored every `/api` response in `fixtures-a.json`: auth refresh/me, branches, branding, notifications, cash/stock defaults, cash and stock reasons, funds, warehouses, payment methods, inventory settings, lists.
- Dynamic mocks: `GET /api/cash-vouchers/partners`, `/api/stock-vouchers/partners` and `/api/customers/search` filter a fixed partner set (KH0001, KH0002, KH00021, NCC001, KNB01) and accept a per-page delay (600 ms in S4). `GET /api/customers` and `/api/suppliers` are paged. `GET /api/debt/open-items` returns `[]` and `GET /api/debt/partner-summary` returns zeros. `POST /api/stock-vouchers` returns 400 `{ partnerId: [...] }`.
- **Unmocked `/api` calls answered 501:**
  - First S4 run: `GET /api/debt/open-items?partnerId=…02&side=Receivable&direction=Increase&forSourceType=Receipt`, `GET /api/debt/open-items?partnerId=…11&side=Payable&direction=Decrease&forSourceType=Receipt`, `GET /api/debt/partner-summary?partnerId=…02`, `GET /api/debt/partner-summary?partnerId=…11`. I mocked these afterwards (they do not affect S4).
  - Final S4 run and S8/mockup run: none. Path-only fallbacks: none.

## Scenarios

### S1 Receipt, keyboard only: PASS
Steps: `/receipts/new` as admin; type `KH0001`, Enter at once; Enter ×7; type an amount; Enter ×2; Ctrl+S; reload.
- The page opened with the focus in `cash-partner`. Lý do nộp and line 1 were "Thu tiền khách hàng".
- After `KH0001` + Enter: KH0001 picked, focus on `cash-partner-name` with the caret at the end (29/29). Lý do nộp and line 1 both read "Thu tiền khách hàng - Công ty TNHH Xây dựng An Phát".
- Enter walked name → `cash-reason` → `cash-person-name` → `cash-partner-address` → `cash-description` → `cash-attached-docs` → `cash-voucher-note` → `cash-line-description-0` (text selected 0–51) → amount → note → a new row `cash-line-description-1`, filled and selected.
- Ctrl+S: one `POST /api/cash-vouchers` with 1 line (the untouched row 2 was dropped) and no `partnerTaxCode` in the payload. Toast "Đã tạo phiếu thu PT00002"; URL moved to `/receipts/<id>`.
- Reopened by a reload: h1 "Phiếu thu PT00002", Print enabled (no "Lưu thay đổi trước khi in" title), so the form was not dirty.
- Evidence: `s1-after-pick.png`, `s1-before-save.png`, `s1-reopened.png`.

### S2 Reason match on a payment: PASS
- `/payments/new`; default reason "Trả tiền nhà cung cấp". Picked KH0002 (customer only).
- The reason switched to "Chi hoàn tiền khách hàng". Yellow line "Đã đổi lý do cho hợp với KH0002 (khách hàng)." (`text-amber-700`). The select flashed (`ring-amber-400`); the flash cleared after about 1650 ms.
- Changed the reason back to "Trả tiền nhà cung cấp": red line `Lý do "Trả tiền nhà cung cấp" cần nhà cung cấp — KH0002 không phải nhà cung cấp`; the partner was kept.
- Evidence: `s2-switched.png`, `s2-mismatch.png`.

**How long the yellow line stays** (spec §1.2 says "chỉ hiện tạm hoặc khi có lỗi"; reported only, not judged). Measured on a receipt (`probe-ime.js`, Mode A) and a payment (S2):
- Still visible at 1, 3, 6 and 10 s, and still visible after Enter ×5 plus typing in another field.
- Picking another partner that also needs a switch replaces the text.
- Changing the reason by hand removes it.
- In practice it stays until the next partner or reason action, or a save/reset. Only the amber ring is temporary (1.6 s).

### S3 Quick add never saves the voucher, plus Esc: PASS
- `/receipts/new`; typed "Công ty Mới Thêm Nhanh", F4. The dialog focused `#name` with the typed text. Roles: Khách hàng checked, NCC unchecked, both enabled.
- Enter walked name → taxCode → phoneNumber → email → group.
- Ctrl+Enter: the dialog closed, partner KH-261015-0001 was selected, and the focus was on `cash-partner-name`. It was still there 1.2 s later.
- URL stayed `/receipts/new`. Toast only "Đã tạo khách hàng KH-261015-0001". Requests after the submit: `POST /api/customers`, `GET /api/cash-vouchers/partners`, `GET /api/debt/open-items`, and no `POST /api/cash-vouchers`.
- Esc path: typed "Esc test KH", F4, Esc. The dialog closed; the code input kept "Esc test KH" and had the focus. Opening with the mouse on `+` and pressing Esc also returned the focus to the code input (not the `+` button).
- Evidence: `s3-quickadd.png`, `s3-after-create.png`, `s3-esc.png`.

### S4 Fast Enter (Mode A, 600 ms delay): PASS
Run on `/receipts/new`, `/stock-out/new` and `/quotations/new`.
- `KH0002` + Enter at once: KH0002 was picked after about 1.2 s, by exact code, even though KH00021 also matched. The focus moved to the name input.
- Stale results: with KH0001's results on screen, typing `NCC001` + Enter at once picked NCC001, not the stale KH0001. On the quotation, NCC001 is not a customer, so nothing was picked.
- `ZZZ999` + Enter at once: no dialog. The list showed "Không tìm thấy …", then "Thêm mới … "ZZZ999"" (Enter / F4, `aria-selected=true`), then "Xem danh mục đầy đủ". A second Enter opened the quick add with Tên = "ZZZ999".
- Evidence: `s4-cash-partner-unknown.png`, `s4-stock-partner-unknown.png`, `s4-customerId-unknown.png`.

### S5 Stock voucher, in and out: PASS
- Row 2 of both forms, in DOM order: `label Đối tượng`, then `.partner-code-name[stock-partner, "Thêm nhanh đối tượng (F4)", stock-partner-name]`, then the two dialog wrappers (`display: contents`), then `label Đ.chỉ`, then `#stock-partner-address`.
- Enter order from the date, partner empty: reason → warehouse → HTTT → `stock-partner` → `stock-partner-name` → `stock-partner-address` → `stock-line-product-code-0`. Unchanged.
- Search "Công ty": "Tìm thấy 6 đối tượng (KH + NCC) đang dùng", columns Mã|Tên|MST|Địa chỉ|SĐT|Loại, with both customers and suppliers in the list.
- Stock-out, reason "Xuất bán hàng": picked NCC001 → "Xuất trả hàng NCC", yellow line, focus on the name.
- Stock-in: the default reason is "Nhập khác" (any role), so KH0001 kept it. With "Nhập mua hàng" selected first, KH0001 → "Nhập hàng bán bị trả lại", yellow line, focus on the name.
- Mocked `400 { partnerId: [...] }` on Ctrl+S (in Mode B, through `page.route`): the message showed in `#stock-partner-error` at l=365/t=256, directly under the partner container (l=365, b=250). The address stayed on row 1 (t=222). There was also a toast "Không thể lưu phiếu / Dữ liệu không hợp lệ" (`useDocumentActions` shows a toast even when every key is mapped; this is by design).
- Evidence: `s5-out-search.png`, `s5-out-switched.png`, `s5-in-search.png`, `s5-in-switched.png`, `s5-in-400-partner.png`.

### S6 Quotation: PASS
- Row: label "Khách hàng" with the `required` class (shows `*`), `+` "Thêm nhanh khách hàng (F4)", and the name input `customerName`. The quotation does not autofocus the field (as the spec says).
- Enter chain: `customerId` (KH0003 + Enter) → `customerName` → `deliveryAddress`. The delivery address ("7 Nguyễn Trãi, Đà Nẵng") and phone were filled from the customer.
- F4 with a customer picked: "Thêm nhanh khách hàng", no role boxes, Tên empty. Esc returned the focus to `customerId`.
- Typed "Khách chưa chọn" without picking, then Ctrl+S: "Vui lòng chọn khách hàng từ danh sách" under the row, focus on `customerId`, no `POST /api/quotations`, URL still `/quotations/new`.
- "F4 needs customers.create" was verified in S7: Sales without `customers.create` has no `+` and F4 does nothing.
- Evidence: `s6-quickadd.png`, `s6-unpicked-blocked.png`.

### S7 Permissions (Mode B): PASS (the plan's expected result was wrong)
- As Accountant `ketoan` (seeded role: `customers.create`, `suppliers.view`, `suppliers.create`):
  - Receipt: `+` visible; F4 opens the quick add with Khách hàng preselected.
  - Payment: `+` visible; F4 opens it with Nhà cung cấp preselected.
- As admin in the matrix UI (`/admin/roles`): unticked "ACCOUNTANT customers.create" and "SALES customers.create", then "Lưu thay đổi". This sent 2 PUTs; the DB then showed both permissions removed.
- Accountant reloads `/receipts/new`: **`+` and F4 are still there.** In the dialog, Khách hàng is disabled and Nhà cung cấp is preselected.
  - This follows spec §6.2 and §5.1: Accountant still has `suppliers.create`, and the `+` is hidden only when no create permission is left.
  - **Correction:** the plan's expected result ("remove `customers.create` … and the `+` disappears") was wrong. Spec §6.2 says the `+` stays while any create permission remains. The app behaves correctly: the `+` disappears only once the role has neither `customers.create` nor `suppliers.create` (verified below). This is not a defect.
- Also unticked "ACCOUNTANT suppliers.create" and reloaded: `+` gone, F4 does nothing.
- Sales `kinhdoanh` without `customers.create` on `/quotations/new`: no `+`, F4 does nothing. Typing "zzz" shows only "Không tìm thấy khách hàng "zzz"" and "Xem danh mục đầy đủ", with no "Thêm mới" row.
- Permissions were restored through the API afterwards.
- Evidence: `s7-accountant-receipt.png`, `s7-accountant-payment.png`, `s7-matrix.png`, `s7-accountant-receipt-no-customers-create.png`, `s7-sales-quotation.png`.
- Side note, outside this feature: on `/admin/roles` the checkboxes render unticked before the role details load. A click in that window toggled the wrong state in my first run (`probe-s7.js`). The script now waits for the box to show the granted state.

### S8 Widths (Mode A): FAIL (D1), with cosmetic D4
Screenshots (each at 1440x900, 1100x800 and 800x900):
- `s8-<w>x<h>-receipt-header[-notice|-error].png`
- `s8-<w>x<h>-stock-main[-notice|-error].png`
- `s8-<w>x<h>-quotation-customer[-error].png`

Mockup renders: `mockup-01-cash-voucher-form.png`, `mockup-02-shared-partner-field.png`.

| Check | 1440 | 1100 | 800 |
|---|---|---|---|
| `scrollWidth <= innerWidth` (receipt / stock / quotation) | 1440/1440 on all | 1100/1100 on all | 800/800 on all |
| Name below code | no (beside) | receipt and quotation: beside; stock: **below** (1024–1279 band, partner column minmax(300px,1.6fr)) | below on all three |
| Name never in the label column | name l ≥ label r on all | yes | yes (name l=369 > label r=359) |
| Partner error under the partner container (guard message; stock also via mocked 400 in S5) | receipt err l=369/t=312 under the container (b=306), above Lý do (334); stock l=365/t=256, container b=250, Đ.chỉ stays on row 1; quotation l=369/t=312, Địa chỉ giao 334 | same, all OK | same, all OK |
| Reason notice does not overlap the next row | receipt notice b=354 < Địa chỉ 360; stock notice b=246 < partner 252 | OK | OK |
| Cash reason row level with the notice | select 312 = Người nộp 312; Lý do label 312→312 | same | n/a (two columns; Người nộp moves down below the notice, as expected) |
| **Stock row 1 level with the notice** | **FAIL**: date, Kho and HTTT tops 188→203 (+15 px); "Lý do xuất" label centred across select + notice | **FAIL**: date 188→195 vs reason 188 | Lý do xuất label centred between the select and the notice (drops ~7 px) |

- The receipt header at 1440 matches mockup-01: row 2 `Đối tượng [mã ✕][+][tên]`, Lý do + Người nộp, Địa chỉ, Lý do nộp, Kèm theo (narrow) + Ghi chú (wide).
- At 1440 the stock yellow line wraps to two lines in the narrow reason cell (t=214, b=246), so the main block grows by 2 text lines, not "about one line" (spec §1.2).
- When code and name wrap (all forms <1024, stock 1024–1279), the row label sits vertically centred between the two lines. On the stock voucher at 1100 the `Đ.chỉ` label and input float between the code line and the name line. See D4 and `s8-1100x800-stock-main.png`, `s8-800x900-receipt-header-error.png`.

### S9 Filters unchanged: PASS
- Stock-out list filter ("Lọc theo đối tượng..."):
  - No `+`. Header "Tìm thấy 6 đối tượng" (no "đang dùng"; keys hint without F4). Columns Mã|Tên|MST|Địa chỉ|SĐT|Loại, with no "Thêm mới" or "Xem danh mục" rows.
  - ArrowDown + Enter selected a partner: `?partnerId=…&partnerName=…` in the URL, and the input shows the name.
- Opening-debt dialog (`/debt/opening` → Thêm → `#opening-debt-partner`):
  - No `+`. "Tìm thấy 4 đối tượng" (customers, Receivable side). SĐT column present, no extra rows.
  - Enter picked KH-261015-0001; the dialog stayed open.
- Evidence: `s9-stock-filter.png`, `s9-opening-debt.png`.

### S10 IME (CDP): FAIL on the quotation (D2)
Each field went through: `Input.imeSetComposition` ("vie" → "viê"), then `Input.dispatchKeyEvent` Enter (VK 229), then a synthetic `keydown` Enter with `isComposing: true`, then `Input.insertText` "việt".
- Receipt `cash-partner`, `cash-partner-name`, `cash-description` and stock `stock-partner`, `stock-partner-name`: the focus stayed, the value was "việt", no dialog opened. **PASS.**
- Quotation `customerId`: the CDP Enter moved the focus to `customerName`, and the `isComposing` Enter moved it on to `deliveryAddress`. The composition was cut: the value stayed "viê" and `compositionend` fired on the blur. `customerName`: moved to `deliveryAddress`, then `deliveryRecipient`. **FAIL.**
- `probe-ime.js` confirms that the browser delivers `keydown Enter/229 isComposing=true` in both forms; only the quotation handler acts on it.
- The real Telex/VNI run stays on the go-live checklist.

### Extra targeted checks (Mode B unless noted)
- **Focus after a pick** (`results-b2-cat.json`, `results-b-qafocus.json`): the focus was sampled every 100 ms for 1.5 s after each pick.
  - List pick (mouse, and Enter in S1/S4): the focus stays on the name input on the receipt, stock-out and quotation. PASS.
  - Quick add (Ctrl+Enter and a mouse click on "Tạo mới", on all three forms): the focus lands on the name input and stays; no voucher POST; URL `/new`. PASS. Evidence: `qa-focus-*.png`.
  - Catalog: pick by Enter and by a mouse click on a row, on all three forms. The focus lands on the name input and stays. PASS.
- **Catalog: type quickly, then Enter before the results** (900 ms delay on `GET /api/customers`): with the rows of "Công" on screen, typed "Bình Minh" + Enter. The dialog stayed open with no detail GET, so nothing stale was picked. After the results arrived, Enter picked KH0003 and the focus went to the name. PASS.
- **Catalog: role tab click, then arrow keys**: FAIL, minor (D3; evidence `cat-role-tab-arrows.png`).
  - After a mouse click on "Nhà cung cấp", the focus stays on the tab button. ArrowDown does not move the row highlight (it stays at 0 of 3).
  - ArrowRight moves the tab focus and, through Radix automatic activation, switches the catalog back to "Danh mục khách hàng". Only then does the search input take the focus.
  - The group tabs behave the same: after clicking "Công ty", ArrowDown does nothing and Enter picks nothing.
- **Stock row 1 and cash reason row level with the notice**: cash OK; stock FAIL (D1).

## Defects

### D1 (Important): stock voucher row 1 drops when the reason notice shows
- Observed: with the yellow or red line under "Lý do xuất", the row-1 grid (`form-inline-grid form-cols-stock-main-1`) keeps `align-items: center`, so every other cell centres on the taller reason cell.
  - 1440: date, Kho and HTTT move down 15 px, and the labels centre between the select and the notice.
  - 1100: the date sits 7 px below the reason select.
  - 800: the "Lý do xuất" label sits between the select and the notice.
- Expected: the controls stay level, as they do on the cash voucher's Lý do row, which has `form-align-top`.
- Cause: `frontend/src/pages/stock-vouchers/components/stock-voucher-main-fields.tsx:108` has `<div className="form-inline-grid form-cols-stock-main-1">` without `form-align-top`. Compare `cash-voucher-header.tsx:183` and `styles/form-inline.css:23-28`.
- Fix: add `form-align-top` to that row. The cells already wrap their control and `FieldError` in a `<div>`, so the controls stay at the top and the labels get `line-height: 1.75rem`.
- Test idea: a page test asserting that the row containing `#stock-reason` has the class `form-align-top` (jsdom cannot measure). Then re-measure in the browser: with the notice shown, the tops of `#stock-voucher-at`, `#stock-reason`, `#stock-warehouse` and `#stock-payment-method` are equal at 1440.

### D2 (Important): quotation Enter navigation ignores `isComposing`
- Observed: an Enter with `isComposing=true` in `customerId`, `customerName` or any general-info field jumps to the next field and cuts the IME composition ("viê" instead of "việt").
- Expected: the composing Enter is left to the input method (go-live checklist item 4 covers the quotation; spec §2.1/§6.4 rule).
- Cause: `frontend/src/pages/quotations/quotation-form-page.tsx:359`, `if (e.key !== 'Enter' || e.ctrlKey || e.altKey) return;`, has no `e.nativeEvent.isComposing` check. This line predates the branch (16aaf5bc), but the checklist now promises the behaviour.
- Fix: `if (e.key !== 'Enter' || e.ctrlKey || e.altKey || e.metaKey || e.nativeEvent.isComposing) return;`
- Test idea: in `quotation-form-page.customer.test.tsx`, focus `customerName`, then `fireEvent.keyDown(customerName, { key: 'Enter', isComposing: true })`, and expect the focus to stay on `customerName`. Also for `customerId` with text typed and nothing picked.

### D3 (Minor): catalog dialog arrow keys after a tab click
- Observed: after clicking a role tab (Khách hàng / Nhà cung cấp) or a group tab (Tất cả / Công ty / …), the focus stays on the tab.
  - ↑/↓ do nothing; Enter picks nothing.
  - ←/→ move between tabs, and Radix's automatic activation switches the role or group (the user's role choice is undone).
- Expected (spec §7, "↑↓ chọn dòng, Enter chọn"): after a tab change the keyboard drives the list again.
- Cause:
  - `partner-catalog-dialog.tsx` (`Tabs onValueChange={setRole}`) remounts `PartnerCatalogList` with `key={role}`. Its mount effect (`partner-catalog-list.tsx`, `searchRef.current?.focus()`) runs during the tab's mousedown, and then the browser's default mousedown focus moves the focus to the tab button.
  - The group tabs never refocus the search.
- Fix: put the focus back on the search input after a role or group change, for example `onMouseDown={(e) => e.preventDefault()}` on the `TabsTrigger`s, or focus the search in `handleGroupChange` and after `setRole` with a `setTimeout(0)`. Another option is to forward ↑/↓/Enter from the dialog content to the list.
- Test idea: in `partner-catalog-dialog.test.tsx`, `userEvent.click(tab 'Nhà cung cấp')` then `keyboard('{ArrowDown}')`, and expect the second row to have `aria-selected=true` and the search input to have the focus. The same for a group tab.

### D4 (Minor, cosmetic): row label not level with the code line when code and name wrap
- Observed: where the name drops below the code (all three forms below 1024 px; the stock voucher at 1024–1279 px), the "Đối tượng" / "Khách hàng" label is vertically centred between the code line and the name line.
  - On the stock voucher at 1100 the `Đ.chỉ` label and address input also sit between the two lines (address top 276 vs code 258 / name 294).
  - The spec's hard rule (the name never enters the label column) is met.
- Cause: the partner rows use the default `align-items: center`: `cash-voucher-header.tsx:165` (`form-inline-grid`), `stock-voucher-main-fields.tsx:167` (`form-cols-stock-partner`), and `quotation-form-page.tsx:813`.
- Fix: give these rows `form-align-top` (labels get line-height 1.75rem and align with the 28 px code input). Check that the stock `field-message-code` row placement still holds.
- Test idea: a class assertion in each form test, plus a re-measure at 800 and 1100 in the browser (label top equals code input top).

## Other observations (no defect claimed)
- The yellow "Đã đổi lý do…" line stays until the next partner or reason action, or a save/reset. It survived more than 10 s, Enter walks and typing elsewhere. Only the amber ring is temporary (about 1.6 s). See S2.
- In the stock form the 1440 reason column is narrow, so the yellow line takes two lines.
- In the stock list filter the selected partner shows its name rather than its code. This is the existing `code: partnerName || partnerId` behaviour.
- When the stock and quotation forms restore a draft with a picked partner, clicking into the code input and typing inserts the characters into the code text. This is the existing "typing over a picked partner starts a new search" rule.


---

## Re-run at 6e57ad22

- Date: 2026-10-15, 16:30–16:53. Checkout: `%TEMP%\cvf-browser2` at `6e57ad22`, the feature branch tip. It contains the D1–D4 fixes (`4b468adc`, `328f055c`, `8f64a481`, `6e57ad22`) and Task 6.1's removal of the reason-based search mode (`fac25400`). `git status` was clean afterwards.
- Isolation (`rerun-isolation-proof.txt`):
  - New scratch DB `qldonhang_cvf_browser`: 50 public tables and the seeded `admin` before the first request.
  - API on 5055: PID 29092 `OrderMgmt.WebApi.exe` from `cvf-browser2`, parent `dotnet run` 14804, both connection strings overridden.
  - Vite on 5181: PID 28720, parent cmd 2936, from `cvf-browser2/frontend`.
  - Nothing listened on 5050. Port 5182 belonged to another session (PID 15696); I did not touch it.
  - Every re-run result file has 0 hits for `:5050`. Hubs were blocked as before.
- Data: re-created through `setup-b.js` and `setup-b2.js`. Mode A fixtures were re-recorded from the new backend (`fixtures-a.json`; the first-run set is kept as `fixtures-a-b4aaeb56.json`). Unmocked `/api` calls (501): none. Path fallbacks: none.
- Evidence naming: re-run screenshots and results carry the prefix `rerun-`, so the first-run files are kept.
- Cleanup (`rerun-cleanup.txt`): stopped PIDs 29092, 14804, 28720 and 2936. Ports 5181, 5055 and 5050 are free. `drop database qldonhang_cvf_browser` returned `DROP DATABASE`, and 0 rows are left.

### Fix verification

| Item | Result | Measurements | Evidence |
|---|---|---|---|
| D1: stock row 1 level with the reason notice | **PASS** | 1440: control tops with the notice `[188,188,188,188]` (was 188/188/203/203) and label tops `[188,188,188,188]`. 1100 (2×2 grid): date/reason `188/188`, Kho/HTTT `238/238`, labels equal to their controls. 800 (one column): labels equal to their controls (232, 268, 318, 354). | `rerun-s8-{1440x900,1100x800,800x900}-stock-main-notice.png` |
| D2: quotation Enter ignores `isComposing` | **PASS** | `customerId`, `customerName`, `deliveryAddress`: after the CDP composition Enter (VK 229) and a synthetic `isComposing` Enter, the focus stays and the composition completes to "việt". Receipt and stock fields still pass. | `rerun-results-b2-s5-s6-s10.json` (S10) |
| D3: catalog tabs return the focus to the search | **PASS** | Clicked "Nhà cung cấp": focus on "Tìm mã, tên nhà cung cấp...", title "Danh mục nhà cung cấp". ArrowDown moves the highlight to row 2; Enter picks NCC002 and the focus moves to `cash-partner-name`. ArrowRight after the click no longer switches the role. Group tab "Công ty": focus on the search; ArrowDown → row 2; Enter picks KNB01, focus on the name. | `rerun-d3-role-tab.png`, `rerun-d3-group-tab.png` |
| D4: partner rows `form-align-top` | **PASS** | When code and name wrap, the label top equals the code input top: receipt 800 `394/394`, stock 800 `374/374`, quotation 800 `394/394`, stock 1100 `258/258`, and the stock 1100 Đ.chỉ label and input are also at 258. Nothing moved at 1440: receipt and quotation code/name at 278, stock code and address at 222 (same as the first run). | `rerun-s8-800x900-*.png`, `rerun-s8-1100x800-stock-main*.png`, `rerun-s8-1440x900-*.png` |

### Regression pass

| # | Scenario | Result | Notes / evidence |
|---|---|---|---|
| 1 | Receipt, keyboard only | PASS | Same walk and auto text as the first run; PT00001 saved with 1 line and no `partnerTaxCode`; Print enabled after the reopen. `rerun-s1-*.png` |
| 3 | Quick add, plus Esc | PASS | No `POST /api/cash-vouchers`; focus on the name, and still there after 1.2 s; Esc returns text and focus to the code input (also after opening with `+`). `rerun-s3-*.png` |
| 4 | Fast Enter, 600 ms (Mode A; extra, because the search changed) | PASS | Exact code is picked, stale results are never used, an unknown code shows "Thêm mới" selected with no dialog, and a second Enter opens it. `rerun-s4-*.png` |
| 5 | Stock voucher in/out | PASS | The search lists both roles; reason switches out→"Xuất trả hàng NCC" and in→"Nhập hàng bán bị trả lại"; Enter order unchanged; the mocked 400 `partnerId` shows under the partner container. Row 2 class is now `form-inline-grid form-cols-stock-partner form-align-top`. `rerun-s5-*.png` |
| 6 | Quotation | PASS | Enter chain `customerId → customerName → deliveryAddress`; F4 quick add has no role boxes; unpicked text blocks the save with "Vui lòng chọn khách hàng từ danh sách". `rerun-s6-*.png` |
| 8 | Widths | PASS on every real check | No horizontal scroll at any width; the name never enters the label column; errors sit under the partner container; notices do not overlap the next row; D1 and D4 as above. The only "FAIL" line in `rerun-results-a-s8-s4.json` is my script's cash-reason-row check at 800 px: there Lý do and Người nộp are on separate rows by design, as noted in the first run. Not a defect. `rerun-s8-*.png` |
| 9 | Filters (search mode removed) | PASS | Stock-out list filter: no `+`, "Tìm thấy 6 đối tượng" (no "đang dùng"), SĐT column, no extra rows; selecting works. Opening-debt dialog picker: no `+`, SĐT column, no extra rows; Enter picks. `rerun-s9-*.png` |
| 10 | IME | PASS | See D2; 8 fields on 3 forms. |

Partner search requests after the mode removal (all re-run request logs):
- The forms send `GET /api/cash-vouchers/partners?type=Receipt&keyword=…&activeOnly=true` and `GET /api/stock-vouchers/partners?type=In|Out&keyword=…&activeOnly=true`.
- The stock list filter sends `GET /api/stock-vouchers/partners?type=Out&keyword=…`, without `activeOnly`.
- The opening-debt picker sends `GET /api/debt/partners?side=Receivable&keyword=…`.
- No request carries `reasonId`.

New defects: none.
