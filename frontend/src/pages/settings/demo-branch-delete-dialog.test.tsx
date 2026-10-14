import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { DemoBranchDeleteDialog } from './demo-branch-delete-dialog';
import type { Branch } from '@/features/branches/types';
import { toast } from '@/lib/use-toast';

const deleteMock = vi.fn();
const previewHookMock = vi.fn();
interface PreviewState {
  data?: { documentCount: number; reassignedUserCount: number };
  isLoading: boolean;
  isFetching: boolean;
  isError: boolean;
  error?: unknown;
}
let previewState: PreviewState;
let mutation: { mutateAsync: typeof deleteMock; isPending: boolean };

const BRANCH: Branch = {
  id: 'b3',
  code: 'DEMO1',
  name: 'CN demo',
  address: null,
  lockedUntil: null,
  openingDate: null,
  isDemo: true,
  sampleDataStatus: 'Ready',
  sampleDataProgress: 100,
};

vi.mock('@/features/branches/hooks', () => ({
  useDemoBranchDeletionPreview: (id: string | null) => {
    previewHookMock(id);
    return previewState;
  },
  useDeleteDemoBranch: () => mutation,
}));
vi.mock('@/lib/use-toast', () => ({ toast: vi.fn() }));

const CODE_LABEL = 'Nhập mã chi nhánh (DEMO1) để xác nhận';

function renderDialog() {
  const onClose = vi.fn();
  const onDeleted = vi.fn();
  const user = userEvent.setup();
  const ui = (branch: Branch | null) => (
    <DemoBranchDeleteDialog branch={branch} onClose={onClose} onDeleted={onDeleted} />
  );
  const view = render(ui(BRANCH));
  return { onClose, onDeleted, user, view, ui };
}

describe('DemoBranchDeleteDialog', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    previewState = {
      data: { documentCount: 250, reassignedUserCount: 2 },
      isLoading: false,
      isFetching: false,
      isError: false,
    };
    deleteMock.mockReset().mockResolvedValue({
      reassignedUserCount: 2,
      keptSampleProductCount: 1,
      keptSamplePartnerCount: 0,
    });
    mutation = { mutateAsync: deleteMock, isPending: false };
  });

  it('shows what will be deleted', () => {
    renderDialog();

    expect(previewHookMock).toHaveBeenCalledWith('b3');
    expect(screen.getByText(/250 chứng từ/)).toBeInTheDocument();
    expect(
      screen.getByText('2 người dùng đang có chi nhánh mặc định là chi nhánh này sẽ được chuyển về Chi nhánh chính.'),
    ).toBeInTheDocument();
  });

  it('hides the reassigned users paragraph when there are none', () => {
    previewState.data = { documentCount: 250, reassignedUserCount: 0 };
    renderDialog();

    expect(screen.getByText(/250 chứng từ/)).toBeInTheDocument();
    expect(screen.queryByText(/người dùng đang có chi nhánh mặc định/)).not.toBeInTheDocument();
  });

  it('confirm needs the exact code', async () => {
    const { user } = renderDialog();
    const confirm = screen.getByRole('button', { name: 'Xóa vĩnh viễn' });
    expect(confirm).toBeDisabled();

    await user.type(screen.getByLabelText(CODE_LABEL), 'demo1');
    expect(confirm).toBeDisabled();

    await user.clear(screen.getByLabelText(CODE_LABEL));
    await user.type(screen.getByLabelText(CODE_LABEL), '  DEMO1  ');
    expect(confirm).toBeEnabled();
  });

  it('deleting calls the mutation, toasts and reports back', async () => {
    const { user, onClose, onDeleted } = renderDialog();
    await user.type(screen.getByLabelText(CODE_LABEL), 'DEMO1');
    await user.click(screen.getByRole('button', { name: 'Xóa vĩnh viễn' }));

    await waitFor(() => expect(onDeleted).toHaveBeenCalledWith('b3'));
    expect(deleteMock).toHaveBeenCalledWith('b3');
    expect(toast).toHaveBeenCalledWith({
      variant: 'success',
      title: 'Đã xóa chi nhánh demo',
      description: 'Giữ lại 1 hàng hóa và 0 đối tượng mẫu vì đang được dùng ở nơi khác.',
    });
    expect(onClose).toHaveBeenCalled();
    expect(onClose.mock.invocationCallOrder[0]).toBeLessThan(onDeleted.mock.invocationCallOrder[0]);
  });

  it('Enter in the input submits the delete', async () => {
    const { user, onDeleted } = renderDialog();
    await user.type(screen.getByLabelText(CODE_LABEL), 'DEMO1{Enter}');

    await waitFor(() => expect(onDeleted).toHaveBeenCalledWith('b3'));
    expect(deleteMock).toHaveBeenCalledWith('b3');
  });

  it('Enter with a wrong code does not delete', async () => {
    const { user } = renderDialog();
    await user.type(screen.getByLabelText(CODE_LABEL), 'nope{Enter}');

    expect(deleteMock).not.toHaveBeenCalled();
  });

  it('no description when nothing is kept', async () => {
    deleteMock.mockResolvedValue({ reassignedUserCount: 0, keptSampleProductCount: 0, keptSamplePartnerCount: 0 });
    const { user, onDeleted } = renderDialog();
    await user.type(screen.getByLabelText(CODE_LABEL), 'DEMO1');
    await user.click(screen.getByRole('button', { name: 'Xóa vĩnh viễn' }));

    await waitFor(() => expect(onDeleted).toHaveBeenCalledWith('b3'));
    expect(toast).toHaveBeenCalledWith({ variant: 'success', title: 'Đã xóa chi nhánh demo' });
  });

  it('server error toasts and keeps the dialog', async () => {
    deleteMock.mockRejectedValue({
      isAxiosError: true,
      message: 'Request failed with status code 409',
      response: {
        status: 409,
        data: {
          success: false,
          error: {
            code: 'SAMPLE_DATA_RUNNING',
            message: 'Đang tạo dữ liệu mẫu.',
            details: { branch: ['Chờ tạo xong rồi xóa.'] },
          },
        },
      },
    });
    const { user, onDeleted, onClose } = renderDialog();
    await user.type(screen.getByLabelText(CODE_LABEL), 'DEMO1');
    await user.click(screen.getByRole('button', { name: 'Xóa vĩnh viễn' }));

    await waitFor(() =>
      expect(toast).toHaveBeenCalledWith({
        variant: 'destructive',
        title: 'Không thể xóa',
        description: 'Đang tạo dữ liệu mẫu.; Chờ tạo xong rồi xóa.',
      }),
    );
    expect(screen.getByRole('dialog')).toBeInTheDocument();
    expect(onDeleted).not.toHaveBeenCalled();
    expect(onClose).not.toHaveBeenCalled();
  });

  it('loading preview', async () => {
    previewState = { isLoading: true, isFetching: true, isError: false };
    const { user } = renderDialog();
    await user.type(screen.getByLabelText(CODE_LABEL), 'DEMO1');

    expect(screen.getByText('Đang kiểm tra dữ liệu...')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Xóa vĩnh viễn' })).toBeDisabled();
  });

  it('a cached preview being refetched does not enable confirm', async () => {
    previewState.isFetching = true;
    const { user } = renderDialog();
    await user.type(screen.getByLabelText(CODE_LABEL), 'DEMO1');

    expect(screen.getByRole('button', { name: 'Xóa vĩnh viễn' })).toBeDisabled();
  });

  it('preview error', async () => {
    previewState = { isLoading: false, isFetching: false, isError: true, error: new Error('Không tải được bản xem trước') };
    const { user } = renderDialog();
    await user.type(screen.getByLabelText(CODE_LABEL), 'DEMO1');

    expect(screen.getByText('Không tải được bản xem trước')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Xóa vĩnh viễn' })).toBeDisabled();
  });

  it('a failed refetch with cached data keeps confirm disabled', async () => {
    previewState.isError = true;
    previewState.error = new Error('Mất kết nối');
    const { user } = renderDialog();
    await user.type(screen.getByLabelText(CODE_LABEL), 'DEMO1');

    expect(screen.getByRole('button', { name: 'Xóa vĩnh viễn' })).toBeDisabled();
  });

  it('while deleting, everything is locked and Escape does not close', async () => {
    const { user, onClose, view, ui } = renderDialog();
    await user.type(screen.getByLabelText(CODE_LABEL), 'DEMO1');
    mutation = { mutateAsync: deleteMock, isPending: true };
    view.rerender(ui(BRANCH));

    expect(screen.getByRole('button', { name: 'Đang xóa...' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Hủy' })).toBeDisabled();
    const input = screen.getByLabelText(CODE_LABEL);
    expect(input).toHaveAttribute('readonly');
    expect(input).not.toBeDisabled();
    input.focus();
    await user.keyboard('{Enter}');
    expect(deleteMock).not.toHaveBeenCalled();
    await user.keyboard('{Escape}');
    expect(onClose).not.toHaveBeenCalled();
  });

  it('Hủy closes the dialog', async () => {
    const { user, onClose } = renderDialog();
    await user.click(screen.getByRole('button', { name: 'Hủy' }));

    expect(onClose).toHaveBeenCalled();
  });

  it('the typed code resets when the dialog reopens', async () => {
    const { user, view, ui } = renderDialog();
    await user.type(screen.getByLabelText(CODE_LABEL), 'DEMO1');

    view.rerender(ui(null));
    view.rerender(ui(BRANCH));

    expect(screen.getByLabelText(CODE_LABEL)).toHaveValue('');
  });
});
