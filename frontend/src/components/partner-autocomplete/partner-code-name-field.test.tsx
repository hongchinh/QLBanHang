import { beforeEach, describe, expect, it, vi } from 'vitest';
import { useState } from 'react';
import { act, cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { PartnerCodeNameField, type PartnerCodeNameFieldProps } from './partner-code-name-field';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { PartnerSearchItem } from '@/features/stock-vouchers/types';
import type { Customer, CustomerListItem, PagedResult, UpsertCustomerRequest } from '@/features/customers/types';

const usePartnerSearchMock = vi.fn();

let granted = new Set<string>();
vi.mock('@/stores/auth-store', () => ({
  useAuthStore: (selector: (s: { hasPermission: (p: string) => boolean; isInRole: () => boolean }) => unknown) =>
    selector({ hasPermission: (p) => granted.has(p), isInRole: () => false }),
}));

const toastMock = vi.hoisted(() => vi.fn());
vi.mock('@/lib/use-toast', () => ({ toast: toastMock }));

// The spies stand in for the API calls; the create hooks keep real mutation state.
const createCustomerFn = vi.hoisted(() => vi.fn());
const createSupplierFn = vi.hoisted(() => vi.fn());
const useCustomersMock = vi.hoisted(() => vi.fn());
const useSuppliersMock = vi.hoisted(() => vi.fn());
vi.mock('@/features/customers/hooks', async () => {
  const { useMutation } = await import('@tanstack/react-query');
  return {
    useCreateCustomer: () => useMutation({ mutationFn: (d: UpsertCustomerRequest) => createCustomerFn(d) }),
    useCustomers: useCustomersMock,
  };
});
vi.mock('@/features/suppliers/hooks', async () => {
  const { useMutation } = await import('@tanstack/react-query');
  return {
    useCreateSupplier: () => useMutation({ mutationFn: (d: UpsertCustomerRequest) => createSupplierFn(d) }),
    useSuppliers: useSuppliersMock,
  };
});
const customerGet = vi.hoisted(() => vi.fn());
const supplierGet = vi.hoisted(() => vi.fn());
vi.mock('@/features/customers/api', () => ({ customersApi: { get: customerGet } }));
vi.mock('@/features/suppliers/api', () => ({ suppliersApi: { get: supplierGet } }));

function pagedOf(item: CustomerListItem): PagedResult<CustomerListItem> {
  return { items: [item], page: 1, pageSize: 20, totalItems: 1, totalPages: 1, hasNextPage: false, hasPreviousPage: false };
}

vi.mock('@/features/stock-vouchers/hooks', () => ({
  usePartnerSearch: (...args: unknown[]) => usePartnerSearchMock(...args),
}));

const partners: PartnerSearchItem[] = [
  {
    id: '11111111-1111-1111-1111-111111111111',
    code: 'NCC01',
    name: 'Công ty Thép Việt',
    status: 'Active',
    isCustomer: false,
    isSupplier: true,
  },
  {
    id: '33333333-3333-3333-3333-333333333333',
    code: 'KH01',
    name: 'Công ty ABC',
    status: 'Active',
    isCustomer: true,
    isSupplier: true,
  },
];

function renderField(overrides: Partial<PartnerCodeNameFieldProps> = {}) {
  const props: PartnerCodeNameFieldProps = {
    label: 'Đối tượng',
    scope: 'partner',
    codeInputId: 'partner-code',
    nameInputId: 'partner-name',
    nameInputProps: {},
    value: null,
    onSelect: vi.fn(),
    ...overrides,
  };
  const view = render(<PartnerCodeNameField {...props} />);
  return { props, ...view };
}

describe('PartnerCodeNameField', () => {
  beforeEach(() => {
    usePartnerSearchMock.mockReset();
    usePartnerSearchMock.mockReturnValue({ data: partners, isFetching: false, isLoading: false, isError: false });
  });

  it('shows the label and focuses the code input when it is clicked', async () => {
    renderField();
    const label = screen.getByText('Đối tượng');
    expect(label).not.toHaveClass('required');
    await userEvent.click(label);
    expect(screen.getByRole('combobox')).toHaveFocus();
  });

  it('marks the label as required', () => {
    renderField({ required: true });
    expect(screen.getByText('Đối tượng')).toHaveClass('required');
  });

  it('renders the code combobox and the name input with their placeholders', () => {
    renderField();
    expect(screen.getByRole('combobox')).toHaveAttribute('id', 'partner-code');
    expect(screen.getByRole('combobox')).toHaveAttribute('placeholder', 'Nhập mã / tên / MST đối tượng...');
    const name = document.getElementById('partner-name');
    expect(name).toHaveAttribute('placeholder', 'Tên đối tượng');
  });

  it('uses customer placeholders for the customer scope and lets namePlaceholder win', () => {
    const { unmount } = renderField({ scope: 'customer', label: 'Khách hàng' });
    expect(screen.getByRole('combobox')).toHaveAttribute('placeholder', 'Nhập mã / tên / MST / địa chỉ / SĐT...');
    expect(document.getElementById('partner-name')).toHaveAttribute('placeholder', 'Tên khách hàng');
    unmount();

    renderField({ namePlaceholder: 'Tên người nhận' });
    expect(document.getElementById('partner-name')).toHaveAttribute('placeholder', 'Tên người nhận');
  });

  it('gives the name input an accessible name from its effective placeholder', () => {
    const { unmount } = renderField();
    expect(screen.getByRole('textbox', { name: 'Tên đối tượng' })).toHaveAttribute('id', 'partner-name');
    unmount();

    renderField({ scope: 'customer', label: 'Khách hàng' });
    expect(screen.getByRole('textbox', { name: 'Tên khách hàng' })).toHaveAttribute('id', 'partner-name');
  });

  it('applies nameInputProps to the name input', () => {
    const onChange = vi.fn();
    renderField({ nameInputProps: { value: 'Anh Nam', onChange } });
    const name = document.getElementById('partner-name') as HTMLInputElement;
    expect(name.value).toBe('Anh Nam');
    fireEvent.change(name, { target: { value: 'Anh Nam B' } });
    expect(onChange).toHaveBeenCalledTimes(1);
  });

  it('renders the error as an alert that the code input points to', () => {
    const { rerender, props } = renderField();
    expect(screen.queryByRole('alert')).toBeNull();
    expect(screen.getByRole('combobox')).not.toHaveAttribute('aria-describedby');

    rerender(<PartnerCodeNameField {...props} error="Vui lòng chọn đối tượng từ danh sách" />);
    const alert = screen.getByRole('alert');
    expect(alert).toHaveTextContent('Vui lòng chọn đối tượng từ danh sách');
    expect(alert).toHaveAttribute('id', 'partner-code-error');
    expect(screen.getByRole('combobox')).toHaveAttribute('aria-describedby', 'partner-code-error');
  });

  it('selects with Enter and moves focus to the name input', async () => {
    const onSelect = vi.fn();
    renderField({ onSelect });
    const code = screen.getByRole('combobox');
    fireEvent.change(code, { target: { value: 'KH01' } });
    await waitFor(() => expect(screen.getByRole('listbox')).toBeInTheDocument());
    await waitFor(() => expect(usePartnerSearchMock).toHaveBeenCalledWith('In', 'KH01', true));
    fireEvent.keyDown(code, { key: 'Enter' });
    expect(onSelect).toHaveBeenCalledWith(partners[1]);
    await waitFor(() => expect(document.getElementById('partner-name')).toHaveFocus());
  });

  it('partner type None disables only the code input', () => {
    renderField({ partnerType: 'None' });
    expect(screen.getByRole('combobox')).toBeDisabled();
    expect(document.getElementById('partner-name')).toBeEnabled();
  });

  it('disabled disables the code input', () => {
    renderField({ disabled: true });
    expect(screen.getByRole('combobox')).toBeDisabled();
  });

  it('searches active partners without a reason', async () => {
    renderField();
    expect(screen.getByRole('combobox')).toBeEnabled();
    fireEvent.change(screen.getByRole('combobox'), { target: { value: 'cong' } });
    await waitFor(() => expect(usePartnerSearchMock).toHaveBeenLastCalledWith('In', 'cong', true));
  });

  it('searches the given stock direction, or uses its own search hook', async () => {
    const { unmount } = renderField({ type: 'Out' });
    fireEvent.change(screen.getByRole('combobox'), { target: { value: 'cong' } });
    await waitFor(() => expect(usePartnerSearchMock).toHaveBeenLastCalledWith('Out', 'cong', true));
    unmount();

    usePartnerSearchMock.mockClear();
    const useSearch = vi.fn().mockReturnValue({ data: partners, isFetching: false, isLoading: false, isError: false });
    renderField({ useSearch });
    fireEvent.change(screen.getByRole('combobox'), { target: { value: 'cong' } });
    await waitFor(() => expect(useSearch).toHaveBeenCalledWith('cong', true, undefined));
    expect(usePartnerSearchMock).not.toHaveBeenCalled();
  });

  it('forwards onUnpickedChange', () => {
    const onUnpickedChange = vi.fn();
    renderField({ onUnpickedChange });
    fireEvent.change(screen.getByRole('combobox'), { target: { value: 'abc' } });
    expect(onUnpickedChange).toHaveBeenLastCalledWith(true);
  });
});

// ---- Quick add and catalog -------------------------------------------------------------------------------

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

const customerRow: CustomerListItem = {
  id: 'id-1', code: 'KH-001', name: 'Công ty ABC', taxCode: '0100000001', phoneNumber: '0901111111',
  contactPerson: 'Anh A', group: 'Company', status: 'Active',
};

function codeInput() {
  return document.getElementById('partner-code') as HTMLInputElement;
}
function nameInput() {
  return document.getElementById('partner-name') as HTMLInputElement;
}
function typeInCode(text: string) {
  fireEvent.change(codeInput(), { target: { value: text } });
}
function dialog() {
  return within(screen.getByRole('dialog'));
}

interface HarnessProps {
  fieldProps: Partial<PartnerCodeNameFieldProps>;
  onSelect: (p: PartnerSearchItem | null) => void;
  onUnpickedChange: (v: boolean) => void;
  outerSubmit?: (e: React.FormEvent) => void;
  outerKey?: () => void;
}

// Holds the picked value like a voucher form does.
function Harness({ fieldProps, onSelect, onUnpickedChange, outerSubmit, outerKey }: HarnessProps) {
  const [client] = useState(() => new QueryClient({ defaultOptions: { mutations: { retry: false } } }));
  const [value, setValue] = useState<PartnerCodeNameFieldProps['value']>(fieldProps.value ?? null);
  const field = (
    <PartnerCodeNameField
      label="Đối tượng"
      scope="partner"
      codeInputId="partner-code"
      nameInputId="partner-name"
      nameInputProps={{}}
      {...fieldProps}
      value={value}
      onSelect={(p) => {
        onSelect(p);
        setValue(p ? { id: p.id, code: p.code, name: p.name } : null);
      }}
      onUnpickedChange={onUnpickedChange}
    />
  );
  return (
    <QueryClientProvider client={client}>
      <button type="button" onClick={() => setValue(null)}>
        reset
      </button>
      {outerSubmit ? (
        // eslint-disable-next-line jsx-a11y/no-noninteractive-element-interactions -- stands in for the voucher form.
        <form onSubmit={outerSubmit} onKeyDown={outerKey}>
          {field}
        </form>
      ) : (
        field
      )}
    </QueryClientProvider>
  );
}

function renderHarness(fieldProps: Partial<PartnerCodeNameFieldProps> = {}, inForm = false) {
  const onSelect = vi.fn();
  const onUnpickedChange = vi.fn();
  const outerSubmit = vi.fn((e: React.FormEvent) => e.preventDefault());
  const outerKey = vi.fn();
  render(
    <Harness
      fieldProps={fieldProps}
      onSelect={onSelect}
      onUnpickedChange={onUnpickedChange}
      outerSubmit={inForm ? outerSubmit : undefined}
      outerKey={inForm ? outerKey : undefined}
    />,
  );
  return { onSelect, onUnpickedChange, outerSubmit, outerKey };
}

describe('quick add and catalog', () => {
  beforeEach(() => {
    granted = new Set(['customers.create', 'suppliers.create', 'customers.view', 'suppliers.view']);
    toastMock.mockReset();
    createCustomerFn.mockReset();
    createSupplierFn.mockReset();
    customerGet.mockReset();
    supplierGet.mockReset();
    useCustomersMock.mockReset();
    useSuppliersMock.mockReset();
    createCustomerFn.mockResolvedValue(record());
    createSupplierFn.mockResolvedValue(record({ code: 'NCC0003', isCustomer: false, isSupplier: true }));
    customerGet.mockResolvedValue(record({ id: 'id-1', code: 'KH-001', name: 'Công ty ABC', isSupplier: true, creditDays: 30 }));
    useCustomersMock.mockReturnValue({ data: pagedOf(customerRow), isLoading: false });
    useSuppliersMock.mockReturnValue({ data: pagedOf(customerRow), isLoading: false });
  });

  const addButton = () => screen.queryByRole('button', { name: /Thêm nhanh/ });

  it('without create permission there is no + button, F4 does nothing and no Thêm mới row', async () => {
    granted = new Set(['customers.view']);
    renderHarness();

    expect(addButton()).toBeNull();
    typeInCode('abc');
    await screen.findByRole('listbox');
    fireEvent.keyDown(codeInput(), { key: 'F4' });
    expect(screen.queryByRole('dialog')).toBeNull();
    expect(screen.queryByText(/Thêm mới đối tượng/)).toBeNull();
    expect(screen.getByText('Xem danh mục đầy đủ')).toBeInTheDocument();
  });

  it('without view permission there is no Xem danh mục đầy đủ row', async () => {
    granted = new Set(['customers.create']);
    renderHarness();

    typeInCode('abc');
    expect(await screen.findByText(/Thêm mới đối tượng "abc"/)).toBeInTheDocument();
    expect(screen.queryByText('Xem danh mục đầy đủ')).toBeNull();
  });

  it('disabled (read-only voucher) has no +, no F4 and no rows', () => {
    renderHarness({ disabled: true });

    expect(addButton()).toBeNull();
    fireEvent.keyDown(codeInput(), { key: 'F4' });
    expect(screen.queryByRole('dialog')).toBeNull();
    expect(screen.queryByText('Xem danh mục đầy đủ')).toBeNull();
  });

  it('partner type None keeps the + button but disabled, and F4 does nothing', () => {
    renderHarness({ partnerType: 'None' });

    expect(addButton()).toBeDisabled();
    fireEvent.keyDown(codeInput(), { key: 'F4' });
    expect(screen.queryByRole('dialog')).toBeNull();
  });

  it('F4 opens the quick add with the trimmed typed text; the supplier box is disabled without suppliers.create', async () => {
    granted = new Set(['customers.create']);
    renderHarness();

    typeInCode('  abc ');
    fireEvent.keyDown(codeInput(), { key: 'F4' });

    expect(await screen.findByText('Thêm nhanh đối tượng')).toBeInTheDocument();
    expect(dialog().getByLabelText('Tên *')).toHaveValue('abc');
    expect(dialog().getByLabelText('Là nhà cung cấp')).toBeDisabled();
  });

  it('F4 with a picked value opens the quick add with an empty name', async () => {
    renderHarness({ value: { id: 'x', code: 'KH01', name: 'Công ty ABC' } });

    codeInput().focus();
    fireEvent.keyDown(codeInput(), { key: 'F4' });

    expect(await screen.findByText('Thêm nhanh đối tượng')).toBeInTheDocument();
    expect(dialog().getByLabelText('Tên *')).toHaveValue('');
  });

  it('a created partner is selected, the dialog closes and the focus lands on the name input', async () => {
    const user = userEvent.setup();
    const { onSelect } = renderHarness();

    typeInCode('Công ty An Phát');
    fireEvent.keyDown(codeInput(), { key: 'F4' });
    await user.click(await screen.findByRole('button', { name: 'Tạo mới' }));

    await waitFor(() => expect(onSelect).toHaveBeenCalledTimes(1));
    expect(onSelect).toHaveBeenCalledWith(expect.objectContaining({ code: 'KH0009', isCustomer: true, isSupplier: false }));
    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull());
    await waitFor(() => expect(nameInput()).toHaveFocus());
  });

  it('ignores a partner created after the user cancelled while it was saving', async () => {
    const user = userEvent.setup();
    let resolve!: (c: Customer) => void;
    createCustomerFn.mockReturnValue(new Promise<Customer>((r) => (resolve = r)));
    const { onSelect } = renderHarness();

    typeInCode('Công ty An Phát');
    fireEvent.keyDown(codeInput(), { key: 'F4' });
    await user.click(await screen.findByRole('button', { name: 'Tạo mới' }));
    await waitFor(() => expect(createCustomerFn).toHaveBeenCalledTimes(1));
    await user.keyboard('{Escape}');
    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull());
    await act(async () => {
      resolve(record());
    });

    expect(onSelect).not.toHaveBeenCalled();
    expect(codeInput()).toHaveValue('Công ty An Phát');
  });

  it('a save that finishes after the dialog was cancelled and reopened is not picked into the second open', async () => {
    const user = userEvent.setup();
    let resolve!: (c: Customer) => void;
    createCustomerFn.mockReturnValue(new Promise<Customer>((r) => (resolve = r)));
    const { onSelect } = renderHarness();

    typeInCode('Công ty An Phát');
    fireEvent.keyDown(codeInput(), { key: 'F4' });
    await user.click(await screen.findByRole('button', { name: 'Tạo mới' }));
    await waitFor(() => expect(createCustomerFn).toHaveBeenCalledTimes(1));
    await user.keyboard('{Escape}');
    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull());
    await waitFor(() => expect(codeInput()).toHaveFocus());

    fireEvent.keyDown(codeInput(), { key: 'F4' });
    await screen.findByRole('dialog');
    await waitFor(() => expect(dialog().getByLabelText('Tên *')).toHaveFocus());
    await act(async () => {
      resolve(record());
    });

    expect(onSelect).not.toHaveBeenCalled();
    expect(screen.getByRole('dialog')).toBeInTheDocument();
    expect(screen.getByRole('dialog')).toContainElement(document.activeElement as HTMLElement);
  });

  it('ignores a catalog row whose details finish loading after the user cancelled the catalog', async () => {
    const user = userEvent.setup();
    let resolve!: (c: Customer) => void;
    customerGet.mockReturnValue(new Promise<Customer>((r) => (resolve = r)));
    const { onSelect } = renderHarness();

    typeInCode('abc');
    await user.click(await screen.findByText('Xem danh mục đầy đủ'));
    await user.click(await dialog().findByText('Công ty ABC'));
    await waitFor(() => expect(customerGet).toHaveBeenCalledTimes(1));
    await user.keyboard('{Escape}');
    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull());
    await act(async () => {
      resolve(record({ id: 'id-1', code: 'KH-001', name: 'Công ty ABC' }));
    });

    expect(onSelect).not.toHaveBeenCalled();
    await waitFor(() => expect(codeInput()).toHaveFocus());
    expect(codeInput()).toHaveValue('abc');
  });

  it('after a pick from the catalog, a later cancelled quick add returns the focus to the code input', async () => {
    const user = userEvent.setup();
    renderHarness();

    typeInCode('abc');
    await user.click(await screen.findByText('Xem danh mục đầy đủ'));
    await user.click(await dialog().findByText('Công ty ABC'));
    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull());
    await waitFor(() => expect(nameInput()).toHaveFocus());

    codeInput().focus();
    fireEvent.keyDown(codeInput(), { key: 'F4' });
    await screen.findByRole('dialog');
    await user.keyboard('{Escape}');

    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull());
    await waitFor(() => expect(codeInput()).toHaveFocus());
  });

  it.each([
    ['Esc', async (user: ReturnType<typeof userEvent.setup>) => user.keyboard('{Escape}')],
    ['Hủy', async (user: ReturnType<typeof userEvent.setup>) => user.click(screen.getByRole('button', { name: 'Hủy' }))],
  ])('cancelling the quick add with %s keeps the typed text and focuses the code input', async (_name, cancel) => {
    const user = userEvent.setup();
    const { onSelect } = renderHarness();

    typeInCode('abc');
    fireEvent.keyDown(codeInput(), { key: 'F4' });
    await screen.findByRole('dialog');
    await cancel(user);

    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull());
    expect(codeInput()).toHaveValue('abc');
    await waitFor(() => expect(codeInput()).toHaveFocus());
    expect(onSelect).not.toHaveBeenCalled();
  });

  it('the catalog row opens the catalog with the typed text; a chosen row is selected and the name is focused', async () => {
    const user = userEvent.setup();
    const { onSelect } = renderHarness();

    typeInCode('abc');
    await user.click(await screen.findByText('Xem danh mục đầy đủ'));

    expect(await screen.findByText('Danh mục khách hàng')).toBeInTheDocument();
    expect(dialog().getByRole('textbox')).toHaveValue('abc');
    await user.click(dialog().getByText('Công ty ABC'));

    await waitFor(() => expect(onSelect).toHaveBeenCalledTimes(1));
    expect(customerGet).toHaveBeenCalledWith('id-1');
    expect(onSelect).toHaveBeenCalledWith(
      expect.objectContaining({ id: 'id-1', code: 'KH-001', isCustomer: true, isSupplier: true, creditDays: 30 }),
    );
    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull());
    await waitFor(() => expect(nameInput()).toHaveFocus());
  });

  it('Esc in the catalog returns the focus to the code input', async () => {
    const user = userEvent.setup();
    renderHarness();

    typeInCode('abc');
    await user.click(await screen.findByText('Xem danh mục đầy đủ'));
    await screen.findByRole('dialog');
    await user.keyboard('{Escape}');

    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull());
    await waitFor(() => expect(codeInput()).toHaveFocus());
    expect(codeInput()).toHaveValue('abc');
  });

  it.each(['quick add', 'catalog'])('after a %s pick the list is closed, and clearing the value leaves a clean field', async (via) => {
    const user = userEvent.setup();
    const { onUnpickedChange } = renderHarness();

    typeInCode('abc');
    if (via === 'quick add') {
      fireEvent.keyDown(codeInput(), { key: 'F4' });
      await user.click(await screen.findByRole('button', { name: 'Tạo mới' }));
    } else {
      await user.click(await screen.findByText('Xem danh mục đầy đủ'));
      await user.click(await screen.findByText('Công ty ABC'));
    }

    await waitFor(() => expect(codeInput()).toHaveValue(via === 'quick add' ? 'KH0009' : 'KH-001'));
    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull());
    expect(screen.queryByRole('listbox')).toBeNull();

    await user.click(screen.getByRole('button', { name: 'reset' }));

    expect(codeInput()).toHaveValue('');
    expect(screen.queryByRole('listbox')).toBeNull();
    expect(onUnpickedChange).toHaveBeenLastCalledWith(false);
  });

  it('a full quick add by keyboard never submits the voucher form nor reaches its key handler', async () => {
    const user = userEvent.setup();
    const { onSelect, outerSubmit, outerKey } = renderHarness({}, true);

    await user.type(codeInput(), 'Công ty An Phát');
    await user.keyboard('{F4}');
    await screen.findByRole('dialog');
    await waitFor(() => expect(dialog().getByLabelText('Tên *')).toHaveFocus());
    outerKey.mockClear();

    await user.keyboard('{Enter}{Enter}{Enter}');
    fireEvent.keyDown(document.activeElement as Element, { key: 's', ctrlKey: true });
    await user.keyboard('{Control>}{Enter}{/Control}');

    await waitFor(() => expect(onSelect).toHaveBeenCalledTimes(1));
    expect(outerSubmit).not.toHaveBeenCalled();
    expect(outerKey).not.toHaveBeenCalled();
  });

  it('Enter and Ctrl+S in the catalog search box never submit the voucher form nor reach its key handler', async () => {
    const user = userEvent.setup();
    const { onSelect, outerSubmit, outerKey } = renderHarness({}, true);

    typeInCode('abc');
    await user.click(await screen.findByText('Xem danh mục đầy đủ'));
    const search = await dialog().findByRole('textbox');
    await dialog().findByText('Công ty ABC');
    outerKey.mockClear();

    fireEvent.keyDown(search, { key: 's', ctrlKey: true });
    fireEvent.keyDown(search, { key: 'Enter' });

    await waitFor(() => expect(onSelect).toHaveBeenCalledTimes(1));
    expect(outerSubmit).not.toHaveBeenCalled();
    expect(outerKey).not.toHaveBeenCalled();
  });

  it('Enter on the close button of the quick add does not reach the voucher form', async () => {
    const user = userEvent.setup();
    const { outerKey } = renderHarness({}, true);

    typeInCode('abc');
    fireEvent.keyDown(codeInput(), { key: 'F4' });
    const close = await dialog().findByRole('button', { name: 'Đóng' });
    outerKey.mockClear();
    close.focus();
    await user.keyboard('{Enter}');

    expect(outerKey).not.toHaveBeenCalled();
  });

  it('scope customer: + needs customers.create, the quick add is titled for customers, the catalog needs customers.view', async () => {
    granted = new Set(['suppliers.create', 'suppliers.view']);
    renderHarness({ scope: 'customer', label: 'Khách hàng' });
    expect(addButton()).toBeNull();
    typeInCode('abc');
    await screen.findByRole('listbox');
    expect(screen.queryByText(/Thêm mới khách hàng/)).toBeNull();
    expect(screen.queryByText('Xem danh mục đầy đủ')).toBeNull();
    cleanup();

    granted = new Set(['customers.create', 'customers.view']);
    renderHarness({ scope: 'customer', label: 'Khách hàng' });
    expect(addButton()).toBeEnabled();
    typeInCode('abc');
    expect(await screen.findByText('Xem danh mục đầy đủ')).toBeInTheDocument();
    fireEvent.keyDown(codeInput(), { key: 'F4' });
    expect(await screen.findByText('Thêm nhanh khách hàng')).toBeInTheDocument();
  });
});
