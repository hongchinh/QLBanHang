import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { PaymentMethodListPage } from './payment-method-list-page';
import type { PaymentMethod } from '@/features/payment-methods/types';

const createMock = vi.fn();
const updateMock = vi.fn();
const METHODS: PaymentMethod[] = [
  { id: 'p1', code: 'TM', name: 'Tiền mặt quầy', fundKind: 'Cash' },
  { id: 'p2', code: 'CK', name: 'Chuyển khoản', fundKind: 'Bank' },
  { id: 'p3', code: 'CN', name: 'Công nợ', fundKind: 'None' },
];

vi.mock('@/features/payment-methods/hooks', () => ({
  usePaymentMethods: () => ({ data: METHODS, isLoading: false, isError: false }),
  useCreatePaymentMethod: () => ({ mutateAsync: createMock, isPending: false }),
  useUpdatePaymentMethod: () => ({ mutateAsync: updateMock, isPending: false }),
  useDeletePaymentMethod: () => ({ mutate: vi.fn(), isPending: false }),
}));

vi.mock('@/lib/use-toast', () => ({ toast: vi.fn() }));

const granted = new Set<string>(['inventory.catalogs.manage']);
vi.mock('@/stores/auth-store', () => ({
  useAuthStore: (selector: (s: { hasPermission: (p: string) => boolean; isInRole: () => boolean }) => unknown) =>
    selector({ hasPermission: (p) => granted.has(p), isInRole: () => false }),
}));

function renderPage() {
  return render(
    <MemoryRouter>
      <PaymentMethodListPage />
    </MemoryRouter>,
  );
}

describe('PaymentMethodListPage', () => {
  beforeEach(() => {
    createMock.mockReset().mockResolvedValue({});
    updateMock.mockReset().mockResolvedValue({});
  });

  it('renders methods and creates one with fundKind', async () => {
    const user = userEvent.setup();
    renderPage();

    expect(screen.getByText('Chuyển khoản')).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: /Thêm phương thức/ }));
    await user.type(screen.getByLabelText('Mã *'), 'QT');
    await user.type(screen.getByLabelText('Tên phương thức *'), 'Quỹ tạm');
    await user.click(screen.getByRole('combobox', { name: 'Loại quỹ' }));
    await user.click(await screen.findByRole('option', { name: 'Tiền mặt' }));
    await user.click(screen.getByRole('button', { name: 'Tạo mới' }));

    await waitFor(() =>
      expect(createMock).toHaveBeenCalledWith({ code: 'QT', name: 'Quỹ tạm', fundKind: 'Cash' }),
    );
  });

  it('shows fund kind label', () => {
    renderPage();

    expect(screen.getByRole('columnheader', { name: 'Loại quỹ' })).toBeInTheDocument();
    expect(within(screen.getByText('TM').closest('tr')!).getByText('Tiền mặt')).toBeInTheDocument();
    expect(within(screen.getByText('CK').closest('tr')!).getByText('Ngân hàng')).toBeInTheDocument();
    expect(within(screen.getByText('CN').closest('tr')!).getByText('Không')).toBeInTheDocument();
  });

  it('edit form starts from the method fund kind and sends it', async () => {
    const user = userEvent.setup();
    renderPage();

    await user.click(within(screen.getByText('CK').closest('tr')!).getByRole('button', { name: 'Sửa phương thức' }));
    expect(screen.getByRole('combobox', { name: 'Loại quỹ' })).toHaveTextContent('Ngân hàng');
    await user.click(screen.getByRole('button', { name: 'Cập nhật' }));

    await waitFor(() =>
      expect(updateMock).toHaveBeenCalledWith({ id: 'p2', data: { name: 'Chuyển khoản', fundKind: 'Bank' } }),
    );
  });
});
