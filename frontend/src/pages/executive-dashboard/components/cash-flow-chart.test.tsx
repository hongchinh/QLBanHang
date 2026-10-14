import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import { formatVnd } from '@/features/dashboard/format';
import type { CashPoint } from '@/features/executive-dashboard/types';
import { CashFlowChart } from './cash-flow-chart';

const POINTS: CashPoint[] = [
  { date: '2026-09-28', receipts: 45_000_000, payments: 12_000_000 },
  { date: '2026-10-05', receipts: 98_000_000, payments: 30_000_000 },
];

const legend = () => Array.from(document.querySelectorAll('.recharts-legend-item-text')).map((e) => e.textContent);
// Recharts also writes the last text it measured into a hidden span of the body: read the axis itself.
const xTicks = (container: HTMLElement) =>
  Array.from(container.querySelectorAll('.recharts-xAxis .recharts-cartesian-axis-tick-value')).map((t) => t.textContent);

describe('CashFlowChart', () => {
  it('draws receipts and payments per bucket with their legend', () => {
    const { container } = render(<CashFlowChart points={POINTS} granularity="Week" />);

    expect(container.querySelector('svg.recharts-surface')).not.toBeNull();
    expect(legend()).toEqual(['Thu', 'Chi']);
    expect(container.querySelectorAll('.recharts-bar')).toHaveLength(2);
  });

  it('labels the X axis with the bucket date as it is', () => {
    const week = render(<CashFlowChart points={POINTS} granularity="Week" />);

    // The first week starts before the period.
    expect(xTicks(week.container)).toEqual(['28/09', '05/10']);
    week.unmount();

    const month = render(<CashFlowChart points={[{ ...POINTS[0], date: '2026-09-01' }]} granularity="Month" />);
    expect(xTicks(month.container)).toEqual(['09/2026']);
  });

  // While another granularity loads, the points kept on screen (keepPreviousData) keep their own labels.
  it('labels the axis by the granularity of the points, while the toggle shows the chosen one', () => {
    const { container } = render(
      <CashFlowChart points={POINTS} granularity="Month" labelGranularity="Week" onGranularityChange={vi.fn()} />,
    );

    expect(xTicks(container)).toEqual(['28/09', '05/10']);
    expect(screen.getByRole('button', { name: 'Tháng' })).toHaveAttribute('aria-pressed', 'true');
  });

  it('has a text alternative: the buckets and the totals', () => {
    render(<CashFlowChart points={POINTS} granularity="Week" onGranularityChange={vi.fn()} />);

    const chart = screen.getByRole('img', {
      name: `Thu và chi theo tuần, 28/09 – 05/10: thu ${formatVnd(143_000_000)}, chi ${formatVnd(42_000_000)}.`,
    });
    expect(chart.querySelector('svg.recharts-surface')).not.toBeNull();
    expect(chart.querySelector('button')).toBeNull();
  });

  it('has no toggle without a handler', () => {
    render(<CashFlowChart points={POINTS} granularity="Week" />);

    expect(screen.queryByRole('button')).not.toBeInTheDocument();
  });

  it('shows the toggle with a handler', () => {
    const onGranularityChange = vi.fn();
    render(<CashFlowChart points={POINTS} granularity="Week" onGranularityChange={onGranularityChange} />);

    fireEvent.click(screen.getByRole('button', { name: 'Ngày' }));
    expect(onGranularityChange).toHaveBeenCalledWith('Day');
  });
});
