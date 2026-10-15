import { useState } from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { useForm } from 'react-hook-form';
import { StockVoucherMainFields, type StockVoucherMainFieldsProps } from './stock-voucher-main-fields';
import { STOCK_VOUCHER_LABELS } from '../labels';
import type { StockVoucherFormParsed, StockVoucherFormValues } from '@/features/stock-vouchers/schema';
import type { StockVoucherDraftPartner } from '@/features/stock-vouchers/use-stock-voucher-draft';
import type { PartnerSearchItem } from '@/features/stock-vouchers/types';
import type { StockReason } from '@/features/stock-reasons/types';

const usePartnerSearchMock = vi.fn();
vi.mock('@/features/stock-vouchers/hooks', () => ({
  usePartnerSearch: (...args: unknown[]) => usePartnerSearchMock(...args),
}));

vi.mock('@/stores/auth-store', () => ({
  useAuthStore: (selector: (s: { hasPermission: () => boolean }) => unknown) => selector({ hasPermission: () => true }),
}));

const supplier: PartnerSearchItem = {
  id: '77777777-7777-4777-8777-777777777777',
  code: 'NCC01',
  name: 'Công ty Thép Việt',
  status: 'Active',
  isCustomer: false,
  isSupplier: true,
};

const R_SUPPLIER: StockReason = {
  id: 'r-1', code: 'NMH', name: 'Nhập mua hàng', direction: 'In', partnerType: 'Supplier', debtEffect: 'PayableIncrease', isSystem: true,
};
const R_NONE: StockReason = {
  id: 'r-2', code: 'NKH', name: 'Nhập khác', direction: 'In', partnerType: 'None', debtEffect: 'None', isSystem: true,
};

interface HarnessProps extends Partial<StockVoucherMainFieldsProps> {
  initialPartner?: StockVoucherDraftPartner | null;
}

// Holds the form and the picked partner like the voucher page does; everything else comes from the props.
function Harness({ initialPartner = null, ...overrides }: HarnessProps) {
  const form = useForm<StockVoucherFormValues, unknown, StockVoucherFormParsed>({
    defaultValues: { voucherAt: '', partnerName: '', partnerAddress: '' } as StockVoucherFormValues,
  });
  const [partner, setPartner] = useState(initialPartner);
  const props: StockVoucherMainFieldsProps = {
    form,
    type: 'In',
    labels: STOCK_VOUCHER_LABELS.In,
    readOnly: false,
    warehouses: [],
    reasons: [R_SUPPLIER, R_NONE],
    paymentMethods: [],
    warehouseId: '',
    reasonId: R_SUPPLIER.id,
    selectedReason: R_SUPPLIER,
    selectedPartner: partner,
    onWarehouseChange: vi.fn(),
    onReasonChange: vi.fn(),
    onSelectPartner: (p) => setPartner(p ? { id: p.id, code: p.code, name: p.name } : null),
    onUnpickedPartnerChange: vi.fn(),
    onEnterGrid: vi.fn(),
    ...overrides,
  };
  // A <form> around the fields like the voucher page.
  return (
    <form>
      <StockVoucherMainFields {...props} />
    </form>
  );
}

function byId(id: string): HTMLInputElement {
  const el = document.getElementById(id);
  if (!el) throw new Error(`#${id} not found`);
  return el as HTMLInputElement;
}

function enter(id: string, init: Partial<KeyboardEventInit> = {}) {
  fireEvent.keyDown(byId(id), { key: 'Enter', ...init });
}

describe('StockVoucherMainFields', () => {
  beforeEach(() => {
    usePartnerSearchMock.mockReset();
    usePartnerSearchMock.mockReturnValue({ data: [supplier], isFetching: false, isLoading: false, isError: false });
  });

  describe('row 2', () => {
    it('has one Đối tượng label for the code input and no separate code or name labels', () => {
      render(<Harness />);

      expect(screen.getByLabelText('Đối tượng')).toBe(byId('stock-partner'));
      expect(screen.queryByText('Mã đối tượng')).toBeNull();
      expect(screen.queryByText('Tên đối tượng')).toBeNull();
    });

    it('puts the code and name inputs in one .partner-code-name container inside a form-cols-stock-partner row', () => {
      render(<Harness />);

      const container = byId('stock-partner').closest('.partner-code-name');
      expect(container).not.toBeNull();
      expect(container).toContainElement(byId('stock-partner-name'));
      const row = container?.closest('.form-inline-grid');
      expect(row).toHaveClass('form-cols-stock-partner');
      expect(row).not.toHaveClass('form-cols-stock-main-2');
    });

    it('renames the address label to Đ.chỉ, still pointing at stock-partner-address', () => {
      render(<Harness />);

      const label = screen.getByText('Đ.chỉ');
      expect(label).toHaveAttribute('for', 'stock-partner-address');
      expect(screen.getByLabelText('Đ.chỉ')).toBe(byId('stock-partner-address'));
      expect(screen.queryByText('Địa chỉ')).toBeNull();
    });

    it('renders partnerError under the partner row', () => {
      render(<Harness partnerError="Đối tượng không hợp lệ" />);

      const message = screen.getByText('Đối tượng không hợp lệ');
      expect(message).toHaveClass('text-destructive');
      expect(byId('stock-partner')).toHaveAttribute('aria-describedby', message.id);
    });
  });

  describe('partner search', () => {
    it.each(['In', 'Out'] as const)('searches customers and suppliers, active only, for a stock-%s voucher', async (type) => {
      render(<Harness type={type} selectedReason={undefined} reasonId="" />);

      fireEvent.change(byId('stock-partner'), { target: { value: 'cong' } });

      await waitFor(() => expect(usePartnerSearchMock).toHaveBeenLastCalledWith(type, 'cong', undefined, true));
    });

    it('does not pass the reason to the search', async () => {
      render(<Harness />);

      fireEvent.change(byId('stock-partner'), { target: { value: 'cong' } });

      await waitFor(() => expect(usePartnerSearchMock).toHaveBeenLastCalledWith('In', 'cong', undefined, true));
      for (const call of usePartnerSearchMock.mock.calls) expect(call[2]).toBeUndefined();
    });

    it('picking a partner calls onSelectPartner and puts the focus on the partner name', async () => {
      const onSelectPartner = vi.fn();
      render(
        <Harness
          onSelectPartner={onSelectPartner}
          initialPartner={null}
        />,
      );

      fireEvent.change(byId('stock-partner'), { target: { value: 'NCC' } });
      fireEvent.mouseDown(within(await screen.findByRole('listbox')).getByText('NCC01'));

      expect(onSelectPartner).toHaveBeenCalledTimes(1);
      expect(onSelectPartner).toHaveBeenCalledWith(supplier);
      await waitFor(() => expect(byId('stock-partner-name')).toHaveFocus());
    });
  });

  describe('Enter order', () => {
    it('walks the main fields and calls onEnterGrid once after the address', () => {
      const onEnterGrid = vi.fn();
      render(<Harness onEnterGrid={onEnterGrid} />);
      byId('stock-voucher-at').focus();

      const steps: [string, string][] = [
        ['stock-voucher-at', 'stock-reason'],
        ['stock-reason', 'stock-warehouse'],
        ['stock-warehouse', 'stock-payment-method'],
        ['stock-payment-method', 'stock-partner'],
        ['stock-partner', 'stock-partner-name'],
        ['stock-partner-name', 'stock-partner-address'],
      ];
      for (const [from, to] of steps) {
        enter(from);
        expect(document.activeElement).toBe(byId(to));
      }
      expect(onEnterGrid).not.toHaveBeenCalled();

      enter('stock-partner-address');

      expect(onEnterGrid).toHaveBeenCalledTimes(1);
    });

    it('Shift+Enter does nothing', () => {
      const onEnterGrid = vi.fn();
      render(<Harness onEnterGrid={onEnterGrid} />);
      byId('stock-reason').focus();

      enter('stock-reason', { shiftKey: true });

      expect(document.activeElement).toBe(byId('stock-reason'));
      expect(onEnterGrid).not.toHaveBeenCalled();
    });

    it('skips a locked partner code (reason without a partner)', () => {
      render(<Harness selectedReason={R_NONE} reasonId={R_NONE.id} />);
      expect(byId('stock-partner')).toBeDisabled();

      enter('stock-payment-method');

      expect(document.activeElement).toBe(byId('stock-partner-name'));
    });
  });

  describe('reason cell', () => {
    it('renders an info notice as an amber status line under the reason select', () => {
      render(<Harness reasonNotice={{ text: 'Đã đổi lý do cho hợp với NCC01 (nhà cung cấp).', tone: 'info' }} />);

      const notice = screen.getByRole('status');
      expect(notice).toHaveTextContent('Đã đổi lý do cho hợp với NCC01 (nhà cung cấp).');
      expect(notice).toHaveClass('text-amber-700');
      expect(notice.parentElement).toContainElement(byId('stock-reason'));
    });

    it('renders an error notice in the destructive colour, not as a status', () => {
      render(<Harness reasonNotice={{ text: 'Lý do "Xuất bán hàng" cần khách hàng — NCC01 không phải khách hàng', tone: 'error' }} />);

      const notice = screen.getByText(/cần khách hàng/);
      expect(notice).toHaveClass('text-destructive');
      expect(notice).not.toHaveClass('text-amber-700');
      expect(screen.queryByRole('status')).toBeNull();
      expect(notice.parentElement).toContainElement(byId('stock-reason'));
    });

    it('renders no notice when there is none', () => {
      render(<Harness reasonNotice={null} />);

      expect(screen.queryByRole('status')).toBeNull();
    });

    it('reasonFlash adds ring-amber-400 to the reason select', () => {
      const { rerender } = render(<Harness reasonFlash />);
      expect(byId('stock-reason')).toHaveClass('ring-amber-400');

      rerender(<Harness reasonFlash={false} />);
      expect(byId('stock-reason')).not.toHaveClass('ring-amber-400');
    });

    it('still renders errors.reasonId under the reason select', async () => {
      function WithReasonError() {
        const form = useForm<StockVoucherFormValues, unknown, StockVoucherFormParsed>({
          defaultValues: { voucherAt: '', partnerName: '', partnerAddress: '' } as StockVoucherFormValues,
        });
        return (
          <form>
            <button type="button" onClick={() => form.setError('reasonId', { type: 'server', message: 'Chọn lý do' })}>
              set error
            </button>
            <StockVoucherMainFields
              form={form}
              type="In"
              labels={STOCK_VOUCHER_LABELS.In}
              readOnly={false}
              warehouses={[]}
              reasons={[R_SUPPLIER]}
              paymentMethods={[]}
              warehouseId=""
              reasonId=""
              selectedPartner={null}
              onWarehouseChange={vi.fn()}
              onReasonChange={vi.fn()}
              onSelectPartner={vi.fn()}
              onUnpickedPartnerChange={vi.fn()}
              onEnterGrid={vi.fn()}
            />
          </form>
        );
      }
      render(<WithReasonError />);

      fireEvent.click(screen.getByRole('button', { name: 'set error' }));

      const message = await screen.findByText('Chọn lý do', { selector: 'p' });
      expect(message.parentElement).toContainElement(byId('stock-reason'));
    });
  });

  describe('read-only', () => {
    it('has no + button', () => {
      const { unmount } = render(<Harness />);
      expect(screen.getByRole('button', { name: /Thêm nhanh/ })).toBeInTheDocument();
      unmount();

      render(<Harness readOnly />);

      expect(screen.queryByRole('button', { name: /Thêm nhanh/ })).toBeNull();
    });
  });
});
