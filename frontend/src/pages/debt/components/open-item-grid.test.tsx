import { describe, expect, it } from 'vitest';
import { fireEvent, render, screen, within } from '@testing-library/react';
import { useState } from 'react';
import { MemoryRouter } from 'react-router-dom';
import { OpenItemGrid, type OpenItemGridProps } from './open-item-grid';
import type { MatchRow } from '@/features/debt/matching';
import type { OpenItem } from '@/features/debt/types';

const A = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';
const B = 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb';

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

const items: OpenItem[] = [
  item({ entryId: A, docCode: 'PX00001', docDate: '2026-10-01', dueDate: '2026-10-31', amount: 1_500_000, settled: 500_000, open: 1_000_000 }),
  item({ entryId: B, docCode: 'PX00002', docDate: '2026-10-05', amount: 300_000, open: 300_000 }),
];

function Harness({ docHref }: { docHref?: OpenItemGridProps['docHref'] }) {
  const [value, setValue] = useState<MatchRow[]>([]);
  return (
    <MemoryRouter>
      <OpenItemGrid title="Chứng từ ghi nợ còn nợ" items={items} value={value} onChange={setValue} docHref={docHref} />
      <output data-testid="rows">{JSON.stringify(value)}</output>
    </MemoryRouter>
  );
}

function enter(input: HTMLElement, text: string) {
  fireEvent.focus(input);
  fireEvent.change(input, { target: { value: text } });
  fireEvent.blur(input);
}

describe('OpenItemGrid', () => {
  it('enters amounts and caps at open', () => {
    render(<Harness />);

    const grid = screen.getByRole('region', { name: 'Chứng từ ghi nợ còn nợ' });
    const row = within(grid).getByText('PX00001').closest('tr')!;
    expect(within(row).getByText('01/10/2026')).toBeInTheDocument();
    expect(within(row).getByText('31/10/2026')).toBeInTheDocument();
    expect(within(row).getByText('1.000.000')).toBeInTheDocument();

    const first = within(grid).getByRole('textbox', { name: 'Đối trừ PX00001' });
    const second = within(grid).getByRole('textbox', { name: 'Đối trừ PX00002' });
    enter(first, '400000');
    enter(second, '300000');

    expect(first).toHaveValue('400.000');
    expect(first).not.toHaveAttribute('aria-invalid');
    expect(within(grid).getByTestId('open-item-total')).toHaveTextContent('700.000');
    expect(JSON.parse(screen.getByTestId('rows').textContent!)).toEqual([
      { entryId: A, amount: 400_000 },
      { entryId: B, amount: 300_000 },
    ]);

    // Above the open amount of the row.
    enter(first, '1200000');
    expect(first).toHaveAttribute('aria-invalid', 'true');
    expect(within(row).getByText('Vượt số còn lại')).toBeInTheDocument();

    // Back to zero: the row leaves the value.
    enter(first, '0');
    expect(first).toHaveValue('');
    expect(within(row).queryByText('Vượt số còn lại')).not.toBeInTheDocument();
    expect(JSON.parse(screen.getByTestId('rows').textContent!)).toEqual([{ entryId: B, amount: 300_000 }]);
  });

  it('flags a negative amount', () => {
    render(<Harness />);

    const grid = screen.getByRole('region', { name: 'Chứng từ ghi nợ còn nợ' });
    const row = within(grid).getByText('PX00002').closest('tr')!;
    const second = within(grid).getByRole('textbox', { name: 'Đối trừ PX00002' });
    enter(second, '-100000');

    expect(second).toHaveAttribute('aria-invalid', 'true');
    expect(within(row).getByText('Số tiền không được âm')).toBeInTheDocument();

    enter(second, '100000');
    expect(second).not.toHaveAttribute('aria-invalid');
    expect(within(row).queryByText('Số tiền không được âm')).not.toBeInTheDocument();
  });

  it('links the document code when a link is given', () => {
    render(<Harness docHref={(sourceType, sourceId) => `/${sourceType}/${sourceId}`} />);

    expect(screen.getByRole('link', { name: 'PX00001' })).toHaveAttribute(
      'href',
      '/StockOut/99999999-9999-9999-9999-999999999999',
    );
  });
});
