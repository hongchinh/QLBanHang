import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { PaymentMethodListPage } from './payment-method-list-page';
import type { PaymentMethod } from '@/features/payment-methods/types';

const createMock = vi.fn();
const METHODS: PaymentMethod[] = [
  { id: 'p1', code: 'TM', name: 'Tiền mặt', isCash: true },
  { id: 'p2', code: 'CK', name: 'Chuyển khoản', isCash: false },
];

vi.mock('@/features/payment-methods/hooks', () => ({
  usePaymentMethods: () => ({ data: METHODS, isLoading: false, isError: false }),
  useCreatePaymentMethod: () => ({ mutateAsync: createMock, isPending: false }),
  useUpdatePaymentMethod: () => ({ mutateAsync: vi.fn(), isPending: false }),
  useDeletePaymentMethod: () => ({ mutate: vi.fn(), isPending: false }),
}));

vi.mock('@/lib/use-toast', () => ({ toast: vi.fn() }));

const granted = new Set<string>(['inventory.catalogs.manage']);
vi.mock('@/stores/auth-store', () => ({
  useAuthStore: (selector: (s: { hasPermission: (p: string) => boolean; isInRole: () => boolean }) => unknown) =>
    selector({ hasPermission: (p) => granted.has(p), isInRole: () => false }),
}));

describe('PaymentMethodListPage', () => {
  beforeEach(() => {
    createMock.mockReset().mockResolvedValue({});
  });

  it('renders methods and creates one with isCash', async () => {
    const user = userEvent.setup();
    render(
      <MemoryRouter>
        <PaymentMethodListPage />
      </MemoryRouter>,
    );

    const cashRow = screen.getByText('TM').closest('tr')!;
    expect(within(cashRow).getByText('Có')).toBeInTheDocument();
    expect(screen.getByText('Chuyển khoản')).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: /Thêm phương thức/ }));
    await user.type(screen.getByLabelText('Mã *'), 'QT');
    await user.type(screen.getByLabelText('Tên phương thức *'), 'Quỹ tạm');
    await user.click(screen.getByLabelText('Tiền mặt'));
    await user.click(screen.getByRole('button', { name: 'Tạo mới' }));

    await waitFor(() =>
      expect(createMock).toHaveBeenCalledWith({ code: 'QT', name: 'Quỹ tạm', isCash: true }),
    );
  });
});
