import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { UserFormDialog } from './user-form-dialog';
import {
  useAdminUserDetail,
  useCreateAdminUser,
  useUpdateAdminUser,
} from '@/features/admin-users/hooks';
import { useBranches } from '@/features/branches/hooks';
import type { AdminUserDetail } from '@/features/admin-users/types';
import type { Branch } from '@/features/branches/types';

vi.mock('@/features/admin-users/hooks', () => ({
  useAdminUserDetail: vi.fn(),
  useCreateAdminUser: vi.fn(),
  useUpdateAdminUser: vi.fn(),
}));
vi.mock('@/features/branches/hooks', () => ({ useBranches: vi.fn() }));
vi.mock('@/lib/use-toast', () => ({ toast: vi.fn() }));

const CN01: Branch = { id: '6f1f7c1e-6a4b-4b53-9a3e-0c0b5a000001', code: 'CN01', name: 'Chi nhánh chính' };
const CN02: Branch = { id: '6f0e7a52-3c4d-4b8e-9a1f-2d3c4b5a6e7f', code: 'CN02', name: 'Chi nhánh 2' };

const createMock = vi.fn();
const updateMock = vi.fn();

function mockBranches(data: Branch[] | undefined) {
  vi.mocked(useBranches).mockReturnValue({ data } as ReturnType<typeof useBranches>);
}

const DETAIL: AdminUserDetail = {
  id: 'user-1',
  username: 'kho01',
  email: 'kho01@example.com',
  fullName: 'Thủ kho 1',
  phoneNumber: null,
  roleCode: 'WAREHOUSE',
  status: 'Active',
  isDeleted: false,
  lastLoginAt: null,
  createdAt: '2026-10-01T00:00:00Z',
  updatedAt: null,
  defaultBranchId: CN02.id,
  defaultBranchName: CN02.name,
};

async function fillCreateForm(user: ReturnType<typeof userEvent.setup>) {
  const inputs = screen.getAllByRole('textbox');
  // Username, Họ tên, Email, Số điện thoại (in form order).
  await user.type(inputs[0], 'kho02');
  await user.type(inputs[1], 'Thủ kho 2');
  await user.type(inputs[2], 'kho02@example.com');
  const password = document.querySelector('input[type="password"]') as HTMLInputElement;
  await user.type(password, 'Matkhau123');
}

describe('UserFormDialog default branch', () => {
  beforeEach(() => {
    createMock.mockReset().mockResolvedValue({});
    updateMock.mockReset().mockResolvedValue({});
    vi.mocked(useCreateAdminUser).mockReturnValue({
      mutateAsync: createMock,
      isPending: false,
    } as unknown as ReturnType<typeof useCreateAdminUser>);
    vi.mocked(useUpdateAdminUser).mockReturnValue({
      mutateAsync: updateMock,
      isPending: false,
    } as unknown as ReturnType<typeof useUpdateAdminUser>);
    vi.mocked(useAdminUserDetail).mockReturnValue({
      data: DETAIL,
      isLoading: false,
    } as ReturnType<typeof useAdminUserDetail>);
  });

  it('create form branch select is optional and omitted means the main branch', async () => {
    mockBranches(undefined);
    const user = userEvent.setup();
    const { rerender } = render(<UserFormDialog mode="create" open onOpenChange={vi.fn()} />);

    mockBranches([CN01, CN02]);
    rerender(<UserFormDialog mode="create" open onOpenChange={vi.fn()} />);

    expect(screen.getByRole('combobox', { name: 'Chi nhánh mặc định' })).toBeInTheDocument();
    await fillCreateForm(user);
    await user.click(screen.getByRole('button', { name: 'Tạo' }));

    await waitFor(() => expect(createMock).toHaveBeenCalledTimes(1));
    expect(createMock.mock.calls[0][0]).toMatchObject({ username: 'kho02' });
    expect(createMock.mock.calls[0][0]).not.toHaveProperty('defaultBranchId');
  });

  it('submitting create with a chosen branch sends defaultBranchId', async () => {
    mockBranches([CN01, CN02]);
    const user = userEvent.setup();
    render(<UserFormDialog mode="create" open onOpenChange={vi.fn()} />);

    await fillCreateForm(user);
    await user.click(screen.getByRole('combobox', { name: 'Chi nhánh mặc định' }));
    await user.click(await screen.findByRole('option', { name: 'CN02 — Chi nhánh 2' }));
    await user.click(screen.getByRole('button', { name: 'Tạo' }));

    await waitFor(() => expect(createMock).toHaveBeenCalledTimes(1));
    expect(createMock.mock.calls[0][0]).toMatchObject({ defaultBranchId: CN02.id });
  });

  it("edit form preselects the user's default branch and sends changes", async () => {
    mockBranches([CN01, CN02]);
    const user = userEvent.setup();
    render(<UserFormDialog mode="edit" userId="user-1" open onOpenChange={vi.fn()} />);

    const trigger = await screen.findByRole('combobox', { name: 'Chi nhánh mặc định' });
    await waitFor(() => expect(trigger).toHaveTextContent('CN02 — Chi nhánh 2'));

    await user.click(trigger);
    await user.click(await screen.findByRole('option', { name: 'CN01 — Chi nhánh chính' }));
    await user.click(screen.getByRole('button', { name: 'Lưu' }));

    await waitFor(() => expect(updateMock).toHaveBeenCalledTimes(1));
    expect(updateMock.mock.calls[0][0]).toMatchObject({
      fullName: 'Thủ kho 1',
      defaultBranchId: CN01.id,
    });
  });
});
