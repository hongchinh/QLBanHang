import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { DemoBranchDeleteDialog } from './demo-branch-delete-dialog';
import type { Branch } from '@/features/branches/types';
import { toast } from '@/lib/use-toast';

const deleteMock = vi.fn();
let previewState: { data?: { documentCount: number; reassignedUserCount: number }; isLoading: boolean; isError: boolean; error?: unknown };

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
  useDemoBranchDeletionPreview: () => previewState,
  useDeleteDemoBranch: () => ({ mutateAsync: deleteMock, isPending: false }),
}));
vi.mock('@/lib/use-toast', () => ({ toast: vi.fn() }));

function renderDialog() {
  const onClose = vi.fn();
  const onDeleted = vi.fn();
  render(<DemoBranchDeleteDialog branch={BRANCH} onClose={onClose} onDeleted={onDeleted} />);
  return { onClose, onDeleted };
}

async function typeCode(value: string) {
  await userEvent.setup().type(screen.getByLabelText('Nhập mã chi nhánh (DEMO1) để xác nhận'), value);
}

describe('DemoBranchDeleteDialog', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    previewState = { data: { documentCount: 250, reassignedUserCount: 2 }, isLoading: false, isError: false };
    deleteMock.mockReset().mockResolvedValue({
      reassignedUserCount: 2,
      keptSampleProductCount: 1,
      keptSamplePartnerCount: 0,
    });
  });

  it('shows what will be deleted', () => {
    renderDialog();

    expect(screen.getByText(/250 chứng từ/)).toBeInTheDocument();
    expect(
      screen.getByText('2 người dùng đang có chi nhánh mặc định là chi nhánh này sẽ được chuyển về Chi nhánh chính.'),
    ).toBeInTheDocument();
  });

  it('confirm needs the exact code', async () => {
    renderDialog();
    const confirm = screen.getByRole('button', { name: 'Xóa vĩnh viễn' });
    expect(confirm).toBeDisabled();

    await typeCode('demo1');
    expect(confirm).toBeDisabled();

    await userEvent.setup().clear(screen.getByLabelText(/Nhập mã chi nhánh/));
    await typeCode('  DEMO1  ');
    expect(confirm).toBeEnabled();
  });

  it('deleting calls the mutation, toasts and reports back', async () => {
    const { onClose, onDeleted } = renderDialog();
    await typeCode('DEMO1');
    await userEvent.setup().click(screen.getByRole('button', { name: 'Xóa vĩnh viễn' }));

    await waitFor(() => expect(onDeleted).toHaveBeenCalledWith('b3'));
    expect(deleteMock).toHaveBeenCalledWith('b3');
    expect(toast).toHaveBeenCalledWith({
      variant: 'success',
      title: 'Đã xóa chi nhánh demo',
      description: 'Giữ lại 1 hàng hóa và 0 đối tượng mẫu vì đang được dùng ở nơi khác.',
    });
    expect(onClose).toHaveBeenCalled();
  });

  it('no description when nothing is kept', async () => {
    deleteMock.mockResolvedValue({ reassignedUserCount: 0, keptSampleProductCount: 0, keptSamplePartnerCount: 0 });
    const { onDeleted } = renderDialog();
    await typeCode('DEMO1');
    await userEvent.setup().click(screen.getByRole('button', { name: 'Xóa vĩnh viễn' }));

    await waitFor(() => expect(onDeleted).toHaveBeenCalledWith('b3'));
    expect(toast).toHaveBeenCalledWith({ variant: 'success', title: 'Đã xóa chi nhánh demo' });
  });

  it('server error toasts and keeps the dialog', async () => {
    const message = 'Đang tạo dữ liệu mẫu, chưa thể xóa.';
    deleteMock.mockRejectedValue({
      isAxiosError: true,
      message: 'Request failed with status code 409',
      response: { status: 409, data: { success: false, error: { code: 'SAMPLE_DATA_RUNNING', message } } },
    });
    const { onDeleted, onClose } = renderDialog();
    await typeCode('DEMO1');
    await userEvent.setup().click(screen.getByRole('button', { name: 'Xóa vĩnh viễn' }));

    await waitFor(() =>
      expect(toast).toHaveBeenCalledWith(expect.objectContaining({ variant: 'destructive', title: 'Không thể xóa' })),
    );
    expect(screen.getByRole('dialog')).toBeInTheDocument();
    expect(onDeleted).not.toHaveBeenCalled();
    expect(onClose).not.toHaveBeenCalled();
  });

  it('loading preview', async () => {
    previewState = { isLoading: true, isError: false };
    renderDialog();
    await typeCode('DEMO1');

    expect(screen.getByText('Đang kiểm tra dữ liệu...')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Xóa vĩnh viễn' })).toBeDisabled();
  });

  it('preview error', async () => {
    previewState = { isLoading: false, isError: true, error: new Error('Không tải được bản xem trước') };
    renderDialog();
    await typeCode('DEMO1');

    expect(screen.getByText('Không tải được bản xem trước')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Xóa vĩnh viễn' })).toBeDisabled();
  });
});
