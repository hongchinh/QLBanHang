import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { BranchesPage } from './branches-page';
import type { Branch, MyBranches } from '@/features/branches/types';
import { useBranchStore } from '@/stores/branch-store';

const createMock = vi.fn();
const deleteMock = vi.fn();
const BRANCHES: Branch[] = [
  { id: 'b1', code: 'CN01', name: 'Chi nhánh chính', address: '1 Lê Lợi', lockedUntil: '2026-09-30' },
  { id: 'b2', code: 'CN02', name: 'Chi nhánh 2', address: null, lockedUntil: null },
];

vi.mock('@/features/branches/hooks', () => ({
  useBranches: () => ({ data: BRANCHES, isLoading: false, isError: false }),
  useCreateBranch: () => ({ mutateAsync: createMock, isPending: false }),
  useUpdateBranch: () => ({ mutateAsync: vi.fn(), isPending: false }),
  useDeleteBranch: () => ({ mutate: deleteMock, isPending: false }),
}));

const ME_AFTER_DELETE: MyBranches = {
  defaultBranchId: 'b1',
  workingBranchId: 'b1',
  canSwitch: true,
  branches: [BRANCHES[0]],
};
const meMock = vi.fn();
vi.mock('@/features/branches/api', () => ({ branchesApi: { me: () => meMock() } }));

vi.mock('@/lib/use-toast', () => ({ toast: vi.fn() }));

const granted = new Set(['branches.manage']);
interface AuthSlice {
  user: { id: string };
  hasPermission: (p: string) => boolean;
  isInRole: () => boolean;
}
vi.mock('@/stores/auth-store', () => ({
  useAuthStore: (selector: (s: AuthSlice) => unknown) =>
    selector({ user: { id: 'u1' }, hasPermission: (p) => granted.has(p), isInRole: () => false }),
}));

function renderPage() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const resetSpy = vi.spyOn(queryClient, 'resetQueries');
  render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter>
        <BranchesPage />
      </MemoryRouter>
    </QueryClientProvider>,
  );
  return { resetSpy };
}

describe('BranchesPage', () => {
  beforeEach(() => {
    createMock.mockReset().mockResolvedValue({});
    deleteMock.mockReset().mockImplementation((_id: string, opts: { onSuccess: () => void }) => opts.onSuccess());
    meMock.mockReset().mockResolvedValue(ME_AFTER_DELETE);
    localStorage.clear();
    useBranchStore.setState({ workingBranchId: 'b2' });
  });

  it('lists branches and creates one', async () => {
    const user = userEvent.setup();
    renderPage();

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

  it('deleting the working branch moves to the default branch and resets branch-scoped queries', async () => {
    const user = userEvent.setup();
    const { resetSpy } = renderPage();

    await user.click(screen.getAllByRole('button', { name: 'Xóa chi nhánh' })[1]);
    await user.click(screen.getByRole('button', { name: 'Xóa' }));

    expect(deleteMock).toHaveBeenCalledWith('b2', expect.anything());
    await waitFor(() => expect(useBranchStore.getState().workingBranchId).toBe('b1'));
    expect(localStorage.getItem('working_branch_u1')).toBe('b1');
    await waitFor(() => expect(resetSpy).toHaveBeenCalled());
  });

  it('deleting another branch keeps the working branch', async () => {
    const user = userEvent.setup();
    renderPage();

    await user.click(screen.getAllByRole('button', { name: 'Xóa chi nhánh' })[0]);
    await user.click(screen.getByRole('button', { name: 'Xóa' }));

    expect(deleteMock).toHaveBeenCalledWith('b1', expect.anything());
    expect(meMock).not.toHaveBeenCalled();
    expect(useBranchStore.getState().workingBranchId).toBe('b2');
  });
});
