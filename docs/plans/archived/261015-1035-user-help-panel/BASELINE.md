# Baseline — user help panel, round 1

> Recorded 2026-10-15 11:57 in the worktree `.claude/worktrees/user-help` on `feat/user-help-panel`, before any code change.

- Base: `qlbanhang/main` at `1a877638d46fce8d5fcc712d8ac06a8a82d223c6` (PR #20). The branch has one commit on top (the plan and the brainstorm).
- Node: v24.18.0 (type stripping works, P7).
- `git grep -n "'F1'" -- src`: no output (no existing F1 binding).

## Overlapping work (Task 0.1 step 2)

| Branch | Commits touching `frontend/src/App.tsx` that are not in `qlbanhang/main` |
|---|---|
| `feat/voucher-payment-qr` | none |
| `feat/cash-voucher-form-redesign` | none |

Other worktrees at the time: `cash-form` (`feat/cash-voucher-form-redesign`), `voucher-qr` (`feat/voucher-payment-qr`), `voucher-qr-fe` (`feat/voucher-payment-qr-fe`), `QLBanHang-wt-archive`. Last base commits on `App.tsx`: `6f975b1d`, `7347c11e`, `ad871c30`.

## Results

| Check | Command | Result |
|---|---|---|
| Typecheck | `npm run typecheck` | clean |
| Lint | `npm run lint` | exit 1: 16 problems, **3 errors**, 13 warnings. Errors: `components/ui/table.tsx:75` (`no-static-element-interactions`), `features/customers/components/customer-catalog-list.tsx:58` and `features/products/components/product-catalog-list.tsx:51` (`no-autofocus`). Known; a later phase must not add to them. |
| Tests | `npm run test -- --testTimeout=30000 --maxWorkers=3` | 2196 passed, **3 failed**, 2199 total (634 suites) |

Known failing tests (not regressions):

1. `src/hooks/useNotificationHub.test.ts` :: useNotificationHub invalidates unread-count query on NewNotification
2. `src/features/bank-accounts/bank-accounts-tab.test.tsx` :: BankAccountsTab adds a new account via the form
3. `src/pages/payment-qr/payment-qr-page.test.tsx` :: PaymentQrPage generates and displays a QR after submitting valid data

The full frontend suite took about 14 minutes under load (other sessions were running tests).
