# Phase 04 — QR button and dialog on the voucher screens

**Status:** [ ] pending
**Complexity:** M

## Objective

- Add the "QR chuyển khoản" button next to "In" on the receipt page and the stock-out page, and the dialog it opens: the QR (264 px, level M, with a quiet zone), the amount, the account details with copy buttons, "Sao chép thông tin", "Tải ảnh QR" (share sheet where files can be shared, else a download); on a phone the dialog is a bottom sheet.
- The data comes from `GET …/payment-qr` (Phase 02); every test here mocks the API, so this phase is independent of Phase 03 and may run in parallel with it, in the frontend worktree of Task 4.0.

## Files

Frontend (`frontend/src/`):

- `features/voucher-payment-qr/types.ts`, `api.ts`, `keys.ts`, `hooks.ts`, `hooks.test.tsx` (new)
- `components/payment-qr/payment-qr-button.tsx`, `payment-qr-button.test.tsx` (new)
- `components/payment-qr/payment-qr-dialog.tsx`, `payment-qr-dialog.test.tsx` (new)
- `components/payment-qr/qr-image.ts`, `qr-image.test.ts` (new — draws the downloadable picture)
- `components/payment-qr/qr-actions.ts`, `qr-actions.test.ts` (new — copy, share-or-download)
- `components/payment-qr/use-min-width.ts`, `use-min-width.test.ts` (new — `matchMedia` hook)
- `components/icons/icon-registry.ts`, `icon-registry.test.ts` (modify — `copy`, `download`, `share`)
- `pages/cash-vouchers/cash-voucher-form-page.tsx`, `cash-voucher-form-page.test.tsx` (modify)
- `pages/stock-vouchers/stock-voucher-form-page.tsx`, `stock-voucher-form-page.test.tsx` (modify)
- `lib/sw-routes.test.ts` (modify — pin the two routes as not cacheable)

## Reference files (read-only)

- `docs/brainstorms/261015-0656-voucher-payment-qr/mockup-02-screen-dialog.html` — the dialog and the sheet (open it in a browser; match structure and wording).
- `pages/payment-qr/payment-qr-page.tsx` (the quotation QR page: `QRCodeCanvas`, canvas download) and its test for how `qrcode.react` is handled under jsdom.
- `components/voucher-print/voucher-print-button.tsx` (button + `disabledReason` pattern, `AppIcon` use), `components/ui/dialog.tsx`, `components/ui/sheet.tsx` (`side="bottom"`), `lib/blob-download.ts` (`saveBlob`), `lib/api-client.ts` (`apiGet`, `getApiError`, `formatApiErrorDetails`), `lib/use-toast`, `lib/utils.ts` (`formatCurrencyVnd`), `features/cash-common/keys.ts` (`cashKeys`), `stores/auth-store.ts` (`hasPermission`).
- `components/icons/icon-registry.ts` and `icon-registry.test.ts`; `components/icons/index.ts` (`AppIcon`).
- `pages/cash-vouchers/cash-voucher-form-page.tsx` and `pages/stock-vouchers/stock-voucher-form-page.tsx` (the right-hand button row that holds `VoucherPrintButton`; the voucher object has `status`; `isDirty`, `busy`).

## Dialog content (what `PaymentQrDialog` shows)

| Part | Content |
|---|---|
| Title | "QR chuyển khoản" with the voucher code as a muted subtitle |
| QR | `QRCodeCanvas` `value={payload}`, `size={264}`, `level="M"`, `marginSize={4}`, white background |
| Amount | `Amount` in large type (`formatCurrencyVnd`) |
| Breakdown | only when `cashPaid > 0`: "Tổng phiếu {total} − đã trả tiền mặt {cashPaid}" |
| Automatic receipt | only with `sourceStockVoucherCode`: "Phiếu thu tự động theo {code}" |
| Rows | Ngân hàng `bankName`; Số tài khoản `accountNumber` + copy button; Chủ tài khoản `accountHolderName`; Nội dung `content` + copy button; Quỹ nhận "{fundName} ({fundCode})" |
| Footer | "Sao chép thông tin", "Tải ảnh QR" |
| Loading | `<AppIcon name="loading" />` |
| 422 | the server `message`; when it is a fund-setup message (`isFundSetupMessage`: contains "chưa khai ngân hàng" or "chưa có quỹ ngân hàng mặc định") and the user has `cash.catalogs.manage`, a link "Mở màn Quỹ" to `/funds` |
| Other errors | `formatApiErrorDetails` text and a "Thử lại" button |

"Sao chép thông tin" copies five lines: `Ngân hàng: …`, `Số tài khoản: …`, `Chủ tài khoản: …`, `Số tiền: 1.000.000 đồng`, `Nội dung: …`. "Tải ảnh QR" saves/shares `QR_{voucherCode}.png`: the QR canvas plus the amount, the content and the line "{bankShortName} · {accountNumber}" drawn on a white 600 × 760 px canvas.

## Tasks

### Task 4.0 — Second worktree (only for a parallel run)

1. If Phase 03 runs at the same time (Phase 00 Task 0.3 decision): from the main checkout, `git worktree add .claude/worktrees/voucher-qr-fe -b feat/voucher-payment-qr-fe feat/voucher-payment-qr` (the branch tip must be the Phase 02 commit; if Phase 03 already committed, branch from the Phase 02 commit hash instead), and link the packages:
   ```powershell
   New-Item -ItemType Junction -Path "D:\Projects\QLBanHang\.claude\worktrees\voucher-qr-fe\frontend\node_modules" -Target "D:\Projects\QLBanHang\frontend\node_modules"
   ```
   Before removing the worktree later, delete only that link with `(Get-Item "<path>").Delete()`.
2. Run Tasks 4.1–4.6 there. Phase 03 edits `features/voucher-print/*`, `components/voucher-print/*` and backend files, and does not touch `components/icons`, `components/payment-qr`, the two form pages or `lib/sw-routes.test.ts`, so the two branches touch disjoint files; Phase 05 Task 5.0 merges them.
3. A run in one worktree skips this task.

### Task 4.1 — Feature: types, API, keys, hook

1. **Write the failing test** `features/voucher-payment-qr/hooks.test.tsx` (mock `@/lib/api-client` `apiGet`; wrap in a `QueryClientProvider`):
   - `useVoucherPaymentQr('cash', 'c1', { enabled: true })` calls `apiGet('/cash-vouchers/c1/payment-qr')`; `('stock', 's1', …)` calls `'/stock-vouchers/s1/payment-qr'`; the result is the DTO.
   - `enabled: false` and an `undefined` id send no request.
   - Two successive mounts (unmount between them) call the API twice (`gcTime: 0`, `staleTime: 0`).
   - `voucherPaymentQrKeys.detail('cash', 'c1')` equals `['cash', 'payment-qr', 'cash', 'c1']` and starts with `cashKeys.all`.
2. **Run the test to verify it fails:** from `frontend/`: `npm run test -- voucher-payment-qr`. Expected: FAIL — module not found.
3. **Write the minimal implementation:** `types.ts` (`PaymentQrKind = 'cash' | 'stock'`; `VoucherPaymentQr` with the camelCase members of the DTO in the SUMMARY), `api.ts` (`voucherPaymentQrApi.get(kind, id)` → `apiGet<VoucherPaymentQr>(`/${kind === 'cash' ? 'cash-vouchers' : 'stock-vouchers'}/${id}/payment-qr`)`), `keys.ts` (`voucherPaymentQrKeys.detail`), `hooks.ts` (`useQuery({ queryKey, queryFn, enabled: options.enabled && !!id, staleTime: 0, gcTime: 0, retry: false, refetchOnMount: 'always' })`; `retry: false` because a 422 is an answer, not a failure).
4. **Run the test to verify it passes:** the command of step 2. Expected: PASS.
5. **Commit:** `git add frontend && git commit -m "feat(payment-qr): voucher payment QR query"`

### Task 4.2 — Icons

1. **Write the failing test:** in `components/icons/icon-registry.test.ts` add rows to the `it.each` table: `['copy', Copy, undefined]`, `['download', Download, undefined]`, `['share', Share2, undefined]` (import the three from `lucide-react`, as the file already does for the others).
2. **Run the test to verify it fails:** `npm run test -- icon-registry`. Expected: FAIL (`iconDef('copy')` is undefined).
3. **Write the minimal implementation:** in the utility section of `ICONS` add `copy: { icon: Copy }`, `download: { icon: Download }`, `share: { icon: Share2 }` and the two new imports (`Download`, `Share2`) to the `lucide-react` import of `icon-registry.ts` (the only file allowed to import it).
4. **Run the test to verify it passes:** `npm run test -- icons` (all icon guard tests). Expected: PASS.
5. **Commit:** `git add frontend && git commit -m "feat(icons): copy, download and share registry names"`

### Task 4.3 — Helpers: media query, picture, copy and share

1. **Write the failing tests.**
   - `use-min-width.test.ts`: with `window.matchMedia` stubbed to `matches: true`, `useMinWidth(640)` returns `true`; with `matches: false`, `false`; a `change` event on the stubbed list updates the value; without `window.matchMedia` (jsdom default) it returns `true` (desktop).
   - `qr-image.test.ts`: `composeQrImage(qrCanvas, { amount: 1_000_000, content: 'PT000125', bankLine: 'Vietcombank · 0123456789' })` returns a canvas of 600 × 760 pixels; with `HTMLCanvasElement.prototype.getContext` spied to return a recording fake context (`fillRect`, `fillText`, `drawImage`, `font` setters), the recorded `fillText` calls contain "1.000.000 đồng", "PT000125" and "Vietcombank · 0123456789" and `drawImage` is called once with the QR canvas; `canvasToBlob(canvas)` resolves the blob from `toBlob` and rejects when `toBlob` yields `null`.
   - `qr-actions.test.ts`:
     - `copyText('x')` calls `navigator.clipboard.writeText('x')` and shows the toast "Đã sao chép"; when `writeText` rejects, or `navigator.clipboard` is undefined, it shows a destructive toast "Không sao chép được" and does not throw.
     - `shareOrDownload(blob, 'QR_PT1.png')`: with `navigator.canShare` returning `true` it calls `navigator.share({ files: [File named 'QR_PT1.png' of type 'image/png'], title: 'QR_PT1' })` and does not call `saveBlob`; when `share` rejects with an `AbortError` nothing else happens; when it rejects with another error it falls back to `saveBlob(blob, 'QR_PT1.png')`; without `canShare` (or `canShare` false) it calls `saveBlob` directly.
     - `copyText` / `shareOrDownload` import `toast` from `@/lib/use-toast` and `saveBlob` from `@/lib/blob-download` (mock both).
2. **Run the tests to verify they fail:** `npm run test -- components/payment-qr`. Expected: FAIL — modules not found.
3. **Write the minimal implementation:** `use-min-width.ts` (`useState` + `matchMedia(`(min-width: ${px}px)`)` with an `addEventListener('change')` effect, safe when `matchMedia` is missing); `qr-image.ts` (`composeQrImage`, `canvasToBlob`; white background, QR centred at the top with a 24 px margin, then the amount in bold 40 px, the content and the bank line in 24 px, centred; all texts use `formatCurrencyVnd` for the amount); `qr-actions.ts` (`copyText`, `shareOrDownload`, `buildQrInfoText(qr)` returning the five lines of the Dialog content section, tested too: `buildQrInfoText` with a fixture gives exactly the five lines).
4. **Run the tests to verify they pass:** the command of step 2. Expected: PASS.
5. **Commit:** `git add frontend && git commit -m "feat(payment-qr): copy, share and picture helpers"`

### Task 4.4 — `PaymentQrDialog`

1. **Write the failing tests** in `payment-qr-dialog.test.tsx` (mock `qrcode.react`'s `QRCodeCanvas` as a `<canvas data-testid="qr" data-value data-level data-margin width>` element; mock `useVoucherPaymentQr`, `@/stores/auth-store` permissions, `./qr-actions` and `./qr-image`; wrap in `MemoryRouter`; a fixture `qr` with `amount 1_000_000`, `total 1_500_000`, `cashPaid 500_000`, bank "Ngân hàng TMCP Ngoại thương Việt Nam" / "Vietcombank", account "0123456789", holder "CONG TY ABC", content "PX000318", fund "NH01"):
   - `shows the qr and the details`: the canvas has `data-value=payload`, `data-level="M"`, `data-margin="4"`, `width=264`; the amount "1.000.000", the five rows with their values.
   - `shows the breakdown only with cash paid`: "Tổng phiếu 1.500.000 − đã trả tiền mặt 500.000" present; absent with `cashPaid: 0`.
   - `shows the automatic receipt line`: with `sourceStockVoucherCode: 'PX000318'` → "Phiếu thu tự động theo PX000318"; absent without.
   - `copy buttons`: the copy button of the account number calls `copyText('0123456789')`, the one of the content `copyText('PX000318')`; "Sao chép thông tin" calls `copyText(buildQrInfoText(qr))`.
   - `download`: "Tải ảnh QR" calls `composeQrImage` with the QR canvas element and `{ amount, content, bankLine: 'Vietcombank · 0123456789' }`, then `shareOrDownload(blob, 'QR_PT000125.png')` (voucher code `PT000125` passed as a prop).
   - `422 message`: the hook returns an error whose `getApiError` is `{ code: 'PAYMENT_QR_UNAVAILABLE', message: 'Phiếu đã hủy.' }` → the message is shown, no link; with the message "Quỹ NH01 chưa khai ngân hàng, số tài khoản hoặc chủ tài khoản." and `cash.catalogs.manage` the link "Mở màn Quỹ" has `href="/funds"`; without that permission no link; the same for "Chi nhánh chưa có quỹ ngân hàng mặc định đủ thông tin QR.".
   - `other errors`: a network error shows its text and "Thử lại" calls `refetch`.
   - `loading`: while `isLoading`, the loading icon shows and no canvas.
   - `dialog or sheet`: with `matchMedia` ≥ 640 px the content has `data-testid="payment-qr-dialog"`; below it `data-testid="payment-qr-sheet"` (a `SheetContent side="bottom"`).
   - `asks the server on every opening`: the hook is called with `{ enabled: open }`; with `open={false}` nothing is fetched.
2. **Run the tests to verify they fail:** `npm run test -- payment-qr-dialog`. Expected: FAIL — module not found.
3. **Write the minimal implementation:** `payment-qr-dialog.tsx` per the Dialog content section; `Dialog` from `@/components/ui/dialog` above 640 px, `Sheet` (`side="bottom"`, rounded top, `max-h-[90dvh]` with scroll, `pb-safe`) below, sharing one inner `PaymentQrBody`; icons only through `AppIcon` (`copy`, `download`, `share` when the share path is used is not needed — the download button always says "Tải ảnh QR"); `isFundSetupMessage` exported for the test; `useAuthStore((s) => s.hasPermission('cash.catalogs.manage'))`; the link is a react-router `Link to="/funds"`; keep the QR canvas ref for `composeQrImage`.
4. **Run the tests to verify they pass:** `npm run test -- components/payment-qr` and `npm run typecheck && npm run lint`. Expected: PASS.
5. **Commit:** `git add frontend && git commit -m "feat(payment-qr): transfer QR dialog and bottom sheet"`

### Task 4.5 — `PaymentQrButton` and the two voucher pages

1. **Write the failing tests.**
   - `payment-qr-button.test.tsx` (mock `PaymentQrDialog`): renders a button named "QR chuyển khoản" (the label span is `hidden sm:inline`, so use the `aria-label`); clicking opens the dialog (`open` true, with `kind`, `id`, `voucherCode`); `disabled` disables it and shows `disabledReason` as the title; closing sets `open` false.
   - `cash-voucher-form-page.test.tsx`: on a saved **receipt** the page shows the "QR chuyển khoản" button, enabled; it is absent on a **payment** voucher and on a new voucher; disabled with the title "Lưu thay đổi trước khi xem QR" while the form is dirty; disabled with the title "Phiếu đã hủy" for a cancelled voucher. (Copy the way the neighbouring "In" button tests set up the page.)
   - `stock-voucher-form-page.test.tsx`: the same for a saved **stock-out** (present), a stock-in (absent), a new voucher (absent), dirty (disabled), cancelled (disabled, "Phiếu đã hủy").
2. **Run the tests to verify they fail:** `npm run test -- payment-qr-button cash-voucher-form-page stock-voucher-form-page`. Expected: FAIL.
3. **Write the minimal implementation:**
   - `payment-qr-button.tsx`: `<Button variant="outline" size="sm" aria-label="QR chuyển khoản" disabled={disabled} title={disabled ? disabledReason : undefined}><AppIcon name="paymentQr" size={16} /><span className="hidden sm:inline">QR chuyển khoản</span></Button>` plus the dialog; state `open`.
   - Both pages: render `<PaymentQrButton kind="cash|stock" id={voucher.id} code={voucher.code} disabled={isDirty || busy || cancelled} disabledReason={cancelled ? 'Phiếu đã hủy' : isDirty ? 'Lưu thay đổi trước khi xem QR' : undefined} />` right after `VoucherPrintButton`, inside the same `isEdit && voucher` guard, only when the voucher is a receipt (`type === 'Receipt'`) or a stock-out (`type === 'Out'`). `cancelled` comes from the voucher's `status` (`'Cancelled'`). This is the only change to the two page files (the cash voucher form redesign edits the same file; keep the diff to these lines).
4. **Run the tests to verify they pass:** the command of step 2, then `npm run test -- cash-vouchers stock-vouchers payment-qr`. Expected: PASS.
5. **Commit:** `git add frontend && git commit -m "feat(payment-qr): QR button on the receipt and stock-out pages"`

### Task 4.6 — The routes are never cached

1. **Write the failing test** in `lib/sw-routes.test.ts`: `isCacheableApiPath('/api/cash-vouchers/3f2c/payment-qr')` and `isCacheableApiPath('/api/stock-vouchers/3f2c/payment-qr')` are `false`.
2. **Run the test to verify its status:** `npm run test -- sw-routes`. Expected: PASS already (the allow-list excludes them); the test pins the rule from `pwa-mobile.md`. If it fails, remove the offending prefix from `CACHEABLE_API_PREFIXES` and say so in the report.
3. **Commit:** `git add frontend && git commit -m "test(payment-qr): payment-qr routes stay out of the service worker cache"`

## Verification

- From `frontend/`: `npm run typecheck`, `npm run lint`, `npm run test -- payment-qr voucher-payment-qr icon-registry sw-routes cash-vouchers stock-vouchers` pass; the full suite has no new failure against `BASELINE.md`.
- `grep -rn "lucide-react" frontend/src/components/payment-qr frontend/src/features/voucher-payment-qr` finds nothing.
- Open `mockup-02-screen-dialog.html` and the running dialog side by side (Phase 05 Task 5.2 does this in a real browser with a mocked API).

## Exit Criteria

- A saved receipt and a saved stock-out show "QR chuyển khoản" (disabled when unsaved changes, busy or cancelled); the dialog asks the server on every opening, draws the same payload at level M and shows the amount, account, content and fund.
- Copying (with the failure toast), "Sao chép thông tin" and "Tải ảnh QR" (share sheet or download) work as specified; 422 messages show, with the `/funds` link only for `cash.catalogs.manage`.
- Below 640 px the dialog is a bottom sheet.
