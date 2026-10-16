import { act, fireEvent, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { useForm, type UseFormReturn } from 'react-hook-form';
import { LineItemsGrid } from './line-items-grid';
import type {
  QuotationFormParsed,
  QuotationFormValues,
  QuotationLineFormValues,
} from '@/features/quotations/schema';
import type { ProductSuggestion } from '@/features/products/types';
import { useAuthStore } from '@/stores/auth-store';

// The real typeahead, unlike line-items-grid.test.tsx: when the grid moves the caret into a filled Mã hàng, its list
// must stay closed, so the next Enter moves on instead of re-picking the product.

// Every search finds this product. A re-pick puts its name, SL 0 and its price on the row.
const catalogP1: ProductSuggestion = {
  id: '11111111-1111-1111-1111-111111111111',
  code: 'P1',
  name: 'Catalog P1',
  unitName: 'pcs',
  pricingMode: 'PerUnit',
  defaultPrice: 999,
  costPrice: 500,
  defaultTaxRate: 0,
  length: undefined,
  width: undefined,
  thickness: undefined,
  trackInventory: true,
  purchaseDiscountRate: 0,
  salesDiscountRate: 0,
  priceIncludesVat: false,
};

vi.mock('@/features/products/hooks', () => ({
  useProductSearch: () => ({
    data: [catalogP1],
    isLoading: false,
    isError: false,
    isFetching: false,
    settledQuery: undefined,
  }),
  useProducts: () => ({ data: { items: [], totalPages: 1, totalItems: 0 }, isLoading: false }),
  useProductGroups: () => ({ data: [] }),
  useProduct: () => ({ data: undefined, isLoading: false }),
}));
vi.mock('@/features/products/components/product-catalog-dialog', () => ({ ProductCatalogDialog: () => null }));
vi.mock('@/features/products/components/product-quick-add-dialog', () => ({ ProductQuickAddDialog: () => null }));

// Rows 'a', 'b', 'c' (indices 0-2). Row 'b' has the code P1, the same as catalogP1.
const lines: QuotationLineFormValues[] = [0, 1, 2].map((i) => ({
  _uiKey: ['a', 'b', 'c'][i],
  sortOrder: i,
  productCode: `P${i}`,
  productName: `Name${i}`,
  unitName: 'pcs',
  pricingMode: 'PerUnit',
  quantity: 5,
  unitPrice: 123,
}));

function setCostPermission(enabled: boolean) {
  useAuthStore.setState({
    accessToken: 'token',
    expiresAt: '2099-01-01T00:00:00Z',
    user: {
      id: '11111111-1111-1111-1111-111111111111',
      username: 'tester',
      email: 'tester@example.com',
      fullName: 'Tester',
      roles: [],
      permissions: enabled ? ['quotations.view_cost'] : [],
    },
  });
}

function renderGrid() {
  const formRef: { current: UseFormReturn<QuotationFormValues, unknown, QuotationFormParsed> | null } = {
    current: null,
  };
  function Harness() {
    const form = useForm<QuotationFormValues, unknown, QuotationFormParsed>({
      defaultValues: {
        customerId: '11111111-1111-1111-1111-111111111111',
        customerName: '',
        quotationDate: '2026-05-13',
        deliveryAddress: '',
        deliveryRecipient: '',
        deliveryPhone: '',
        deliveryDate: '',
        deliveryNote: '',
        taxRate: 0,
        discount: 0,
        freight: 0,
        internalNote: '',
        lines,
      },
    });
    formRef.current = form;
    return <LineItemsGrid form={form} />;
  }
  render(<Harness />);
  return { form: () => formRef.current! };
}

function byId(id: string): HTMLElement {
  const el = document.getElementById(id);
  if (!el) throw new Error(`#${id} not found`);
  return el;
}

describe('LineItemsGrid with the real product typeahead', () => {
  beforeEach(() => {
    setCostPermission(false);
  });

  it.each([
    ['without quotations.view_cost', false, 'line-unit-price-0'],
    ['with quotations.view_cost', true, 'line-unit-cost-0'],
  ])("Enter from row 0's last cell into row 1's Mã hàng leaves its list closed and keeps row 1 (%s)", async (_name, cost, lastCell) => {
    setCostPermission(cost);
    const { form } = renderGrid();

    act(() => byId(lastCell).focus());
    fireEvent.keyDown(byId(lastCell), { key: 'Enter' });
    expect(document.activeElement).toBe(byId('line-product-code-1'));
    expect(screen.queryByRole('listbox')).toBeNull();

    fireEvent.keyDown(document.activeElement!, { key: 'Enter' });

    await waitFor(() => expect(document.activeElement).toBe(byId('line-name-1')));
    expect(form().getValues('lines.1')).toMatchObject({ productCode: 'P1', productName: 'Name1', quantity: 5, unitPrice: 123 });
  });

  it("Ctrl+Delete in row 0's Mã hàng moves the caret into the next Mã hàng with its list closed, and Enter keeps the row", async () => {
    const { form } = renderGrid();

    act(() => byId('line-product-code-0').focus());
    expect(screen.queryByRole('listbox')).toBeNull();
    fireEvent.keyDown(byId('line-product-code-0'), { key: 'Delete', ctrlKey: true });

    expect(form().getValues('lines.0._uiKey')).toBe('b');
    expect(document.activeElement).toBe(byId('line-product-code-0'));
    expect(screen.queryByRole('listbox')).toBeNull();

    fireEvent.keyDown(document.activeElement!, { key: 'Enter' });

    await waitFor(() => expect(document.activeElement).toBe(byId('line-name-0')));
    expect(form().getValues('lines.0')).toMatchObject({ productCode: 'P1', productName: 'Name1', quantity: 5, unitPrice: 123 });
  });

  it("a click on row 0's trash button moves the caret into the next Mã hàng with its list closed, and Enter keeps the row", async () => {
    const user = userEvent.setup();
    const { form } = renderGrid();

    await user.click(screen.getAllByRole('button', { name: 'Xóa dòng' })[0]);
    expect(document.activeElement).toBe(byId('line-product-code-0'));
    expect(screen.queryByRole('listbox')).toBeNull();

    fireEvent.keyDown(document.activeElement!, { key: 'Enter' });

    await waitFor(() => expect(document.activeElement).toBe(byId('line-name-0')));
    expect(form().getValues('lines.0')).toMatchObject({ productCode: 'P1', productName: 'Name1', quantity: 5, unitPrice: 123 });
  });
});
