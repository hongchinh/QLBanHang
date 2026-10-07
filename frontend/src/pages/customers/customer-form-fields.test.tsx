import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { CustomerFormFields, type CustomerFormFieldsProps } from './customer-form-fields';
import type { Customer } from '@/features/customers/types';

let granted = new Set<string>();
vi.mock('@/stores/auth-store', () => ({
  useAuthStore: (selector: (s: { hasPermission: (p: string) => boolean; isInRole: () => boolean }) => unknown) =>
    selector({ hasPermission: (p) => granted.has(p), isInRole: () => false }),
}));

const DUAL_ROLE: Customer = {
  id: 'c1',
  code: 'KH0001',
  name: 'Công ty Đa Vai Trò',
  group: 'Company',
  status: 'Active',
  createdAt: '2026-10-01T00:00:00Z',
  isCustomer: true,
  isSupplier: true,
};

function renderFields(props: Partial<CustomerFormFieldsProps> = {}) {
  const onSubmit = vi.fn();
  const { unmount } = render(
    <MemoryRouter>
      <CustomerFormFields
        isEdit={false}
        submitting={false}
        submitError=""
        hasSubmitError={false}
        onSubmit={onSubmit}
        onCancel={vi.fn()}
        {...props}
      />
    </MemoryRouter>,
  );
  return { onSubmit, unmount };
}

describe('CustomerFormFields roles', () => {
  beforeEach(() => {
    granted = new Set(['customers.create', 'customers.update']);
  });

  it('own role checkbox is locked checked', () => {
    renderFields();
    const own = screen.getByLabelText('Là khách hàng');
    expect(own).toBeChecked();
    expect(own).toBeDisabled();
  });

  it("other role checkbox is enabled only with that role's permission and is submitted", async () => {
    const first = renderFields();
    expect(screen.getByLabelText('Là nhà cung cấp')).toBeDisabled();
    first.unmount();

    granted.add('suppliers.create');
    const user = userEvent.setup();
    const { onSubmit } = renderFields();

    const other = screen.getByLabelText('Là nhà cung cấp');
    expect(other).toBeEnabled();
    await user.type(screen.getByLabelText('Tên khách hàng *'), 'ACME');
    await user.click(other);
    await user.click(screen.getByRole('button', { name: 'Tạo mới' }));

    await waitFor(() => expect(onSubmit).toHaveBeenCalled());
    expect(onSubmit.mock.calls[0][0]).toMatchObject({ name: 'ACME', isCustomer: true, isSupplier: true });
  });

  it('supplier role shows supplier title and back link', () => {
    granted = new Set(['suppliers.create']);
    renderFields({ role: 'supplier' });

    expect(screen.getByRole('heading', { name: 'Thêm nhà cung cấp' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Quay lại' })).toHaveAttribute('href', '/suppliers');
    expect(screen.getByLabelText('Tên nhà cung cấp *')).toBeInTheDocument();
    expect(screen.getByLabelText('Là nhà cung cấp')).toBeChecked();
    expect(screen.getByLabelText('Là nhà cung cấp')).toBeDisabled();
    expect(screen.getByLabelText('Là khách hàng')).toBeDisabled();
  });

  it('dual-role partner without suppliers.update is read-only with a notice', () => {
    renderFields({ isEdit: true, initial: DUAL_ROLE });

    expect(
      screen.getByText('Đối tượng này cũng là nhà cung cấp — cần quyền suppliers.update để sửa.'),
    ).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Cập nhật' })).toBeDisabled();
  });
});
