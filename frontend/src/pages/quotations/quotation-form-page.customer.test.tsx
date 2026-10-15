import React from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import type * as ReactRouterDom from 'react-router-dom';
import type * as LineItemsGridModule from './components/line-items-grid';
import type * as CustomerHooks from '@/features/customers/hooks';
import type * as CustomerApi from '@/features/customers/api';
import type * as SupplierHooks from '@/features/suppliers/hooks';
import { QuotationFormPage } from './quotation-form-page';
import { readQuotationDraft, writeQuotationDraft } from '@/features/quotations/use-quotation-draft';
import type { QuotationFormValues } from '@/features/quotations/schema';
import type { Quotation } from '@/features/quotations/types';
import type { Customer, CustomerListItem, CustomerSearchItem, PagedResult } from '@/features/customers/types';

// ---- mocks ---------------------------------------------------------------------------------------------------

const routeParams: { id: string } = { id: 'new' };
const navigateMock = vi.fn();
vi.mock('react-router-dom', async () => {
  const actual = await vi.importActual<typeof ReactRouterDom>('react-router-dom');
  return {
    ...actual,
    useParams: () => routeParams,
    useNavigate: () => navigateMock,
    Link: ({ children, to, ...rest }: { children: React.ReactNode; to: string }) =>
      React.createElement('a', { href: String(to), ...rest }, children),
  };
});

// hasPermission reads the set per case.
const permissionState: { granted: Set<string> } = { granted: new Set() };
const hasPermissionMock = (p: string) => permissionState.granted.has(p);
vi.mock('@/stores/auth-store', () => ({
  useAuthStore: Object.assign(
    (selector: (s: { user: { id: string }; permissions: string[]; hasPermission: (p: string) => boolean }) => unknown) =>
      selector({ user: { id: 'user-test' }, permissions: [], hasPermission: hasPermissionMock }),
    { getState: () => ({ user: { id: 'user-test' }, hasPermission: hasPermissionMock }) },
  ),
}));

const createQuotationMock = vi.fn();
const quotationState: { data: Quotation | undefined } = { data: undefined };
const mutation = (fn: ReturnType<typeof vi.fn>) => ({ mutateAsync: fn, isPending: false, isError: false, error: null });
vi.mock('@/features/quotations/hooks', () => ({
  useQuotation: () => ({ data: quotationState.data, isLoading: false }),
  useCreateQuotation: () => mutation(createQuotationMock),
  useUpdateQuotation: () => mutation(vi.fn()),
  useTransitionQuotation: () => mutation(vi.fn()),
  useCloneQuotation: () => ({ mutateAsync: vi.fn(), isPending: false }),
  useQuotationActivities: () => ({ data: [], isLoading: false, isError: false, error: null, refetch: vi.fn() }),
}));

// The page renders without a QueryClientProvider: the customer search, the create hook and the catalog list are
// mocks, the other exports of the module stay real.
const customerState: { data: Customer | undefined } = { data: undefined };
const searchResults: { data: CustomerSearchItem[] } = { data: [] };
const customerSearchMock = vi.fn();
const createCustomerMock = vi.fn();
const customerGetMock = vi.fn();
const catalogRow: CustomerListItem = {
  id: 'cat-1', code: 'KH-001', name: 'Công ty ABC', taxCode: '0100000001', group: 'Company', status: 'Active',
  isCustomer: true, isSupplier: false,
};
const paged = (item: CustomerListItem): PagedResult<CustomerListItem> => ({
  items: [item], page: 1, pageSize: 20, totalItems: 1, totalPages: 1, hasNextPage: false, hasPreviousPage: false,
});
vi.mock('@/features/customers/hooks', async (importOriginal) => ({
  ...(await importOriginal<typeof CustomerHooks>()),
  useCustomer: () => ({ data: customerState.data }),
  useCustomersSearch: (keyword: string, options: unknown) => {
    customerSearchMock(keyword, options);
    return { data: keyword ? searchResults.data : [], isFetching: false, isLoading: false, isError: false };
  },
  useCreateCustomer: () => ({
    mutateAsync: createCustomerMock, isPending: false, isError: false, error: null, reset: vi.fn(),
  }),
  useCustomers: () => ({ data: paged(catalogRow), isLoading: false }),
}));
// The quick add dialog always mounts the supplier create hook too, whatever the scope.
vi.mock('@/features/suppliers/hooks', async (importOriginal) => ({
  ...(await importOriginal<typeof SupplierHooks>()),
  useCreateSupplier: () => ({ mutateAsync: vi.fn(), isPending: false, isError: false, error: null, reset: vi.fn() }),
}));
vi.mock('@/features/customers/api', async (importOriginal) => {
  const actual = await importOriginal<typeof CustomerApi>();
  return { ...actual, customersApi: { ...actual.customersApi, get: (...args: unknown[]) => customerGetMock(...args) } };
});

vi.mock('@/components/auth/can', () => ({
  Can: ({ children }: { children: React.ReactNode }) => React.createElement(React.Fragment, null, children),
}));

// LineItemsGrid uses getBoundingClientRect and complex grid navigation (jsdom has no layout), and TotalsPanel
// depends on computed totals: both are stubbed, the customer row is what is under test.
vi.mock('./components/line-items-grid', async (importOriginal) => {
  const { forwardRef, createElement } = await import('react');
  const actual = await importOriginal<typeof LineItemsGridModule>();
  return {
    ...actual,
    LineItemsGrid: forwardRef((_props: unknown, _ref: unknown) => createElement('div', { 'data-testid': 'line-items-grid' })),
  };
});
vi.mock('./components/totals-panel', () => ({
  TotalsPanel: () => React.createElement('div', { 'data-testid': 'totals-panel' }),
}));

// ---- fixtures ------------------------------------------------------------------------------------------------

const CUST_ID = '99999999-9999-4999-8999-999999999999';
const CUST_NEW_ID = '88888888-8888-4888-8888-888888888888';
const QUOTATION_ID = '77777777-7777-4777-8777-777777777777';

const found: CustomerSearchItem = {
  id: CUST_ID,
  code: 'KH01',
  name: 'Công ty An Phát',
  taxCode: '0300000001',
  companyAddress: '1 Lê Lợi',
  defaultShippingAddress: '2 Nguyễn Huệ',
  contactPerson: 'Anh Nam',
  phoneNumber: '0901234567',
  status: 'Active',
};

const VALID_VALUES: QuotationFormValues = {
  customerId: CUST_ID,
  customerName: 'Tên trong nháp',
  quotationDate: '2026-05-29',
  deliveryAddress: '',
  deliveryRecipient: '',
  deliveryPhone: '',
  transportVehicleNumber: '',
  deliveryDate: '2026-05-30',
  deliveryNote: '',
  taxRate: 10,
  discount: 0,
  freight: 0,
  advancePayment: 0,
  internalNote: '',
  lines: [
    {
      _uiKey: 'key1',
      sortOrder: 0,
      productName: 'Hàng nháp A',
      unitName: 'Cái',
      pricingMode: 'PerUnit',
      quantity: 2,
      unitPrice: 50000,
    },
  ],
};

// A draft the form can be saved from: a picked customer and one valid line.
function seedValidDraft() {
  writeQuotationDraft('user-test', VALID_VALUES, { id: CUST_ID, code: 'KH77', name: 'Công ty nháp' });
}

function customerRecord(over: Partial<Customer> = {}): Customer {
  return {
    id: CUST_NEW_ID,
    code: 'KH9',
    name: 'Công ty mới',
    group: 'Company',
    status: 'Active',
    createdAt: '2026-10-01T00:00:00Z',
    isCustomer: true,
    isSupplier: false,
    ...over,
  };
}

const QUICK_ADD_IDS = [
  'code', 'name', 'taxCode', 'phoneNumber', 'email', 'group', 'creditDays', 'contactPerson', 'companyAddress',
  'defaultShippingAddress', 'note', 'role-customer', 'role-supplier',
];

function byId(id: string): HTMLInputElement {
  const el = document.getElementById(id);
  if (!el) throw new Error(`#${id} not found`);
  return el as HTMLInputElement;
}

function dialog() {
  return within(screen.getByRole('dialog'));
}

async function pickCustomer() {
  searchResults.data = [found];
  fireEvent.change(byId('customerId'), { target: { value: 'KH' } });
  fireEvent.mouseDown(within(await screen.findByRole('listbox')).getByText('KH01'));
  await waitFor(() => expect(byId('customerId').value).toBe('KH01'));
}

const saveButton = () => screen.getByRole('button', { name: 'Lưu tạm' });
const addButton = () => screen.queryByRole('button', { name: /Thêm nhanh/ });
const flush = () => new Promise((resolve) => setTimeout(resolve, 20));

describe('QuotationFormPage — customer row on the shared partner field', () => {
  beforeEach(() => {
    localStorage.clear();
    routeParams.id = 'new';
    quotationState.data = undefined;
    customerState.data = undefined;
    searchResults.data = [];
    permissionState.granted = new Set();
    navigateMock.mockReset();
    createQuotationMock.mockReset();
    createQuotationMock.mockResolvedValue({ id: QUOTATION_ID, code: 'BG0001' });
    customerSearchMock.mockReset();
    createCustomerMock.mockReset();
    createCustomerMock.mockResolvedValue(customerRecord());
    customerGetMock.mockReset();
  });

  describe('markup', () => {
    it('has one required Khách hàng label for #customerId and no Mã KH / Tên KH labels', () => {
      render(<QuotationFormPage />);

      const label = screen.getByText('Khách hàng', { selector: 'label' });
      expect(label).toHaveClass('required');
      expect(label).toHaveAttribute('for', 'customerId');
      expect(screen.getByLabelText('Khách hàng')).toBe(byId('customerId'));
      expect(screen.queryByText('Mã KH')).toBeNull();
      expect(screen.queryByText('Tên KH')).toBeNull();
    });

    it('gives the name input and the code input their placeholders', () => {
      render(<QuotationFormPage />);

      expect(byId('customerName')).toHaveAttribute('placeholder', 'Tên khách hàng');
      expect(byId('customerId')).toHaveAttribute('placeholder', 'Nhập mã / tên / MST / địa chỉ / SĐT...');
    });

    it('uses the single-pair row class, not the four-column customer row', () => {
      render(<QuotationFormPage />);

      const row = byId('customerId').closest('.form-inline-grid');
      expect(row).not.toHaveClass('form-cols-customer');
      // The label stays on the first line when the name input wraps below the code input.
      expect(row).toHaveClass('form-align-top');
      expect(byId('customerId').closest('.partner-code-name')).toContainElement(byId('customerName'));
    });

    it('renders no element with an id the quick add dialog uses', () => {
      render(<QuotationFormPage />);

      for (const id of QUICK_ADD_IDS) expect(document.getElementById(id), `#${id}`).toBeNull();
    });

    it('searches active customers only', async () => {
      render(<QuotationFormPage />);

      fireEvent.change(byId('customerId'), { target: { value: 'KH' } });

      await waitFor(() => expect(customerSearchMock).toHaveBeenLastCalledWith('KH', { activeOnly: true, limit: 20 }));
    });

    it('an inactive customer of a quotation in edit mode still shows the warning', () => {
      routeParams.id = QUOTATION_ID;
      quotationState.data = {
        id: QUOTATION_ID,
        code: 'BG0001',
        quotationDate: '2026-10-01',
        ownerUserId: 'user-test',
        isOwnerDeleted: false,
        canEdit: true,
        canClone: true,
        customerId: CUST_ID,
        customerName: 'Công ty cũ',
        subtotal: 0,
        discount: 0,
        freight: 0,
        taxRate: 10,
        taxAmount: 0,
        total: 0,
        advancePayment: 0,
        status: 'Draft',
        lines: [],
        createdAt: '2026-10-01T00:00:00Z',
      };
      customerState.data = customerRecord({ id: CUST_ID, code: 'KH01', name: 'Công ty cũ', status: 'Inactive' });

      render(<QuotationFormPage />);

      expect(screen.getByText('Khách hàng đã ngừng sử dụng')).toBeInTheDocument();
      expect(byId('customerId').value).toBe('KH01');
    });
  });

  describe('picking and clearing', () => {
    it('sets the customer, fills the empty delivery fields and puts the focus on the customer name', async () => {
      render(<QuotationFormPage />);
      // The save error of the empty customer goes away once a customer is picked.
      fireEvent.click(saveButton());
      expect(await screen.findByText('Chọn khách hàng')).toBeInTheDocument();

      await pickCustomer();

      await waitFor(() => expect(screen.queryByText('Chọn khách hàng')).toBeNull());
      expect(byId('customerName').value).toBe('Công ty An Phát');
      expect(byId('deliveryAddress').value).toBe('2 Nguyễn Huệ');
      expect(byId('deliveryRecipient').value).toBe('Anh Nam');
      expect(byId('deliveryPhone').value).toBe('0901234567');
      await waitFor(() => expect(byId('customerName')).toHaveFocus());
    });

    it('keeps delivery fields the user already filled', async () => {
      render(<QuotationFormPage />);
      fireEvent.change(byId('deliveryAddress'), { target: { value: 'Kho 5' } });
      fireEvent.change(byId('deliveryPhone'), { target: { value: '0999' } });

      await pickCustomer();

      expect(byId('deliveryAddress').value).toBe('Kho 5');
      expect(byId('deliveryPhone').value).toBe('0999');
      expect(byId('deliveryRecipient').value).toBe('Anh Nam');
    });

    it('✕ empties both the code and the name', async () => {
      render(<QuotationFormPage />);
      await pickCustomer();

      fireEvent.click(screen.getByRole('button', { name: 'Bỏ chọn đối tượng' }));

      await waitFor(() => expect(byId('customerId').value).toBe(''));
      expect(byId('customerName').value).toBe('');
    });

    it('leaving the name empty restores the catalog name', async () => {
      render(<QuotationFormPage />);
      await pickCustomer();
      fireEvent.change(byId('customerName'), { target: { value: '' } });

      fireEvent.blur(byId('customerName'));

      await waitFor(() => expect(byId('customerName').value).toBe('Công ty An Phát'));
    });
  });

  describe('Enter order', () => {
    it('Enter in the empty code goes to the name, then to the delivery address; Shift+Enter goes back', () => {
      render(<QuotationFormPage />);
      byId('customerId').focus();

      fireEvent.keyDown(byId('customerId'), { key: 'Enter' });
      expect(document.activeElement).toBe(byId('customerName'));
      fireEvent.keyDown(byId('customerName'), { key: 'Enter' });
      expect(document.activeElement).toBe(byId('deliveryAddress'));
      fireEvent.keyDown(byId('deliveryAddress'), { key: 'Enter', shiftKey: true });
      expect(document.activeElement).toBe(byId('customerName'));
      fireEvent.keyDown(byId('customerName'), { key: 'Enter', shiftKey: true });
      expect(document.activeElement).toBe(byId('customerId'));
      fireEvent.keyDown(byId('customerId'), { key: 'Enter', shiftKey: true });
      expect(document.activeElement).toBe(byId('deliveryDate'));
    });

    it('Enter in a picked code goes to the name', async () => {
      render(<QuotationFormPage />);
      await pickCustomer();
      byId('customerId').focus();

      fireEvent.keyDown(byId('customerId'), { key: 'Enter' });

      expect(document.activeElement).toBe(byId('customerName'));
    });

    it('the Enter of the autocomplete (typed text, no pick yet) does not walk to the next field', async () => {
      render(<QuotationFormPage />);
      byId('customerId').focus();
      fireEvent.change(byId('customerId'), { target: { value: 'zzz' } });

      fireEvent.keyDown(byId('customerId'), { key: 'Enter' });
      await flush();

      expect(document.activeElement).toBe(byId('customerId'));
    });

    it('Enter with the list open picks the customer and the focus ends on the name', async () => {
      render(<QuotationFormPage />);
      searchResults.data = [found];
      byId('customerId').focus();
      fireEvent.change(byId('customerId'), { target: { value: 'KH' } });
      await screen.findByRole('listbox');

      fireEvent.keyDown(byId('customerId'), { key: 'Enter' });

      await waitFor(() => expect(byId('customerId').value).toBe('KH01'));
      await waitFor(() => expect(byId('customerName')).toHaveFocus());
      expect(byId('deliveryAddress')).not.toHaveFocus();
    });

    it('a composing Enter (Telex/VNI) in the name, the address or a typed code keeps the focus', async () => {
      render(<QuotationFormPage />);
      byId('customerName').focus();
      fireEvent.keyDown(byId('customerName'), { key: 'Enter', isComposing: true });
      expect(document.activeElement).toBe(byId('customerName'));

      byId('deliveryAddress').focus();
      fireEvent.keyDown(byId('deliveryAddress'), { key: 'Enter', isComposing: true });
      expect(document.activeElement).toBe(byId('deliveryAddress'));

      byId('customerId').focus();
      fireEvent.change(byId('customerId'), { target: { value: 'zzz' } });
      fireEvent.keyDown(byId('customerId'), { key: 'Enter', isComposing: true });
      await flush();
      expect(document.activeElement).toBe(byId('customerId'));
    });
  });

  describe('quick add and catalog', () => {
    it('F4 and + need customers.create: a user with only suppliers.create gets neither', async () => {
      permissionState.granted = new Set(['suppliers.create']);
      render(<QuotationFormPage />);

      expect(addButton()).toBeNull();
      fireEvent.change(byId('customerId'), { target: { value: 'abc' } });
      fireEvent.keyDown(byId('customerId'), { key: 'F4' });
      expect(screen.queryByRole('dialog')).toBeNull();
    });

    it('with customers.create the dialog is titled for customers, has no role boxes and creates through the customer mock', async () => {
      permissionState.granted = new Set(['customers.create']);
      render(<QuotationFormPage />);
      fireEvent.change(byId('customerId'), { target: { value: 'Công ty mới' } });

      fireEvent.keyDown(byId('customerId'), { key: 'F4' });

      expect(await screen.findByText('Thêm nhanh khách hàng')).toBeInTheDocument();
      await waitFor(() => expect(byId('name')).toHaveFocus());
      expect(byId('name').value).toBe('Công ty mới');
      expect(document.getElementById('role-customer')).toBeNull();
      expect(document.getElementById('role-supplier')).toBeNull();

      fireEvent.keyDown(byId('name'), { key: 'Enter', ctrlKey: true });

      await waitFor(() => expect(createCustomerMock).toHaveBeenCalledTimes(1));
      expect(createCustomerMock).toHaveBeenCalledWith(expect.objectContaining({ name: 'Công ty mới', isCustomer: true }));
      await waitFor(() => expect(byId('customerId').value).toBe('KH9'));
      expect(byId('customerName').value).toBe('Công ty mới');
      await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull());
      await waitFor(() => expect(byId('customerName')).toHaveFocus());
      expect(createQuotationMock).not.toHaveBeenCalled();
    });

    it('the + button opens the same dialog', async () => {
      permissionState.granted = new Set(['customers.create']);
      render(<QuotationFormPage />);

      fireEvent.click(screen.getByRole('button', { name: /Thêm nhanh/ }));

      expect(await screen.findByText('Thêm nhanh khách hàng')).toBeInTheDocument();
    });

    it('Ctrl+S and Enter typed inside the open quick add dialog do not save the quotation', async () => {
      permissionState.granted = new Set(['customers.create']);
      seedValidDraft();
      render(<QuotationFormPage />);
      fireEvent.click(screen.getByRole('button', { name: /Thêm nhanh/ }));
      await screen.findByText('Thêm nhanh khách hàng');
      await waitFor(() => expect(byId('name')).toHaveFocus());

      fireEvent.keyDown(byId('name'), { key: 's', ctrlKey: true });
      fireEvent.keyDown(byId('name'), { key: 'Enter' });
      await flush();

      expect(createQuotationMock).not.toHaveBeenCalled();
      expect(screen.getByRole('dialog')).toBeInTheDocument();

      // The draft can be saved: the same key outside the dialog saves.
      fireEvent.click(dialog().getByRole('button', { name: 'Hủy' }));
      await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull());
      fireEvent.keyDown(byId('deliveryNote'), { key: 's', ctrlKey: true });
      await waitFor(() => expect(createQuotationMock).toHaveBeenCalledTimes(1));
    });

    it('a catalog pick sets the customer, fills only the empty delivery fields and focuses the name', async () => {
      permissionState.granted = new Set(['customers.view']);
      customerGetMock.mockResolvedValue(
        customerRecord({
          id: CUST_ID,
          code: 'KH-001',
          name: 'Công ty ABC',
          defaultShippingAddress: '2 Nguyễn Huệ',
          contactPerson: 'Anh Nam',
          phoneNumber: '0901234567',
        }),
      );
      render(<QuotationFormPage />);
      fireEvent.change(byId('deliveryPhone'), { target: { value: '0999' } });
      fireEvent.click(saveButton());
      expect(await screen.findByText('Chọn khách hàng')).toBeInTheDocument();
      fireEvent.change(byId('customerId'), { target: { value: 'abc' } });

      fireEvent.mouseDown(await screen.findByText('Xem danh mục đầy đủ'));
      fireEvent.click(await dialog().findByText('Công ty ABC'));

      await waitFor(() => expect(customerGetMock).toHaveBeenCalledWith('cat-1'));
      await waitFor(() => expect(byId('customerId').value).toBe('KH-001'));
      expect(byId('customerName').value).toBe('Công ty ABC');
      // customerId is set: its save error is gone.
      await waitFor(() => expect(screen.queryByText('Chọn khách hàng')).toBeNull());
      expect(byId('deliveryAddress').value).toBe('2 Nguyễn Huệ');
      expect(byId('deliveryRecipient').value).toBe('Anh Nam');
      expect(byId('deliveryPhone').value).toBe('0999');
      await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull());
      await waitFor(() => expect(byId('customerName')).toHaveFocus());
    });

    it('the catalog row needs customers.view', async () => {
      permissionState.granted = new Set(['customers.create']);
      const { unmount } = render(<QuotationFormPage />);
      fireEvent.change(byId('customerId'), { target: { value: 'abc' } });
      await screen.findByText(/Thêm mới khách hàng "abc"/);
      expect(screen.queryByText('Xem danh mục đầy đủ')).toBeNull();
      unmount();

      permissionState.granted = new Set(['customers.view']);
      render(<QuotationFormPage />);
      fireEvent.change(byId('customerId'), { target: { value: 'abc' } });

      expect(await screen.findByText('Xem danh mục đầy đủ')).toBeInTheDocument();
    });
  });

  describe('save guard', () => {
    it('typed text without a pick blocks the save, says so and focuses the code; clearing the text removes the message', async () => {
      render(<QuotationFormPage />);
      fireEvent.change(byId('customerId'), { target: { value: 'zzz' } });

      fireEvent.click(saveButton());

      expect(await screen.findByText('Vui lòng chọn khách hàng từ danh sách')).toBeInTheDocument();
      await waitFor(() => expect(byId('customerId')).toHaveFocus());
      expect(screen.queryByText('Chọn khách hàng')).toBeNull();
      expect(createQuotationMock).not.toHaveBeenCalled();

      fireEvent.change(byId('customerId'), { target: { value: '' } });
      expect(screen.queryByText('Vui lòng chọn khách hàng từ danh sách')).toBeNull();
    });

    it('an empty code still shows the zod customerId message', async () => {
      render(<QuotationFormPage />);

      fireEvent.click(saveButton());

      expect(await screen.findByText('Chọn khách hàng')).toBeInTheDocument();
      expect(screen.queryByText('Vui lòng chọn khách hàng từ danh sách')).toBeNull();
      expect(createQuotationMock).not.toHaveBeenCalled();
    });
  });

  describe('drafts', () => {
    it('a draft with selectedCustomer restores the code and the name', () => {
      seedValidDraft();

      render(<QuotationFormPage />);

      expect(byId('customerId').value).toBe('KH77');
      expect(byId('customerName').value).toBe('Tên trong nháp');
    });

    it('autosave still writes selectedCustomer', async () => {
      seedValidDraft();
      render(<QuotationFormPage />);

      fireEvent.change(byId('deliveryNote'), { target: { value: 'Giao buổi sáng' } });

      await waitFor(
        () => expect(readQuotationDraft('user-test')?.values.deliveryNote).toBe('Giao buổi sáng'),
        { timeout: 5000 },
      );
      expect(readQuotationDraft('user-test')?.selectedCustomer).toEqual({ id: CUST_ID, code: 'KH77', name: 'Công ty nháp' });
    });
  });
});
