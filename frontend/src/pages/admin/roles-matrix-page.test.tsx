import { beforeEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { RolesMatrixPage } from './roles-matrix-page';
import type { PermissionDto, RoleDetail, RoleListItem } from '@/features/admin-roles/types';

const listMock = vi.fn();
const listPermissionsMock = vi.fn();
const getDetailMock = vi.fn();
const updatePermissionsMock = vi.fn();
const createMock = vi.fn();

vi.mock('@/features/admin-roles/api', () => ({
  adminRolesApi: {
    list: (...args: unknown[]) => listMock(...args),
    listPermissions: (...args: unknown[]) => listPermissionsMock(...args),
    getDetail: (...args: unknown[]) => getDetailMock(...args),
    create: (...args: unknown[]) => createMock(...args),
    updatePermissions: (...args: unknown[]) => updatePermissionsMock(...args),
    update: vi.fn(),
    remove: vi.fn(),
  },
}));

const hasPermissionMock = vi.fn();
const isInRoleMock = vi.fn();
vi.mock('@/stores/auth-store', () => ({
  useAuthStore: (selector: (s: { hasPermission: typeof hasPermissionMock; isInRole: typeof isInRoleMock }) => unknown) =>
    selector({ hasPermission: hasPermissionMock, isInRole: isInRoleMock }),
}));

vi.mock('@/lib/use-toast', () => ({ toast: vi.fn() }));

const PERMISSIONS: PermissionDto[] = [
  { code: 'quotations.view', name: 'Xem báo giá', module: 'sales' },
  { code: 'quotations.delete', name: 'Xoá báo giá', module: 'sales' },
  { code: 'customers.view', name: 'Xem khách hàng', module: 'catalog' },
  { code: 'customers.create', name: 'Tạo khách hàng', module: 'catalog' },
  { code: 'roles.view', name: 'Xem vai trò', module: 'system' },
  { code: 'roles.manage', name: 'Quản lý vai trò', module: 'system' },
];

const ROLES: RoleListItem[] = [
  { id: 'r-admin', code: 'ADMIN', name: 'Quản trị', isSystem: true, permissionCount: 6, userCount: 1 },
  { id: 'r-sales', code: 'SALES', name: 'Kinh doanh', isSystem: true, permissionCount: 2, userCount: 3 },
  { id: 'r-manager', code: 'MANAGER', name: 'Quản lý', isSystem: true, permissionCount: 6, userCount: 1 },
  { id: 'r-custom', code: 'TEST_LEAD', name: 'Trưởng nhóm', isSystem: false, permissionCount: 1, userCount: 0 },
];

const DETAILS: Record<string, RoleDetail> = {
  'r-admin': {
    id: 'r-admin',
    code: 'ADMIN',
    name: 'Quản trị',
    isSystem: true,
    permissionCodes: PERMISSIONS.map((p) => p.code),
    userCount: 1,
    createdAt: '2026-01-01T00:00:00Z',
  },
  'r-sales': {
    id: 'r-sales',
    code: 'SALES',
    name: 'Kinh doanh',
    isSystem: true,
    permissionCodes: ['quotations.view', 'customers.view'],
    userCount: 3,
    createdAt: '2026-01-01T00:00:00Z',
  },
  'r-manager': {
    id: 'r-manager',
    code: 'MANAGER',
    name: 'Quản lý',
    isSystem: true,
    permissionCodes: PERMISSIONS.map((p) => p.code),
    userCount: 1,
    createdAt: '2026-01-01T00:00:00Z',
  },
  'r-custom': {
    id: 'r-custom',
    code: 'TEST_LEAD',
    name: 'Trưởng nhóm',
    isSystem: false,
    permissionCodes: ['quotations.view'],
    userCount: 0,
    createdAt: '2026-01-01T00:00:00Z',
  },
};

function renderPage() {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false, gcTime: 0 } },
  });
  return render(
    <QueryClientProvider client={client}>
      <RolesMatrixPage />
    </QueryClientProvider>,
  );
}

beforeEach(() => {
  listMock.mockReset();
  listPermissionsMock.mockReset();
  getDetailMock.mockReset();
  updatePermissionsMock.mockReset();
  createMock.mockReset();
  hasPermissionMock.mockReset();
  isInRoleMock.mockReset();

  listMock.mockResolvedValue(ROLES);
  listPermissionsMock.mockResolvedValue(PERMISSIONS);
  getDetailMock.mockImplementation(async (id: string) => DETAILS[id]);
  hasPermissionMock.mockReturnValue(true);
  isInRoleMock.mockReturnValue(false);
});

async function waitForMatrix() {
  // Wait until detail data has populated state — SALES.quotations.view should be checked.
  await waitFor(
    () => {
      const box = screen.getByRole('checkbox', { name: /SALES quotations\.view/i }) as HTMLInputElement;
      expect(box.checked).toBe(true);
    },
    { timeout: 5000 },
  );
}

describe('RolesMatrixPage', () => {
  it('renders matrix and disables ADMIN column checkboxes', async () => {
    renderPage();
    await waitForMatrix();

    const adminBox = screen.getByRole('checkbox', { name: /ADMIN quotations\.view/i }) as HTMLInputElement;
    expect(adminBox.checked).toBe(true);
    expect(adminBox.disabled).toBe(true);
  });

  it('toggling SALES.quotations.delete enables Save and shows dirty badge', async () => {
    renderPage();
    await waitForMatrix();

    const box = screen.getByRole('checkbox', { name: /SALES quotations\.delete/i }) as HTMLInputElement;
    expect(box.checked).toBe(false);
    fireEvent.click(box);

    await waitFor(() =>
      expect(screen.getByText(/1 thay đổi chưa lưu/i)).toBeInTheDocument(),
    );
    expect(screen.getByRole('button', { name: /Lưu thay đổi/i })).toBeEnabled();
  });

  it('saves only dirty role via updatePermissions and clears dirty state', async () => {
    updatePermissionsMock.mockResolvedValue(DETAILS['r-sales']);
    renderPage();
    await waitForMatrix();

    fireEvent.click(screen.getByRole('checkbox', { name: /SALES quotations\.delete/i }));
    fireEvent.click(screen.getByRole('button', { name: /Lưu thay đổi/i }));

    await waitFor(() => expect(updatePermissionsMock).toHaveBeenCalledTimes(1));
    expect(updatePermissionsMock).toHaveBeenCalledWith('r-sales', {
      permissionCodes: expect.arrayContaining(['quotations.view', 'customers.view', 'quotations.delete']),
    });
    // Custom role (r-custom) wasn't toggled → must not be called.
    expect(updatePermissionsMock.mock.calls.find((c) => c[0] === 'r-custom')).toBeUndefined();

    await waitFor(() => expect(screen.queryByText(/thay đổi chưa lưu/i)).not.toBeInTheDocument());
  });

  it('partial failure keeps dirty for rejected role', async () => {
    // SALES succeeds, custom role fails.
    updatePermissionsMock.mockImplementation((roleId: string) => {
      if (roleId === 'r-sales') return Promise.resolve(DETAILS['r-sales']);
      return Promise.reject(new Error('boom'));
    });
    renderPage();
    await waitForMatrix();

    fireEvent.click(screen.getByRole('checkbox', { name: /SALES quotations\.delete/i }));
    fireEvent.click(screen.getByRole('checkbox', { name: /TEST_LEAD customers\.view/i }));

    expect(screen.getByText(/2 thay đổi chưa lưu/i)).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: /Lưu thay đổi/i }));

    await waitFor(() => expect(updatePermissionsMock).toHaveBeenCalledTimes(2));
    // Only TEST_LEAD remains dirty.
    await waitFor(() =>
      expect(screen.getByText(/1 thay đổi chưa lưu/i)).toBeInTheDocument(),
    );
  });

  it('ADMIN checkbox cannot be toggled (disabled)', async () => {
    renderPage();
    await waitForMatrix();

    const adminBox = screen.getByRole('checkbox', { name: /ADMIN customers\.view/i }) as HTMLInputElement;
    expect(adminBox.disabled).toBe(true);
    // Clicking a disabled input doesn't change state and no dirty badge should appear.
    fireEvent.click(adminBox);
    expect(screen.queryByText(/thay đổi chưa lưu/i)).not.toBeInTheDocument();
  });

  it('self-lockout opens ConfirmDialog when removing roles.manage from own role', async () => {
    isInRoleMock.mockImplementation((code: string) => code === 'MANAGER');
    updatePermissionsMock.mockResolvedValue(DETAILS['r-manager']);
    renderPage();
    await waitForMatrix();

    // Untick MANAGER → roles.manage.
    const box = screen.getByRole('checkbox', { name: /MANAGER roles\.manage/i }) as HTMLInputElement;
    expect(box.checked).toBe(true);
    fireEvent.click(box);

    fireEvent.click(screen.getByRole('button', { name: /Lưu thay đổi/i }));

    await waitFor(() =>
      expect(screen.getByText(/Bạn sắp bỏ quyền quản lý vai trò khỏi role của chính mình/i)).toBeInTheDocument(),
    );
    // No request yet — waiting on confirm.
    expect(updatePermissionsMock).not.toHaveBeenCalled();

    fireEvent.click(screen.getByRole('button', { name: /Tiếp tục lưu/i }));
    await waitFor(() => expect(updatePermissionsMock).toHaveBeenCalledTimes(1));
  });

  it('renders the Kho module and toggles stock_in.view', async () => {
    listPermissionsMock.mockResolvedValue([
      ...PERMISSIONS,
      { code: 'stock_in.view', name: 'Xem phiếu nhập kho', module: 'inventory' },
    ]);
    renderPage();
    await waitForMatrix();

    expect(screen.getByText('Kho')).toBeInTheDocument();
    const box = screen.getByRole('checkbox', { name: /SALES stock_in\.view/i }) as HTMLInputElement;
    expect(box.checked).toBe(false);

    fireEvent.click(box);

    expect(box.checked).toBe(true);
    await waitFor(() =>
      expect(screen.getByText(/1 thay đổi chưa lưu/i)).toBeInTheDocument(),
    );
  });

  it('renders an unknown module at the end with its raw name', async () => {
    listPermissionsMock.mockResolvedValue([
      ...PERMISSIONS,
      { code: 'cash.view', name: 'Xem phiếu thu chi', module: 'cash' },
    ]);
    renderPage();
    await waitForMatrix();

    expect(screen.getByText('cash')).toBeInTheDocument();
    expect(screen.getByRole('checkbox', { name: /SALES cash\.view/i })).toBeInTheDocument();
  });

  it('opens create dialog and submits POST', async () => {
    createMock.mockResolvedValue({
      ...DETAILS['r-custom'],
      id: 'r-new',
      code: 'NEW_ROLE',
      name: 'Role mới',
    });
    renderPage();
    await waitForMatrix();

    fireEvent.click(screen.getByRole('button', { name: /Thêm role/i }));
    const dialog = await screen.findByRole('dialog');
    fireEvent.change(within(dialog).getByLabelText(/Mã vai trò/i), {
      target: { value: 'NEW_ROLE' },
    });
    fireEvent.change(within(dialog).getByLabelText(/Tên hiển thị/i), {
      target: { value: 'Role mới' },
    });
    fireEvent.click(within(dialog).getByRole('button', { name: /Tạo vai trò/i }));

    await waitFor(() => expect(createMock).toHaveBeenCalledTimes(1));
    expect(createMock).toHaveBeenCalledWith(
      expect.objectContaining({ code: 'NEW_ROLE', name: 'Role mới' }),
    );
  });
});
