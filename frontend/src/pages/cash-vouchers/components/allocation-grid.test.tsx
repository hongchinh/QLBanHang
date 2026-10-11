import { describe, expect, it } from 'vitest';
import { fireEvent, render, screen, within } from '@testing-library/react';
import { useState } from 'react';
import { AllocationGrid } from './allocation-grid';
import type { CashAllocationFormValues } from '@/features/cash-vouchers/schema';
import type { OpenItem } from '@/features/debt/types';

const A = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';
const B = 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb';
const C = 'cccccccc-cccc-cccc-cccc-cccccccccccc';

function item(overrides: Partial<OpenItem>): OpenItem {
  return {
    entryId: A,
    sourceType: 'StockOut',
    sourceId: '99999999-9999-9999-9999-999999999999',
    docCode: 'PX00001',
    docDate: '2026-10-01',
    dueDate: null,
    postedAt: '2026-10-01T03:00:00Z',
    direction: 'Increase',
    amount: 0,
    settled: 0,
    open: 0,
    ...overrides,
  };
}

// Earliest due: C (due = doc date 10-03), then B (due 10-10), then A (due 10-31).
const items: OpenItem[] = [
  item({ entryId: A, docCode: 'PX00001', docDate: '2026-10-01', dueDate: '2026-10-31', amount: 500_000, open: 500_000 }),
  item({ entryId: B, docCode: 'PX00002', docDate: '2026-10-05', dueDate: '2026-10-10', amount: 300_000, open: 300_000 }),
  item({ entryId: C, docCode: 'PX00003', docDate: '2026-10-03', dueDate: null, amount: 200_000, open: 200_000 }),
];

interface HarnessProps {
  initial?: CashAllocationFormValues[];
  total: number;
  otherSettled?: number;
  readOnly?: boolean;
}

function Harness({ initial = [], total, otherSettled = 0, readOnly = false }: HarnessProps) {
  const [value, setValue] = useState<CashAllocationFormValues[]>(initial);
  return (
    <AllocationGrid
      items={items}
      value={value}
      total={total}
      otherSettled={otherSettled}
      readOnly={readOnly}
      onChange={setValue}
      onSuggest={setValue}
    />
  );
}

function amountInput(docCode: string): HTMLInputElement {
  return screen.getByRole('textbox', { name: `Phân bổ ${docCode}` }) as HTMLInputElement;
}

describe('AllocationGrid', () => {
  it('shows open items and remaining', () => {
    render(<Harness total={1_000_000} initial={[{ entryId: A, docCode: 'PX00001', amount: 100_000 }]} />);

    const row = screen.getByText('PX00001').closest('tr')!;
    expect(within(row).getByText('01/10/2026')).toBeInTheDocument();
    expect(within(row).getByText('31/10/2026')).toBeInTheDocument();
    expect(within(row).getByText('500.000')).toBeInTheDocument();
    expect(amountInput('PX00001')).toHaveValue('100.000');
    expect(screen.getByText('PX00002')).toBeInTheDocument();
    expect(screen.getByText('PX00003')).toBeInTheDocument();

    expect(screen.getByTestId('allocation-total')).toHaveTextContent('100.000');
    expect(screen.getByTestId('allocation-remaining')).toHaveTextContent('900.000');
    expect(screen.queryByText('Tổng phân bổ vượt số tiền phiếu')).not.toBeInTheDocument();
  });

  it('suggest fills earliest due first', () => {
    render(<Harness total={400_000} />);

    fireEvent.click(screen.getByRole('button', { name: 'Gợi ý' }));

    expect(amountInput('PX00003')).toHaveValue('200.000');
    expect(amountInput('PX00002')).toHaveValue('200.000');
    expect(amountInput('PX00001')).toHaveValue('');
    expect(screen.getByTestId('allocation-total')).toHaveTextContent('400.000');
    expect(screen.getByTestId('allocation-remaining')).toHaveTextContent('0');

    fireEvent.click(screen.getByRole('button', { name: 'Bỏ phân bổ' }));

    expect(amountInput('PX00003')).toHaveValue('');
    expect(amountInput('PX00002')).toHaveValue('');
    expect(screen.getByTestId('allocation-remaining')).toHaveTextContent('400.000');
  });

  it('over-allocation shows error', () => {
    render(<Harness total={350_000} />);

    const input = amountInput('PX00002');
    fireEvent.focus(input);
    fireEvent.change(input, { target: { value: '400000' } });
    fireEvent.blur(input);

    // Above the open amount of the row.
    expect(input).toHaveAttribute('aria-invalid', 'true');
    expect(within(input.closest('tr')!).getByText('Phân bổ vượt số còn nợ')).toBeInTheDocument();

    // Above the voucher amount.
    const remaining = screen.getByTestId('allocation-remaining');
    expect(remaining).toHaveTextContent('-50.000');
    expect(remaining).toHaveClass('text-destructive');
    expect(screen.getByText('Tổng phân bổ vượt số tiền phiếu')).toBeInTheDocument();
  });

  it('other settled reduces the capacity', () => {
    render(<Harness total={1_000_000} otherSettled={700_000} />);

    expect(screen.getByText(/Đã đối trừ ở màn khác: 700\.000/)).toBeInTheDocument();
    expect(screen.getByTestId('allocation-remaining')).toHaveTextContent('300.000');

    fireEvent.click(screen.getByRole('button', { name: 'Gợi ý' }));

    expect(amountInput('PX00003')).toHaveValue('200.000');
    expect(amountInput('PX00002')).toHaveValue('100.000');
    expect(amountInput('PX00001')).toHaveValue('');
    expect(screen.getByTestId('allocation-remaining')).toHaveTextContent('0');
  });

  it('read-only hides inputs', () => {
    render(
      <Harness total={1_000_000} readOnly initial={[{ entryId: B, docCode: 'PX00002', amount: 250_000 }]} />,
    );

    expect(screen.queryAllByRole('textbox')).toHaveLength(0);
    expect(screen.queryByRole('button', { name: 'Gợi ý' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Bỏ phân bổ' })).not.toBeInTheDocument();
    const row = screen.getByText('PX00002').closest('tr')!;
    expect(within(row).getByText('250.000')).toBeInTheDocument();
    expect(screen.getByTestId('allocation-total')).toHaveTextContent('250.000');
  });
});
