# Phase 05 — Frontend: saved bank accounts settings tab

**Status:** [ ] pending
**Complexity:** S

## Objective

Let a user manage their own saved receiving bank accounts (add/edit/delete/set default) from a new
"Tài khoản ngân hàng" tab inside the existing "Cài đặt của tôi" page, and use the default saved
account to pre-fill the payment-qr page.

## Files

- `frontend/src/features/bank-accounts/types.ts` (new)
- `frontend/src/features/bank-accounts/api.ts` (new)
- `frontend/src/features/bank-accounts/hooks.ts` (new)
- `frontend/src/features/bank-accounts/schema.ts` (new)
- `frontend/src/features/bank-accounts/bank-accounts-tab.tsx` (new)
- `frontend/src/pages/settings/my-quotation-settings-page.tsx` (edit)
- `frontend/src/pages/payment-qr/payment-qr-page.tsx` (edit)

## Tasks

### 1. `bank-accounts` feature module

1. Write the failing test — create `frontend/src/features/bank-accounts/hooks.test.ts`:

```ts
import { describe, expect, it, vi } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { useMyBankAccounts } from './hooks';
import { bankAccountsApi } from './api';

vi.mock('./api', () => ({
  bankAccountsApi: {
    list: vi.fn(),
    create: vi.fn(),
    update: vi.fn(),
    remove: vi.fn(),
    setDefault: vi.fn(),
  },
}));

function wrapper({ children }: { children: React.ReactNode }) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return <QueryClientProvider client={qc}>{children}</QueryClientProvider>;
}

describe('useMyBankAccounts', () => {
  it('fetches saved accounts', async () => {
    vi.mocked(bankAccountsApi.list).mockResolvedValue([
      {
        id: 'acc-1', bankId: 'bank-1', bankCode: 'VCB', bankName: 'Vietcombank', bankBin: '970436',
        accountNumber: '0123456789', accountName: 'Nguyen Van A', isDefault: true,
      },
    ]);

    const { result } = renderHook(() => useMyBankAccounts(), { wrapper });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(result.current.data?.[0].isDefault).toBe(true);
  });
});
```

   Run: `cd frontend && npm run test -- bank-accounts/hooks.test.ts` / Expected: FAIL (modules
   don't exist).

2. Create `frontend/src/features/bank-accounts/types.ts`:

```ts
export interface UserBankAccount {
  id: string;
  bankId: string;
  bankCode: string;
  bankName: string;
  bankBin: string;
  accountNumber: string;
  accountName: string;
  isDefault: boolean;
}

export interface CreateUserBankAccountRequest {
  bankId: string;
  accountNumber: string;
  accountName: string;
  isDefault?: boolean;
}

export interface UpdateUserBankAccountRequest {
  bankId: string;
  accountNumber: string;
  accountName: string;
}
```

3. Create `frontend/src/features/bank-accounts/api.ts`:

```ts
import { apiDelete, apiGet, apiPost, apiPut } from '@/lib/api-client';
import type { CreateUserBankAccountRequest, UpdateUserBankAccountRequest, UserBankAccount } from './types';

export const bankAccountsApi = {
  list: () => apiGet<UserBankAccount[]>('/me/bank-accounts'),
  create: (data: CreateUserBankAccountRequest) => apiPost<UserBankAccount>('/me/bank-accounts', data),
  update: (id: string, data: UpdateUserBankAccountRequest) =>
    apiPut<UserBankAccount>(`/me/bank-accounts/${id}`, data),
  remove: (id: string) => apiDelete<void>(`/me/bank-accounts/${id}`),
  setDefault: (id: string) => apiPut<UserBankAccount>(`/me/bank-accounts/${id}/default`),
};
```

4. Create `frontend/src/features/bank-accounts/hooks.ts`:

```ts
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { bankAccountsApi } from './api';
import type { CreateUserBankAccountRequest, UpdateUserBankAccountRequest } from './types';

const KEYS = { list: ['bank-accounts'] as const };

export function useMyBankAccounts() {
  return useQuery({ queryKey: KEYS.list, queryFn: bankAccountsApi.list });
}

export function useCreateBankAccount() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (data: CreateUserBankAccountRequest) => bankAccountsApi.create(data),
    onSuccess: () => qc.invalidateQueries({ queryKey: KEYS.list }),
  });
}

export function useUpdateBankAccount() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, data }: { id: string; data: UpdateUserBankAccountRequest }) =>
      bankAccountsApi.update(id, data),
    onSuccess: () => qc.invalidateQueries({ queryKey: KEYS.list }),
  });
}

export function useDeleteBankAccount() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => bankAccountsApi.remove(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: KEYS.list }),
  });
}

export function useSetDefaultBankAccount() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => bankAccountsApi.setDefault(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: KEYS.list }),
  });
}
```

5. Run test — `cd frontend && npm run test -- bank-accounts/hooks.test.ts` / Expected: PASS.
6. Create `frontend/src/features/bank-accounts/schema.ts`:

```ts
import { z } from 'zod';

export const bankAccountFormSchema = z.object({
  bankId: z.string().min(1, 'Chọn ngân hàng'),
  accountNumber: z.string().regex(/^[0-9]{6,19}$/, 'Số tài khoản chỉ gồm 6-19 chữ số'),
  accountName: z.string().min(1, 'Nhập tên chủ tài khoản').max(255),
});

export type BankAccountFormValues = z.infer<typeof bankAccountFormSchema>;
```

7. Commit — `git commit -m "feat(bank-accounts): add saved bank account feature module"`.

### 2. Bank accounts settings tab UI

1. Write the failing test — create `frontend/src/features/bank-accounts/bank-accounts-tab.test.tsx`:

```tsx
import { describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { BankAccountsTab } from './bank-accounts-tab';
import { bankAccountsApi } from './api';
import { banksApi } from '@/features/banks/api';

vi.mock('./api', () => ({
  bankAccountsApi: { list: vi.fn(), create: vi.fn(), update: vi.fn(), remove: vi.fn(), setDefault: vi.fn() },
}));
vi.mock('@/features/banks/api', () => ({ banksApi: { list: vi.fn() } }));

function renderTab() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={qc}>
      <BankAccountsTab />
    </QueryClientProvider>,
  );
}

describe('BankAccountsTab', () => {
  it('lists saved accounts and marks the default one', async () => {
    vi.mocked(banksApi.list).mockResolvedValue([
      { id: 'bank-1', code: 'VCB', name: 'Vietcombank', shortName: 'VCB', bin: '970436' },
    ]);
    vi.mocked(bankAccountsApi.list).mockResolvedValue([
      {
        id: 'acc-1', bankId: 'bank-1', bankCode: 'VCB', bankName: 'Vietcombank', bankBin: '970436',
        accountNumber: '0123456789', accountName: 'Nguyen Van A', isDefault: true,
      },
    ]);

    renderTab();

    expect(await screen.findByText('0123456789')).toBeInTheDocument();
    expect(screen.getByText(/mặc định/i)).toBeInTheDocument();
  });

  it('adds a new account via the form', async () => {
    vi.mocked(banksApi.list).mockResolvedValue([
      { id: 'bank-1', code: 'VCB', name: 'Vietcombank', shortName: 'VCB', bin: '970436' },
    ]);
    vi.mocked(bankAccountsApi.list).mockResolvedValue([]);
    vi.mocked(bankAccountsApi.create).mockResolvedValue({
      id: 'acc-2', bankId: 'bank-1', bankCode: 'VCB', bankName: 'Vietcombank', bankBin: '970436',
      accountNumber: '9998887776', accountName: 'New Account', isDefault: true,
    });

    const user = userEvent.setup();
    renderTab();

    await user.click(await screen.findByRole('button', { name: /thêm tài khoản/i }));
    await user.click(screen.getByRole('combobox', { name: /ngân hàng/i }));
    await user.click(await screen.findByText('Vietcombank'));
    await user.type(screen.getByLabelText(/số tài khoản/i), '9998887776');
    await user.type(screen.getByLabelText(/chủ tài khoản/i), 'New Account');
    await user.click(screen.getByRole('button', { name: /^lưu$/i }));

    await waitFor(() => expect(bankAccountsApi.create).toHaveBeenCalledWith(
      expect.objectContaining({ bankId: 'bank-1', accountNumber: '9998887776', accountName: 'New Account' }),
    ));
  });
});
```

   Run: `cd frontend && npm run test -- bank-accounts-tab.test.tsx` / Expected: FAIL (component
   doesn't exist).

2. Create `frontend/src/features/bank-accounts/bank-accounts-tab.tsx` following the structure of
   `frontend/src/features/branding/branding-tab.tsx` and `frontend/src/pages/settings/my-quotation-settings-page.tsx`:
   - `useMyBankAccounts()` to list; render each as a `Card` row showing `bankName`,
     `accountNumber`, `accountName`, and a "Mặc định" badge/text when `isDefault` is true.
   - Each row has "Sửa" (edit), "Xoá" (delete, wrapped in the existing `ConfirmDialog` component
     the same way `quotation-form-page.tsx` uses it for destructive actions), and — only when not
     already default — "Đặt mặc định" buttons wired to `useUpdateBankAccount`,
     `useDeleteBankAccount`, `useSetDefaultBankAccount` respectively, each followed by a
     `toast(...)` call matching the success/error toast pattern used throughout
     `my-quotation-settings-page.tsx` (`getErrorMessage(err)` on failure).
   - A "Thêm tài khoản" button that opens an inline form (or a `Dialog` — reuse
     `frontend/src/components/ui/dialog.tsx` primitives the same way
     `customer-quick-add-dialog.tsx` does) built with `useForm<BankAccountFormValues>({ resolver:
     zodResolver(bankAccountFormSchema) })`: a bank `Select` (`aria-label="Ngân hàng"`, `Controller`
     name `bankId`, options from `useBanks()`), `Input` `id="accountNumber"` labeled "Số tài
     khoản", `Input` `id="accountName"` labeled "Chủ tài khoản". Submit button labeled exactly
     "Lưu" (so the test's `/^lưu$/i` matcher is unambiguous — do not reuse that literal string for
     any other button on the page). On submit calls `useCreateBankAccount().mutateAsync(...)` (or
     `useUpdateBankAccount` when editing an existing row), then closes the form/dialog.
   - Empty state: when the list is empty, show a short message ("Chưa có tài khoản nhận tiền nào
     được lưu.") plus the same "Thêm tài khoản" button.

3. Run test — `cd frontend && npm run test -- bank-accounts-tab.test.tsx` / Expected: PASS.
4. Commit — `git commit -m "feat(bank-accounts): add bank accounts settings tab UI"`.

### 3. Wire the tab into "Cài đặt của tôi"

1. Edit `frontend/src/pages/settings/my-quotation-settings-page.tsx`:
   - Import `{ BankAccountsTab } from '@/features/bank-accounts/bank-accounts-tab'`.
   - Restructure `MyQuotationSettingsPage` so the `Tabs` component is always rendered (not only
     when `canManageBranding`), with a new `TabsTrigger value="bank-accounts"` labeled "Tài khoản
     ngân hàng" and matching `TabsContent value="bank-accounts"` rendering `<BankAccountsTab />`.
     Keep the existing conditional `branding` tab exactly as-is; only the "always show Tabs, always
     include bank-accounts tab" structure changes. The non-`canManageBranding` branch that
     currently renders `<QuotationSettingsTabContent />` directly (without `Tabs`) must become a
     `Tabs` with two triggers: `quotation` and `bank-accounts`.
2. Manual check: `cd frontend && npm run dev`, open "Cài đặt của tôi", confirm the new tab renders
   and a saved account can be added, edited, deleted, and set as default.
3. Commit — `git commit -m "feat(bank-accounts): wire bank accounts tab into my-quotation-settings page"`.

### 4. Pre-fill payment-qr page from the default saved account

1. Edit `frontend/src/pages/payment-qr/payment-qr-page.tsx` (created in Phase 04): call
   `useMyBankAccounts()`; once loaded, if no bank/account has been picked yet by the user, find the
   entry with `isDefault === true` and set `bankId`/`accountNumber`/`accountName` form defaults
   from it via `form.reset({ ...currentValues, bankId: def.bankId, accountNumber:
   def.accountNumber, accountName: def.accountName })`, run once (guard with a `useRef` flag so it
   doesn't clobber user edits on every render/refetch). If there is no default account, leave the
   fields empty as before — the user still picks manually.
2. Manual check: save a default bank account in settings, then open `/qr-thanh-toan` fresh and
   confirm the bank/account number/account name are pre-filled.
3. Commit — `git commit -m "feat(payment-qr): pre-fill form from default saved bank account"`.

## Verification

- `cd frontend && npm run typecheck`
- `cd frontend && npm run test -- bank-accounts payment-qr`
- `cd frontend && npm run build`

## Exit Criteria

- "Cài đặt của tôi" has a working "Tài khoản ngân hàng" tab: add/edit/delete/set-default all work
  end to end against the Phase 03 backend endpoints.
- `/qr-thanh-toan` pre-fills bank/account from the user's default saved account when one exists.
