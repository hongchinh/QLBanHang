import { useState } from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { PartnerQuickAddDialog, type PartnerQuickAddDialogProps } from './partner-quick-add-dialog';
import { StopFormEvents } from './stop-form-events';
import { toPartnerSearchItem } from '@/features/customers/partner-search-item';
import type { Customer, UpsertCustomerRequest } from '@/features/customers/types';

let granted = new Set<string>();
vi.mock('@/stores/auth-store', () => ({
  useAuthStore: (selector: (s: { hasPermission: (p: string) => boolean; isInRole: () => boolean }) => unknown) =>
    selector({ hasPermission: (p) => granted.has(p), isInRole: () => false }),
}));

const toastMock = vi.hoisted(() => vi.fn());
vi.mock('@/lib/use-toast', () => ({ toast: toastMock }));

// The spies stand in for the API call; the hooks keep real mutation state (isPending / isError / error).
const createCustomerFn = vi.hoisted(() => vi.fn());
const createSupplierFn = vi.hoisted(() => vi.fn());
vi.mock('@/features/customers/hooks', async () => {
  const { useMutation } = await import('@tanstack/react-query');
  return { useCreateCustomer: () => useMutation({ mutationFn: (d: UpsertCustomerRequest) => createCustomerFn(d) }) };
});
vi.mock('@/features/suppliers/hooks', async () => {
  const { useMutation } = await import('@tanstack/react-query');
  return { useCreateSupplier: () => useMutation({ mutationFn: (d: UpsertCustomerRequest) => createSupplierFn(d) }) };
});

function record(over: Partial<Customer> = {}): Customer {
  return {
    id: 'p1',
    code: 'KH0009',
    name: 'Công ty An Phát',
    group: 'Company',
    status: 'Active',
    createdAt: '2026-10-01T00:00:00Z',
    isCustomer: true,
    isSupplier: false,
    ...over,
  };
}

function props(over: Partial<PartnerQuickAddDialogProps> = {}): PartnerQuickAddDialogProps {
  return {
    open: true,
    onOpenChange: vi.fn(),
    initialName: 'Công ty An Phát',
    scope: 'partner',
    preferredRole: 'customer',
    onCreated: vi.fn(),
    ...over,
  };
}

function Wrap({ children }: { children: React.ReactNode }) {
  const [client] = useState(() => new QueryClient({ defaultOptions: { mutations: { retry: false } } }));
  return <QueryClientProvider client={client}>{children}</QueryClientProvider>;
}

function renderDialog(over: Partial<PartnerQuickAddDialogProps> = {}) {
  const p = props(over);
  const view = render(
    <Wrap>
      <StopFormEvents>
        <PartnerQuickAddDialog {...p} />
      </StopFormEvents>
    </Wrap>,
  );
  return { p, ...view };
}

beforeEach(() => {
  granted = new Set(['customers.create', 'suppliers.create']);
  toastMock.mockReset();
  createCustomerFn.mockReset();
  createSupplierFn.mockReset();
  createCustomerFn.mockResolvedValue(record());
  createSupplierFn.mockResolvedValue(record({ code: 'NCC0003', isCustomer: false, isSupplier: true }));
});

describe('PartnerQuickAddDialog', () => {
  it('renders nothing and needs no query client while closed', () => {
    render(<PartnerQuickAddDialog {...props({ open: false })} />);

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it('titles by scope; labels are generic in both scopes', () => {
    const first = renderDialog();
    expect(screen.getByText('Thêm nhanh đối tượng')).toBeInTheDocument();
    expect(screen.getByLabelText('Mã')).toBeInTheDocument();
    expect(screen.getByLabelText('Tên *')).toBeInTheDocument();
    first.unmount();

    renderDialog({ scope: 'customer' });
    expect(screen.getByText('Thêm nhanh khách hàng')).toBeInTheDocument();
    expect(screen.getByLabelText('Mã')).toBeInTheDocument();
    expect(screen.getByLabelText('Tên *')).toBeInTheDocument();
  });

  it('prefills Tên and focuses it with the caret at the end', async () => {
    renderDialog({ initialName: 'Công ty An Phát' });

    const input = screen.getByLabelText('Tên *') as HTMLInputElement;
    expect(input).toHaveValue('Công ty An Phát');
    await waitFor(() => expect(input).toHaveFocus());
    expect(input.selectionStart).toBe(input.value.length);
    expect(input.selectionEnd).toBe(input.value.length);
  });

  it('checks only the preferred role when the user may create it', () => {
    renderDialog({ preferredRole: 'supplier' });

    expect(screen.getByLabelText('Là nhà cung cấp')).toBeChecked();
    expect(screen.getByLabelText('Là khách hàng')).not.toBeChecked();
  });

  it('checks the other role when the preferred one is not allowed', () => {
    granted = new Set(['customers.create']);
    renderDialog({ preferredRole: 'supplier' });

    expect(screen.getByLabelText('Là khách hàng')).toBeChecked();
    expect(screen.getByLabelText('Là nhà cung cấp')).not.toBeChecked();
    expect(screen.getByLabelText('Là nhà cung cấp')).toBeDisabled();
  });

  it('scope customer shows no role boxes', () => {
    renderDialog({ scope: 'customer' });

    expect(screen.queryByLabelText('Là khách hàng')).not.toBeInTheDocument();
    expect(screen.queryByLabelText('Là nhà cung cấp')).not.toBeInTheDocument();
  });

  it('a supplier-only record is created through the supplier hook', async () => {
    const user = userEvent.setup();
    const { p } = renderDialog({ preferredRole: 'supplier' });

    await user.click(screen.getByRole('button', { name: 'Tạo mới' }));

    await waitFor(() => expect(createSupplierFn).toHaveBeenCalledTimes(1));
    expect(createSupplierFn.mock.calls[0][0]).toMatchObject({ isSupplier: true, isCustomer: false });
    expect(createCustomerFn).not.toHaveBeenCalled();
    await waitFor(() => expect(p.onCreated).toHaveBeenCalledTimes(1));
    expect(toastMock).toHaveBeenCalledWith(
      expect.objectContaining({ variant: 'success', title: 'Đã tạo nhà cung cấp', description: 'NCC0003' }),
    );
  });

  it('a customer, or both roles, are created through the customer hook with both flags', async () => {
    const user = userEvent.setup();
    renderDialog({ preferredRole: 'customer' });
    await user.click(screen.getByLabelText('Là nhà cung cấp'));

    await user.click(screen.getByRole('button', { name: 'Tạo mới' }));

    await waitFor(() => expect(createCustomerFn).toHaveBeenCalledTimes(1));
    expect(createCustomerFn.mock.calls[0][0]).toMatchObject({ isCustomer: true, isSupplier: true });
    expect(createSupplierFn).not.toHaveBeenCalled();
  });

  it('success: toast, onCreated with the search item and the dialog closes', async () => {
    const user = userEvent.setup();
    const created = record({ creditDays: 30 });
    createCustomerFn.mockResolvedValue(created);
    const { p } = renderDialog();

    await user.click(screen.getByRole('button', { name: 'Tạo mới' }));

    await waitFor(() => expect(p.onCreated).toHaveBeenCalledTimes(1));
    expect(p.onCreated).toHaveBeenCalledWith(toPartnerSearchItem(created));
    expect(p.onOpenChange).toHaveBeenCalledWith(false);
    expect(toastMock).toHaveBeenCalledWith(
      expect.objectContaining({ variant: 'success', title: 'Đã tạo khách hàng', description: 'KH0009' }),
    );
  });

  it('failure: destructive toast, error box, dialog stays open with the typed values', async () => {
    const user = userEvent.setup();
    createCustomerFn.mockRejectedValue(new Error('Mã đã tồn tại'));
    const { p } = renderDialog();
    await user.type(screen.getByLabelText('Mã'), 'KH1');

    await user.click(screen.getByRole('button', { name: 'Tạo mới' }));

    await waitFor(() =>
      expect(toastMock).toHaveBeenCalledWith(
        expect.objectContaining({ variant: 'destructive', title: 'Không thể lưu', description: 'Mã đã tồn tại' }),
      ),
    );
    expect(await screen.findByText('Mã đã tồn tại')).toBeInTheDocument();
    expect(p.onCreated).not.toHaveBeenCalled();
    expect(p.onOpenChange).not.toHaveBeenCalled();
    expect(screen.getByLabelText('Mã')).toHaveValue('KH1');
    expect(screen.getByLabelText('Tên *')).toHaveValue('Công ty An Phát');
  });

  it('Hủy closes the dialog', async () => {
    const user = userEvent.setup();
    const { p } = renderDialog();

    await user.click(screen.getByRole('button', { name: 'Hủy' }));

    expect(p.onOpenChange).toHaveBeenCalledWith(false);
  });

  it('Escape closes the dialog', async () => {
    const { p } = renderDialog();

    await userEvent.setup().keyboard('{Escape}');

    expect(p.onOpenChange).toHaveBeenCalledWith(false);
  });

  it('calls onCloseAutoFocus of the parent when the dialog closes', async () => {
    const onCloseAutoFocus = vi.fn();
    const p = props({ onCloseAutoFocus });
    const ui = (open: boolean) => (
      <Wrap>
        <StopFormEvents>
          <PartnerQuickAddDialog {...p} open={open} />
        </StopFormEvents>
      </Wrap>
    );
    const { rerender } = render(ui(true));
    expect(onCloseAutoFocus).not.toHaveBeenCalled();

    rerender(ui(false));

    await waitFor(() => expect(onCloseAutoFocus).toHaveBeenCalledTimes(1));
  });
});

describe('PartnerQuickAddDialog inside a voucher form', () => {
  function setup(over: Partial<PartnerQuickAddDialogProps> = {}) {
    const outerSubmit = vi.fn((e: React.FormEvent) => e.preventDefault());
    const outerKey = vi.fn();
    const p = props(over);
    render(
      <Wrap>
        {/* eslint-disable-next-line jsx-a11y/no-noninteractive-element-interactions -- stands in for the voucher form. */}
        <form onSubmit={outerSubmit} onKeyDown={outerKey}>
          <StopFormEvents>
            <PartnerQuickAddDialog {...p} />
          </StopFormEvents>
        </form>
      </Wrap>,
    );
    return { p, outerSubmit, outerKey };
  }

  it('creating through the button never submits the voucher form', async () => {
    const user = userEvent.setup();
    const { p, outerSubmit } = setup();

    await user.click(screen.getByRole('button', { name: 'Tạo mới' }));

    await waitFor(() => expect(p.onCreated).toHaveBeenCalledTimes(1));
    expect(outerSubmit).not.toHaveBeenCalled();
  });

  it('creating through Enter on the last field never submits the voucher form', async () => {
    const user = userEvent.setup();
    const { p, outerSubmit, outerKey } = setup({ scope: 'customer' });

    screen.getByLabelText('Ghi chú').focus();
    await user.keyboard('{Enter}');

    await waitFor(() => expect(p.onCreated).toHaveBeenCalledTimes(1));
    expect(createCustomerFn).toHaveBeenCalledTimes(1);
    expect(outerSubmit).not.toHaveBeenCalled();
    expect(outerKey).not.toHaveBeenCalled();
  });

  it('creating through Ctrl+Enter never submits the voucher form', async () => {
    const user = userEvent.setup();
    const { p, outerSubmit, outerKey } = setup();

    screen.getByLabelText('Mã').focus();
    await user.keyboard('{Control>}{Enter}{/Control}');

    await waitFor(() => expect(p.onCreated).toHaveBeenCalledTimes(1));
    expect(outerSubmit).not.toHaveBeenCalled();
    expect(outerKey).not.toHaveBeenCalled();
  });

  it('Ctrl+S and Enter in a field or on the close button never reach the form key handler', async () => {
    const user = userEvent.setup();
    const { outerKey } = setup();

    const code = screen.getByLabelText('Mã');
    await user.click(code);
    await user.keyboard('ab{Enter}');
    fireEvent.keyDown(code, { key: 's', ctrlKey: true });
    const close = screen.getByRole('button', { name: 'Đóng' });
    fireEvent.keyDown(close, { key: 'Enter' });
    fireEvent.keyDown(close, { key: 's', ctrlKey: true });

    expect(outerKey).not.toHaveBeenCalled();
  });

  it('Tab on the close button loops to Mã and Shift+Tab on Mã loops back', () => {
    setup();
    const close = screen.getByRole('button', { name: 'Đóng' });
    const code = screen.getByLabelText('Mã');

    close.focus();
    fireEvent.keyDown(close, { key: 'Tab' });
    expect(code).toHaveFocus();

    fireEvent.keyDown(code, { key: 'Tab', shiftKey: true });
    expect(close).toHaveFocus();
  });

  it('Escape closes the dialog although keydown does not reach the form', async () => {
    const { p, outerKey } = setup();

    await userEvent.setup().keyboard('{Escape}');

    expect(p.onOpenChange).toHaveBeenCalledWith(false);
    expect(outerKey).not.toHaveBeenCalled();
  });
});
