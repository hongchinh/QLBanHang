# Execution Report — voucher payment QR

> Plan: [SUMMARY.md](SUMMARY.md) · Branch: `feat/voucher-payment-qr` (from `qlbanhang/main` `1a877638`, not pushed) · Frontend lane: `feat/voucher-payment-qr-fe`, merged in `6e1512d2` · Executed 2026-10-15 · Baseline: [BASELINE.md](BASELINE.md) · Go-live gate: [GO-LIVE-CHECKLIST.md](GO-LIVE-CHECKLIST.md)

## Outcome

All six phases (00–05) are complete. A receipt (01-TT) and a stock-out voucher (02-VT) can carry a VietQR transfer QR on the printed Excel / PDF (single and bulk, "Kèm QR chuyển khoản" remembered per user) and on the voucher screen (a "QR chuyển khoản" dialog, a bottom sheet on a phone, with copy and "Tải ảnh QR"). Money goes to a bank fund of the voucher's branch; funds now carry a bank and an account holder. The backend owns the rules and the payload, so paper and screen carry the same payload. The accountant's questions and the real-paper scan table are still open; they are the go-live gate in the checklist.

The change is 92 files, +11,800 / −88 lines against `1a877638` (backend 38 files, frontend 36, docs 18).

| Phase | Delivered |
|---|---|
| 00 Worktree, baseline | Worktree, branch, plan and brainstorm committed, failure baseline (`BASELINE.md`). |
| 01 Bank and account holder on funds | `Fund.BankId`, `AccountHolderName`, migration, DTOs, validation; bank select and holder field on the fund form. |
| 02 Rules, service, routes | `VoucherPaymentQrRules`, `VoucherPaymentQrService`, `VoucherPaymentQrDto`, `GET api/{cash,stock}-vouchers/{id}/payment-qr` (422 `PAYMENT_QR_UNAVAILABLE`). |
| 03 QR on the printed vouchers | `QrPngRenderer` (QRCoder), QR fields and text rules on the print models, QR beside the lines on 01-TT, payment block on 02-VT, `includeQr` on the four print routes; frontend switch (`qr-pref-storage`, receipt menu item, stock-out price dialog checkbox). |
| 04 Screen and dialog | `features/voucher-payment-qr`, `components/payment-qr` (`PaymentQrButton`, `PaymentQrDialog`), copy / share / picture helpers, icons, wiring into both pages, routes kept out of the service worker cache. |
| 05 Docs, real output, verification | Merge of the lanes, go-live checklist, real-browser walk-through, project docs, final verification and review. |

## Tasks

| Task | Status | Area |
|---|---|---|
| 0.1 Worktree, branch, plan commit | done | docs |
| 0.2 Baseline | done | docs |
| 0.3 Parallel lanes | done (two lanes) | process |
| 1.1 Fund columns, DTOs, validation, migration | done | backend: `Fund`, `FundService`, validator, one migration, funds tests |
| 1.2 Fund form | done | frontend: types, bank select, holder field, tests |
| 2.0 Test helper | done | backend: `CreateBankFundAsync` extended |
| 2.1 `VoucherPaymentQrRules` | done | backend: pure rules + unit tests |
| 2.2 Service, DTO, routes | done | backend: service, resolver, two controllers, integration tests |
| 3.1 `QrPngRenderer` and packages | done | backend: QRCoder, ZXing.Net in tests |
| 3.2 Models, request flag, text rules | done | backend: print models, `VoucherPrintRules` QR helpers |
| 3.3 01-TT QR beside the lines | done | backend: `CashVoucherFormBuilder` |
| 3.4 02-VT payment block | done | backend: stock-out builder |
| 3.5 Print service, routes, integration tests | done | backend: `includeQr` on four routes |
| 3.6 Real PDF: measure, tune, look | done | backend: scale model measured on real PDFs |
| 3.7 `qr-pref-storage`, request flag, API | done | frontend |
| 3.8 Receipt print menu item | done | frontend |
| 3.9 Stock-out price dialog checkbox | done | frontend |
| 4.0 Second worktree | done | process |
| 4.1 Feature: types, API, keys, hook | done | frontend |
| 4.2 Icons | done | frontend: `copy`, `download`, `share` |
| 4.3 Helpers | done | frontend: media query, picture, copy, share |
| 4.4 `PaymentQrDialog` | done | frontend |
| 4.5 `PaymentQrButton` and two pages | done | frontend |
| 4.6 Routes never cached | done | frontend: test pins `CACHEABLE_API_PREFIXES` |
| 5.0 Merge the lanes and main | done | merge `6e1512d2`, no conflicts |
| 5.1 Go-live checklist | done | docs |
| 5.2 Real browser | done | see below |
| 5.3 Project documentation | done | docs: `SUMMARY.md`, `conventions.md`, `system-architecture.md`, `product-goals.md`, `directory-structure.md` |
| 5.4 Final verification | done | see below |

## Commits per phase

| Phase | Commits | Notes |
|---|---|---|
| 00 | `606400c4`, `8195f6b9` | plan commit; baseline |
| 01 | `04601373` (1.1), `95a8fbf6` (1.2), fixes `05a6d7a3`, `930ae781` | fixes added the missing tests: guard, legacy account numbers, bank select; validation on the update path |
| 02 | `4f5d7238` (2.1), `18ade8aa` (2.0 + 2.2), fix `5191a056` | `5191a056` made the permission tests able to fail and covered `ResolveMany` edges |
| 03 backend | `72d158ce` (3.1), `0e11f9fa` (3.2), `8fbc1766` (3.3), `9ff4f703` (3.4), `523c64a3` (3.5), `a21b0355` (3.6), fixes `91fbf07f`, `a3edccb6`, `b381d162` | tests decode with plain ZXing.Net; caption row cleared; Cost / None tests made able to fail |
| 03 frontend (lane) | `81538894` (3.7), `dbd3712e` (3.8), `7a915548` (3.9), fixes `3bf7c6ad`, `9495d65b` | `9495d65b`: the "Kèm QR" switch stayed stale across list buttons |
| 04 (lane) | `89a6c73b` (4.1), `38c1150c` (4.2), `dab38dd8` (4.3), `fc7c869c` (4.4), `c9959a1e` (4.5), `2ed12159` (4.6), fixes `b4847e86`, `602e27b1`, `68b440dd` | review fixes for the dialog and the helpers (stale QR on reopen) |
| 05 | `ad94ad0b` (5.1), `6e1512d2` (5.0 merge), `095cbbaa` (5.3), fixes `c651b35d`, `817b8846`, `e0faa69d`, `50925459`, and the completion commit | `c651b35d` A5 columns; `817b8846` pinned dialog footer; `e0faa69d` docs corrections; `50925459` canvas accessible name |

## How it was run

- Subagent-driven: a fresh implementer per task (sonnet), test first with the RED line reported.
- Two lanes: the backend (Phases 00–03 backend) in `.claude/worktrees/voucher-qr`; Phase 04 and the frontend tasks of Phase 03 (3.7–3.9) in `.claude/worktrees/voucher-qr-fe` on `feat/voucher-payment-qr-fe`, created from the Phase 02 commit and merged back in Task 5.0.
- **Review outcomes.** Every task had an independent spec and quality review by a separate opus reviewer in a detached checkout. Most found Important or minor items that were fixed in follow-up commits with mutation proofs. Notable catches:
  - Task 1.2: test gaps (the bank guard, legacy account numbers, the bank select).
  - Task 1.1: validation on the update path was untested.
  - Phase 02: permission tests could not fail (the branch check hid the permission check).
  - Task 3.1: a test-only bindings package pulled in SkiaSharp 4 (replaced by plain ZXing.Net).
  - Task 3.4: the picture overlapped the caption row, padding untested.
  - Task 3.5: the Cost / None tests could not fail.
  - Task 3.6: a real-PDF measurement found that the A5 receipt fits by height (zoom 71–73 % instead of 90 %) and that the column-width model was wrong (QR off-centre); both fixed.
  - Frontend: a stale QR on reopen; the print switch stale across list buttons.
  - Dialog action row cut off at 1366×768: fixed with a pinned footer.
  - The final whole-implementation review: "Ready to merge".

## Verification outcome vs `BASELINE.md`

| Check | Baseline (`606400c4`) | Final | Result |
|---|---|---|---|
| `dotnet build OrderMgmt.sln` | 0 errors | 0 errors | ok |
| `dotnet test OrderMgmt.sln` | 19 failed / 1632 total | 20 failed / 1773 total | the 19 baseline failures (same names) + `ProductGroupCrudTests.Create_with_explicit_code_stores_that_code`; no regression |
| `npm run typecheck` | clean | clean | ok |
| `npm run lint` | 16 problems (3 errors, 13 warnings) | 16 problems (3 errors, 13 warnings) | same as baseline |
| `npm run test` | 3 failed / 2199 (226 files) | 3 failed / 2296 (234 files) | the same 3 known tests, now named |
| `npm run build` | passes | passes | ok |

- The twentieth backend failure failed with an Npgsql read timeout under load (the run took 1 h 22 m while other sessions' tests ran). It passes alone (`ProductGroupCrudTests` 11/11), so it is a load flake, not a regression.
- The three frontend failures are now confirmed by name (and recorded in `BASELINE.md`): `useNotificationHub` "invalidates unread-count query on NewNotification"; `bank-accounts-tab` "adds a new account via the form"; `payment-qr-page` "generates and displays a QR after submitting valid data".
- The suites grew by 141 backend tests and 97 frontend tests.

## Real output (Task 3.6)

- QR 24.9–25.07 mm on A5 01-TT (sheet zoom 71–73 % with a QR, about 81 % without) and on A4 02-VT.
- The picture embedded in the PDF is a lossless 295 px PNG (about 6 px per module, QR version 6). OpenCV decoded all 8 sample pages and the payloads matched.
- Maximum-length receipt: zoom 48 %, the QR capped to its columns at 19.2 mm, 228 px, still decodes.
- Page sizes: A5 landscape 595.3 × 419.6 pt; A4 595.3 × 841.9 pt. A 40-line stock-out is 2 pages with the payment block and the signatures together on page 2.

## Real browser (Task 5.2)

Run against a real backend on the scratch database `qldonhang_vqr_smoke` (dropped afterwards) with Edge headless. All five walk-through steps passed:

1. Fund form with a bank.
2. Receipt dialog, bottom sheet at 390 px, disabled button with its title, no stale QR after save.
3. Print menu switch persisted; PDF has 1 image with the tick and 0 without; the QR decodes to the payload.
4. Sale paid by transfer: the price dialog checkbox is disabled for Giá vốn; PDF payment block present.
5. The 422 texts, and the "Mở màn Quỹ" link only with `cash.catalogs.manage`.

Defects found:

- D1: dialog buttons cut off at 1366×768 (fixed, `817b8846`).
- D2: the stock-out header wraps to 2 rows at 1280 px when the "Chưa có thông tin đơn vị" hint shows (not fixed, low).
- D3: desktop Chromium, Edge and Safari report `canShare({files})` true, so "Tải ảnh QR" opens the system share dialog instead of downloading (decided and implemented, follow-up 1).

Not run: a phone or an iPhone install, a second branch.

## Deviations from the plan

- (a) The lane split: Phase 04 and Tasks 3.7–3.9 ran in a second worktree and were merged in Task 5.0 without conflicts.
- (b) `isFundSetupMessage` is not exported from the dialog (react-refresh lint rule); covered through the UI tests.
- (c) An extra try/catch toast "Không tạo được ảnh QR" around the download.
- (d) `pb-safe` replaced by `pb-[max(1.5rem,env(safe-area-inset-bottom))]` and the dialog footer pinned; `boostLevel={false}` on `QRCodeCanvas`.
- (e) `ZXing.Net.Bindings.SkiaSharp` dropped (SkiaSharp 4 conflict, plan Q8 fallback); tests decode with plain ZXing.Net.
- (f) The A5 print zoom is computed from the finished sheet instead of the constant `A5LandscapeScale = 0.90` (Q7 said Task 3.6 corrects it; it needed a model, not a constant), and the QR size is capped to its two columns (below about 62 % zoom it prints under 25 mm).
- (g) `WritePaymentBlock` is guarded to A4 only.
- (h) The stock print QR is resolved only in the `Voucher` price mode.
- (i) The "Kèm QR" switch state is re-read when a print menu opens (the plan read it once).
- (j) The QR version is 6 (41 modules), not the 7 / 53 assumed.
- (k) Test adaptations: seeded reason `XKH` for the non-sale case, payment-method codes `CK` / `TM`, `StockReason.Direction`.
- (l) The fund update refuses an inactive bank even when unchanged (a product choice to confirm).

## Residual risks, follow-ups, decisions for the user

1. **Desktop "Tải ảnh QR".** Decided and implemented: share only on touch devices (`pointer: coarse`), a computer always downloads the PNG (commit `64638097`).
2. **A5 receipt with a QR prints at 71–73 %** (text about 8 pt instead of 11 pt) against about 81 % without. Accept it, make the QR about 22 mm (about 76 %), or shorten the caption rows.
3. **A fund whose bank was deactivated later** blocks every edit of that fund until a bank is re-picked (the select shows blank).
4. **Go-live gate:** the three accountant questions of `GO-LIVE-CHECKLIST.md` and the real-paper scan table (3 bank apps). iPhone installed app: `navigator.share` runs after an `await`, WebKit may refuse, and the fallback download does not work in installed mode.
5. **Low items:**
   - the stock-out header wrap at 1280 px (D2);
   - the QR `Content` shown on screen and paper is the raw voucher code while the payload carries it normalised to 25 ASCII characters (differs only for codes over 25 characters or non-ASCII);
   - the `FundIncomplete` wording also appears for an inactive fund or a deactivated bank;
   - the dialog keys its "Mở màn Quỹ" link on message substrings (a structured reason would be sturdier);
   - `VietQrPayloadBuilder` accepts amounts over EMVCo's 13 digits;
   - `Total` and `CashPaid` are not rounded in the DTO.
6. **Merge hygiene (plan Q11).** The main checkout `D:\Projects\QLBanHang` holds uncommitted edits of `docs/SUMMARY.md` (it already lists the brainstorm) and untracked copies of this plan and the brainstorm folders. Before merging this branch run `git checkout -- docs/SUMMARY.md` there (or keep identical rows) and remove the untracked duplicates `docs/plans/261015-0919-voucher-payment-qr` and `docs/brainstorms/261015-0656-voucher-payment-qr`, or the merge will refuse. The cash voucher form redesign (worktree `cash-form`) edits the same cash voucher form page and docs; whichever merges second rebases.
7. **Nothing is pushed and no PR is opened.** PRs go to remote `qlbanhang` (no `gh` CLI); open the compare URL for `feat/voucher-payment-qr` there.
