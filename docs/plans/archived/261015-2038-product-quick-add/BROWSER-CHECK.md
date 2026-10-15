# Browser check: product quick add (Task 4.4)

- Date: 2026-10-15, 21:54–22:13 (Asia/Ho_Chi_Minh)
- Code under test: detached checkout `%TEMP%\pqa-browser` at `e79b6ec2` (feature branch tip at the start of the run). Read-only run: no source edits, commits or pushes. `git status` in the checkout was clean before it was removed. The branch tip moved to `6b0afd3e` during the run (another session); that commit was not tested.
- Browser: Microsoft Edge (headless), driven by `playwright-core` 1.64.1 (`chromium.launch({ channel: 'msedge' })`). Each context used `serviceWorkers: 'block'`, locale `vi-VN` and time zone `Asia/Ho_Chi_Minh`.
- Scripts (kept in the session scratchpad, not committed): `lib.js` (reused from the cash voucher check, ports changed), `setup.js` (data through the real API), `scen.js` (S1–S6, run once as Admin and again as the Warehouse user), `perm.js` (removes or restores `products.create` on WAREHOUSE), `probes.js` (P1–P5). Raw results: `results-*.json`, each with the full request log.
- Screenshots: `browser-check/`.

## Summary

| # | Scenario | Admin | Warehouse (with `products.create`) |
|---|---|---|---|
| S1 | Stock out: `TL-50` + Enter, quick add, line filled, voucher not saved, found in row 2 | PASS | PASS |
| S2 | Stock in: F4 on an empty cell, Esc | PASS | PASS |
| S3 | Quotation: add row with the arrows, focus to Tên; free-text line | PASS | n/a (the seeded Warehouse role has no quotation permission) |
| S4 | Opening stock: add row, row gets the product, focus back in the code cell | PASS | PASS |
| S5 | Warehouse WITHOUT `products.create`: no add row, F4 does nothing | – | PASS |
| S6 | Stock card (and cost recalculation): no add row, F4 does nothing | PASS | PASS (stock card; the Warehouse role cannot open cost recalculation) |
| P1 | Held Enter in the code cell on an unknown code | INFO (nothing saved; focus walks the dialog fields) | – |
| P2 | Held Ctrl+Enter on save: exactly one `POST /api/products` | **FAIL** (D1) | – |
| P3 | IME: a composing Enter in the dialog fields does nothing | PASS | – |
| P3b | IME: a composing Enter in the code cell | **FAIL** (D2) | – |
| P4 | Dialog at 1440 / 1100 / 800; scrolls; error box on top after a failed save | PASS, with D3 (error box out of view) | – |
| P5 | Quotation: arrows past real results to the add row | PASS | – |

Defects: D1 Important, D2 Important, D3 Minor (details below). **All three are fixed in `44df4a8a`**; see "Re-check on 44df4a8a" at the end.

## Isolation proof (`isolation-proof.txt`, `cleanup.txt`)

- `:5050` was already listening before the run: PID 21308, `D:\Projects\QLBanHang\backend\src\OrderMgmt.WebApi\bin\Debug\net9.0\OrderMgmt.WebApi.exe` (the developer backend). I did not touch it. No script sent anything to it: every page request was logged (`page.on('request')`, plus the rewritten target of every `/api` call), every node `fetch` went to `http://localhost:5056`, each script ends with `assertNo5050()`, and every `results-*.json` has 0 occurrences of `:5050`.
- `/api/*` was rewritten to `:5056` with `route.continue`. `/hubs/*` was aborted and hub WebSockets were closed through `context.routeWebSocket` (a hub socket on `:5186` would otherwise go through the Vite proxy to `:5050`). The SignalR notification hub was therefore offline; no scenario needs it.
- Scratch DB `qldonhang_pqa_browser`, created at 21:54. Before the first request it had 50 public tables and one user (`admin`).
- API: started from `pqa-browser/backend/src/OrderMgmt.WebApi` with `ConnectionStrings__Default` and `ConnectionStrings__DefaultConnection` both set to the scratch DB (the app reads `ConnectionStrings:Default`; `appsettings.Development.json` points it at `qldonhang_test`, which the override replaces), `ASPNETCORE_URLS=http://localhost:5056`, `ASPNETCORE_ENVIRONMENT=Development`, `Database__AutoMigrateAndSeed=true` and `--no-launch-profile`. The log shows `Now listening on: http://localhost:5056`. PID 22180 (`OrderMgmt.WebApi.exe` from `pqa-browser`), parent `dotnet run` PID 6652.
- Vite: `npx vite --port 5186 --strictPort` from `pqa-browser/frontend`, PID 24800 (parent cmd 13108). `vite.config.ts` was not edited. `frontend/node_modules` was a junction to the main checkout's `node_modules`.
- `qldonhang_test`, `qldonhang` and `qldonhang_integtest*` were never connected to.

## Data (real API on :5056, `setup.json`)

Created as admin: product group "Tôn lợp" (TON), unit "Tấm", product TL-01 "Tôn lạnh 0.45", customer KH0001, supplier NCC001, and the Warehouse user `kho` (role WAREHOUSE "Kho / giao hàng"). The seed provided KHO01 and the stock reasons. On this fresh DB the WAREHOUSE role already had `products.create` (Phase 01 seed grant). For S5 I removed it with `PUT /api/admin/roles/{id}/permissions` (`perm-remove.txt`), logged `kho` in again, and restored it afterwards. The login limit (5 per minute) was handled by the 61 s wait in `lib.js`; it did not trigger.

Product codes: the first Admin run used `TL-50` exactly. Every later run adds a suffix (`TL-50b`, `TL-50-K` for the Warehouse user, and so on) because the earlier run had already created the product (see S1).

## Scenarios

### S1 Stock out, unknown code + Enter: PASS (Admin and Warehouse)
`/stock-out/new`, cell `stock-line-product-code-0`.
- Typed `TL-50`, waited for the results: the list showed "Tìm thấy 0 sản phẩm" with the single row `Thêm mới hàng hóa "TL-50"` (Enter / F4, `aria-selected=true`); the keys hint shows "F4 thêm mới".
- Enter: dialog "Thêm nhanh hàng hóa" opened, Mã = `TL-50`, focus in `#name` (Tên).
- Typed Tên, picked Nhóm "Tôn lợp", typed a NEW unit by hand ("Cuộn"; Warehouse: "Bó"; the list shows "Sẽ thêm mới"), Ctrl+Enter.
- Requests after Ctrl+Enter: `POST /api/lookups/units`, `GET /api/lookups/units`, `POST /api/products` (one), `GET /api/products/search?q=TL-50…`, `POST /api/stock-vouchers/stock-at` (the on-hand lookup for the line). No `POST`/`PUT /api/stock-vouchers` at any point.
- The dialog closed, the line shows `TL-50 | Tôn lạnh màu 0.50 … | Cuộn`, and the focus is on `stock-line-quantity-0` (the next Enter stop, "Số lượng dòng 1"). The URL stayed `/stock-out/new`. The header shows "Nháp chưa lưu từ 22:01" (the draft marker) and the toast "Đã tạo hàng hóa".
- Row 2: Enter from SL walked `stock-line-unit-price-0 → stock-line-product-code-1`. Typing the same code there lists it: `TL-50b Tôn lạnh màu 0.50 admin` (selected) and, below it, `Thêm mới hàng hóa "TL-50b"` with only F4 as its key.
- Note on the run with `TL-50` itself: every step above passed; only my script's row-2 step failed (row 2 does not exist until the Enter walk creates it). I fixed the script and reran with `TL-50b`. The screenshots are from the rerun.
- Fast Enter (typed `TL-77b` and pressed Enter before the results): no dialog; the list opened with the add row highlighted (`Enter / F4`). A second Enter opened the dialog. This matches the partner field's behaviour (cash voucher check S4).
- Evidence: `admin-so-list.png`, `admin-so-dialog-open.png`, `admin-so-dialog-filled.png`, `admin-so-after-save.png`, `admin-so-row2-found.png`, `admin-so-fast-enter.png`, and the same with prefix `kho-`.

### S2 Stock in, F4 on an empty cell, Esc: PASS (Admin and Warehouse)
- `/stock-in/new`, click `stock-line-product-code-0`, F4: the dialog opened with the focus in `#code` (Mã).
- Esc: the dialog closed, focus back on `stock-line-product-code-0`, `aria-expanded=false`, no list rendered.
- Evidence: `admin-si-f4.png`, `admin-si-esc.png`, `kho-si-f4.png`, `kho-si-esc.png`.

### S3 Quotation: PASS (Admin)
- `/quotations/new`, `line-product-code-0`, typed `tôn mớib`: the only row was `Thêm mới hàng hóa "tôn mớib"` (already highlighted, so the arrows had nothing to move; P5 covers arrows past real results). Enter opened the dialog with Tên = `tôn mớib`, Mã empty and the focus in Mã.
- Picked the group, unit "Tấm", Ctrl+Enter: one `POST /api/products`; the product got the generated code `HH-261015-0001`; the line shows the code, name and ĐVT "Tấm"; focus on `line-name-0` (the Tên hàng textarea). No `POST`/`PUT /api/quotations`; "Nháp chưa lưu" shown.
- Free-text line (row 2, added with "Thêm dòng"): typed `XYZ-999b`, waited for the results, Enter: **the dialog opened directly** (the add row was the only row and already highlighted; focus in `#name`, since the seed was read as a code). Esc: dialog closed, focus back in `line-product-code-1`, value `XYZ-999b` kept. Enter again: no dialog, the focus moved to `line-name-1`; typing a name there works, so a line can be entered without a catalog product.
- Evidence: `admin-q-addrow.png`, `admin-q-dialog.png`, `admin-q-after-save.png`, `admin-q-freetext-enter.png`, `admin-q-freetext-name.png`.

### S4 Opening stock: PASS (Admin and Warehouse)
- `/inventory/opening-stock`, warehouse KHO01 preselected. The grid starts empty, so I pressed "Thêm dòng". (The yellow "Chi nhánh chưa có ngày đầu kỳ" notice was shown; it only blocks saving, which was not tested.)
- Typed `tôn hoa senb` (Warehouse: `tôn kho`): the add row was highlighted; Enter opened the dialog with Tên prefilled. I set Mã `THS-01b` (Warehouse `TK-01`), group, unit, then Ctrl+Enter on Mã.
- The row shows `THS-01b | tôn hoa senb | Tấm`; the focus is back in `opening-product-code-0`, and the list stays closed (`aria-expanded=false`).
- Evidence: `admin-os-addrow.png`, `admin-os-after-save.png`, `kho-os-addrow.png`, `kho-os-after-save.png`.

### S5 Warehouse without `products.create`: PASS
After removing the permission and logging in again, on `/stock-out/new`, `/stock-in/new` and `/inventory/opening-stock`, typing `ZZ-404` shows only "Tìm thấy 0 sản phẩm" / "Không tìm thấy sản phẩm phù hợp" and "Xem danh mục đầy đủ". There is no "Thêm mới" row and no "F4 thêm mới" in the keys hint. F4 and Enter open nothing, and the focus stays in the cell. 0 `POST /api/products`.
Evidence: `kho-noperm-noperm-stock-out-new.png`, `kho-noperm-noperm-stock-in-new.png`, `kho-noperm-noperm-inventory-opening-stock.png`.

### S6 Stock card and cost recalculation: PASS
`/inventory/stock-card` (`#stock-card-product`) and `/settings/recalc-cost` (`#recalc-product`, Admin only) with `ZZ-404`: no add row, no F4 hint; F4 and Enter open nothing. Evidence: `admin-stock-card-product-noadd.png`, `admin-recalc-product-noadd.png`, `kho-stock-card-product-noadd.png`.

### P1 Held Enter in the code cell on an unknown code: INFO
CDP key events on `/stock-in/new`, settled list for `HOLD-n`: one Enter keydown, then `n-1` keydowns with `autoRepeat: true` every 33 ms.
- 10 keydowns: the first opened the dialog (Mã `HOLD-10`, focus Tên); the repeats walked the fields `productGroupId → unitId → specification → pricingMode → length → … → defaultPrice`. Nothing was saved.
- 30 keydowns: the walk reached Ghi chú, and the next Enter submitted the form. Validation stopped it ("Tên hàng hóa là bắt buộc", "Chọn nhóm hàng hóa", "Nhập đơn vị tính") and the focus went back to Tên, then the walk went on. 0 `POST /api/products`.
- Esc afterwards returns the focus to the cell.
- A held Enter cannot create a product here, because group and unit are required and start empty. The focus does run away through the fields, though. Reported, not claimed as a defect. Evidence: `p1-held-enter-10.png`, `p1-held-enter-30.png`.

### P2 Held Ctrl+Enter on save: FAIL (D1)
- P2: seed `CE-01c` (with a code), 15 Ctrl+Enter keydowns (1 + 14 auto-repeats, 33 ms): **3** `POST /api/products`, statuses 200, 409, 409. Toasts: "Đã tạo hàng hóa CE-01c" and twice "Không thể lưu — Mã hàng 'CE-01c' đã tồn tại.". The line got CE-01c.
- P2b: seed `ctrl enter giữd` (name only, so the code is generated): **3** `POST /api/products`, all 200. **Three products were created**: `HH-261015-0002`, `-0003` and `-0004`, 80–120 ms apart (checked in the scratch DB). The line got `HH-261015-0002`.
- P2c: two separate Ctrl+Enter presses 150 ms apart (no auto-repeat), name-only seed: **2** products created (`HH-261015-0005`, `-0006`).
- Evidence: `p2-held-ctrl-enter.png`, `p2b-held-ctrl-enter.png`, `p2c-held-ctrl-enter.png`.

### P3 IME in the dialog: PASS
In `code`, `name`, `unitId`, `specification`, `defaultPrice` and `note`: CDP `Input.imeSetComposition` ("vie" → "viê"), then the browser's own Enter during composition (VK 229), then synthetic `keydown` Enter and Ctrl+Enter with `isComposing: true`, then `Input.insertText` "việt". The focus stayed in each field, the text completed to "việt" (`defaultPrice` is a number input, so it stays empty), the dialog stayed open, and there were 0 `POST /api/products`.

### P3b IME in the code cell: FAIL (D2)
- With the list settled on `IME-d` (add row highlighted), a synthetic `keydown` Enter with `isComposing: true` opened the dialog.
- Real composition: typed `XYZ`, started a composition `tô` and waited 1.5 s (the search for `XYZtô` settled while composing, as when the user pauses mid-word). The browser's Enter during composition arrived as `Enter/229/composing=true` and **opened the dialog** with "XYZtô".
- Evidence: `p3-cell-ime.png`.

### P4 Layout: PASS, with D3
The dialog was opened with F4 on `TL-50` (which exists, so Enter would pick it).

| Width | Dialog box | Height vs 90vh | Content | Page `scrollWidth` |
|---|---|---|---|---|
| 1440x900 | 272,45 896x810 | 808 ≤ 810 | scrollHeight 1234 > clientHeight 808: the dialog scrolls | 1440 |
| 1100x800 | 102,40 896x720 | 718 ≤ 720 | scrolls | 1100 |
| 800x900 | 0,45 800x810 | 808 ≤ 810 | scrolls; fields two per row (342 px) | 800 |

- Mã, Tên, Nhóm and ĐVT are visible without scrolling at every width (tops 174–290).
- At 800 px the dialog takes the full width with no side margin (the shared `DialogContent`), so it reads as edge to edge. This is cosmetic and not specific to this feature.
- Duplicate code: saved `TL-50` again by scrolling to the bottom and clicking "Lưu và chọn". The response was 409, the dialog stayed open, and the red box "Mã hàng 'TL-50' đã tồn tại." sits at the top of the form, above "Thông tin chung" (correct order). But it is out of view: box top −322/−479 px vs dialog top 45/40, with scrollTop 426/578 (D3). A toast "Không thể lưu / Mã hàng 'TL-50' đã tồn tại." shows top right, over the dialog.
- Evidence: `p4-{1440x900,1100x800,800x900}-dialog.png`, `-error.png` (as left after the save) and `-error-top.png` (scrolled to the top).

### P5 Quotation, arrows past real results: PASS
- `tôn` lists 7 products, then `Thêm mới hàng hóa "tôn"` (F4 only).
- ArrowDown ×7 reaches the add row. Tab from the add row goes to the first result, and ArrowUp from the first result wraps to the add row.
- Enter opened the dialog with Tên = `tôn`, Mã empty, focus in Mã. Esc returned the focus to `line-product-code-0`.
- Evidence: `p5-q-arrows.png`, `p5-q-dialog.png`.

## Defects

### D1 (Important): a held or double Ctrl+Enter creates the product more than once
- Repro: open the quick add from any product cell with a name-only seed (for example type `tôn mới` and pick the add row), fill group and unit, then press Ctrl+Enter twice within about 150 ms, or hold it.
- Expected: one `POST /api/products` and one product (the plan's probe criterion).
- Actual: one product per press after the first save finishes. P2b created 3 products with generated codes `HH-261015-0002/3/4`; P2c created 2. With a typed code, the extra posts get 409 and show "Không thể lưu … đã tồn tại" toasts after the success toast.
- Likely cause: `ProductFormFields`'s `busy` ref (`product-form-fields.tsx:98-111`) only covers the time a submit runs, and is released in `finally`. After a successful save, `QuickAddBody` calls `onOpenChange(false)`, but the dialog content stays mounted and focused during the close animation. Further Ctrl+Enter keydowns reach `handleEnterKeys` and start new submits (`submitting` is false again by then).
- Fix idea: in `QuickAddBody`, keep a `savedRef` that is set on success, and return early from `handleSubmit` once it is set. Or keep `busy` set after a successful submit (the form is about to unmount).
- Test idea: in `product-quick-add-dialog.test.tsx`, resolve the create mock, fire Ctrl+Enter, await, fire Ctrl+Enter again before the dialog unmounts, and expect `create` to have been called once.
- The product page probably shares the release-on-success window, but it navigates away; not checked.

### D2 (Important): a composing Enter in the product code cell opens the quick add
- Repro: in a stock-in line, type `XYZ`, start an IME composition (the search settles while you compose), then press Enter to confirm the composition.
- Expected: the composing Enter belongs to the input method (the same rule as the dialog fields, which pass P3, and the cash voucher check's D2).
- Actual: the dialog opens with the half-composed text. With a matching product, the same Enter would pick it; that part is pre-existing.
- Cause: `ProductTypeaheadCell.handleKeyDown` (`pages/quotations/components/product-typeahead-cell.tsx:214`) has no `e.nativeEvent.isComposing` check. The branch adds the F4, waiting-Enter and add-row paths to that handler.
- Fix: return at the top of `handleKeyDown` when `e.nativeEvent.isComposing`.
- Test idea: `fireEvent.keyDown(cell, { key: 'Enter', isComposing: true })` with the add row highlighted; expect no dialog.

### D3 (Minor): the save error box is out of view when the save starts from the bottom of the dialog
- Repro: open the dialog at any width, scroll to the bottom (or let Enter walk to Ghi chú), and save with a duplicate code.
- Expected: the error box at the top of the dialog is visible.
- Actual: the box is above the visible area (scrollTop 426–578). Only the toast tells the user what happened, and the toast covers part of the dialog's top-right fields.
- Fix idea: scroll the dialog to the top (or `scrollIntoView` the box) when `submitError` becomes non-empty.

## Other observations (no defect claimed)
- Enter before the results (S1 fast Enter) shows the list with the add row highlighted; a second Enter opens the dialog. This is the same two-step behaviour as the partner field.
- When the seed reads as a name (`tôn mới`, `tôn`), the dialog focuses Mã (empty, "Để trống để tự sinh"), and Tên is prefilled. When it reads as a code (`TL-50`, `XYZ-999b`), the dialog focuses Tên.
- Toasts appear top right on this branch (the bottom-centre toast change is not in it).

## Cleanup (`cleanup.txt`)
- Stopped PIDs 22180 (`OrderMgmt.WebApi.exe`), 6652 (`dotnet run`), 24800 (Vite node) and 13108 (its cmd). At 22:13 nothing listened on 5056 or 5186. Port 5050 is still the developer backend PID 21308, which I did not touch.
- Restored `products.create` on WAREHOUSE before the drop. `drop database qldonhang_pqa_browser` returned `DROP DATABASE`, and `pg_database` has 0 rows with that name.
- I removed the `frontend/node_modules` junction first (checked `LinkType -eq 'Junction'`, then `[System.IO.Directory]::Delete(path, $false)`), and then ran `git worktree remove --force`. `D:\Projects\QLBanHang\frontend\node_modules` still has 500 entries. `%TEMP%\pqa-browser` is gone and is no longer in `git worktree list`.

---

## Re-check on 44df4a8a

- Date: 2026-10-15, 22:19–22:25. Checkout: detached `%TEMP%\pqa-browser2` at `44df4a8a`, which contains "fix(products): one quick add save per open; IME Enter in the code cell; error box in view". `git status` in the checkout was clean before it was removed.
- Scope: S1 smoke, P1, P2 (plus P2b, P2c and the new P2d), P3, P3b and P4. Same scripts with the ports and the `recheck-` file prefix set by environment variables. Evidence: `browser-check/recheck-*.png`; raw results: `recheck-results-*.json`.
- Isolation (`recheck-isolation-proof.txt`):
  - New scratch DB `qldonhang_pqa_browser2`: 50 public tables and the one user `admin` before the first request.
  - API on 5057: PID 17952 `OrderMgmt.WebApi.exe` from `pqa-browser2`, parent `dotnet run` 6940. Both connection strings were overridden, plus `ASPNETCORE_ENVIRONMENT=Development`, `Database__AutoMigrateAndSeed=true` and `--no-launch-profile`.
  - Vite on 5187: PID 20888 from `pqa-browser2/frontend`.
  - Port 5050 is still only the developer backend PID 21308, untouched. `serviceWorkers: 'block'`, `/hubs/*` blocked, and 0 hits for `:5050` in every `recheck-results-*.json`.
- Data: `setup.js` as before (group, unit "Tấm", TL-01, KH0001, NCC001, user `kho`).

| Item | Result | Measurements | Evidence |
|---|---|---|---|
| S1 smoke: stock out `TL-50` → quick add → line filled | **PASS** | Enter with the add row shown opened the dialog with Mã `TL-50` and the focus in Tên. Ctrl+Enter: one `POST /api/lookups/units` (new unit "Cuộn"), one `POST /api/products`, no `POST`/`PUT /api/stock-vouchers`. The line reads `TL-50 / Tôn lạnh màu 0.50 admin / Cuộn`; focus on `stock-line-quantity-0`; "Nháp chưa lưu" shown; URL `/stock-out/new`. Row 2 finds `TL-50`. | `recheck-admin-so-*.png` |
| D1 / P2: held Ctrl+Enter (15 keydowns: 1 + 14 auto-repeat), seed with a code | **PASS (fixed)** | 1 `POST /api/products` (200); one toast "Đã tạo hàng hóa CE-01r"; the line got CE-01r; focus on SL. | `recheck-p2-held-ctrl-enter.png` |
| D1 / P2b: held Ctrl+Enter, name-only seed (generated code) | **PASS (fixed)** | 1 POST (200), one product `HH-261015-0001` (was 3 before). | `recheck-p2b-held-ctrl-enter.png` |
| D1 / P2c: two separate Ctrl+Enter presses 150 ms apart | **PASS (fixed)** | 1 POST (200), one product `HH-261015-0002` (was 2 before). | `recheck-p2c-held-ctrl-enter.png` |
| P2d: failed save, then a corrected Ctrl+Enter | **PASS** | Mã `TL-50` (duplicate) + Ctrl+Enter: 409; the dialog stays open with the error box "Mã hàng 'TL-50' đã tồn tại.". Changed Mã to `TL-51r`, Ctrl+Enter: exactly 1 more POST (200); the dialog closed; the cell is `TL-51r`; focus on SL. The first failure does not block the next save. | `recheck-p2d-duplicate.png`, `recheck-p2d-corrected.png` |
| DB check after P2–P2d | **PASS** | `products` holds exactly TL-01, TL-50, CE-01r, HH-261015-0001, HH-261015-0002 and TL-51r. No duplicates. | – |
| D2 / P3b: composing Enter in the code cell | **PASS (fixed)** | Synthetic `keydown` Enter with `isComposing: true` with the add row highlighted: no dialog, focus stays in the cell. A normal Enter right after opens the dialog. Real CDP composition `tô` (search settled while composing), browser Enter `Enter/229/composing=true`: no dialog, value `XYZtô`. After the composition commits, a normal Enter opens the dialog (Tên `XYZtô`, Mã empty). | `recheck-p3-cell-ime.png`, `recheck-p3b-normal-enter.png` |
| P3: composing Enter in the dialog fields (regression) | **PASS** | Same as before: focus stays in all 6 fields, text completes to "việt", 0 POST. | – |
| D3 / P4: error box in view after a save from the bottom | **PASS (fixed)** | Saved a duplicate `TL-50` after scrolling the dialog to the bottom. The box is fully inside the dialog's scroll viewport at every width (`getBoundingClientRect` vs the dialog rect): 1440x900 box 46–92 in dialog 45–855; 1100x800 box 41–87 in dialog 40–760; 800x900 box 46–92 in dialog 45–855. The dialog scrolled to scrollTop 58 (from 426/578), so the box sits just above "Thông tin chung" (card top 108/103). Sizes unchanged: height ≤ 90vh, no page horizontal scroll. | `recheck-p4-{1440x900,1100x800,800x900}-{dialog,error,error-top}.png` |
| P1: held Enter in the code cell on an unknown code | **Changed, as intended** | 10 keydowns (1 + 9 repeats): the dialog opened once and the focus stayed in Tên; the repeats were ignored (before: they walked to Giá bán mặc định). 30 keydowns: the focus sat on the dialog itself while the lookups loaded, then Tên, and stayed there. No validation messages, 0 POST. Esc returns to the cell. | `recheck-p1-held-enter-10.png`, `recheck-p1-held-enter-30.png` |

Defect status: **D1 fixed, D2 fixed, D3 fixed.** New defects: none.

Re-check cleanup (`recheck-cleanup.txt`):
- Stopped PIDs 17952, 6940, 20888 and 3776. At 22:25 nothing listened on 5057 or 5187; 5050 is still only PID 21308.
- I removed the `frontend/node_modules` junction first (checked that it was a junction, then deleted it non-recursively), and then ran `git worktree remove --force`. `D:\Projects\QLBanHang\frontend\node_modules` still has 500 entries, and `%TEMP%\pqa-browser2` is gone.
- `drop database qldonhang_pqa_browser2` returned `DROP DATABASE`, and 0 `qldonhang_pqa_browser%` databases remain.

---

## Re-check on 221ab10f

- Date: 2026-10-15, 22:28–22:31. Checkout: detached `%TEMP%\pqa-browser3` at `221ab10f`, which contains "fix: composing Enter does not move focus in the line grids" (`line-items-grid.tsx` and `stock-line-grid.tsx` now ignore an Enter with `isComposing`). `git status` in the checkout was clean before it was removed.
- Script: `ime2.js`, new. Each check uses two kinds of Enter:
  - **Real composition:** CDP `Input.imeSetComposition`, a 1.5 s pause (the search of the composing text settles), then the browser's own Enter during composition. A key logger confirmed that the page received `Enter/229/composing=true`. Then `Input.insertText` commits the text.
  - **Synthetic:** a `keydown` Enter with `isComposing: true`.
- The S1 smoke reuses `scen.js`. Evidence: `browser-check/recheck2-*.png`; raw results: `recheck2-results-*.json`.
- Isolation (`recheck2-isolation-proof.txt`):
  - New scratch DB `qldonhang_pqa_browser3`: 50 public tables and the one user `admin` before the first request.
  - API on 5058: PID 6568 from `pqa-browser3`, parent `dotnet run` 25276. Both connection strings were overridden.
  - Vite on 5188: PID 17328, parents cmd 7148 and npx 10648.
  - Port 5050 is still only the developer backend PID 21308, untouched. `serviceWorkers: 'block'`, `/hubs/*` blocked, and 0 hits for `:5050` in the result files.
- Data: `setup.js` (group, unit "Tấm", TL-01 "Tôn lạnh 0.45", KH0001, NCC001, user `kho`).

| # | Check | Result | Measurements | Evidence |
|---|---|---|---|---|
| 1a | Quotation, Mã hàng: `XYZ` + composition `tô`, composing Enter | **PASS** | Real composition: focus stays on `line-product-code-0`, value `XYZtô`, no dialog, Tên empty. The list still shows `Thêm mới hàng hóa "XYZtô"` highlighted. Synthetic composing Enter: focus stays, no dialog. Then a normal Enter opens the quick add, as before; Esc returns to the cell. | `recheck2-q-real-compose-enter.png` |
| 1b | Quotation, Mã hàng: composition `tôn` in an empty cell, with TL-01 highlighted in the list | **PASS** | Real composition: focus stays, nothing picked (Tên empty), no dialog. Synthetic: the same. Then a normal Enter picks TL-01: code `TL-01`, Tên "Tôn lạnh 0.45", focus on `line-name-0` (as before). | `recheck2-q-real-compose-nopick.png`, `recheck2-q-normal-enter-pick.png` |
| 2 | Stock out, code cell of a row that already has TL-01 | **PASS** | Real composition `ô` at the end: focus stays on `stock-line-product-code-0`, value `TL-01ô`, no dialog. Synthetic composing Enter on `TL-01`: focus stays, no dialog. | `recheck2-so-code-compose.png` |
| 3 | Stock out, Số lượng (`text`, inputmode `decimal`) | **PASS** | Real composition `5` + composing Enter: focus stays on `stock-line-quantity-0`. Synthetic: the same. A normal Enter then moves to `stock-line-unit-price-0`. | `recheck2-so-qty-compose.png`, `recheck2-so-qty-normal-enter.png` |
| – | Saves during checks 1–3 | **PASS** | 0 `POST`/`PUT` to stock-vouchers, quotations or products. | – |
| 4 | S1 smoke: stock out `TL-50` → quick add | **PASS** | The dialog opened with Mã `TL-50` and the focus in Tên. One unit POST, one product POST, no voucher save. Line `TL-50 / Tôn lạnh màu 0.50 admin / Cuộn`, focus on `stock-line-quantity-0`, "Nháp chưa lưu" shown, row 2 finds `TL-50`. | `recheck2-admin-so-*.png` |

New defects: none.

Re-check cleanup (`recheck2-cleanup.txt`):
- Stopped PIDs 6568, 25276, 17328, 7148 and 10648. At 22:31 nothing listened on 5058 or 5188; 5050 is still only PID 21308.
- I removed the junction first (checked that it was a junction, then deleted it non-recursively), and then ran `git worktree remove --force`. `D:\Projects\QLBanHang\frontend\node_modules` still has 500 entries, and `%TEMP%\pqa-browser3` is gone and no longer in `git worktree list`.
- `drop database qldonhang_pqa_browser3` returned `DROP DATABASE`, and 0 `qldonhang_pqa_browser%` databases remain.
