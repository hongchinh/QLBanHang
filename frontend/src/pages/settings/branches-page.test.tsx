import { beforeEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { BranchesPage } from './branches-page';
import type { Branch, MyBranches } from '@/features/branches/types';
import { useBranchStore } from '@/stores/branch-store';
import { toast } from '@/lib/use-toast';

const createMock = vi.fn();
const deleteMock = vi.fn();
const setOpeningDateMock = vi.fn();
const BRANCHES: Branch[] = [
  {
    id: 'b1',
    code: 'CN01',
    name: 'Chi nhánh chính',
    address: '1 Lê Lợi',
    lockedUntil: '2026-09-30',
    openingDate: '2026-01-01',
  },
  { id: 'b2', code: 'CN02', name: 'Chi nhánh 2', address: null, lockedUntil: null, openingDate: null },
  {
    id: 'b3',
    code: 'DEMO1',
    name: 'CN demo',
    address: null,
    lockedUntil: null,
    openingDate: null,
    isDemo: true,
    sampleDataStatus: 'Ready',
    sampleDataProgress: 100,
  },
  {
    id: 'b4',
    code: 'DEMO2',
    name: 'CN demo 2',
    address: null,
    lockedUntil: null,
    openingDate: null,
    isDemo: true,
    sampleDataStatus: 'Running',
    sampleDataProgress: 45,
  },
  {
    id: 'b5',
    code: 'DEMO3',
    name: 'CN demo 3',
    address: null,
    lockedUntil: null,
    openingDate: null,
    isDemo: true,
    sampleDataStatus: 'Failed',
    sampleDataError: 'XBH-0012: lỗi',
  },
];

vi.mock('@/features/branches/hooks', () => ({
  useBranches: () => ({ data: BRANCHES, isLoading: false, isError: false }),
  useCreateBranch: () => ({ mutateAsync: createMock, isPending: false }),
  useUpdateBranch: () => ({ mutateAsync: vi.fn(), isPending: false }),
  useDeleteBranch: () => ({ mutate: deleteMock, isPending: false }),
  useDemoBranchDeletionPreview: () => ({ data: undefined, isLoading: false, isError: false }),
  useDeleteDemoBranch: () => ({ mutate: vi.fn(), isPending: false }),
  useSetBranchOpeningDate: () => ({ mutateAsync: setOpeningDateMock, isPending: false }),
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
    setOpeningDateMock.mockReset().mockResolvedValue(BRANCHES[1]);
    vi.mocked(toast).mockReset();
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
        withSampleData: false,
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

  it('sets the opening date', async () => {
    const user = userEvent.setup();
    renderPage();

    expect(screen.getByRole('columnheader', { name: 'Ngày đầu kỳ' })).toBeInTheDocument();
    expect(screen.getByText('01/01/2026')).toBeInTheDocument();

    await user.click(screen.getAllByRole('button', { name: 'Đặt ngày đầu kỳ' })[1]);
    const dialog = await screen.findByRole('dialog');
    const input = within(dialog).getByLabelText('Ngày đầu kỳ');
    expect(input).toHaveValue('');
    fireEvent.change(input, { target: { value: '2026-10-01' } });
    await user.click(within(dialog).getByRole('button', { name: 'Lưu' }));

    await waitFor(() => expect(setOpeningDateMock).toHaveBeenCalledWith({ id: 'b2', openingDate: '2026-10-01' }));
    expect(toast).toHaveBeenCalledWith(expect.objectContaining({ variant: 'success' }));
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
  });

  it('shows the conflict message', async () => {
    const message = 'Chi nhánh đã có chứng từ, không đổi được ngày đầu kỳ.';
    setOpeningDateMock.mockReset().mockRejectedValue({
      isAxiosError: true,
      message: 'Request failed with status code 409',
      response: { status: 409, data: { success: false, error: { code: 'BRANCH_HAS_DOCUMENTS', message } } },
    });
    const user = userEvent.setup();
    renderPage();

    await user.click(screen.getAllByRole('button', { name: 'Đặt ngày đầu kỳ' })[0]);
    const dialog = await screen.findByRole('dialog');
    expect(within(dialog).getByLabelText('Ngày đầu kỳ')).toHaveValue('2026-01-01');
    fireEvent.change(within(dialog).getByLabelText('Ngày đầu kỳ'), { target: { value: '2026-02-01' } });
    await user.click(within(dialog).getByRole('button', { name: 'Lưu' }));

    await waitFor(() =>
      expect(toast).toHaveBeenCalledWith(expect.objectContaining({ variant: 'destructive', description: message })),
    );
    expect(screen.getByRole('dialog')).toBeInTheDocument();
  });

  const rowOf = (code: string) => screen.getByText(code).closest('tr') as HTMLElement;

  it('shows the demo badges', () => {
    renderPage();

    expect(within(rowOf('DEMO1')).getByText('Demo')).toBeInTheDocument();
    expect(within(rowOf('DEMO2')).getByText('Đang tạo dữ liệu 45%')).toBeInTheDocument();
    expect(within(rowOf('DEMO3')).getByText('Lỗi tạo dữ liệu')).toHaveAttribute('title', 'XBH-0012: lỗi');
    expect(within(rowOf('CN01')).queryByText('Demo')).not.toBeInTheDocument();
  });

  it('demo rows use the demo delete action', async () => {
    const user = userEvent.setup();
    renderPage();

    const row = within(rowOf('DEMO1'));
    expect(row.queryByRole('button', { name: 'Xóa chi nhánh' })).not.toBeInTheDocument();
    await user.click(row.getByRole('button', { name: 'Xóa chi nhánh demo' }));
    expect(await screen.findByRole('dialog', { name: 'Xóa chi nhánh demo?' })).toBeInTheDocument();
  });

  it('demo delete is disabled while seeding', () => {
    renderPage();

    expect(within(rowOf('DEMO2')).getByRole('button', { name: 'Xóa chi nhánh demo' })).toBeDisabled();
  });

  it('normal rows keep the normal delete', async () => {
    const user = userEvent.setup();
    renderPage();

    const row = within(rowOf('CN01'));
    expect(row.queryByRole('button', { name: 'Xóa chi nhánh demo' })).not.toBeInTheDocument();
    await user.click(row.getByRole('button', { name: 'Xóa chi nhánh' }));
    expect(await screen.findByRole('dialog', { name: 'Xóa chi nhánh?' })).toBeInTheDocument();
  });
});
