import { describe, expect, it } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import { TableCell } from '@/components/ui/table';
import { DataTable } from './data-table';
import type { DataColumn } from './data-table';

interface Fund {
  id: string;
  name: string;
  balance: number;
}

const ROWS: Fund[] = [
  { id: 'f1', name: 'Quỹ tiền mặt', balance: 19_300 },
  { id: 'f2', name: 'Ngân hàng', balance: 10_000 },
];

const COLUMNS: DataColumn<Fund>[] = [
  { key: 'name', header: 'Quỹ', cell: (row) => row.name },
  { key: 'balance', header: 'Số dư', align: 'right', cell: (row) => row.balance.toLocaleString('vi-VN') },
];

function renderTable(footer?: React.ReactNode) {
  return render(<DataTable columns={COLUMNS} rows={ROWS} rowKey={(row) => row.id} footer={footer} />);
}

describe('DataTable', () => {
  it('has one header cell per column and one row per item', () => {
    renderTable();

    expect(screen.getAllByRole('columnheader').map((h) => h.textContent)).toEqual(['Quỹ', 'Số dư']);
    const rows = screen.getAllByRole('row');
    // The header row, then the items.
    expect(rows).toHaveLength(3);
    expect(within(rows[1]).getAllByRole('cell').map((c) => c.textContent)).toEqual(['Quỹ tiền mặt', '19.300']);
    expect(within(rows[2]).getAllByRole('cell').map((c) => c.textContent)).toEqual(['Ngân hàng', '10.000']);
  });

  it('right-aligns a numeric column in tabular figures, header and cells', () => {
    renderTable();

    const [nameHeader, balanceHeader] = screen.getAllByRole('columnheader');
    expect(balanceHeader).toHaveClass('text-right', 'tabular-nums');
    expect(nameHeader).not.toHaveClass('text-right');
    expect(nameHeader).not.toHaveClass('tabular-nums');
    for (const row of screen.getAllByRole('row').slice(1)) {
      const [name, balance] = within(row).getAllByRole('cell');
      expect(balance).toHaveClass('text-right', 'tabular-nums');
      expect(name).not.toHaveClass('text-right');
      expect(name).not.toHaveClass('tabular-nums');
    }
  });

  it('renders the footer as the last row', () => {
    renderTable(
      <>
        <TableCell>Tổng cộng</TableCell>
        <TableCell className="text-right tabular-nums">29.300</TableCell>
      </>,
    );

    const rows = screen.getAllByRole('row');
    expect(rows).toHaveLength(4);
    expect(within(rows[3]).getAllByRole('cell').map((c) => c.textContent)).toEqual(['Tổng cộng', '29.300']);
  });

  it('has no footer row without a footer', () => {
    const { container } = renderTable();

    expect(container.querySelector('tfoot')).toBeNull();
  });

  it('passes the index to rowKey', () => {
    const keys: string[] = [];
    render(
      <DataTable
        columns={COLUMNS}
        rows={ROWS}
        rowKey={(row, index) => {
          keys.push(`${index}:${row.id}`);
          return `${index}`;
        }}
      />,
    );

    expect(keys).toEqual(['0:f1', '1:f2']);
  });
});
