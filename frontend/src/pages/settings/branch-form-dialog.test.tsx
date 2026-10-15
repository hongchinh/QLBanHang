import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { BranchFormDialog } from './branch-form-dialog';
import type { Branch } from '@/features/branches/types';
import { toast } from '@/lib/use-toast';

const createMock = vi.fn();
const updateMock = vi.fn();

vi.mock('@/features/branches/hooks', () => ({
  useCreateBranch: () => ({ mutateAsync: createMock, isPending: false }),
  useUpdateBranch: () => ({ mutateAsync: updateMock, isPending: false }),
  useSetBranchOpeningDate: () => ({ mutateAsync: vi.fn(), isPending: false }),
}));

vi.mock('@/lib/use-toast', () => ({ toast: vi.fn() }));

const CHECKBOX_LABEL = 'Tạo kèm dữ liệu mẫu để đào tạo';
const BRANCH: Branch = {
  id: 'b1',
  code: 'CN01',
  name: 'Chi nhánh chính',
  address: null,
  lockedUntil: null,
  openingDate: null,
};

function renderDialog(initial?: Branch) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <QueryClientProvider client={queryClient}>
      <BranchFormDialog open onOpenChange={vi.fn()} initial={initial} />
    </QueryClientProvider>,
  );
}

describe('BranchFormDialog', () => {
  beforeEach(() => {
    createMock.mockReset().mockResolvedValue({});
    updateMock.mockReset().mockResolvedValue({});
    vi.mocked(toast).mockReset();
  });

  it('create mode shows the sample data checkbox, unchecked', () => {
    renderDialog();

    expect(screen.getByLabelText(CHECKBOX_LABEL)).not.toBeChecked();
    expect(screen.getByText(/Tạo danh mục mẫu, tồn kho, công nợ/)).toBeVisible();
  });

  it('creating with the box ticked sends withSampleData and shows the demo toast', async () => {
    const user = userEvent.setup();
    renderDialog();

    await user.type(screen.getByLabelText('Mã chi nhánh *'), 'CN09');
    await user.type(screen.getByLabelText('Tên chi nhánh *'), 'Chi nhánh demo');
    await user.click(screen.getByLabelText(CHECKBOX_LABEL));
    await user.click(screen.getByRole('button', { name: 'Tạo mới' }));

    await waitFor(() =>
      expect(createMock).toHaveBeenCalledWith({
        code: 'CN09',
        name: 'Chi nhánh demo',
        address: null,
        withSampleData: true,
      }),
    );
    expect(toast).toHaveBeenCalledWith({
      variant: 'success',
      title: 'Đã tạo chi nhánh demo',
      description: 'Dữ liệu mẫu đang được tạo, có thể mất khoảng một phút.',
    });
  });

  it('creating without the box sends withSampleData false', async () => {
    const user = userEvent.setup();
    renderDialog();

    await user.type(screen.getByLabelText('Mã chi nhánh *'), 'CN10');
    await user.type(screen.getByLabelText('Tên chi nhánh *'), 'Chi nhánh thường');
    await user.click(screen.getByRole('button', { name: 'Tạo mới' }));

    await waitFor(() =>
      expect(createMock).toHaveBeenCalledWith({
        code: 'CN10',
        name: 'Chi nhánh thường',
        address: null,
        withSampleData: false,
      }),
    );
    expect(toast).toHaveBeenCalledWith({ variant: 'success', title: 'Đã tạo chi nhánh' });
  });

  it('edit mode has no checkbox', () => {
    renderDialog(BRANCH);

    expect(screen.queryByLabelText(CHECKBOX_LABEL)).toBeNull();
  });

  it('edit mode submits without withSampleData', async () => {
    const user = userEvent.setup();
    renderDialog(BRANCH);

    const name = screen.getByLabelText('Tên chi nhánh *');
    await user.clear(name);
    await user.type(name, 'Chi nhánh đổi tên');
    await user.click(screen.getByRole('button', { name: 'Cập nhật' }));

    await waitFor(() =>
      expect(updateMock).toHaveBeenCalledWith({ id: 'b1', data: { name: 'Chi nhánh đổi tên', address: null } }),
    );
    expect(createMock).not.toHaveBeenCalled();
  });
});
