import { describe, it, expect, vi, beforeEach } from 'vitest';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { PartnerCatalogDialog, type PartnerCatalogDialogProps } from './partner-catalog-dialog';
import { StopFormEvents } from './stop-form-events';
import { toPartnerSearchItem } from '@/features/customers/partner-search-item';
import type { Customer, PagedResult, CustomerListItem } from '@/features/customers/types';

let granted = new Set<string>();
vi.mock('@/stores/auth-store', () => ({
  useAuthStore: (selector: (s: { hasPermission: (p: string) => boolean; isInRole: () => boolean }) => unknown) =>
    selector({ hasPermission: (p) => granted.has(p), isInRole: () => false }),
}));

const toastMock = vi.hoisted(() => vi.fn());
vi.mock('@/lib/use-toast', () => ({ toast: toastMock }));

const customerGet = vi.hoisted(() => vi.fn());
const supplierGet = vi.hoisted(() => vi.fn());
vi.mock('@/features/customers/api', () => ({ customersApi: { get: customerGet } }));
vi.mock('@/features/suppliers/api', () => ({ suppliersApi: { get: supplierGet } }));

const useCustomersMock = vi.hoisted(() => vi.fn());
const useSuppliersMock = vi.hoisted(() => vi.fn());
vi.mock('@/features/customers/hooks', () => ({ useCustomers: useCustomersMock }));
vi.mock('@/features/suppliers/hooks', () => ({ useSuppliers: useSuppliersMock }));

function paged(item: CustomerListItem): PagedResult<CustomerListItem> {
  return { items: [item], page: 1, pageSize: 20, totalItems: 1, totalPages: 1, hasNextPage: false, hasPreviousPage: false };
}

const customerRow: CustomerListItem = {
  id: 'id-1', code: 'KH-001', name: 'Công ty ABC', taxCode: '0100000001', phoneNumber: '0901111111',
  contactPerson: 'Anh A', group: 'Company', status: 'Active',
};
const supplierRow: CustomerListItem = {
  id: 'id-9', code: 'NCC-009', name: 'Nhà máy Sơn', taxCode: '0100000009', phoneNumber: '0909999999',
  contactPerson: 'Chị S', group: 'Company', status: 'Active',
};

function record(over: Partial<Customer> = {}): Customer {
  return {
    id: 'id-1',
    code: 'KH-001',
    name: 'Công ty ABC',
    taxCode: '0100000001',
    companyAddress: '12 Đường Số 1, Q.1, TP.HCM',
    defaultShippingAddress: '34 Nguyễn Huệ',
    contactPerson: 'Anh A',
    phoneNumber: '0901111111',
    email: 'abc@example.com',
    group: 'Company',
    status: 'Active',
    createdAt: '2024-01-01T00:00:00Z',
    isCustomer: true,
    isSupplier: true,
    creditDays: 30,
    ...over,
  };
}

function props(over: Partial<PartnerCatalogDialogProps> = {}): PartnerCatalogDialogProps {
  return {
    open: true,
    onOpenChange: vi.fn(),
    initialQuery: '',
    scope: 'partner',
    preferredRole: 'customer',
    onSelect: vi.fn(),
    ...over,
  };
}

function renderDialog(over: Partial<PartnerCatalogDialogProps> = {}) {
  const p = props(over);
  const view = render(
    <StopFormEvents>
      <PartnerCatalogDialog {...p} />
    </StopFormEvents>,
  );
  return { p, ...view };
}

function searchInput() {
  return screen.getByRole('textbox') as HTMLInputElement;
}

beforeEach(() => {
  granted = new Set(['customers.view', 'suppliers.view']);
  toastMock.mockReset();
  customerGet.mockReset();
  supplierGet.mockReset();
  useCustomersMock.mockReset();
  useSuppliersMock.mockReset();
  customerGet.mockResolvedValue(record());
  supplierGet.mockResolvedValue(record({ id: 'id-9', code: 'NCC-009', name: 'Nhà máy Sơn', isCustomer: false }));
  useCustomersMock.mockReturnValue({ data: paged(customerRow), isLoading: false });
  useSuppliersMock.mockReturnValue({ data: paged(supplierRow), isLoading: false });
});

describe('PartnerCatalogDialog', () => {
  it('renders nothing while closed', () => {
    renderDialog({ open: false });

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(useCustomersMock).not.toHaveBeenCalled();
  });

  it('scope customer: title Danh mục khách hàng, no role switch', () => {
    renderDialog({ scope: 'customer' });

    expect(screen.getByRole('dialog')).toBeInTheDocument();
    expect(screen.getByText('Danh mục khách hàng')).toBeInTheDocument();
    expect(screen.queryByRole('tab', { name: 'Khách hàng' })).not.toBeInTheDocument();
    expect(screen.queryByRole('tab', { name: 'Nhà cung cấp' })).not.toBeInTheDocument();
  });

  it('scope partner with both view permissions: a Khách hàng | Nhà cung cấp switch, starting on preferredRole', () => {
    const first = renderDialog({ preferredRole: 'supplier' });

    expect(screen.getByRole('tab', { name: 'Khách hàng' })).toHaveAttribute('aria-selected', 'false');
    expect(screen.getByRole('tab', { name: 'Nhà cung cấp' })).toHaveAttribute('aria-selected', 'true');
    expect(screen.getByText('Danh mục nhà cung cấp')).toBeInTheDocument();
    expect(useSuppliersMock).toHaveBeenCalled();
    expect(useCustomersMock).not.toHaveBeenCalled();
    first.unmount();

    renderDialog({ preferredRole: 'customer' });
    expect(screen.getByRole('tab', { name: 'Khách hàng' })).toHaveAttribute('aria-selected', 'true');
    expect(screen.getByText('Danh mục khách hàng')).toBeInTheDocument();
  });

  it('switching the role keeps the typed query and lists the other role with it', async () => {
    const user = userEvent.setup();
    renderDialog();
    await user.type(searchInput(), 'abc');
    await waitFor(() => expect(useCustomersMock).toHaveBeenLastCalledWith(expect.objectContaining({ search: 'abc' })));

    await user.click(screen.getByRole('tab', { name: 'Nhà cung cấp' }));

    await waitFor(() => expect(useSuppliersMock).toHaveBeenCalledWith(expect.objectContaining({ search: 'abc' })));
    expect(searchInput()).toHaveValue('abc');
    expect(screen.getByText('NCC-009')).toBeInTheDocument();
    expect(screen.getByPlaceholderText('Tìm mã, tên nhà cung cấp...')).toBeInTheDocument();
  });

  it('the search input shows initialQuery; closing and reopening starts again from it', async () => {
    const user = userEvent.setup();
    const p = props({ initialQuery: 'ABC' });
    const ui = (open: boolean) => (
      <StopFormEvents>
        <PartnerCatalogDialog {...p} open={open} />
      </StopFormEvents>
    );
    const { rerender } = render(ui(true));
    expect(searchInput()).toHaveValue('ABC');
    await user.type(searchInput(), 'xyz');
    expect(searchInput()).toHaveValue('ABCxyz');

    rerender(ui(false));
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    rerender(ui(true));

    expect(searchInput()).toHaveValue('ABC');
  });

  it('a user with only suppliers.view gets no switch and the supplier list, even preferring customers', () => {
    granted = new Set(['suppliers.view']);
    renderDialog({ preferredRole: 'customer' });

    expect(screen.queryByRole('tab', { name: 'Khách hàng' })).not.toBeInTheDocument();
    expect(screen.queryByRole('tab', { name: 'Nhà cung cấp' })).not.toBeInTheDocument();
    expect(screen.getByText('Danh mục nhà cung cấp')).toBeInTheDocument();
    expect(useSuppliersMock).toHaveBeenCalled();
    expect(useCustomersMock).not.toHaveBeenCalled();
  });

  it('a user with only customers.view gets the customer list even preferring suppliers', () => {
    granted = new Set(['customers.view']);
    renderDialog({ preferredRole: 'supplier' });

    expect(screen.getByText('Danh mục khách hàng')).toBeInTheDocument();
    expect(useCustomersMock).toHaveBeenCalled();
    expect(useSuppliersMock).not.toHaveBeenCalled();
  });

  it.each([
    ['scope customer without customers.view', 'customer', ['suppliers.view']],
    ['scope partner with neither view permission', 'partner', []],
  ] as const)('%s: renders nothing and never calls a list hook', (_name, scope, perms) => {
    granted = new Set(perms);
    renderDialog({ scope });

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(useCustomersMock).not.toHaveBeenCalled();
    expect(useSuppliersMock).not.toHaveBeenCalled();
  });

  it('choosing a customer row loads customersApi.get, selects the search item with the roles and closes', async () => {
    const user = userEvent.setup();
    const rec = record();
    customerGet.mockResolvedValue(rec);
    const { p } = renderDialog();

    await user.click(screen.getByText('KH-001').closest('tr')!);

    await waitFor(() => expect(p.onSelect).toHaveBeenCalledTimes(1));
    expect(customerGet).toHaveBeenCalledWith('id-1');
    expect(supplierGet).not.toHaveBeenCalled();
    expect(p.onSelect).toHaveBeenCalledWith(toPartnerSearchItem(rec));
    expect(p.onSelect).toHaveBeenCalledWith(
      expect.objectContaining({ isCustomer: true, isSupplier: true, creditDays: 30 }),
    );
    expect(p.onOpenChange).toHaveBeenCalledWith(false);
  });

  it('choosing a supplier row loads suppliersApi.get', async () => {
    const user = userEvent.setup();
    const rec = record({ id: 'id-9', code: 'NCC-009', isCustomer: false, creditDays: 15 });
    supplierGet.mockResolvedValue(rec);
    const { p } = renderDialog({ preferredRole: 'supplier' });

    await user.click(screen.getByText('NCC-009').closest('tr')!);

    await waitFor(() => expect(p.onSelect).toHaveBeenCalledTimes(1));
    expect(supplierGet).toHaveBeenCalledWith('id-9');
    expect(customerGet).not.toHaveBeenCalled();
    expect(p.onSelect).toHaveBeenCalledWith(toPartnerSearchItem(rec));
    expect(p.onOpenChange).toHaveBeenCalledWith(false);
  });

  it('a double click loads the detail once and selects once', async () => {
    const user = userEvent.setup();
    let resolveLoad: (value: Customer) => void = () => {};
    customerGet.mockReturnValue(new Promise<Customer>((resolve) => { resolveLoad = resolve; }));
    const { p } = renderDialog();

    await user.dblClick(screen.getByText('KH-001').closest('tr')!);
    expect(customerGet).toHaveBeenCalledTimes(1);

    resolveLoad(record());
    await waitFor(() => expect(p.onSelect).toHaveBeenCalledTimes(1));
    expect(customerGet).toHaveBeenCalledTimes(1);
  });

  it('Enter while a detail load is running does not start a second one', async () => {
    customerGet.mockReturnValue(new Promise<Customer>(() => {}));
    renderDialog();

    fireEvent.keyDown(searchInput(), { key: 'Enter' });
    fireEvent.keyDown(searchInput(), { key: 'Enter' });

    expect(customerGet).toHaveBeenCalledTimes(1);
  });

  it.each([
    ['resolves', false],
    ['fails', true],
  ] as const)('a detail load that %s after the dialog closed is ignored', async (_name, fails) => {
    const user = userEvent.setup();
    let settle: (value?: Customer) => void = () => {};
    customerGet.mockReturnValue(
      new Promise<Customer>((resolve, reject) => {
        settle = fails ? () => reject(new Error('boom')) : () => resolve(record());
      }),
    );
    const p = props();
    const ui = (open: boolean) => (
      <StopFormEvents>
        <PartnerCatalogDialog {...p} open={open} />
      </StopFormEvents>
    );
    const { rerender } = render(ui(true));
    await user.click(screen.getByText('KH-001').closest('tr')!);
    expect(customerGet).toHaveBeenCalledTimes(1);

    rerender(ui(false));
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    settle();
    await new Promise((resolve) => setTimeout(resolve, 50));

    expect(p.onSelect).not.toHaveBeenCalled();
    expect(p.onOpenChange).not.toHaveBeenCalled();
    expect(toastMock).not.toHaveBeenCalled();
  });

  it('Enter in the search box picks the first row', async () => {
    const { p } = renderDialog();

    fireEvent.keyDown(searchInput(), { key: 'Enter' });

    await waitFor(() => expect(p.onSelect).toHaveBeenCalledTimes(1));
    expect(customerGet).toHaveBeenCalledWith('id-1');
  });

  it.each([
    ['customer', 'Không thể tải thông tin khách hàng. Vui lòng thử lại.'],
    ['supplier', 'Không thể tải thông tin nhà cung cấp. Vui lòng thử lại.'],
  ] as const)('a failed %s detail load shows the destructive toast and leaves the dialog open', async (role, title) => {
    const user = userEvent.setup();
    (role === 'customer' ? customerGet : supplierGet).mockRejectedValue(new Error('boom'));
    const { p } = renderDialog({ preferredRole: role });

    await user.click(screen.getByText(role === 'customer' ? 'KH-001' : 'NCC-009').closest('tr')!);

    await waitFor(() => expect(toastMock).toHaveBeenCalledWith({ variant: 'destructive', title }));
    expect(p.onSelect).not.toHaveBeenCalled();
    expect(p.onOpenChange).not.toHaveBeenCalled();
    expect(screen.getByRole('dialog')).toBeInTheDocument();
  });

  it('Escape closes the dialog', async () => {
    const { p } = renderDialog();

    await userEvent.setup().keyboard('{Escape}');

    expect(p.onOpenChange).toHaveBeenCalledWith(false);
  });

  it('focuses the search input on opening', () => {
    renderDialog();

    expect(searchInput()).toHaveFocus();
  });

  it('calls onCloseAutoFocus of the parent when the dialog closes', async () => {
    const onCloseAutoFocus = vi.fn();
    const p = props({ onCloseAutoFocus });
    const ui = (open: boolean) => (
      <StopFormEvents>
        <PartnerCatalogDialog {...p} open={open} />
      </StopFormEvents>
    );
    const { rerender } = render(ui(true));
    expect(onCloseAutoFocus).not.toHaveBeenCalled();

    rerender(ui(false));

    await waitFor(() => expect(onCloseAutoFocus).toHaveBeenCalledTimes(1));
  });
});

describe('PartnerCatalogDialog inside a voucher form', () => {
  function setup(over: Partial<PartnerCatalogDialogProps> = {}) {
    const outerSubmit = vi.fn((e: React.FormEvent) => e.preventDefault());
    const outerKey = vi.fn();
    const p = props(over);
    render(
      // eslint-disable-next-line jsx-a11y/no-noninteractive-element-interactions -- stands in for the voucher form.
      <form onSubmit={outerSubmit} onKeyDown={outerKey}>
        <StopFormEvents>
          <PartnerCatalogDialog {...p} />
        </StopFormEvents>
      </form>,
    );
    return { p, outerSubmit, outerKey };
  }

  it('Ctrl+S and Enter in the search box or on the close button never reach the form key handler', async () => {
    const user = userEvent.setup();
    const { outerKey, outerSubmit } = setup();

    await user.click(searchInput());
    await user.keyboard('ab{Enter}');
    fireEvent.keyDown(searchInput(), { key: 's', ctrlKey: true });
    const close = screen.getByRole('button', { name: 'Đóng' });
    fireEvent.keyDown(close, { key: 'Enter' });
    fireEvent.keyDown(close, { key: 's', ctrlKey: true });

    expect(outerKey).not.toHaveBeenCalled();
    expect(outerSubmit).not.toHaveBeenCalled();
  });

  it('Tab on the close button loops to the search box and Shift+Tab on it loops back', () => {
    setup({ scope: 'customer' });
    const close = screen.getByRole('button', { name: 'Đóng' });
    const input = searchInput();

    close.focus();
    fireEvent.keyDown(close, { key: 'Tab' });
    expect(input).toHaveFocus();

    fireEvent.keyDown(input, { key: 'Tab', shiftKey: true });
    expect(close).toHaveFocus();
  });

  it('Escape closes the dialog although keydown does not reach the form', async () => {
    const { p, outerKey } = setup();

    await userEvent.setup().keyboard('{Escape}');

    expect(p.onOpenChange).toHaveBeenCalledWith(false);
    expect(outerKey).not.toHaveBeenCalled();
  });
});
