import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { useForm } from 'react-hook-form';
import { LineItemsGrid } from './line-items-grid';
import type {
  QuotationFormParsed,
  QuotationFormValues,
  QuotationLineFormValues,
} from '@/features/quotations/schema';
import { useAuthStore } from '@/stores/auth-store';

const cellProps = vi.hoisted(() => ({ last: undefined as Record<string, unknown> | undefined }));

vi.mock('./product-typeahead-cell', () => ({
  ProductTypeaheadCell: (props: { value: string; onChange: (value: string) => void }) => {
    cellProps.last = props;
    return (
      <input
        aria-label="product-code"
        value={props.value}
        onChange={(e) => props.onChange((e.target as HTMLInputElement).value)}
      />
    );
  },
}));

const baseLines: QuotationLineFormValues[] = [
  {
    sortOrder: 0,
    productCode: 'A001',
    productName: 'Alpha',
    unitName: 'pcs',
    pricingMode: 'PerUnit',
    quantity: 2,
    unitPrice: 100,
    unitCost: 60,
  },
  {
    sortOrder: 1,
    productCode: 'B001',
    productName: 'Beta',
    unitName: 'pcs',
    pricingMode: 'PerUnit',
    quantity: 1,
    unitPrice: 50,
    unitCost: 30,
  },
];

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

function renderGrid(lines: QuotationLineFormValues[] = baseLines) {
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
    return <LineItemsGrid form={form} />;
  }

  return render(<Harness />);
}

describe('LineItemsGrid', () => {
  beforeEach(() => {
    setCostPermission(false);
  });

  it('turns on product quick add for the code cell and returns to the name cell', () => {
    renderGrid([baseLines[0]]);

    expect(cellProps.last?.allowQuickAdd).toBe(true);
    expect(cellProps.last?.nextFocusId).toBe('line-name-0');
  });

  it('renders existing rows and subtotal footer', () => {
    renderGrid();

    expect(screen.getByDisplayValue('Alpha')).toBeInTheDocument();
    expect(screen.getByDisplayValue('Beta')).toBeInTheDocument();
    expect(screen.getByText(/Tổng thành tiền bán:\s*250\s*₫/)).toBeInTheDocument();
    expect(screen.queryByText(/Tổng thành tiền nhập:/)).not.toBeInTheDocument();
    expect(screen.queryByText(/Tổng lợi nhuận:/)).not.toBeInTheDocument();
  });

  it('formats quantity values with comma groups and dot decimals', () => {
    renderGrid([
      { ...baseLines[0], quantity: 2 },
      { ...baseLines[1], quantity: 44545.45 },
    ]);

    expect(screen.getByDisplayValue('2')).toBeInTheDocument();
    expect(screen.getByDisplayValue('44,545.45')).toBeInTheDocument();
  });

  it('keeps dot decimal text while editing quantity', async () => {
    const user = userEvent.setup();
    renderGrid([{ ...baseLines[0], quantity: 1 }]);
    const quantityInput = screen.getByLabelText('Số lượng');

    await user.click(quantityInput);
    await user.clear(quantityInput);
    await user.type(quantityInput, '44545.45');
    expect(quantityInput).toHaveValue('44545.45');

    await user.tab();
    await waitFor(() => {
      expect(screen.getByLabelText('Số lượng')).toHaveValue('44,545.45');
    });
  });

  it('renders sales amount header and hides cost columns without cost permission', () => {
    renderGrid();

    expect(screen.getByRole('columnheader', { name: 'Đơn giá bán' })).toBeInTheDocument();
    expect(screen.getByRole('columnheader', { name: 'Thành tiền bán' })).toBeInTheDocument();
    expect(screen.queryByRole('columnheader', { name: 'Đơn giá nhập' })).not.toBeInTheDocument();
    expect(screen.queryByRole('columnheader', { name: 'Thành tiền nhập' })).not.toBeInTheDocument();
    expect(screen.queryByRole('columnheader', { name: 'Lợi nhuận' })).not.toBeInTheDocument();
  });

  it('renders cost and profit columns with cost permission', () => {
    setCostPermission(true);
    renderGrid();

    expect(screen.getByRole('columnheader', { name: 'Đơn giá nhập' })).toBeInTheDocument();
    expect(screen.getByRole('columnheader', { name: 'Thành tiền nhập' })).toBeInTheDocument();
    expect(screen.getByRole('columnheader', { name: 'Lợi nhuận' })).toBeInTheDocument();
    expect(screen.getAllByLabelText('Đơn giá nhập')).toHaveLength(2);
    expect(screen.getByText(/Tổng thành tiền nhập:\s*150\s*₫/)).toBeInTheDocument();
    expect(screen.getByText(/Tổng lợi nhuận:\s*100\s*₫/)).toBeInTheDocument();
  });

  it('adds a row when clicking add line', async () => {
    renderGrid([baseLines[0]]);

    fireEvent.click(screen.getByRole('button', { name: /thêm dòng/i }));

    await waitFor(() => {
      expect(screen.getAllByLabelText('product-code')).toHaveLength(2);
    });
  });

  it('removes a single row with row trash button', async () => {
    renderGrid();

    fireEvent.click(screen.getAllByRole('button', { name: /xóa dòng/i })[0]);

    await waitFor(() => {
      expect(screen.queryByDisplayValue('Alpha')).not.toBeInTheDocument();
      expect(screen.getByDisplayValue('Beta')).toBeInTheDocument();
    });
  });

  it('clears all rows after confirm dialog confirmation', async () => {
    renderGrid();

    fireEvent.click(screen.getByRole('button', { name: /xóa tất cả dòng/i }));
    expect(screen.getByText('Xóa tất cả dòng?')).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'Xóa' }));

    await waitFor(() => {
      expect(screen.getByText(/chưa có dòng nào/i)).toBeInTheDocument();
      expect(screen.getByRole('button', { name: /xóa tất cả dòng/i })).toBeDisabled();
    });
  });

  it('an Enter that commits an IME composition does not move the focus', () => {
    renderGrid();
    const unit = document.getElementById('line-unit-0') as HTMLElement;
    unit.focus();
    fireEvent.keyDown(unit, { key: 'Enter', isComposing: true, keyCode: 229 });
    expect(document.activeElement).toBe(unit);
    fireEvent.keyDown(unit, { key: 'Enter' });
    expect(document.activeElement).not.toBe(unit);
  });

  it('removes active row with Ctrl+Delete', async () => {
    renderGrid();
    const betaInput = screen.getByDisplayValue('Beta');

    fireEvent.focus(betaInput);
    fireEvent.keyDown(betaInput, { key: 'Delete', ctrlKey: true });

    await waitFor(() => {
      expect(screen.getByDisplayValue('Alpha')).toBeInTheDocument();
      expect(screen.queryByDisplayValue('Beta')).not.toBeInTheDocument();
    });
  });
});
