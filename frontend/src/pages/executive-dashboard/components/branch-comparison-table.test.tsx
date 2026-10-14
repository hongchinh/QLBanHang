import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, within } from '@testing-library/react';
import type { BranchComparisonRow } from '@/features/executive-dashboard/types';
import { BranchComparisonTable } from './branch-comparison-table';

const ROWS: BranchComparisonRow[] = [
  { branchId: 'b1', branchCode: 'HN', branchName: 'Hà Nội', revenue: 1_620_000_000, marginPct: 15.1, receivable: 1_800_000_000, stockValue: 2_710_000_000 },
  { branchId: 'b2', branchCode: 'HY', branchName: 'Hưng Yên', revenue: 840_000_000, marginPct: null, receivable: null, stockValue: null },
];

const bodyRows = () => within(screen.getAllByRole('rowgroup')[1]).getAllByRole('row');
const cells = (row: HTMLElement) => within(row).getAllByRole('cell').map((c) => c.textContent);

describe('BranchComparisonTable', () => {
  it('has one row per branch with compact money and the margin', () => {
    render(<BranchComparisonTable rows={ROWS} onSelectBranch={vi.fn()} />);

    expect(screen.getAllByRole('columnheader').map((h) => h.textContent)).toEqual([
      'Chi nhánh', 'Doanh thu', 'Lãi gộp %', 'Phải thu', 'Tồn kho',
    ]);
    expect(bodyRows().map(cells)).toEqual([
      ['HN — Hà Nội', '1,62 tỷ', '15,1%', '1,80 tỷ', '2,71 tỷ'],
      ['HY — Hưng Yên', '840 tr', '—', '—', '—'],
    ]);
  });

  it('right-aligns the figures', () => {
    render(<BranchComparisonTable rows={ROWS} onSelectBranch={vi.fn()} />);

    const headers = screen.getAllByRole('columnheader');
    expect(headers[0]).not.toHaveClass('text-right');
    for (const header of headers.slice(1)) expect(header).toHaveClass('text-right', 'tabular-nums');
  });

  it('a branch without revenue figures shows "—" there too', () => {
    render(<BranchComparisonTable rows={[{ ...ROWS[0], revenue: null }]} onSelectBranch={vi.fn()} />);

    expect(cells(bodyRows()[0])[1]).toBe('—');
  });

  it('clicking a branch name selects the branch', () => {
    const onSelectBranch = vi.fn();
    render(<BranchComparisonTable rows={ROWS} onSelectBranch={onSelectBranch} />);

    const button = screen.getByRole('button', { name: 'HY — Hưng Yên' });
    expect(button).toHaveAttribute('type', 'button');
    fireEvent.click(button);

    expect(onSelectBranch).toHaveBeenCalledTimes(1);
    expect(onSelectBranch).toHaveBeenCalledWith('b2');
  });
});
