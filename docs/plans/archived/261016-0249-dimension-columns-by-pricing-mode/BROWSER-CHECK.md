# Browser check: dimension columns by pricing mode (Phase 04, Tasks 4.2-4.9)

- Date: 2026-10-16, 11:41-13:44 (Asia/Ho_Chi_Minh). Result files carry UTC times (local minus 7 h).
- Code under test: the worktree `D:\Projects\QLBanHang\.claude\worktrees\dimension-columns`, branch `feat/dimension-columns`. `git status --short` at the start of Task 4.9 listed only the untracked `browser-check/` screenshots. In detail:
  - First pass (11:52-13:11): the branch at `1dfc8a21` (no code change since the Phase 03 tip). It found defect D1.
  - Fix: `8f326adc` "fix(lib): flash the dimension columns a quick add shows" (committed 13:40:26).
  - Final pass: the fixed code. The stock-out and stock-in walks (13:43, 13:44) and the probes (13:42) ran after the commit. The quotation walk (13:38) and the first re-check segments (stock-out 13:36, stock-in 13:37) ran on the working tree of the fix before it was committed; the last code change reached Vite at 13:35:17 (`vite.log`), so that tree is the one committed at 13:40.
- Browser: Microsoft Edge (headless), driven by `playwright-core` 1.64.1 (`chromium.launch({ channel: 'msedge', headless: true, args: ['--explicitly-allowed-ports=5061'] })`). Edge blocks port 5061 (SIP) with `ERR_UNSAFE_PORT` unless it is allowed (script correction S2). Each context: `serviceWorkers: 'block'`, locale `vi-VN`, time zone `Asia/Ho_Chi_Minh`, viewport 1366x768 (probe P4 adds `reducedMotion: 'reduce'`; P3 adds the `dark` class on `<html>`).
- Scripts (session scratchpad `dimcols-browser/`, not committed): `lib.js`, `setup.js`, `smoke.js`, `walk-stock.js` (`Out` / `In`), `walk-quote.js`, `probes.js`, `diag-quickadd.js`. Raw results: `results-*.json`, each with its checks and the isolation counts; request logs `*.requests.log`.
- Screenshots: `browser-check/` (this folder, committed).

## Summary

The PASS column is the final pass on the fixed code: `results-stock-out.final.json`, `results-stock-in.final.json`, `results-quote.recheck-d1.json`, `results-probes.recheck-d1.json`. The "First pass" column is the run before the fix.

| Walk | Rows | First pass (before D1 fix) | Final pass (fixed code) |
|---|---|---|---|
| Stock-out, mockup 03 steps 1-11 | 1, 2, 3, 4, 5, 6, 7, 8, 9, 9b, 10, 11 (12) | 11/12: **row 5 FAIL (D1)** | **12/12 PASS** |
| Stock-in, steps 1-11 | 1-8, 9, 9b, 10, 11 (12) | 12/12 PASS | **12/12 PASS** |
| Quotation, steps 1-8 and the Mã hàng list | 1-8, M1, M2, M3 (11) | 11/11 PASS | **11/11 PASS** |
| Probes | P1, P2, P3a, P3b, P4, P5, P6, P7 (8) | 8/8 PASS | **8/8 PASS** |

Final pass, row by row (every row is PASS; no FAIL remains):

| Walk | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 9b | 10 | 11 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Stock-out | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| Stock-in | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |

| Quotation | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | M1 | M2 | M3 |
|---|---|---|---|---|---|---|---|---|---|---|---|
| Result | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |

| Probe | P1 | P2 | P3a | P3b | P4 | P5 | P6 | P7 |
|---|---|---|---|---|---|---|---|---|
| Result | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |

Defects: D1 (Important), found by stock-out row 5, fixed in `8f326adc`, re-checked PASS. Script corrections: S1, S2, S3 (not defects). Plan corrections: none.

## Isolation proof (`isolation-proof.txt`)

- Before the run (11:41), `select datname from pg_database where datname like 'qldonhang%'` listed `qldonhang_integration_test`, `qldonhang_integtest`, 15 other `qldonhang_integtest_*` databases of other sessions and `qldonhang_test`. `qldonhang_integtest_dimcols` was not among them. Ports 5061 and 5191 were free; 5050 was not listening.
- Only `qldonhang_integtest_dimcols` was created and used. API: `dotnet run --no-launch-profile` from the worktree's `backend/src/OrderMgmt.WebApi` with `ConnectionStrings__Default` and `ConnectionStrings__DefaultConnection` both set to that database, `ASPNETCORE_URLS=http://localhost:5061`, `Database__AutoMigrateAndSeed=true`, `RateLimiting__LoginPermitLimit=100`. First API PID 19408; it died twice with the agents that had started it and was restarted the same way. The final pass ran against PID 25516 (`...\dimension-columns\backend\src\OrderMgmt.WebApi\bin\Debug\net9.0\OrderMgmt.WebApi.exe`, port 5061; `api.log` shows "Now listening on: http://localhost:5061"). Vite: PID 16176 (`node ...\dimension-columns\frontend\node_modules\vite\bin\vite.js --port 5191 --strictPort`), unchanged for the whole check. `vite.config.ts` was not edited.
- `pg_stat_activity` (13:12): the only `qldonhang%` database with connections was `qldonhang_integtest_dimcols` (1). `qldonhang_test`, `qldonhang` and the shared `qldonhang_integtest*` databases were never connected to by any script.
- Port 5050 had no listener at the start, at 13:12 and at the end of the check (no developer backend was running), so no request could reach it by accident; every `/api` call was in addition rewritten to `:5061` by `route.continue`, `/hubs/*` was aborted and its WebSockets closed.
- `grep -c ':5050'` over all 14 request logs (`*.requests.log`: `diag-quickadd`, `diag-quickadd.before-fix`, `probes`, `probes.recheck-d1`, `setup`, `smoke`, `walk-quote`, `walk-quote.recheck-d1`, `walk-stock-in`, `walk-stock-in.recheck-d1`, `walk-stock-in.final`, `walk-stock-out`, `walk-stock-out.recheck-d1`, `walk-stock-out.final`): **0** in each.
- `isolation` in the result files (browser `/api` requests = rewritten to 5061, requests to 5050 = 0):

| File | Browser `/api` requests | Rewritten to 5061 | To 5050 |
|---|---|---|---|
| `results-stock-out.final.json` | 114 | 114 | 0 |
| `results-stock-in.final.json` | 109 | 109 | 0 |
| `results-quote.recheck-d1.json` | 14 | 14 | 0 |
| `results-probes.recheck-d1.json` | 153 | 153 | 0 |
| first pass: stock-out / stock-in / quote / probes | 114 / 109 / 14 / 153 | same | 0 |
| re-check segments: stock-out / stock-in / diag-quickadd | 38 / 27 / 38 | same | 0 |

## Data (real API on :5061, `setup.json`)

Created by `setup.js` as `admin` (re-runnable; it looks up before it creates): group CN "Cách nhiệt"; units cuộn, m, m², m³; warehouse KHO01 "Kho chính"; reasons "Nhập khác" and "Xuất khác" (`PartnerType.Any`, no debt effect, so no **Đối tượng**). `setup.json` check: the search `CN-` returned all six codes, none missing.

| Code | Name | Unit | `pricingMode` | Dài | Rộng | Dày | Giá bán | Giá vốn |
|---|---|---|---|---|---|---|---|---|
| CN-BTT50 | Bông thủy tinh 50mm | cuộn | PerUnit | - | - | - | 465000 | 410000 |
| CN-BK48 | Băng keo bạc 48mm | cuộn | PerUnit | - | - | - | 32000 | 25000 |
| CN-EPS50 | Tôn xốp EPS 50mm | m | PerLinearMeter | 6000 | - | - | 235000 | 198000 |
| CN-PE05 | Tấm cách nhiệt PE OPP 5mm | m² | PerSquareMeter | 2400 | 1200 | 5 | 18500 | 15200 |
| CN-XPS50 | Xốp XPS cách nhiệt 50mm | m³ | PerCubicMeter | 1200 | 600 | 50 | 2150000 | 1820000 |
| CN-EPS-NL | Tôn xốp EPS chưa phát sinh | m | PerLinearMeter | 6000 | - | - | 235000 | 198000 |

- Stock for the vouchers saved in step 10: stock-in `PN00001` (status 200): CN-BTT50 and CN-BK48 quantity 1000; CN-EPS50 100 x 6000; CN-PE05 1000 x 2400 x 1200 x 5; CN-XPS50 100 x 1200 x 600 x 50. CN-EPS-NL never got a saved voucher (step 9 changes its mode and the service refuses that after ledger rows); the walk's `try/finally` put it back to `PerLinearMeter` after every step 9 (log: `CN-EPS-NL back: {"status":200,"pricingMode":"PerLinearMeter"}`).
- Created by the walks (stock-out row 5 quick add, one new code per run): `CN-XPS51`, `CN-XPS51-2`, `-3`, `-4`, `-5`, `-6` (6 products; `products` held 12 rows before the drop).
- Step 10 saved and cancelled one voucher per run: `PN00002`, `PN00003` (stock-in) and `PX00001`, `PX00002` (stock-out). Nothing else was saved.

## Stock-out walk (`results-stock-out.final.json`, `so-NN-*.png`): 12/12 PASS

New context per segment (steps 1-8; 9-9b; 10-11), `login`, `/stock-out/new`, **Lý do xuất** "Xuất khác", **Kho** KHO01. "Measured" is the value read from the page; every measured value equals the Expected cell of the plan.

| # | Action | Expected | Measured | Evidence |
|---|---|---|---|---|
| 1 | Phiếu mới: click `stock-line-product-code-0` | headers include SL, no Số tấm/Dài/Rộng/Dày; width 1142px; no `col-appeared` | same; 0 `col-appeared` | `so-01-phieu-moi.png` |
| 2 | Pick CN-BTT50 from the list, type 12 | caret on `stock-line-quantity-0`; headers unchanged; no flash | caret `stock-line-quantity-0`; headers unchanged; 0 `col-appeared` | `so-02-chon-hang-cai.png` |
| 3 | Insert, pick CN-EPS50 from the list | Số tấm and Dài appear and flash; caret `stock-line-sheet-count-1`; 1288px; row 0 cells disabled "—"; length-1 = 6000 | Số tấm, Dài only; th flash, td 2/2 each; caret `stock-line-sheet-count-1`; 1288px; row 0 cells disabled, empty, "—"; 6000 | `so-03-chon-hang-md.png` |
| 4 | Insert, pick CN-PE05 from the full catalog (search "CN-PE") | Rộng appears and flashes; caret `stock-line-sheet-count-2`; 1364px; width-2 = 1200 | catalog opened with "CN-PE"; Rộng th flash, td 3/3; caret `stock-line-sheet-count-2`; 1364px; width-0 and width-1 "—"; 1200 | `so-04-chon-hang-m2-danh-muc.png` |
| 5 | Insert, `CN-XPS51` + F4, quick add (m³, 1200x600x50), **Lưu và chọn** | Dày appears and flashes; caret `stock-line-sheet-count-3`; 1428px; thickness-2 disabled "—"; thickness-3 = 50 | save 200; Dày th flash, td 4/4; caret `stock-line-sheet-count-3`; 1428px; thickness-2 disabled "—"; 50. **First pass: FAIL (D1), see Defects.** | `so-05-chon-hang-m3-them-nhanh.png` |
| 6 | Click `stock-line-thickness-3`, Ctrl+Delete | caret `stock-line-product-code-2`, `aria-expanded=false`; Dày gone, Rộng stays; 3 rows; 1364px; no flash | as expected; 0 `col-appeared` | `so-06-ctrl-delete-dong-m3.png` |
| 7 | Row 3 code: select all, type `CN-BK`, click CN-BK48 | Rộng still there while the list shows, gone at once after the pick; caret `stock-line-quantity-2`; Số tấm, Dài remain | list open (`aria-expanded=true`) with Rộng shown; then Rộng gone; caret `stock-line-quantity-2`; Số tấm, Dài remain | `so-07-doi-hang-m2-sang-cai.png` |
| 8 | "Xóa dòng 2" (CN-EPS50 row) | caret `stock-line-product-code-1` (CN-BK48 moved up), list closed; Số tấm, Dài gone; 2 rows; 1142px | as expected; row 1 = CN-BK48 | `so-08-xoa-dong-md.png` |
| 9 | New context; CN-BTT50 12, CN-EPS-NL 8; PUT CN-EPS-NL to PerSquareMeter; scroll to the right end; Ctrl+S | 400 with `lines[1].width`; Rộng appears (forced) and flashes; width-1 disabled "—" `aria-invalid=true` with message; width-0 "—"; caret `stock-line-product-code-1`; cell and message between the pinned Tên hàng and Số còn lại; still `/stock-out/new` | 400 "Chiều rộng phải lớn hơn 0."; Rộng th flash; width-1 disabled, empty, "—", `aria-invalid=true`, message shown; caret `stock-line-product-code-1`, list closed; cell and message rects inside the gap (0.5 px tolerance, see note); URL `/stock-out/new` | `so-09-loi-may-chu-cot-an.png` |
| 9b | Row 2 code: type `EPS-NL` (fresh query), click CN-EPS-NL | message gone, no `aria-invalid`; Rộng stays; width-1 enabled, empty; caret `stock-line-sheet-count-1` | search returned CN-EPS-NL as PerSquareMeter; message gone; no `aria-invalid`; Rộng shown; width-1 enabled, empty; caret `stock-line-sheet-count-1` | `so-09b-chon-lai-hang.png` |
| 10 | New context; CN-BTT50 12, CN-EPS50 8, CN-PE05 10; Ctrl+S, **Hủy phiếu**, reload | POST has `lines[2].width` 1200 and `thickness` 5 with Dày hidden; read-only grid; headers Số tấm, Dài, Rộng; values per row; no flash on mount | POST width 1200, thickness 5 (Dày hidden); save 200, cancel 200; no Thêm dòng, 0 trash buttons and no action header, every input disabled; headers Số tấm, Dài, Rộng; CN-PE05 row 10, 2400, 1200; CN-EPS50 row 8, 6000, "—"; CN-BTT50 row "—" x3; no `col-appeared` (also none recorded by a MutationObserver) | `so-10-phieu-da-huy.png` |
| 11 | **Cột & phím Enter** | **Hiện cột**: Kho, SL tồn, Số tiền, %CK, Tiền CK, %VAT, Tiền VAT, Ghi chú + muted line; Enter table keeps its columns and rows | exactly those 8 checkboxes; muted `<p>` "Số tấm, Dài, Rộng, Dày tự hiện theo hàng tính md, m², m³ có trong phiếu."; section "Phím Enter dừng ở (Mã hàng luôn là ô đầu)"; columns Kho, Tấm, Dài, Rộng, Dày, SL, Đơn giá, %CK, Tiền CK, %VAT, Ghi chú; rows Cái, md, m², m³ | `so-11-cot-phim-enter.png` |

Note (tolerance, no correction): in row 9 the input `stock-line-width-1` has `left` 634.5 px and the pinned **Tên hàng** header has `right` 635 px. The cell is scrolled exactly to the pinned edge; the 0.5 px is half of a collapsed 1 px border. The script accepts 0.5 px (`left >= tenHang.right - 0.5`, `right <= soConLai.left + 0.5`). The right side has 0.5 px of room the other way (cell right 709.5 against Số còn lại left 1191). Probe P6 measures the same kind of gap with no tolerance and passes (`betweenExact: true`).

## Stock-in walk (`results-stock-in.final.json`, `si-NN-*.png`): 12/12 PASS

Same as the stock-out walk on `/stock-in/new` with **Lý do nhập** "Nhập khác", with the differences of Task 4.5: steps 3, 4 and 5 pick `CN-EPS50`, `CN-PE05`, `CN-XPS50` from the suggestion list; step 5 expects thickness-3 = 50. Every measured value equals the stock-out column above, except as noted.

| # | Measured (differences from stock-out only) | Evidence |
|---|---|---|
| 1-3 | as stock-out: 1142px, no flash; caret `stock-line-quantity-0`; Số tấm, Dài flash (td 2/2), caret `stock-line-sheet-count-1`, 1288px, length-1 = 6000 | `si-01-phieu-moi.png`, `si-02-chon-hang-cai.png`, `si-03-chon-hang-md.png` |
| 4 | list pick: Rộng th flash, td 3/3; caret `stock-line-sheet-count-2`; 1364px; width-2 = 1200 | `si-04-chon-hang-m2.png` |
| 5 | list pick: Dày th flash, td 4/4; caret `stock-line-sheet-count-3`; 1428px; thickness-2 disabled "—"; thickness-3 = 50 | `si-05-chon-hang-m3.png` |
| 6-8 | as stock-out: caret `stock-line-product-code-2`, Dày gone, 1364px; Rộng gone at once, caret `stock-line-quantity-2`; caret `stock-line-product-code-1`, Số tấm and Dài gone, 1142px | `si-06-ctrl-delete-dong-m3.png`, `si-07-doi-hang-m2-sang-cai.png`, `si-08-xoa-dong-md.png` |
| 9 | 400 `lines[1].width` "Chiều rộng phải lớn hơn 0."; Rộng forced and flashing; width-1 disabled "—" `aria-invalid=true` with message; caret `stock-line-product-code-1`; rect checks true; URL `/stock-in/new` | `si-09-loi-may-chu-cot-an.png` |
| 9b | message gone, Rộng shown, width-1 enabled and empty; caret `stock-line-sheet-count-1` (fresh query `EPS-NL` returned PerSquareMeter) | `si-09b-chon-lai-hang.png` |
| 10 | POST width 1200, thickness 5; save 200, cancel 200 (`PN00003` in the final pass); read-only; values 10/2400/1200, 8/6000/"—", "—" x3 | `si-10-phieu-da-huy.png` |
| 11 | same 8 checkboxes, muted line, Enter table columns and rows | `si-11-cot-phim-enter.png` |

## Quotation walk (`results-quote.recheck-d1.json`, `q-NN-*.png`): 11/11 PASS

New context, `login`, `/quotations/new` (one empty line, nothing saved).

| # | Action | Expected | Measured | Evidence |
|---|---|---|---|---|
| 1 | Click `line-product-code-0` | `aria-expanded=false`; 10 headers + empty action header, no dimension header; min-width 1136px | as expected: #, Mã hàng, Tên hàng, ĐVT, SL, Đơn giá bán, Thành tiền bán, Đơn giá nhập, Thành tiền nhập, Lợi nhuận, ""; 1136px | `q-01-phieu-moi.png` |
| 2 | Pick CN-BTT50 | caret `line-name-0`; headers unchanged | as expected | `q-02-chon-hang-cai.png` |
| 3 | Insert, pick CN-EPS50, then Enter from `line-name-1` | Số tấm, Dài between ĐVT and SL and flash; caret `line-name-1`; 1282px; row 0 cells disabled "—"; Enter walk `line-unit-1`, `line-sheet-count-1`, `line-length-1`, `line-unit-price-1`, `line-unit-cost-1` | as expected; th flash and every td flash; 1282px; Enter walk exactly that; SL of the md row computed (disabled) | `q-03-chon-hang-md.png` |
| 4 | Insert, pick CN-PE05 | Rộng appears and flashes; caret `line-name-2`; 1358px; width-0, width-1 "—" | as expected | `q-04-chon-hang-m2.png` |
| 5 | Insert, pick CN-XPS50, type 50 in `line-thickness-3` | Dày appears and flashes; caret `line-name-3`; 1422px; thickness-2 disabled "—"; thickness-3 enabled and empty | as expected; typed 50 kept | `q-05-chon-hang-m3.png` |
| 6 | Click `line-thickness-3`, Ctrl+Delete, then Enter | caret `line-product-code-2`, list closed, no `listbox`; Dày gone; 3 rows; 1358px. After Enter: caret `line-name-2`, row 2 unchanged | as expected; row 2 still CN-PE05, "Tấm cách nhiệt PE OPP 5mm", 18.500 | `q-06-ctrl-delete-dong-m3.png` |
| 7 | Row 3 code: select all, type `CN-BK48`, click it | list opens, Rộng still shown, then gone at once; caret `line-name-2`; 1282px | as expected; row 2 = CN-BK48 | `q-07-doi-hang-m2-sang-cai.png` |
| 8 | Delete the CN-EPS50 row (second "Xóa dòng"), then Enter | caret `line-product-code-1` (CN-BK48 moved up), list closed; Số tấm, Dài gone; 1136px. After Enter: caret `line-name-1`, row 1 kept | as expected; row 1 = CN-BK48, "Băng keo bạc 48mm", SL 0, 32.000 | `q-08-xoa-dong-md.png` |
| M1 | Click into `line-product-code-0` (CN-BTT50), wait 600 ms | `aria-expanded=false`, no `listbox` | as expected | `q-code-click.png` |
| M2 | Click `line-unit-cost-0`, Enter, Enter | caret `line-product-code-1`, list closed; then `line-name-1`; row 1 unchanged | as expected; row 1 unchanged (CN-BK48, cuộn, 0, 32.000, 25.000) | none (state read) |
| M3 | In `line-product-code-1` type `CN-BK48`; Esc | list opens (`aria-expanded=true`, `listbox` shows CN-BK48); Esc closes; row 1 unchanged | as expected; stays on `/quotations/new` | `q-code-type.png` |

## Probes (`results-probes.recheck-d1.json`): 8/8 PASS

| Probe | Measured | Evidence |
|---|---|---|
| P1 flash, light, stock-out (CN-BTT50, CN-BK48, then CN-PE05 picked) | Số tấm, Dài, Rộng `th`: `col-appeared` and 1 animation each (`col-appeared-head`); the 6 disabled "—" inputs of rows 1-2: 1 animation each; row 3 `td` of `stock-line-length-2` and `stock-line-width-2`: 1 animation each; 12 elements with `col-appeared`, 18 grid animations in total; caret `stock-line-sheet-count-2`, background `rgb(255, 248, 220)`; at 0 ms header `rgb(180, 83, 9)` with white text, flash cell `rgb(255, 240, 179)`; at 200 ms header `rgb(133, 85, 51)`; 1.5 s after the pick: 0 `col-appeared`, 0 running animations, header `rgb(0, 91, 172)` | `flash-stock-light.png` (animations paused at 200 ms) |
| P2 header contrast, light | 80 samples over 1303.9 ms, one per frame; minimum **5.02:1** (5.0216, at 0 ms, white on `#b45309`), **6.79:1** at rest (white on `#005bac`), maximum 7.38:1; text white in every sample; 0 unparsed; limit 4.5:1 | none |
| P3a dark mode, P1 checks | `<html class="dark">` kept; same 3 headers, 1 animation each, same header colours as light at 0 ms, 200 ms and rest (`rgb(180, 83, 9)`, `rgb(133, 85, 51)`, `rgb(0, 91, 172)`); flash cell starts from the dark `--nav-match` `rgba(250, 204, 21, 0.3)` (light: `rgb(255, 240, 179)`); 1.5 s after: 0 `col-appeared`, 0 running animations, header `rgb(0, 91, 172)` | `flash-stock-dark.png` |
| P3b dark mode, P2 sampling | 81 samples over 1317 ms; minimum **5.02:1**, **6.79:1** at rest, maximum 7.38:1; text white throughout | none |
| P4 reduced motion | `prefers-reduced-motion: reduce` matches; Số tấm, Dài, Rộng: `th` and 3/3 `td` still have `col-appeared`; header `rgb(0, 91, 172)` 6 ms after the pick; **0** animations in `table.stock-grid` (0 in the document); caret `stock-line-sheet-count-2` | `flash-stock-reduced.png` |
| P5 flash, quotation | 3 headers, 1 animation each; the 6 disabled "—" inputs of rows 1-2: 1 animation each; `td` of `line-sheet-count-2`, `line-length-2`, `line-width-2`: 1 animation each; caret `line-name-2` (`rgb(255, 248, 220)`); header start `rgb(180, 83, 9)`, white text; cell start `rgb(255, 240, 179)`; 1.5 s after: 0 `col-appeared`, 0 animations, header `rgb(0, 91, 172)` | `flash-quote-light.png` |
| P6 scroll anchor, stock-out | table 1428px, wrap 1100px, `scrollLeft` **329** (max 329) after clicking `stock-line-vat-rate-3`; after Ctrl+Delete: caret `stock-line-vat-rate-2`, Dày gone, table 1364px, `scrollLeft` **265** (max 265); focused input rect x 1130.5-1189.5 between the pinned Tên hàng right edge 635 and Số còn lại left edge 1190 (`betweenExact: true`, no tolerance) | `scroll-stock.png` |
| P7 scroll anchor, quotation | min-width 1422px, `scrollLeft` **35** (max 322) after clicking `line-unit-cost-3`; after Ctrl+Delete: caret `line-unit-cost-2`, Dày gone, 1358px, `scrollLeft` **35** (max 258, unchanged); focused input rect x 1178-1289, y 617.3-650.3 inside `.accounting-grid-wrap` x 253-1353, y 511.8-651.3 (`insideExact: true`) | `scroll-quote.png` |

## Defects

### D1 (Important): a pick by quick add (F4, Lưu và chọn) shows the new dimension columns without the flash

Found by Task 4.4 row 5 (stock-out). It failed in the first run (`results-stock-out.json`) and in the segment 1 re-run in a new context (`results-stock-out.rerun-seg1.json`, quick add code `CN-XPS51-2`), and `diag-quickadd.js` confirmed it (`results-diag-quickadd.before-fix.json`, code `CN-XPS51-3`). 0 requests to 5050 in every log.

- **Repro (shortest):** new context, `login`, `/stock-out/new`, **Lý do xuất** "Xuất khác". Click `stock-line-product-code-0`, type a code not used yet (`CN-XPS51-<n>`), wait 600 ms, press `F4`. In **Thêm nhanh hàng hóa** fill **Tên hàng hóa** "Xốp XPS cách nhiệt 51mm", **Nhóm hàng hóa** "Cách nhiệt", **Đơn vị tính** "m³", **Loại giá** "Theo m³", **Dài** 1200, **Rộng** 600, **Dày** 50, click **Lưu và chọn**. The walk's own repro is row 3 (`stock-line-product-code-3`), where only **Dày** is new.
- **Expected:** the dimension columns the pick shows flash: their `th` and every `td` get `col-appeared` for 1.2 s (brainstorm SUMMARY.md:102 "Pick (typeahead, full catalog, or quick add with F4 → Lưu và chọn): columns update at once"; :107 "Flash: the class `col-appeared` on the `<th>` and `<td>` of newly shown dimension columns ... It does not run on the first render"; mockup 03 step 3; plan Task 4.4 row 5 "**Dày** appears and flashes").
- **Actual:** the columns appeared at once and everything else was right (caret `stock-line-sheet-count-3`, width 1428px, `stock-line-thickness-2` disabled "—", `stock-line-thickness-3` = 50), but nothing ever got `col-appeared`:
  - walk, row 5: **Dày** `th` `col-appeared` false (expected true) and 0 of 4 `td` (expected 4/4); the **Dày** header was inserted at page time 12896 ms, 557 ms before the measurement, inside the 1.2 s window; a MutationObserver started at the step logged no `col-appeared` mark;
  - `diag-quickadd.js` run A (row 0): the `<table class="stock-grid">` was the same DOM node before and after the pick (no remount) and the observer saw no `col-appeared` change at all;
  - control run B (list pick of CN-XPS50 on row 0, same page state): `col-appeared` landed on the four new `th` and their `td` in the same frame as the header change. The list pick (row 3), the full catalog pick (row 4) and the forced error (row 9) flashed too.
- **Failing test lines (RED, before the fix):**
  - `src/pages/stock-vouchers/components/stock-line-grid.test.tsx` › product quick add › `a quick-added m² product flashes its new columns and keeps the focused cell in view (browser check D1)`: `expect(element).toHaveClass("col-appeared")` / `Expected the element to have class: col-appeared` / `Received: cell-number` (the Số tấm `th`). After the hook fix alone it failed on the scroll anchor: `expected "Mock" to be called with arguments: [ { block: 'nearest', … } ]` / `Number of calls: 0`.
  - `src/lib/use-appeared-keys.test.ts` › `marks the keys a commit shows after React threw away a render that had read them (browser check D1)`: `AssertionError: expected [] to deeply equal [ 'length' ]`.
- **Cause:** the grids read their rows from `form.getValues`, a store outside React. The quick add pick runs in Radix's `onCloseAutoFocus` timer, so the form watcher update has default priority; the cell then calls `focus()`, and the focus event queues a discrete update on the grid that changes nothing. React renders that discrete update first: the grid body already reads the new rows, so the layout effect of `useAppearedKeys` gets new deps, but no state changed at that priority, so React bails out, dropping the effect and keeping its deps. The default-priority render then commits the new columns, finds equal deps and never runs the effect: no mark (and no scroll-anchor `scrollIntoView`, keyed the same way). jsdom hid it: under vitest, `window.event` keeps the first event React wrote into it (a `focusin`), so every update made outside a React handler was discrete there and rendered together with the focus update, without the bail-out.
- **Fix:** commit `8f326adc` "fix(lib): flash the dimension columns a quick add shows". The layout effect of `useAppearedKeys` (signature unchanged) and the scroll-anchor effects of `stock-line-grid.tsx` and `line-items-grid.tsx` have no dependency list and compare with the value of the last commit kept in their ref. The quotation grid has no path that triggers the bail-out today (its quick add moves the focus with a timer, `nextFocusId`) but it shares the hook and the effect pattern. The new grid test hides the stale `window.event` while the dialog closes (`withBrowserWindowEvent`); a hook test replays the bail-out with `flushSync`. Checks at the fix: `npx vitest run src/lib src/pages/stock-vouchers src/pages/quotations`: 58 files, 900 tests passed; `npm run typecheck` exit 0; eslint on the five files: 0 errors, 1 warning that was already there (`line-items-grid.tsx:112`, `react-refresh/only-export-components`).
- **Re-check on `8f326adc`: PASS.**
  - Stock-out segment 1 (`results-stock-out.recheck-d1.json`, quick add code `CN-XPS51-4`): rows 1-8 PASS; row 5: **Dày** `th` `col-appeared` true with 1 animation, 4/4 `td`, caret `stock-line-sheet-count-3`, width 1428px.
  - `diag-quickadd.js` run A (`CN-XPS51-5`, `results-diag-quickadd.json`): `col-appeared` lands on the four new `th` (Số tấm, Dài, Rộng, Dày) and their `td`, as in control run B.
  - The code changed in shared files, so the stock-in segment 1 (`results-stock-in.recheck-d1.json`) rows 1-8 PASS, and the quotation walk (`results-quote.recheck-d1.json`) 1-8 and M1-M3 PASS.
  - Then a full final pass: stock-out 12/12 (`results-stock-out.final.json`, row 5 with `CN-XPS51-6`), stock-in 12/12, probes 8/8 (the tables above).
  - The re-runs rewrote `so-01` to `so-08`, `si-01` to `si-08`, `q-01` to `q-08`, `q-code-click`, `q-code-type`; `so-05-chon-hang-m3-them-nhanh.png` now shows the **Dày** flash.

## Script and data corrections (not defects)

- **S1 (Task 4.2 step 6 `api()`, Task 4.3).** Node's `fetch` (undici) refused `http://localhost:5061` with `TypeError: fetch failed ... Error: bad port`: 5060 and 5061 (SIP) are on the Fetch-spec blocked-port list. `lib.js` `api()` now uses Node's `http` module (and refuses any port other than 5061). Re-run: `setup.js` exit 0, `setup.json` written, check ok.
- **S2 (Task 4.2 step 6 `launch()`).** Edge (Chromium) blocks the same port: every rewritten `/api` call failed with `net::ERR_UNSAFE_PORT` (`/api/auth/refresh`, `/api/auth/login`, `/api/settings/branding/icon/192`), so login never left `/login`. `launch()` now passes `args: ['--explicitly-allowed-ports=5061']`. Re-run of `smoke.js`: login OK, `/stock-out/new` grid rendered, 15 browser `/api` requests, 15 rewritten to 5061, 0 to 5050.
- **S3 (instrumentation only).** After the row 5 failure `walk-stock.js` got a read-only MutationObserver init script and a page-time `timeline()` for rows 3-5. The segment 1 re-run gave the same result as the first run (rows 1-4 and 6-8 PASS, row 5 FAIL), so the failure was not a timing artefact. The stock-in walk ran with the instrumented script. The re-run reused the log names, so `walk-stock-out.log` and `walk-stock-out.requests.log` then held the segment 1 re-run only (the first full run's isolation counts stayed in `results-stock-out.json`: 114/114/0); the script now derives the log names from `--out`.
- Quotation walk and probes: no failure on the first pass, so no re-run for a script reason. Both were re-run once after the D1 fix (above).

PASS re-runs of S1 to S3: `setup.js` and `smoke.js` after S1 and S2 (as listed), the segment 1 re-run after S3, and the full final pass on `8f326adc` (12/12, 12/12, 11/11, 8/8).

## Plan corrections

None. No Expected cell of Task 4.4-4.7 contradicted the brainstorm or a Phase 01-03 test; every measured value of the final pass equals its Expected cell. (The tolerance of 0.5 px in row 9 is explained under the stock-out table; it is a property of the script's rect check, not a change of an Expected cell.)

## Cleanup

Done on 2026-10-16 after the last run (13:44), by Task 4.9.

- Servers: `Get-NetTCPConnection -LocalPort 5061,5191 -State Listen` gave PIDs 16176 (Vite, port 5191) and 25516 (`OrderMgmt.WebApi.exe`, port 5061); both were stopped with `Stop-Process`. `netstat -ano | grep LISTENING | grep -E ':(5061|5191) '` then printed nothing. Port 5050 printed nothing either (no listener, as at the start). No process with `dimension-columns` in its command line (`dotnet`, `node`, `OrderMgmt.WebApi`, `cmd`, `npx`) was left.
- Database: `drop database "qldonhang_integtest_dimcols" with (force)` returned `DROP DATABASE`, and `select count(*) from pg_database where datname = 'qldonhang_integtest_dimcols'` printed `0`. No other database was touched.
- `git status --short` listed only `BROWSER-CHECK.md` and `browser-check/` (the backend `bin/` and `obj/` are ignored). No source file was changed by this task.
- Not committed, kept in the session scratchpad: `dimcols-browser/` (scripts, logs, raw results).
