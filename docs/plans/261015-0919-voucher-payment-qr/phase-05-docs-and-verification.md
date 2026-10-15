# Phase 05 — Docs, real output, go-live checklist, final verification

**Status:** [ ] pending
**Complexity:** M

## Objective

- Merge the tracks (if Phase 04 ran in its own worktree) and `qlbanhang/main`, and prove the merged result.
- See the feature work end to end in a real browser (mocked or scratch backend), not only in unit tests.
- Write the go-live checklist for the accountant, update the project documentation, and run the final verification.

## Files

- `docs/plans/261015-0919-voucher-payment-qr/GO-LIVE-CHECKLIST.md` (new)
- `docs/code-standard/conventions.md`, `docs/architecture/system-architecture.md`, `docs/architecture/pwa-mobile.md`, `docs/codebase/directory-structure.md`, `docs/project-pdr/product-goals.md`, `docs/SUMMARY.md` (modify)
- `docs/brainstorms/261015-0656-voucher-payment-qr/SUMMARY.md` (modify — status and next steps)
- No production code, except fixes for defects Tasks 5.0–5.2 find (each with a test).

## Tasks

### Task 5.0 — Merge the tracks and main

1. If Phase 04 ran in `.claude/worktrees/voucher-qr-fe`: in the main worktree `voucher-qr`, `git merge feat/voucher-payment-qr-fe` (the two branches touch disjoint files; expect no conflict). Remove the second worktree afterwards: delete its `frontend/node_modules` junction first with `(Get-Item "<path>").Delete()`, check the main checkout's `frontend/node_modules` still has its packages, then `git worktree remove` (a lingering "Permission denied" on the folder is harmless; git drops the record).
2. `git ls-remote --heads qlbanhang main` and compare with the base `3ae3804`. If main moved, `git fetch qlbanhang && git merge qlbanhang/main` and resolve conflicts keeping both sides (typical places: `docs/SUMMARY.md`, `conventions.md`, `icon-registry.ts`, the two voucher form pages). A migration added on main after `20261014130035_AddBranchSampleData` makes the `AddFundBankAccount` snapshot stale: if `dotnet ef migrations has-pending-model-changes -p src/OrderMgmt.Infrastructure -s src/OrderMgmt.WebApi` reports changes, **stop and report** (do not regenerate silently).
3. `dotnet build OrderMgmt.sln` and, from `frontend/`, `npm run typecheck`. No commit unless the merge needs one (the merge commit is the commit).

### Task 5.1 — Go-live checklist

1. Write `GO-LIVE-CHECKLIST.md` in the plan folder, in Vietnamese, for the accountant and the PO (same shape as `docs/plans/archived/261014-1632-tt133-voucher-print-forms/GO-LIVE-CHECKLIST.md`):
   - **Câu hỏi cần trả lời** (the three open questions of the brainstorm, each with "phần mềm đổi gì theo từng câu trả lời"): (1) phiếu thu ghi quỹ tiền mặt nhưng khách chuyển khoản theo QR — sửa quỹ của phiếu hay lập phiếu chuyển quỹ (the software today sends the money to the branch's default bank fund and says so in the dialog's "Quỹ nhận"); (2) nội dung chuyển khoản chỉ có số phiếu, có cần thêm mã khách (tối đa 25 ký tự) không; (3) phiếu xuất HTTT chuyển khoản ghi một phần đã trả (`PaidAmount < Total`): phần đó là tiền đã nhận (cọc) hay sẽ chuyển — nếu là đã nhận thì QR nên là `Total − PaidAmount`.
   - **Thiết lập trước khi dùng:** admin khai ngân hàng, số tài khoản (chỉ chữ và số, tối đa 19 ký tự) và chủ tài khoản cho từng quỹ ngân hàng ở màn Quỹ; đặt quỹ ngân hàng mặc định cho từng chi nhánh; ai thấy nút "QR chuyển khoản" (quyền xem phiếu); ô "Kèm QR chuyển khoản" mặc định bật và được nhớ theo từng người dùng.
   - **Quét thử** (a table to tick): từ giấy in thật — 01-TT A5 (PDF qua LibreOffice) và 02-VT A4 — và từ màn hình, mỗi chỗ bằng ít nhất 3 app ngân hàng; kiểm tra số tiền, số tài khoản, tên chủ tài khoản và nội dung hiện đúng trước khi xác nhận chuyển; thử "Tải ảnh QR" trên iPhone chạy app dạng cài đặt (bảng chia sẻ → lưu ảnh / gửi Zalo) và trên máy tính (tải file); thử sao chép trên HTTPS.
   - **Rủi ro cần biết:** phiếu xuất trả đủ bằng chuyển khoản và phiếu thu tự động của nó cho hai QR giống hệt nhau (khách có thể quét hai lần — tắt "Kèm QR" khi in phiếu thu tự động); phiếu thu tự động ghi quỹ tiền mặt (khách đã đưa tiền mặt) không có QR; QR trên bản in cũ không đổi khi phiếu được sửa sau khi in; phiếu đã hủy không có QR.
2. Commit: `git add docs/plans/261015-0919-voucher-payment-qr/GO-LIVE-CHECKLIST.md && git commit -m "docs(plan): voucher payment QR go-live checklist"`

### Task 5.2 — End to end in a real browser

1. Start a scratch stack from the worktree, on a database of its own so no dev data is touched: backend `ConnectionStrings__Default="Host=localhost;Port=5432;Database=qldonhang_vqr_smoke;Username=postgres;Password=1"` with `Database:AutoMigrateAndSeed=true`, `dotnet run --project src/OrderMgmt.WebApi` (`QuotationExport:LibreOfficePath` set for PDF), frontend `npm run dev` (login `admin` / `Admin@123`). If the main checkout's stack holds the ports, use other ports through the Vite proxy settings, or record "not run" with the reason.
2. With a browser driver (`playwright-core` or Edge headless; see the brainstorm visual tooling notes in the memory index) walk through, taking a screenshot at 1280 px and at 390 px:
   1. **Quỹ**: open the fund form of a bank fund; pick a bank, enter an account number and holder; save; the fund list still loads.
   2. **Phiếu thu**: create a receipt on that fund; "QR chuyển khoản" opens the dialog: amount, account, content = the receipt code; copy buttons; at 390 px it is a bottom sheet. Edit a field without saving: the button is disabled with the title.
   3. **In**: the print menu shows "Kèm QR chuyển khoản" ticked; untick it, reload the page: still unticked; the menu stays open after the tick; "In (PDF)" opens a tab (open the downloaded PDF with PyMuPDF and count images: 1 with the tick, 0 without).
   4. **Phiếu xuất**: create a sale paid by transfer; the dialog shows the total; the price dialog has the checkbox, disabled for "Giá vốn"; PDF has the payment block.
   5. **Lỗi**: a branch without a ready bank fund shows the 422 text; the "Mở màn Quỹ" link shows for the admin and not for a user without `cash.catalogs.manage`.
3. **Look at the screenshots** (Read tool) against `mockup-02-screen-dialog.html`: nothing clipped, QR square, buttons not overflowing at 390 px. Fix defects with a test; record the rest.
4. Stop both servers, drop the scratch database (`DROP DATABASE qldonhang_vqr_smoke` through `psql`). Nothing from this task is committed except fixes.

### Task 5.3 — Project documentation

1. `docs/code-standard/conventions.md`:
   - a **Payment QR** block (after the voucher print rules of **TT133 Report Forms**): the rules live in `VoucherPaymentQrRules` (pure, `Application/CashDebt/PaymentQr`) and the service has one resolver for the screen routes and for printing (`GetFor*Async` throw 422 `PAYMENT_QR_UNAVAILABLE`, `ResolveManyAsync` returns only the vouchers that get a QR); the payload always comes from `VietQrPayloadBuilder.Build`; the QR amount of a stock-out is `Total` minus what was paid into a **cash** fund; the receiving account is the voucher's own bank fund (no fall-back when it is incomplete), else the branch's default bank fund; the content is the voucher code, or the source stock-out code for an automatic receipt, and an automatic receipt in a cash fund gets no QR (`AutoReceiptInCash`); `includeQr` defaults to `false` on every print route and is ignored for Cost/None price modes; the print models carry the DTO and `VoucherFormBuilder` places the picture (`BeginQrBeside`/`EndQrBeside` on 01-TT, `WritePaymentBlock` on 02-VT) at 25 mm on paper using the page's shrink scale; tests decode the PNG back with ZXing.Net;
   - **Frontend:** the screen draws `qrcode.react` at level `M` with `marginSize={4}`; `payment-qr` queries sit under `cashKeys.all` with `staleTime: 0, gcTime: 0`; the two routes are never cacheable; "Kèm QR" is remembered per user by `qr-pref-storage` (`qldh:voucher-print-qr:{userId}`, default on); the file share path (`navigator.canShare({ files })`) falls back to `saveBlob`.
2. `docs/codebase/directory-structure.md`: `Application/CashDebt/PaymentQr`, `Infrastructure/Qr/QrPngRenderer`; the controller rows of `CashVouchersController` and `StockVouchersController` get `payment-qr`; the `FundsController` row mentions `BankId`/`AccountHolderName`; Frontend Structure gets `components/payment-qr/`, `features/voucher-payment-qr/`, `features/voucher-print/qr-pref-storage.ts`.
3. `docs/architecture/system-architecture.md`: a "Payment QR" paragraph in the cash & debt or TT133 voucher print section: the flow (screen: `payment-qr` route → rules → DTO → `qrcode.react`; paper: `VoucherPrintService` → `ResolveManyAsync` inside the snapshot → `QrPngRenderer` → picture in the workbook), the access rules (view permission of the type, working branch), and the fund fields.
4. `docs/architecture/pwa-mobile.md`: one sentence in the API cache allow-list section that `cash-vouchers/*/payment-qr` and `stock-vouchers/*/payment-qr` are branch- and permission-scoped and are never cached.
5. `docs/project-pdr/product-goals.md`: current modules — the transfer QR on receipts and stock-out vouchers (print and screen); keep the non-goal "no payment confirmation / bank matching" (SePay is separate); the business rules users see (which vouchers get a QR, the amount of a stock-out, the receiving fund, the "Kèm QR" switch).
6. `docs/SUMMARY.md`: update the descriptions of the files above to mention the payment QR; after the executor archives the plan, point the plan row at `plans/archived/261015-0919-voucher-payment-qr/SUMMARY.md` ("Archived plan (executed; report in `EXECUTION-REPORT.md`, go-live gate in `GO-LIVE-CHECKLIST.md`)").
7. `docs/brainstorms/261015-0656-voucher-payment-qr/SUMMARY.md`: status "đã hiện thực" with a link to the plan; mark the next steps done; add the measured printed QR width and the page sizes of Task 3.6.
8. Check every statement against the code (`grep` the routes, class names, storage key, error messages and the registry names) before committing.
9. Commit: `git add docs && git commit -m "docs: voucher payment QR"`

### Task 5.4 — Final verification

1. Run the **Final Verification** block of `SUMMARY.md`. Compare the backend failures with `BASELINE.md` test by test, and the frontend failures and lint counts. Under load run vitest with `--testTimeout=30000 --maxWorkers=3`; a test that fails only by timeout is re-run alone before it counts.
2. `git status --short` is clean: no `VoucherQrSamplesTests.cs`, no sample files, no `tsconfig.*.tsbuildinfo` change; `git diff qlbanhang/main --numstat` shows no CRLF-only rewrites (compare with `--ignore-cr-at-eol`).
3. `git log qlbanhang/main..HEAD --oneline` lists the commits of the phases; check `git log qlbanhang/main..HEAD -- frontend/tsconfig.app.tsbuildinfo` is empty.
4. The executor's completion step (skill `subagent-driven-development`) writes `EXECUTION-REPORT.md` (outcome against the baseline, commits per phase, the measured QR size and page sizes, deviations, whether Task 5.2 ran, the open accountant questions) and archives the plan folder; then fix the `docs/SUMMARY.md` row as in Task 5.3 step 6, mark every phase `[x]` in `SUMMARY.md` and its file, and commit `chore(plan): voucher payment QR complete`.
5. Do not push or open the PR without the user's request: PRs go to the `qlbanhang` remote (no `gh` CLI; give the compare URL and body), and `source/` is never committed.

## Verification

```bash
export TEST_DB_CONNECTION="Host=localhost;Port=5432;Database=qldonhang_integtest_vqr;Username=postgres;Password=1"
cd backend && dotnet build OrderMgmt.sln && dotnet test OrderMgmt.sln --logger "trx;LogFileName=final.trx" --results-directory "$TEMP/vqr-trx"
cd ../frontend && npm run typecheck && npm run lint && npm run test -- --testTimeout=30000 && npm run build
git checkout -- tsconfig.app.tsbuildinfo tsconfig.node.tsbuildinfo
git status --short
```

## Exit Criteria

- The merged branch builds, and its suites show no failure beyond `BASELINE.md`.
- The dialog, the print switch and the printed QR were exercised in a real browser (or the reason it was not is recorded).
- `GO-LIVE-CHECKLIST.md` states the three accountant questions, the fund setup and the scan table.
- The docs match the code; `EXECUTION-REPORT.md` is written and the plan is archived by the executor.
