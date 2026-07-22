# VietQR Payment QR Screen

## Goal

Add a screen where a user enters a bank, account number and amount and gets a VietQR-compliant
(NAPAS 247 / EMVCo) QR code image that any Vietnamese banking app can scan to auto-fill the
recipient account and amount for a bank transfer. Users can save their own receiving bank
account(s) for reuse, and the quotation detail screen can jump into this screen with the
quotation total and code pre-filled.

## Scope

In scope:
- Backend `Banks` lookup table (seeded with major Vietnamese NAPAS-member banks) and
  `UserBankAccounts` table (per-user saved receiving accounts).
- Backend VietQR/EMVCo payload generation implemented from scratch (TLV encoding + CRC16-CCITT
  checksum) — no third-party VietQR API call.
- Backend CRUD for a user's own saved bank accounts.
- Frontend standalone page `/qr-thanh-toan`: pick/save bank + account, enter amount + transfer
  content, render QR (client-side, from the backend-provided payload string), download as PNG.
- Frontend "Tài khoản ngân hàng" tab inside "Cài đặt của tôi" to manage saved accounts.
- A "Tạo QR thanh toán" button on the quotation detail/form page that opens the QR page with the
  quotation total and code pre-filled via query params.

Out of scope:
- No QR generation history/audit log.
- No payment confirmation / bank webhook / reconciliation — the app has no way to know the
  transfer actually happened.
- No admin screen for managing the `Banks` list; it is seeded and updated by editing code.
- No new permission constants — every endpoint only requires `[Authorize]` (any logged-in user).

## Assumptions

- The seeded bank list (Phase 01) uses publicly known NAPAS BIN codes for major Vietnamese banks.
  This is reference data, not sourced from the project — acceptable as a maintained code constant
  per the approved design ("danh sách tĩnh... seed sẵn").
- VND has no minor unit, so amounts are encoded as whole-number strings (no decimal point) in the
  EMVCo `54` field.
- The account-holder name (field `59`) and city (field `60`) are encoded using EMVCo's restricted
  alphanumeric-with-limited-special-characters charset, so Vietnamese diacritics are stripped and
  the string is upper-cased before encoding. This matches how real VietQR generators behave.
- Merchant city (`60`) is not collected from the user; it is hard-coded to `"VIETNAM"` since no
  other value is available.

## Risks

- An incorrect TLV/CRC implementation would produce a QR that banking apps reject or misparse.
  Mitigated by a well-known independent CRC16-CCITT-FALSE test vector (`"123456789"` → `29B1`) plus
  structural assertions on every TLV field in the payload builder tests (Phase 02).
- EMVCo field-length limits (max 99 bytes per sub-field) could be exceeded by a long transfer
  content or account name; mitigated by validator max-lengths in Phase 02/03.

## Phases

- [x] Phase 01 — Backend: Bank & UserBankAccount persistence (M) — `phase-01-backend-persistence.md`
- [x] Phase 02 — Backend: VietQR payload generation + banks/QR endpoints (M) — `phase-02-backend-vietqr-generation.md`
- [x] Phase 03 — Backend: user bank account CRUD endpoints (M) — `phase-03-backend-bank-account-crud.md`
- [x] Phase 04 — Frontend: payment-qr page (M) — `phase-04-frontend-payment-qr-page.md`
- [x] Phase 05 — Frontend: saved bank accounts settings tab (S) — `phase-05-frontend-bank-accounts-settings.md`
- [x] Phase 06 — Frontend: quotation integration + nav (S) — `phase-06-frontend-quotation-integration.md`

## Final Verification

From `backend/`:
```
dotnet build
dotnet test
```

From `frontend/`:
```
npm run typecheck
npm run test
npm run build
```

Manual check: run backend + frontend dev servers, open `/qr-thanh-toan`, pick a bank, enter an
account number/amount/content, confirm a QR renders and downloads as PNG. Open a quotation, click
"Tạo QR thanh toán", confirm the amount and content are pre-filled.

**Real-device scan (required, not just visual inspection):** scan at least one generated QR with an
actual Vietnamese banking app (VCB/Techcombank/MB, etc., or the NAPAS-compliant test scanner used by
the team) and confirm it reads the correct bank, account number, account name, amount, and content.
Unit tests only assert TLV/CRC structure — they cannot catch a structurally-valid payload that a
real bank app still mis-parses or rejects (e.g. due to the hard-coded merchant city `"VIETNAM"`).
This is the actual acceptance test for the feature's core promise, not the automated suites.

## Rollback / Recovery

Each phase is a separate commit. To roll back, `git revert` the phase commits in reverse order —
Phase 06 → 01. The Phase 01 migration is additive only (`AddBanksAndUserBankAccounts`); reverting
it drops the two new tables via `dotnet ef database update <previous-migration>` if it was already
applied to a shared database.
