import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { WarehouseListPage } from './warehouse-list-page';
import type { Warehouse } from '@/features/warehouses/types';

const createMock = vi.fn();
const WAREHOUSES: Warehouse[] = [
  {
    id: 'w1',
    code: 'KHO01',
    name: 'Kho chính',
    branchId: 'b1',
    branchCode: 'CN01',
    branchName: 'Chi nhánh chính',
    isActive: true,
  },
];

vi.mock('@/features/warehouses/hooks', () => ({
  useWarehouses: () => ({ data: WAREHOUSES, isLoading: false, isError: false }),
  useCreateWarehouse: () => ({ mutateAsync: createMock, isPending: false }),
  useUpdateWarehouse: () => ({ mutateAsync: vi.fn(), isPending: false }),
  useDeleteWarehouse: () => ({ mutate: vi.fn(), isPending: false }),
}));

vi.mock('@/features/branches/hooks', () => ({
  useBranches: () => ({
    data: [
      { id: 'b1', code: 'CN01', name: 'Chi nhánh chính' },
      { id: 'b2', code: 'CN02', name: 'Chi nhánh 2' },
    ],
  }),
}));

vi.mock('@/lib/use-toast', () => ({ toast: vi.fn() }));

let granted = new Set<string>();
vi.mock('@/stores/auth-store', () => ({
  useAuthStore: (selector: (s: { hasPermission: (p: string) => boolean; isInRole: () => boolean }) => unknown) =>
    selector({ hasPermission: (p) => granted.has(p), isInRole: () => false }),
}));

function renderPage() {
  return render(
    <MemoryRouter>
      <WarehouseListPage />
    </MemoryRouter>,
  );
}

describe('WarehouseListPage', () => {
  beforeEach(() => {
    createMock.mockReset().mockResolvedValue({});
    granted = new Set(['inventory.catalogs.manage']);
  });

  it('renders warehouses and creates one through the dialog', async () => {
    const user = userEvent.setup();
    renderPage();

    expect(screen.getByText('KHO01')).toBeInTheDocument();
    expect(screen.getByText('Kho chính')).toBeInTheDocument();
    expect(screen.getByText('CN01 — Chi nhánh chính')).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: /Thêm kho/ }));
    await user.type(screen.getByLabelText('Mã kho *'), 'KHO02');
    await user.type(screen.getByLabelText('Tên kho *'), 'Kho phụ');
    await user.click(screen.getByRole('button', { name: 'Tạo mới' }));

    await waitFor(() =>
      expect(createMock).toHaveBeenCalledWith({
        code: 'KHO02',
        name: 'Kho phụ',
        branchId: undefined,
        isActive: true,
      }),
    );
  });

  it('branch filter is shown only with branches.access_all', () => {
    const { unmount } = renderPage();
    expect(screen.queryByRole('combobox', { name: 'Lọc chi nhánh' })).not.toBeInTheDocument();
    unmount();

    granted = new Set(['inventory.catalogs.manage', 'branches.access_all']);
    renderPage();
    // Without a branch the backend lists the working branch only, so the default says so.
    expect(screen.getByRole('combobox', { name: 'Lọc chi nhánh' })).toHaveTextContent('Chi nhánh làm việc');
  });
});
