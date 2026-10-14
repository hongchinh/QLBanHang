import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import type { SalesPoint } from '@/features/executive-dashboard/types';
import { ProfitChart } from './profit-chart';

const POINTS: SalesPoint[] = [
  { date: '2026-10-01', revenue: 120_000_000, cost: 100_000_000, grossProfit: 20_000_000 },
  { date: '2026-10-02', revenue: 90_000_000, cost: 80_000_000, grossProfit: 10_000_000 },
];
const WITHOUT_COST: SalesPoint[] = POINTS.map((p) => ({ ...p, cost: null, grossProfit: null }));

const legend = () => Array.from(document.querySelectorAll('.recharts-legend-item-text')).map((e) => e.textContent);
// Recharts also writes the last text it measured into a hidden span of the body: read the axis itself.
const xTicks = (container: HTMLElement) =>
  Array.from(container.querySelectorAll('.recharts-xAxis .recharts-cartesian-axis-tick-value')).map((t) => t.textContent);

describe('ProfitChart', () => {
  it('draws a chart of the points', () => {
    const { container } = render(<ProfitChart points={POINTS} canViewCost granularity="Day" />);

    expect(container.querySelector('svg.recharts-surface')).not.toBeNull();
  });

  it('with the cost right: revenue and cost bars and the gross profit line', () => {
    const { container } = render(<ProfitChart points={POINTS} canViewCost granularity="Day" />);

    expect(legend()).toEqual(['Doanh thu', 'Giá vốn', 'Lãi gộp']);
    expect(container.querySelectorAll('.recharts-bar')).toHaveLength(2);
    expect(container.querySelectorAll('.recharts-line')).toHaveLength(1);
  });

  it('without the cost right: the revenue bars only', () => {
    const { container } = render(<ProfitChart points={WITHOUT_COST} canViewCost={false} granularity="Day" />);

    expect(legend()).toEqual(['Doanh thu']);
    expect(screen.queryByText('Giá vốn')).not.toBeInTheDocument();
    expect(screen.queryByText('Lãi gộp')).not.toBeInTheDocument();
    expect(container.querySelectorAll('.recharts-bar')).toHaveLength(1);
    expect(container.querySelector('.recharts-line')).toBeNull();
  });

  it('labels the X axis by day for days and weeks, by month for months', () => {
    const day = render(<ProfitChart points={POINTS} canViewCost granularity="Day" />);
    expect(xTicks(day.container)).toEqual(['01/10', '02/10']);
    day.unmount();

    const week = render(<ProfitChart points={[{ ...POINTS[0], date: '2026-09-28' }]} canViewCost granularity="Week" />);
    expect(xTicks(week.container)).toEqual(['28/09']);
    week.unmount();

    const month = render(
      <ProfitChart points={[{ ...POINTS[0], date: '2026-09-01' }, { ...POINTS[1], date: '2026-10-01' }]} canViewCost granularity="Month" />,
    );
    expect(xTicks(month.container)).toEqual(['09/2026', '10/2026']);
  });

  it('prints the Y axis in compact money', () => {
    const { container } = render(<ProfitChart points={POINTS} canViewCost granularity="Day" />);

    const ticks = Array.from(container.querySelectorAll('.recharts-yAxis .recharts-cartesian-axis-tick-value')).map(
      (t) => t.textContent,
    );
    expect(ticks.length).toBeGreaterThan(1);
    expect(ticks).toContain('0');
    expect(ticks.some((t) => t?.endsWith(' tr'))).toBe(true);
  });

  it('shows the toggle only with a handler', () => {
    const { unmount } = render(<ProfitChart points={POINTS} canViewCost granularity="Day" />);
    expect(screen.queryByRole('button', { name: 'Tuần' })).not.toBeInTheDocument();
    unmount();

    const onGranularityChange = vi.fn();
    render(
      <ProfitChart points={POINTS} canViewCost granularity="Week" onGranularityChange={onGranularityChange} dayDisabled />,
    );
    expect(screen.getByRole('button', { name: 'Tuần' })).toHaveAttribute('aria-pressed', 'true');
    expect(screen.getByRole('button', { name: 'Ngày' })).toBeDisabled();
    fireEvent.click(screen.getByRole('button', { name: 'Tháng' }));
    expect(onGranularityChange).toHaveBeenCalledWith('Month');
  });
});
