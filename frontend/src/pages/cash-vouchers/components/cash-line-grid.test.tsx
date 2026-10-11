import { describe, expect, it } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import { useForm, type UseFormReturn } from 'react-hook-form';
import { CashLineGrid } from './cash-line-grid';
import { toFormDefaults } from '@/features/cash-vouchers/payload';
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
