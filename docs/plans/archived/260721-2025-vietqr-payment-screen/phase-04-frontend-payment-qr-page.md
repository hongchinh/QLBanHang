# Phase 04 — Frontend: payment-qr page

**Status:** [ ] pending
**Complexity:** M

## Objective

Add the standalone `/qr-thanh-toan` page: pick a bank + enter account number/name, enter amount +
transfer content, call the backend to get the VietQR payload, render it as a QR image client-side,
and let the user download it as PNG.

## Files

- `frontend/package.json` (edit — add `qrcode.react`)
- `frontend/src/features/banks/types.ts` (new)
- `frontend/src/features/banks/api.ts` (new)
- `frontend/src/features/banks/hooks.ts` (new)
- `frontend/src/features/payment-qr/types.ts` (new)
- `frontend/src/features/payment-qr/api.ts` (new)
- `frontend/src/features/payment-qr/hooks.ts` (new)
- `frontend/src/features/payment-qr/schema.ts` (new)
- `frontend/src/pages/payment-qr/payment-qr-page.tsx` (new)
- `frontend/src/App.tsx` (edit — add route)
- `frontend/src/components/layout/app-layout.tsx` (edit — add nav entry)
- `frontend/src/pages/payment-qr/payment-qr-page.test.tsx` (new)

## Tasks

### 1. Install QR rendering library

1. Run `cd frontend && npm install qrcode.react` (adds a React 18-compatible QR renderer exposing
   `QRCodeCanvas`). Confirm the installed version in `frontend/package.json` supports React 18
   (any `3.x` release does).
2. Commit — `git commit -m "chore(frontend): add qrcode.react dependency"`.

### 2. `banks` feature module (read-only lookup)

1. Write the failing test — create `frontend/src/features/banks/hooks.test.ts`:

```ts
import { describe, expect, it, vi } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { useBanks } from './hooks';
import { banksApi } from './api';

vi.mock('./api', () => ({
  banksApi: { list: vi.fn() },
}));

function wrapper({ children }: { children: React.ReactNode }) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return <QueryClientProvider client={qc}>{children}</QueryClientProvider>;
}

describe('useBanks', () => {
  it('fetches the bank list', async () => {
    vi.mocked(banksApi.list).mockResolvedValue([
      { id: '1', code: 'VCB', name: 'Vietcombank', shortName: 'VCB', bin: '970436' },
    ]);

    const { result } = renderHook(() => useBanks(), { wrapper });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(result.current.data?.[0].code).toBe('VCB');
  });
});
```

   Run: `cd frontend && npm run test -- banks/hooks.test.ts` / Expected: FAIL (module `./hooks`
   and `./api` don't exist yet).

2. Create `frontend/src/features/banks/types.ts`:

```ts
export interface Bank {
  id: string;
  code: string;
  name: string;
  shortName?: string;
  bin: string;
}
```

3. Create `frontend/src/features/banks/api.ts`:

```ts
import { apiGet } from '@/lib/api-client';
import type { Bank } from './types';

export const banksApi = {
  list: () => apiGet<Bank[]>('/banks'),
};
```

4. Create `frontend/src/features/banks/hooks.ts`:

```ts
import { useQuery } from '@tanstack/react-query';
import { banksApi } from './api';

export function useBanks() {
  return useQuery({ queryKey: ['banks'], queryFn: banksApi.list });
}
```

5. Run test — `cd frontend && npm run test -- banks/hooks.test.ts` / Expected: PASS.
6. Commit — `git commit -m "feat(banks): add read-only bank list feature module"`.

### 3. `payment-qr` feature module

1. Write the failing test — create `frontend/src/features/payment-qr/schema.test.ts`:

```ts
import { describe, expect, it } from 'vitest';
import { paymentQrSchema } from './schema';

describe('paymentQrSchema', () => {
  it('accepts a valid payload', () => {
    const result = paymentQrSchema.safeParse({
      bankId: 'bank-1',
      accountNumber: '0123456789',
      accountName: 'Nguyen Van A',
      amount: 150000,
      content: 'TT DH-0001',
    });
    expect(result.success).toBe(true);
  });

  it('rejects a non-numeric account number', () => {
    const result = paymentQrSchema.safeParse({
      bankId: 'bank-1',
      accountNumber: 'abc',
      accountName: 'Nguyen Van A',
      amount: 150000,
    });
    expect(result.success).toBe(false);
  });

  it('rejects a zero amount', () => {
    const result = paymentQrSchema.safeParse({
      bankId: 'bank-1',
      accountNumber: '0123456789',
      accountName: 'Nguyen Van A',
      amount: 0,
    });
    expect(result.success).toBe(false);
  });
});
```

   Run: `cd frontend && npm run test -- payment-qr/schema.test.ts` / Expected: FAIL (module doesn't
   exist).

2. Create `frontend/src/features/payment-qr/schema.ts`:

```ts
import { z } from 'zod';

export const paymentQrSchema = z.object({
  bankId: z.string().min(1, 'Chọn ngân hàng'),
  accountNumber: z
    .string()
    .regex(/^[0-9]{6,19}$/, 'Số tài khoản chỉ gồm 6-19 chữ số'),
  accountName: z.string().min(1, 'Nhập tên chủ tài khoản').max(255),
  amount: z.coerce.number().positive('Số tiền phải lớn hơn 0').max(999_999_999_999),
  content: z.string().max(255).optional(),
});

export type PaymentQrFormValues = z.infer<typeof paymentQrSchema>;
```

3. Run test — `cd frontend && npm run test -- payment-qr/schema.test.ts` / Expected: PASS.
4. Create `frontend/src/features/payment-qr/types.ts`:

```ts
export interface GenerateQrRequest {
  bankId: string;
  accountNumber: string;
  accountName: string;
  amount: number;
  content?: string;
}

export interface GenerateQrResponse {
  payload: string;
}
```

5. Create `frontend/src/features/payment-qr/api.ts`:

```ts
import { apiPost } from '@/lib/api-client';
import type { GenerateQrRequest, GenerateQrResponse } from './types';

export const paymentQrApi = {
  generate: (data: GenerateQrRequest) => apiPost<GenerateQrResponse>('/payment-qr/generate', data),
};
```

6. Create `frontend/src/features/payment-qr/hooks.ts`:

```ts
import { useMutation } from '@tanstack/react-query';
import { paymentQrApi } from './api';
import type { GenerateQrRequest } from './types';

export function useGenerateQr() {
  return useMutation({
    mutationFn: (data: GenerateQrRequest) => paymentQrApi.generate(data),
  });
}
```

7. Commit — `git commit -m "feat(payment-qr): add payment-qr feature module"`.

### 4. Payment QR page

1. Write the failing test — create `frontend/src/pages/payment-qr/payment-qr-page.test.tsx`:

```tsx
import { describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { PaymentQrPage } from './payment-qr-page';
import { banksApi } from '@/features/banks/api';
import { paymentQrApi } from '@/features/payment-qr/api';

vi.mock('@/features/banks/api', () => ({ banksApi: { list: vi.fn() } }));
vi.mock('@/features/payment-qr/api', () => ({ paymentQrApi: { generate: vi.fn() } }));

function renderPage(initialEntries = ['/qr-thanh-toan']) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={qc}>
      <MemoryRouter initialEntries={initialEntries}>
        <PaymentQrPage />
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe('PaymentQrPage', () => {
  it('pre-fills amount and content from query params', async () => {
    vi.mocked(banksApi.list).mockResolvedValue([
      { id: 'bank-1', code: 'VCB', name: 'Vietcombank', shortName: 'VCB', bin: '970436' },
    ]);

    renderPage(['/qr-thanh-toan?amount=150000&content=DH-0001']);

    await waitFor(() => expect(screen.getByLabelText(/số tiền/i)).toHaveValue(150000));
    expect(screen.getByLabelText(/nội dung/i)).toHaveValue('DH-0001');
  });

  it('generates and displays a QR after submitting valid data', async () => {
    vi.mocked(banksApi.list).mockResolvedValue([
      { id: 'bank-1', code: 'VCB', name: 'Vietcombank', shortName: 'VCB', bin: '970436' },
    ]);
    vi.mocked(paymentQrApi.generate).mockResolvedValue({ payload: '00020101021...6304ABCD' });

    const user = userEvent.setup();
    renderPage();

    await waitFor(() => expect(banksApi.list).toHaveBeenCalled());
    await user.click(screen.getByRole('combobox', { name: /ngân hàng/i }));
    await user.click(await screen.findByText('Vietcombank'));
    await user.type(screen.getByLabelText(/số tài khoản/i), '0123456789');
    await user.type(screen.getByLabelText(/chủ tài khoản/i), 'Nguyen Van A');
    await user.type(screen.getByLabelText(/số tiền/i), '150000');
    await user.click(screen.getByRole('button', { name: /tạo qr/i }));

    await waitFor(() => expect(paymentQrApi.generate).toHaveBeenCalledWith(
      expect.objectContaining({ bankId: 'bank-1', accountNumber: '0123456789', amount: 150000 }),
    ));
    expect(await screen.findByRole('button', { name: /tải ảnh/i })).toBeInTheDocument();
  });
});
```

   Run: `cd frontend && npm run test -- payment-qr-page.test.tsx` / Expected: FAIL (page component
   doesn't exist yet).

2. Create `frontend/src/pages/payment-qr/payment-qr-page.tsx`:
   - Read `amount` and `content` from `useSearchParams()` on mount; use as `defaultValues` for
     `amount` (coerced to number) and `content` in `useForm<PaymentQrFormValues>({ resolver:
     zodResolver(paymentQrSchema), defaultValues: {...} })`.
   - Fetch banks via `useBanks()`; render a `Select`/`SelectTrigger` with `aria-label="Ngân hàng"`
     wired through a `Controller` (`name="bankId"`), listing `bank.name` as each `SelectItem`'s
     visible label (`SelectItem key={bank.id} value={bank.id}`).
   - `Input` fields: `id="accountNumber"` with `<Label htmlFor="accountNumber">Số tài khoản</Label>`,
     `id="accountName"` labeled "Chủ tài khoản", `id="amount"` type `number` labeled "Số tiền
     (VNĐ)", `id="content"` labeled "Nội dung chuyển khoản" (optional).
   - On submit (`handleSubmit`), call `useGenerateQr().mutateAsync({ bankId, accountNumber,
     accountName, amount, content })`; on success store the returned `payload` string in local
     `useState<string | null>`.
   - When `payload` is set, render:
     - `<QRCodeCanvas value={payload} size={280} ref={canvasContainerRef} />` from `qrcode.react`
       (wrap it in a `<div ref={qrWrapperRef}>` if `QRCodeCanvas` doesn't forward a ref directly —
       use `document.querySelector('canvas')` inside the wrapper as a fallback to obtain the
       `<canvas>` element for export).
     - A summary block below showing bank name, account number, account name, formatted amount
       (`formatCurrencyVnd` from `@/lib/utils`) and content.
     - A "Tải ảnh QR" button (`aria-label`/accessible name matching `/tải ảnh/i` so the test finds
       it) that reads the `<canvas>` element's `toDataURL('image/png')`, creates an `<a>` with
       `download="qr-thanh-toan.png"`, clicks it, and removes it — following the exact
       blob-download pattern already used in
       `frontend/src/pages/settings/my-quotation-settings-page.tsx`'s `handleDownload` functions
       (create anchor, `document.body.appendChild`, `click()`, `document.body.removeChild`) but
       using a data URL directly instead of `URL.createObjectURL` since canvas export gives a data
       URL, not a `Blob`.
   - Submit button labeled "Tạo QR" (`aria-label`/name matching `/tạo qr/i`), disabled while
     `useGenerateQr().isPending`, using `ButtonLoader` the same way `quotation-form-page.tsx` does
     for its own pending buttons.
   - Wrap the page body in the same `<div className="space-y-6">` + `<h1
     className="text-2xl font-bold">` header pattern used by `MyQuotationSettingsPage`.

3. Run test — `cd frontend && npm run test -- payment-qr-page.test.tsx` / Expected: PASS.
4. Commit — `git commit -m "feat(payment-qr): add payment QR generation page"`.

### 5. Route + navigation

1. Edit `frontend/src/App.tsx`:
   - Add `import { PaymentQrPage } from '@/pages/payment-qr/payment-qr-page';`.
   - Add a route inside the protected `<Route element={<ProtectedRoute><AppLayout /></ProtectedRoute>}>`
     block, sibling to `settings/my-quotation-settings` (no `permission` prop — any authenticated
     user):
     ```tsx
     <Route path="qr-thanh-toan" element={<PaymentQrPage />} />
     ```
2. Edit `frontend/src/components/layout/app-layout.tsx` — add a nav entry to the `'Chức năng'`
   group array, after the `quotations` entry, with no `permission` key (visible to everyone) and
   the `QrCode` icon from `lucide-react` (add it to the existing `lucide-react` import list at the
   top of the file):
   ```tsx
   { to: '/qr-thanh-toan', label: 'Tạo mã QR thanh toán', icon: QrCode },
   ```
3. Manual check: `cd frontend && npm run dev`, log in, open `/qr-thanh-toan` from the sidebar,
   pick a bank, fill the form, submit, confirm a QR renders and "Tải ảnh QR" downloads a PNG. Then
   scan the rendered QR (or the downloaded PNG) with a real Vietnamese banking app and confirm it
   correctly reads bank/account number/account name/amount/content — this is the only check that
   validates the payload actually works with a real bank scanner, not just that it's structurally
   well-formed.
4. Commit — `git commit -m "feat(payment-qr): route and nav entry for payment QR page"`.

## Verification

- `cd frontend && npm run typecheck`
- `cd frontend && npm run test -- banks payment-qr`
- `cd frontend && npm run build`

## Exit Criteria

- `/qr-thanh-toan` is reachable from the sidebar for any logged-in user.
- The form validates bank/account number/amount, calls the backend, renders a QR from the returned
  payload, and can download it as PNG.
- Query params `amount` and `content` pre-fill the form when present.
