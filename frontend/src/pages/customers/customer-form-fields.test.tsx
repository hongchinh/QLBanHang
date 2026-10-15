import { beforeEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
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

describe('CustomerFormFields credit days', () => {
  beforeEach(() => {
    granted = new Set(['customers.create', 'customers.update', 'suppliers.create', 'suppliers.update']);
  });

  async function renderCreditDays() {
    const user = userEvent.setup();
    const { onSubmit } = renderFields();
    await user.type(screen.getByLabelText('Tên khách hàng *'), 'A');
    const input = screen.getByLabelText('Số ngày được nợ');
    const submitWith = async (typed: string) => {
      onSubmit.mockClear();
      await user.clear(input);
      if (typed) await user.type(input, typed);
      await user.click(screen.getByRole('button', { name: 'Tạo mới' }));
    };
    return { onSubmit, submitWith };
  }

  it('credit days accepts 0..3650', async () => {
    const { onSubmit, submitWith } = await renderCreditDays();

    for (const [typed, expected] of [
      ['0', 0],
      ['3650', 3650],
      ['', undefined],
    ] as const) {
      await submitWith(typed);
      await waitFor(() => expect(onSubmit).toHaveBeenCalledTimes(1));
      expect(onSubmit.mock.calls[0][0].creditDays).toBe(expected);
    }
    expect(screen.queryByTestId('creditDays-error')).not.toBeInTheDocument();
  });

  it('credit days rejects values outside 0..3650 and fractions', async () => {
    const { onSubmit, submitWith } = await renderCreditDays();

    for (const typed of ['-1', '3651']) {
      await submitWith(typed);
      expect(await screen.findByTestId('creditDays-error')).toBeInTheDocument();
      expect(onSubmit).not.toHaveBeenCalled();
    }

    // A fraction is stopped by the number input's own step check (and by the schema, see schema.test.ts).
    await submitWith('1.5');
    expect(onSubmit).not.toHaveBeenCalled();
  });

  it('supplier form submits credit days', async () => {
    const user = userEvent.setup();
    const { onSubmit } = renderFields({ role: 'supplier' });
    await user.type(screen.getByLabelText('Tên nhà cung cấp *'), 'ACME');
    await user.type(screen.getByLabelText('Số ngày được nợ'), '45');
    await user.click(screen.getByRole('button', { name: 'Tạo mới' }));

    await waitFor(() => expect(onSubmit).toHaveBeenCalled());
    expect(onSubmit.mock.calls[0][0]).toMatchObject({ name: 'ACME', isSupplier: true, creditDays: 45 });
  });

  it('edit starts from the stored credit days', () => {
    renderFields({ isEdit: true, initial: { ...DUAL_ROLE, creditDays: 30 } });
    expect(screen.getByLabelText('Số ngày được nợ')).toHaveValue(30);
  });
});

describe('CustomerFormFields quick add mode', () => {
  const FREE = { rolesMode: 'free' as const, initialRoles: { isCustomer: false, isSupplier: true } };

  beforeEach(() => {
    granted = new Set(['customers.create', 'suppliers.create']);
  });

  it('initialName fills the name input', () => {
    renderFields({ ...FREE, initialName: 'Công ty An Phát' });
    expect(screen.getByLabelText('Tên *')).toHaveValue('Công ty An Phát');
  });

  it('free mode renders both role checkboxes from initialRoles', () => {
    renderFields(FREE);
    expect(screen.getByLabelText('Là nhà cung cấp')).toBeChecked();
    expect(screen.getByLabelText('Là khách hàng')).not.toBeChecked();
    expect(screen.getByLabelText('Là nhà cung cấp')).toBeEnabled();
    expect(screen.getByLabelText('Là khách hàng')).toBeEnabled();
  });

  it('free mode disables a role without its create permission', () => {
    granted = new Set(['customers.create']);
    const first = renderFields(FREE);
    expect(screen.getByLabelText('Là nhà cung cấp')).toBeDisabled();
    expect(screen.getByLabelText('Là khách hàng')).toBeEnabled();
    first.unmount();

    granted = new Set();
    renderFields(FREE);
    expect(screen.getByLabelText('Là nhà cung cấp')).toBeDisabled();
    expect(screen.getByLabelText('Là khách hàng')).toBeDisabled();
  });

  it('free mode requires one role and submits both when both are checked', async () => {
    const user = userEvent.setup();
    const { onSubmit } = renderFields(FREE);
    await user.type(screen.getByLabelText('Tên *'), 'ACME');

    await user.click(screen.getByLabelText('Là nhà cung cấp'));
    await user.click(screen.getByRole('button', { name: 'Tạo mới' }));
    expect(await screen.findByText('Chọn ít nhất một vai trò')).toBeInTheDocument();
    expect(onSubmit).not.toHaveBeenCalled();

    await user.click(screen.getByLabelText('Là nhà cung cấp'));
    await user.click(screen.getByLabelText('Là khách hàng'));
    await user.click(screen.getByRole('button', { name: 'Tạo mới' }));
    await waitFor(() => expect(onSubmit).toHaveBeenCalledTimes(1));
    expect(onSubmit.mock.calls[0][0]).toMatchObject({ name: 'ACME', isCustomer: true, isSupplier: true });
  });

  it('free mode uses generic labels and the generic empty-name message', async () => {
    const user = userEvent.setup();
    renderFields(FREE);
    expect(screen.getByLabelText('Mã')).toBeInTheDocument();
    expect(screen.getByLabelText('Tên *')).toBeInTheDocument();
    expect(screen.getByLabelText('Nhóm')).toBeInTheDocument();
    expect(screen.queryByLabelText('Mã khách hàng')).not.toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'Tạo mới' }));
    expect(await screen.findByText('Tên là bắt buộc')).toBeInTheDocument();
  });

  it('free mode without the role box keeps the initial roles', async () => {
    const user = userEvent.setup();
    const { onSubmit } = renderFields({
      rolesMode: 'free',
      showRoles: false,
      initialRoles: { isCustomer: true, isSupplier: false },
    });
    expect(screen.queryByLabelText('Là khách hàng')).not.toBeInTheDocument();
    expect(screen.queryByLabelText('Là nhà cung cấp')).not.toBeInTheDocument();
    expect(screen.getByLabelText('Mã')).toBeInTheDocument();
    expect(screen.getByLabelText('Tên *')).toBeInTheDocument();

    await user.type(screen.getByLabelText('Tên *'), 'ACME');
    await user.click(screen.getByRole('button', { name: 'Tạo mới' }));
    await waitFor(() => expect(onSubmit).toHaveBeenCalledTimes(1));
    expect(onSubmit.mock.calls[0][0]).toMatchObject({ isCustomer: true, isSupplier: false });
  });

  it('onSubmit is not called while submitting', async () => {
    const user = userEvent.setup();
    const { onSubmit } = renderFields({ ...FREE, submitting: true, enterNavigation: true, initialName: 'A' });
    screen.getByLabelText('Mã').focus();
    await user.keyboard('{Control>}{Enter}{/Control}');
    // The role boxes are the last fields in this mode: Enter on the last one is the "submit" step.
    screen.getByLabelText('Là nhà cung cấp').focus();
    await user.keyboard('{Enter}');
    await new Promise((resolve) => setTimeout(resolve, 50));
    expect(onSubmit).not.toHaveBeenCalled();
  });
});

describe('CustomerFormFields Enter navigation', () => {
  const NAV = {
    rolesMode: 'free' as const,
    initialRoles: { isCustomer: true, isSupplier: false },
    enterNavigation: true,
    initialName: 'ACME',
  };

  beforeEach(() => {
    granted = new Set(['customers.create', 'suppliers.create']);
  });

  it('Enter walks the fields in order and Shift+Enter walks back', async () => {
    const user = userEvent.setup();
    const { onSubmit } = renderFields(NAV);
    const order = [
      'Mã',
      'Tên *',
      'Mã số thuế',
      'Số điện thoại',
      'Email',
      'Nhóm',
      'Số ngày được nợ',
      'Người liên hệ',
      'Địa chỉ công ty',
      'Địa chỉ giao hàng mặc định',
      'Ghi chú',
      'Là khách hàng',
      'Là nhà cung cấp',
    ];
    const field = (label: string) => screen.getByLabelText(label);

    field(order[0]).focus();
    for (let i = 1; i < order.length; i += 1) {
      await user.keyboard('{Enter}');
      expect(field(order[i])).toHaveFocus();
    }
    expect(onSubmit).not.toHaveBeenCalled();

    for (let i = order.length - 2; i >= 0; i -= 1) {
      await user.keyboard('{Shift>}{Enter}{/Shift}');
      expect(field(order[i])).toHaveFocus();
    }
    expect(onSubmit).not.toHaveBeenCalled();
  });

  it('Enter in a textarea moves on without adding a newline', async () => {
    const user = userEvent.setup();
    renderFields(NAV);
    const address = screen.getByLabelText('Địa chỉ công ty');
    await user.type(address, 'Số 1');
    await user.keyboard('{Enter}');
    expect(address).toHaveValue('Số 1');
    expect(screen.getByLabelText('Địa chỉ giao hàng mặc định')).toHaveFocus();
  });

  it('Enter on the closed group trigger moves on and does not open the list', async () => {
    const user = userEvent.setup();
    renderFields(NAV);
    const trigger = screen.getByLabelText('Nhóm');
    trigger.focus();
    await user.keyboard('{Enter}');
    expect(screen.getByLabelText('Số ngày được nợ')).toHaveFocus();
    expect(trigger).toHaveAttribute('aria-expanded', 'false');
    expect(screen.queryByRole('listbox')).not.toBeInTheDocument();
  });

  it('skips a disabled role checkbox and submits from the last enabled field', async () => {
    granted = new Set(['customers.create']);
    const user = userEvent.setup();
    const { onSubmit } = renderFields(NAV);
    screen.getByLabelText('Ghi chú').focus();
    await user.keyboard('{Enter}');
    expect(screen.getByLabelText('Là khách hàng')).toHaveFocus();
    expect(onSubmit).not.toHaveBeenCalled();

    await user.keyboard('{Enter}');
    await waitFor(() => expect(onSubmit).toHaveBeenCalledTimes(1));
    expect(onSubmit.mock.calls[0][0]).toMatchObject({ name: 'ACME', isCustomer: true, isSupplier: false });
    await new Promise((resolve) => setTimeout(resolve, 50));
    expect(onSubmit).toHaveBeenCalledTimes(1);
  });

  it('without the role box Enter in Ghi chú submits', async () => {
    const user = userEvent.setup();
    const { onSubmit } = renderFields({ ...NAV, showRoles: false });
    screen.getByLabelText('Ghi chú').focus();
    await user.keyboard('{Enter}');
    await waitFor(() => expect(onSubmit).toHaveBeenCalledTimes(1));
    await new Promise((resolve) => setTimeout(resolve, 50));
    expect(onSubmit).toHaveBeenCalledTimes(1);
  });

  it('Shift+Enter in the first field does not submit', async () => {
    const user = userEvent.setup();
    const { onSubmit } = renderFields(NAV);
    screen.getByLabelText('Mã').focus();
    await user.keyboard('{Shift>}{Enter}{/Shift}');
    expect(screen.getByLabelText('Mã')).toHaveFocus();
    expect(onSubmit).not.toHaveBeenCalled();
  });

  it('Ctrl+Enter submits from any field', async () => {
    const user = userEvent.setup();
    const { onSubmit } = renderFields(NAV);
    screen.getByLabelText('Mã').focus();
    await user.keyboard('{Control>}{Enter}{/Control}');
    await waitFor(() => expect(onSubmit).toHaveBeenCalledTimes(1));
    expect(onSubmit.mock.calls[0][0]).toMatchObject({ name: 'ACME' });
  });

  it('Ctrl+Enter does not submit a partner locked by its other role; with suppliers.update it does', async () => {
    granted = new Set(['customers.update']);
    const user = userEvent.setup();
    const locked = renderFields({ isEdit: true, initial: DUAL_ROLE, enterNavigation: true });
    expect(screen.getByRole('button', { name: 'Cập nhật' })).toBeDisabled();
    screen.getByLabelText('Tên khách hàng *').focus();
    await user.keyboard('{Control>}{Enter}{/Control}');
    await new Promise((resolve) => setTimeout(resolve, 50));
    expect(locked.onSubmit).not.toHaveBeenCalled();
    locked.unmount();

    granted = new Set(['customers.update', 'suppliers.update']);
    const open = renderFields({ isEdit: true, initial: DUAL_ROLE, enterNavigation: true });
    screen.getByLabelText('Tên khách hàng *').focus();
    await user.keyboard('{Control>}{Enter}{/Control}');
    await waitFor(() => expect(open.onSubmit).toHaveBeenCalledTimes(1));
  });

  it('ignores Enter while composing', () => {
    const { onSubmit } = renderFields(NAV);
    const code = screen.getByLabelText('Mã');
    code.focus();
    fireEvent.keyDown(code, { key: 'Enter', isComposing: true });
    expect(code).toHaveFocus();
    expect(onSubmit).not.toHaveBeenCalled();
  });

  it('leaves Enter on the buttons alone', async () => {
    const user = userEvent.setup();
    const onCancel = vi.fn();
    const { onSubmit } = renderFields({ ...NAV, onCancel });
    const cancel = screen.getByRole('button', { name: 'Hủy' });
    cancel.focus();
    await user.keyboard('{Enter}');
    expect(onCancel).toHaveBeenCalledTimes(1);
    expect(cancel).toHaveFocus();

    const create = screen.getByRole('button', { name: 'Tạo mới' });
    create.focus();
    await user.keyboard('{Enter}');
    expect(create).toHaveFocus();
    await waitFor(() => expect(onSubmit).toHaveBeenCalledTimes(1));
  });

  it('without enterNavigation Enter in Tên does not move the focus', async () => {
    const user = userEvent.setup();
    renderFields({ ...NAV, enterNavigation: false });
    const name = screen.getByLabelText('Tên *');
    name.focus();
    await user.keyboard('{Enter}');
    expect(screen.getByLabelText('Mã số thuế')).not.toHaveFocus();
  });
});
