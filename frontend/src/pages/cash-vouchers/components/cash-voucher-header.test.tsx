import { beforeEach, describe, expect, it, vi } from 'vitest';
import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { useForm, type UseFormReturn } from 'react-hook-form';
import {
  CashVoucherHeader,
  type CashFundOption,
  type CashReasonOption,
  type CashVoucherHeaderProps,
} from './cash-voucher-header';
import { toFormDefaults } from '@/features/cash-vouchers/payload';
import type { CashVoucherFormParsed, CashVoucherFormValues } from '@/features/cash-vouchers/schema';
import { CASH_VOUCHER_LABELS } from '../labels';
import { CASH_HEADER_FIELD_IDS } from '../utils/cash-navigation';

vi.mock('@/stores/auth-store', () => ({
  useAuthStore: (selector: (s: { hasPermission: () => boolean }) => unknown) => selector({ hasPermission: () => true }),
}));

vi.mock('@/lib/use-toast', () => ({ toast: vi.fn() }));

const partnerSearchMock = vi.fn();
vi.mock('@/features/cash-vouchers/hooks', () => ({
  useCashPartnerSearch: (type: string, keyword: string, activeOnly?: boolean) => {
    partnerSearchMock(type, keyword, activeOnly);
    return { data: [], isFetching: false, isLoading: false, isError: false };
  },
}));

const createCustomerMock = vi.fn();
const createSupplierMock = vi.fn();
vi.mock('@/features/customers/hooks', () => ({
  useCreateCustomer: () => ({ mutateAsync: createCustomerMock, reset: vi.fn(), isPending: false, isError: false, error: null }),
}));
vi.mock('@/features/suppliers/hooks', () => ({
  useCreateSupplier: () => ({ mutateAsync: createSupplierMock, reset: vi.fn(), isPending: false, isError: false, error: null }),
}));

const FUND = '11111111-1111-4111-8111-111111111111';
const R_TKH = '22222222-2222-4222-8222-222222222221';
const R_TK = '22222222-2222-4222-8222-222222222222';
const R_THNCC = '22222222-2222-4222-8222-222222222225';

const funds: CashFundOption[] = [{ id: FUND, code: 'QTM', name: 'Quỹ tiền mặt', kind: 'Cash' }];
const reasons: CashReasonOption[] = [
  { id: R_TKH, name: 'Thu tiền khách hàng', partnerType: 'Customer', debtEffect: 'ReceivableDecrease' },
  { id: R_TK, name: 'Thu khác', partnerType: 'None', debtEffect: 'None' },
  { id: R_THNCC, name: 'Thu hoàn tiền NCC', partnerType: 'Supplier', debtEffect: 'PayableIncrease' },
];

type HeaderOverrides = Partial<Omit<CashVoucherHeaderProps, 'form'>>;

function renderHeader(overrides: HeaderOverrides = {}) {
  const handlers = {
    onSelectPartner: vi.fn(),
    onReasonChange: vi.fn(),
    onUnpickedPartnerChange: vi.fn(),
    onPartnerNameChange: vi.fn(),
    onDescriptionEdited: vi.fn(),
    onEnterGrid: vi.fn(),
  };
  const formRef: { current: UseFormReturn<CashVoucherFormValues, unknown, CashVoucherFormParsed> | null } = {
    current: null,
  };
  function Harness(props: HeaderOverrides) {
    const form = useForm<CashVoucherFormValues, unknown, CashVoucherFormParsed>({
      defaultValues: { ...toFormDefaults(undefined, undefined, 'Receipt'), fundId: FUND, reasonId: R_TKH },
    });
    formRef.current = form;
    return (
      <form>
        <CashVoucherHeader
          form={form}
          type="Receipt"
          labels={CASH_VOUCHER_LABELS.Receipt}
          funds={funds}
          reasons={reasons}
          selectedReason={reasons[0]}
          selectedPartner={null}
          readOnly={false}
          {...handlers}
          {...props}
        />
      </form>
    );
  }
  const view = render(<Harness {...overrides} />);
  return {
    ...handlers,
    form: () => formRef.current!,
    rerender: (next: HeaderOverrides) => view.rerender(<Harness {...overrides} {...next} />),
  };
}

function byId(id: string): HTMLInputElement {
  const el = document.getElementById(id);
  if (!el) throw new Error(`#${id} not found`);
  return el as HTMLInputElement;
}

const enter = (el: Element, init: KeyboardEventInit = {}) => fireEvent.keyDown(el, { key: 'Enter', ...init });
const enterOnActive = (init: KeyboardEventInit = {}) => enter(document.activeElement as Element, init);

describe('CashVoucherHeader layout (section-01 §1.1)', () => {
  beforeEach(() => partnerSearchMock.mockReset());

  it('has no MST input', () => {
    renderHeader();

    expect(screen.queryByLabelText(/MST/)).toBeNull();
    expect(document.getElementById('cash-partner-tax-code')).toBeNull();
  });

  it('puts the partner code and name in one container and the ten fields in Enter order', () => {
    renderHeader();

    const row = byId('cash-partner').closest('.partner-code-name');
    expect(row).not.toBeNull();
    expect(byId('cash-partner-name').closest('.partner-code-name')).toBe(row);
    for (let i = 1; i < CASH_HEADER_FIELD_IDS.length; i++) {
      const before = byId(CASH_HEADER_FIELD_IDS[i - 1]);
      const after = byId(CASH_HEADER_FIELD_IDS[i]);
      expect(before.compareDocumentPosition(after) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    }
  });

  it('puts Kèm theo before Ghi chú in the last row', () => {
    renderHeader();

    const row = byId('cash-attached-docs').closest('.form-cols-attached-note');
    expect(row).not.toBeNull();
    expect(row).toContainElement(byId('cash-voucher-note'));
    expect(byId('cash-attached-docs').compareDocumentPosition(byId('cash-voucher-note')) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    const rows = document.querySelectorAll('fieldset > .form-inline-grid');
    expect(rows[rows.length - 1]).toBe(row);
    expect(byId('cash-attached-docs')).toHaveAttribute('placeholder', 'Số chứng từ gốc');
  });

  it('puts the partner row on form-align-top so its label stays on the first line when the name wraps', () => {
    renderHeader();

    expect(byId('cash-partner').closest('.form-inline-grid')).toHaveClass('form-align-top');
  });

  it.each(['Receipt', 'Payment'] as const)('searches active partners of the %s voucher type without a reason', (type) => {
    renderHeader({ type, labels: CASH_VOUCHER_LABELS[type] });

    expect(partnerSearchMock).toHaveBeenLastCalledWith(type, expect.any(String), true);
  });
});

describe('CashVoucherHeader Enter walk (section-01 §2.1)', () => {
  it('Enter walks the ten fields; an empty code box goes on to the name', () => {
    renderHeader();

    byId('cash-voucher-at').focus();
    expect(enterOnActive()).toBe(false);
    expect(byId('cash-fund')).toHaveFocus();
    enterOnActive();
    expect(byId('cash-partner')).toHaveFocus();
    enterOnActive();
    expect(byId('cash-partner-name')).toHaveFocus();
    for (const id of CASH_HEADER_FIELD_IDS.slice(4)) {
      enterOnActive();
      expect(byId(id)).toHaveFocus();
    }
  });

  it('Shift+Enter walks back; Shift+Enter in Ngày giờ does nothing', () => {
    const { onEnterGrid } = renderHeader();

    byId('cash-voucher-note').focus();
    for (const id of [...CASH_HEADER_FIELD_IDS].reverse().slice(1)) {
      enterOnActive({ shiftKey: true });
      expect(byId(id)).toHaveFocus();
    }
    enterOnActive({ shiftKey: true });
    expect(byId('cash-voucher-at')).toHaveFocus();
    expect(onEnterGrid).not.toHaveBeenCalled();
  });

  it('Enter in Ghi chú calls onEnterGrid once', () => {
    const { onEnterGrid } = renderHeader();

    byId('cash-voucher-note').focus();
    expect(enterOnActive()).toBe(false);

    expect(onEnterGrid).toHaveBeenCalledTimes(1);
  });

  it('does not walk on a composing, a Ctrl or an already handled Enter', () => {
    const { onEnterGrid } = renderHeader();
    const note = byId('cash-voucher-note');
    const fund = byId('cash-fund');

    for (const el of [fund, note]) {
      el.focus();
      expect(enter(el, { isComposing: true })).toBe(true);
      expect(enter(el, { ctrlKey: true })).toBe(true);
      expect(el).toHaveFocus();
    }

    // A handler closer to the field already took this Enter.
    const handled = (e: Event) => e.preventDefault();
    for (const el of [fund, note]) {
      el.addEventListener('keydown', handled);
      el.focus();
      enter(el);
      expect(el).toHaveFocus();
    }
    expect(onEnterGrid).not.toHaveBeenCalled();
  });

  it('a locked partner (reason without partner) is skipped and says Không cần đối tượng', () => {
    renderHeader({ selectedReason: reasons[1] });

    expect(byId('cash-partner')).toBeDisabled();
    expect(byId('cash-partner')).toHaveAttribute('placeholder', 'Không cần đối tượng');
    byId('cash-fund').focus();
    enterOnActive();
    expect(byId('cash-partner-name')).toHaveFocus();
    enterOnActive({ shiftKey: true });
    expect(byId('cash-fund')).toHaveFocus();
  });
});

describe('CashVoucherHeader messages (section-01 §1.1, §4)', () => {
  it('shows the partner error under the partner row', () => {
    renderHeader({ partnerError: 'Đối tượng không hợp lệ' });

    const message = screen.getByText('Đối tượng không hợp lệ');
    const row = byId('cash-partner').closest('.partner-code-name') as HTMLElement;
    expect(message.parentElement).toBe(row.parentElement);
    expect(row.compareDocumentPosition(message) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    expect(byId('cash-reason').parentElement).not.toContainElement(message);
  });

  it('shows the reason error in the reason cell', async () => {
    const { form } = renderHeader();

    act(() => form().setError('reasonId', { type: 'server', message: 'Lý do không hợp lệ' }));

    const message = await screen.findByText('Lý do không hợp lệ');
    // The select's own cell, not the whole row (it would land under Người nộp below 1024px).
    expect(byId('cash-reason').parentElement).not.toContainElement(byId('cash-person-name'));
    expect(byId('cash-reason').parentElement).toContainElement(message);
    expect(message).toHaveClass('text-destructive');
  });

  it('an info notice is amber with role status, an error notice is destructive; both under Lý do', () => {
    const { rerender } = renderHeader({ reasonNotice: { text: 'Đã đổi lý do', tone: 'info' } });

    const info = screen.getByRole('status');
    expect(info).toHaveTextContent('Đã đổi lý do');
    expect(info.className).toMatch(/amber/);
    expect(info).not.toHaveClass('text-destructive');
    expect(byId('cash-reason').parentElement).not.toContainElement(byId('cash-person-name'));
    expect(byId('cash-reason').parentElement).toContainElement(info);
    // The message makes the cell taller: the row aligns its labels and the person input to the top.
    expect(byId('cash-reason').closest('.form-inline-grid')).toHaveClass('form-align-top');

    rerender({ reasonNotice: { text: 'Lý do cần khách hàng', tone: 'error' } });
    const error = screen.getByText('Lý do cần khách hàng');
    expect(error).toHaveClass('text-destructive');
    expect(error.className).not.toMatch(/amber/);
    expect(byId('cash-reason').parentElement).toContainElement(error);
  });

  it('shows only the reason error when there is also a notice', async () => {
    const { form } = renderHeader({ reasonNotice: { text: 'Đã đổi lý do', tone: 'info' } });

    act(() => form().setError('reasonId', { type: 'server', message: 'Lý do không hợp lệ' }));

    expect(await screen.findByText('Lý do không hợp lệ')).toBeInTheDocument();
    expect(screen.queryByText('Đã đổi lý do')).toBeNull();
  });

  it('reasonFlash puts an amber ring on the reason select', () => {
    const { rerender } = renderHeader();
    expect(byId('cash-reason')).not.toHaveClass('ring-amber-400');

    rerender({ reasonFlash: true });

    expect(byId('cash-reason')).toHaveClass('ring-amber-400');
  });
});

describe('CashVoucherHeader inputs', () => {
  it('lists the reasons and reports a change', () => {
    const { onReasonChange } = renderHeader();

    const names = within(byId('cash-reason')).getAllByRole('option').map((o) => o.textContent);
    expect(names).toEqual(['-- Chọn lý do --', 'Thu tiền khách hàng', 'Thu khác', 'Thu hoàn tiền NCC']);
    fireEvent.change(byId('cash-reason'), { target: { value: R_THNCC } });

    expect(onReasonChange).toHaveBeenCalledWith(R_THNCC);
  });

  it('reports typing in the partner name and in Lý do nộp', () => {
    const { onPartnerNameChange, onDescriptionEdited, form } = renderHeader();

    fireEvent.change(byId('cash-partner-name'), { target: { value: 'Anh Tư' } });
    expect(onPartnerNameChange).toHaveBeenCalledWith('Anh Tư');
    expect(form().getValues('partnerName')).toBe('Anh Tư');

    fireEvent.change(byId('cash-description'), { target: { value: 'Thu tạm ứng' } });
    expect(onDescriptionEdited).toHaveBeenCalledWith('Thu tạm ứng');
    expect(form().getValues('description')).toBe('Thu tạm ứng');
  });

  it('read-only disables every field and has no quick add', () => {
    renderHeader({ readOnly: true });

    for (const id of CASH_HEADER_FIELD_IDS) expect(byId(id)).toBeDisabled();
    expect(screen.queryByRole('button', { name: /Thêm nhanh/ })).toBeNull();
  });

  it('keys inside the real quick add dialog do not reach the header', async () => {
    const { onEnterGrid } = renderHeader();
    expect(screen.getByRole('button', { name: /Thêm nhanh/ })).toBeInTheDocument();

    fireEvent.change(byId('cash-partner'), { target: { value: 'Công ty mới' } });
    fireEvent.keyDown(byId('cash-partner'), { key: 'F4' });
    const dialog = await screen.findByRole('dialog');
    const name = within(dialog).getByLabelText('Tên *');
    await waitFor(() => expect(name).toHaveFocus());
    expect(name).toHaveValue('Công ty mới');

    enterOnActive();
    expect((document.activeElement as HTMLElement).closest('[role="dialog"]')).not.toBeNull();
    expect(document.activeElement).not.toBe(name);
    enterOnActive({ shiftKey: true });
    expect(name).toHaveFocus();

    expect(onEnterGrid).not.toHaveBeenCalled();
    expect(createCustomerMock).not.toHaveBeenCalled();
  });
});
