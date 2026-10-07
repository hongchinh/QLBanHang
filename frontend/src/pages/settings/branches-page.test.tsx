import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { BranchesPage } from './branches-page';
import type { Branch } from '@/features/branches/types';

const createMock = vi.fn();
const BRANCHES: Branch[] = [
  { id: 'b1', code: 'CN01', name: 'Chi nhánh chính', address: '1 Lê Lợi', lockedUntil: '2026-09-30' },
];

vi.mock('@/features/branches/hooks', () => ({
  useBranches: () => ({ data: BRANCHES, isLoading: false, isError: false }),
  useCreateBranch: () => ({ mutateAsync: createMock, isPending: false }),
  useUpdateBranch: () => ({ mutateAsync: vi.fn(), isPending: false }),
  useDeleteBranch: () => ({ mutate: vi.fn(), isPending: false }),
}));

vi.mock('@/lib/use-toast', () => ({ toast: vi.fn() }));

const granted = new Set(['branches.manage']);
vi.mock('@/stores/auth-store', () => ({
  useAuthStore: (selector: (s: { hasPermission: (p: string) => boolean; isInRole: () => boolean }) => unknown) =>
    selector({ hasPermission: (p) => granted.has(p), isInRole: () => false }),
}));

describe('BranchesPage', () => {
  beforeEach(() => {
    createMock.mockReset().mockResolvedValue({});
  });

  it('lists branches and creates one', async () => {
    const user = userEvent.setup();
    render(
      <MemoryRouter>
        <BranchesPage />
      </MemoryRouter>,
    );

    expect(screen.getByText('CN01')).toBeInTheDocument();
    expect(screen.getByText('Chi nhánh chính')).toBeInTheDocument();
    expect(screen.getByText('1 Lê Lợi')).toBeInTheDocument();
    expect(screen.getByText('30/09/2026')).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: /Thêm chi nhánh/ }));
    await user.type(screen.getByLabelText('Mã chi nhánh *'), 'CN02');
    await user.type(screen.getByLabelText('Tên chi nhánh *'), 'Chi nhánh 2');
    await user.type(screen.getByLabelText('Địa chỉ'), '2 Hai Bà Trưng');
    await user.click(screen.getByRole('button', { name: 'Tạo mới' }));

    await waitFor(() =>
      expect(createMock).toHaveBeenCalledWith({
        code: 'CN02',
        name: 'Chi nhánh 2',
        address: '2 Hai Bà Trưng',
      }),
    );
  });
});
