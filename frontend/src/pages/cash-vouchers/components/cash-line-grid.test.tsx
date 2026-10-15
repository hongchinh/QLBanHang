import { describe, expect, it } from 'vitest';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { useForm, type UseFormReturn } from 'react-hook-form';
import { CashLineGrid } from './cash-line-grid';
import { createEmptyCashLine, toFormDefaults } from '@/features/cash-vouchers/payload';
import type { CashVoucherFormParsed, CashVoucherFormValues } from '@/features/cash-vouchers/schema';

function renderGrid(readOnly = false) {
  const formRef: { current: UseFormReturn<CashVoucherFormValues, unknown, CashVoucherFormParsed> | null } = {
    current: null,
  };
  function Harness() {
    const form = useForm<CashVoucherFormValues, unknown, CashVoucherFormParsed>({
      defaultValues: toFormDefaults(undefined, undefined, 'Receipt'),
    });
    formRef.current = form;
    return <CashLineGrid form={form} readOnly={readOnly} />;
  }
  render(<Harness />);
  return () => formRef.current!.getValues();
}

function typeMoney(input: HTMLElement, text: string) {
  fireEvent.focus(input);
  fireEvent.change(input, { target: { value: text } });
  fireEvent.blur(input);
}

describe('CashLineGrid', () => {
  it('adds and removes lines and totals amounts', () => {
    const values = renderGrid();

    fireEvent.change(screen.getByRole('textbox', { name: 'Diễn giải dòng 1' }), {
      target: { value: 'Thu tiền hàng' },
    });
    typeMoney(screen.getByRole('textbox', { name: 'Số tiền dòng 1' }), '1.000.000');

    fireEvent.click(screen.getByRole('button', { name: /Thêm dòng/ }));
    expect(screen.getByRole('textbox', { name: 'Diễn giải dòng 2' })).toBeInTheDocument();
    typeMoney(screen.getByRole('textbox', { name: 'Số tiền dòng 2' }), '500000');
    fireEvent.change(screen.getByRole('textbox', { name: 'Ghi chú dòng 2' }), { target: { value: 'Tạm ứng' } });

    expect(screen.getByRole('textbox', { name: 'Số tiền dòng 1' })).toHaveValue('1.000.000');
    expect(screen.getByTestId('cash-lines-total')).toHaveTextContent('1.500.000');
    expect(values().lines).toMatchObject([
      { description: 'Thu tiền hàng', amount: 1_000_000 },
      { amount: 500_000, note: 'Tạm ứng' },
    ]);

    fireEvent.click(screen.getByRole('button', { name: 'Xóa dòng 1' }));

    expect(screen.queryByRole('textbox', { name: 'Diễn giải dòng 2' })).not.toBeInTheDocument();
    expect(screen.getByTestId('cash-lines-total')).toHaveTextContent('500.000');
    expect(values().lines).toHaveLength(1);
    expect(values().lines[0]).toMatchObject({ amount: 500_000, note: 'Tạm ứng' });
  });

  it('read-only mode disables inputs and hides row actions', () => {
    renderGrid(true);

    expect(screen.getByRole('textbox', { name: 'Diễn giải dòng 1' })).toBeDisabled();
    expect(screen.queryByRole('button', { name: /Thêm dòng/ })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Xóa dòng 1' })).not.toBeInTheDocument();
  });
});

// Two rows and a header description of Thu tiền khách hàng, plus the header note the grid walks back to.
function renderEnterGrid(readOnly = false) {
  const formRef: { current: UseFormReturn<CashVoucherFormValues, unknown, CashVoucherFormParsed> | null } = {
    current: null,
  };
  function Harness() {
    const form = useForm<CashVoucherFormValues, unknown, CashVoucherFormParsed>({
      defaultValues: {
        ...toFormDefaults(undefined, undefined, 'Receipt'),
        description: 'Thu tiền khách hàng',
        lines: [
          { ...createEmptyCashLine('Dòng một'), note: 'ghi chú một' },
          { ...createEmptyCashLine('Dòng hai'), note: 'ghi chú hai' },
        ],
      },
    });
    formRef.current = form;
    return (
      <>
        <input id="cash-voucher-note" aria-label="Ghi chú phiếu" />
        <CashLineGrid form={form} readOnly={readOnly} />
      </>
    );
  }
  render(<Harness />);
  return () => formRef.current!.getValues();
}

const cell = (name: string) => screen.getByRole('textbox', { name });
const enter = (el: HTMLElement, init: KeyboardEventInit = {}) => fireEvent.keyDown(el, { key: 'Enter', ...init });

describe('CashLineGrid Enter (section-01 §2.2)', () => {
  it('Enter walks description -> amount -> note -> description of the next row', () => {
    renderEnterGrid();

    const description = cell('Diễn giải dòng 1') as HTMLInputElement;
    description.focus();
    expect(enter(description)).toBe(false);
    expect(cell('Số tiền dòng 1')).toHaveFocus();

    enter(cell('Số tiền dòng 1'));
    expect(cell('Ghi chú dòng 1')).toHaveFocus();

    enter(cell('Ghi chú dòng 1'));
    const next = cell('Diễn giải dòng 2') as HTMLInputElement;
    expect(next).toHaveFocus();
    expect([next.selectionStart, next.selectionEnd]).toEqual([0, next.value.length]);
  });

  it('Enter in Số tiền selects the text of the Ghi chú it lands on', () => {
    renderEnterGrid();
    enter(cell('Số tiền dòng 1'));

    const note = cell('Ghi chú dòng 1') as HTMLInputElement;
    expect(note).toHaveFocus();
    expect([note.selectionStart, note.selectionEnd]).toEqual([0, note.value.length]);
  });

  it('Enter in the Ghi chú of the last row appends a row with the header description and focuses it', async () => {
    const values = renderEnterGrid();

    cell('Ghi chú dòng 2').focus();
    expect(enter(cell('Ghi chú dòng 2'))).toBe(false);

    await waitFor(() => expect(cell('Diễn giải dòng 3')).toHaveFocus());
    expect(values().lines).toHaveLength(3);
    expect(values().lines[2]).toMatchObject({ description: 'Thu tiền khách hàng', amount: 0 });
  });

  it('typed amount digits survive moving on', () => {
    const values = renderEnterGrid();
    const amount = cell('Số tiền dòng 1');

    amount.focus();
    fireEvent.change(amount, { target: { value: '1500' } });
    enter(amount);

    expect(values().lines[0].amount).toBe(1500);
    expect(cell('Ghi chú dòng 1')).toHaveFocus();
  });

  it('Shift+Enter walks back: Ghi chú -> Số tiền -> Diễn giải', () => {
    renderEnterGrid();

    cell('Ghi chú dòng 1').focus();
    expect(enter(cell('Ghi chú dòng 1'), { shiftKey: true })).toBe(false);
    expect(cell('Số tiền dòng 1')).toHaveFocus();

    enter(cell('Số tiền dòng 1'), { shiftKey: true });
    expect(cell('Diễn giải dòng 1')).toHaveFocus();
  });

  it('Shift+Enter: Diễn giải of row 2 -> Ghi chú of row 1; Diễn giải of row 1 -> the header note', () => {
    renderEnterGrid();

    enter(cell('Diễn giải dòng 2'), { shiftKey: true });
    expect(cell('Ghi chú dòng 1')).toHaveFocus();

    enter(cell('Diễn giải dòng 1'), { shiftKey: true });
    expect(cell('Ghi chú phiếu')).toHaveFocus();
  });

  it('Insert and the Thêm dòng button add a row with the header description', async () => {
    const values = renderEnterGrid();

    fireEvent.keyDown(cell('Diễn giải dòng 1'), { key: 'Insert' });
    await waitFor(() => expect(cell('Diễn giải dòng 3')).toHaveFocus());
    expect(values().lines[2].description).toBe('Thu tiền khách hàng');

    fireEvent.click(screen.getByRole('button', { name: /Thêm dòng/ }));
    await waitFor(() => expect(cell('Diễn giải dòng 4')).toHaveFocus());
    expect(values().lines[3].description).toBe('Thu tiền khách hàng');
  });

  it('a read-only grid ignores Enter', () => {
    renderEnterGrid(true);

    expect(enter(cell('Diễn giải dòng 1'))).toBe(true);
    expect(cell('Số tiền dòng 1')).not.toHaveFocus();
  });

  it('does not handle Enter while composing, with Ctrl, or on the delete button', () => {
    renderEnterGrid();
    const description = cell('Diễn giải dòng 1');
    description.focus();

    expect(enter(description, { isComposing: true })).toBe(true);
    expect(enter(description, { ctrlKey: true })).toBe(true);
    expect(enter(screen.getByRole('button', { name: 'Xóa dòng 1' }))).toBe(true);
    expect(description).toHaveFocus();
  });
});
