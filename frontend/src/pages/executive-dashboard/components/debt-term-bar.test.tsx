import { describe, expect, it } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import type { AgingBucket } from '@/features/cash-reports/types';
import type { DebtSideSummary } from '@/features/executive-dashboard/types';
import { DebtTermBar } from './debt-term-bar';

const BUCKETS: AgingBucket[] = [
  { from: 1, to: 30 },
  { from: 31, to: 60 },
  { from: 61, to: 90 },
  { from: 91, to: null },
];
const side = (over: Partial<DebtSideSummary> = {}): DebtSideSummary => ({
  balance: 1_500, totalOpen: 3_000, unallocated: 1_500, notDue: 0, overdue: 3_000, overduePartnerCount: 1,
  overdueAmounts: [2_000, 1_000, 0, 0], dueSoon: 0, dueSoonCount: 0, ...over,
});

const bodyRows = () => within(screen.getAllByRole('rowgroup')[1]).getAllByRole('row');
const cells = (row: HTMLElement) => within(row).getAllByRole('cell').map((c) => c.textContent);
const segments = (container: HTMLElement) =>
  Array.from(container.querySelectorAll<HTMLElement>('[data-segment]')).map((s) => ({
    label: s.dataset.segment,
    width: s.style.width,
    colour: Array.from(s.classList).find((c) => c.startsWith('bg-')),
  }));

describe('DebtTermBar', () => {
  it('lists "Trong hạn" and each overdue bucket with its amount and its whole-percent share', () => {
    render(<DebtTermBar side={side()} buckets={BUCKETS} />);

    expect(bodyRows().map(cells)).toEqual([
      ['Trong hạn', '0', '0%'],
      ['Quá hạn 1–30 ngày', '2.000', '67%'],
      ['Quá hạn 31–60 ngày', '1.000', '33%'],
      ['Quá hạn 61–90 ngày', '0', '0%'],
      ['Quá hạn > 90 ngày', '0', '0%'],
    ]);
    const headers = screen.getAllByRole('columnheader');
    expect(headers[1]).toHaveClass('text-right', 'tabular-nums');
    expect(headers[2]).toHaveClass('text-right', 'tabular-nums');
  });

  it('draws one bar whose segments are the shares of the open amount, in the colours of the terms', () => {
    const { container } = render(
      <DebtTermBar side={side({ notDue: 1_000, totalOpen: 4_000, overdueAmounts: [2_000, 500, 300, 200] })} buckets={BUCKETS} />,
    );

    expect(segments(container)).toEqual([
      { label: 'Trong hạn', width: '25%', colour: 'bg-primary' },
      { label: 'Quá hạn 1–30 ngày', width: '50%', colour: 'bg-amber-400' },
      { label: 'Quá hạn 31–60 ngày', width: '12.5%', colour: 'bg-orange-500' },
      { label: 'Quá hạn 61–90 ngày', width: '7.5%', colour: 'bg-rose-500' },
      { label: 'Quá hạn > 90 ngày', width: '5%', colour: 'bg-rose-700' },
    ]);
  });

  it('draws no segment for an empty term', () => {
    const { container } = render(<DebtTermBar side={side()} buckets={BUCKETS} />);

    expect(segments(container).map((s) => s.label)).toEqual(['Quá hạn 1–30 ngày', 'Quá hạn 31–60 ngày']);
  });

  it('notes the payments not matched yet', () => {
    render(<DebtTermBar side={side()} buckets={BUCKETS} />);

    expect(screen.getByText('Chưa đối trừ: 1.500')).toBeInTheDocument();
  });

  it('has no note when everything is matched', () => {
    render(<DebtTermBar side={side({ unallocated: 0, balance: 3_000 })} buckets={BUCKETS} />);

    expect(screen.queryByText(/Chưa đối trừ/)).not.toBeInTheDocument();
  });

  it('says so when there is no open debt', () => {
    const { container } = render(
      <DebtTermBar side={side({ totalOpen: 0, overdue: 0, overdueAmounts: [0, 0, 0, 0], unallocated: 0, balance: 0 })} buckets={BUCKETS} />,
    );

    expect(screen.getByText('Không có công nợ')).toBeInTheDocument();
    expect(screen.queryByRole('table')).not.toBeInTheDocument();
    expect(segments(container)).toEqual([]);
  });

  it('still notes an unmatched payment when nothing is open', () => {
    render(
      <DebtTermBar side={side({ totalOpen: 0, overdue: 0, overdueAmounts: [0, 0, 0, 0], unallocated: 700, balance: -700 })} buckets={BUCKETS} />,
    );

    expect(screen.getByText('Không có công nợ')).toBeInTheDocument();
    expect(screen.getByText('Chưa đối trừ: 700')).toBeInTheDocument();
  });
});
