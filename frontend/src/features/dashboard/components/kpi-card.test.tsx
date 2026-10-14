import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { KpiCard } from './kpi-card';

describe('KpiCard', () => {
  it('formats VND currency for big numbers', () => {
    render(
      <KpiCard
        label="Doanh thu"
        format="currency"
        kpi={{ value: 1_234_567, deltaPct: null, spark: [] }}
      />,
    );
    expect(screen.getByText(/1\.234\.567/)).toBeInTheDocument();
  });

  it('renders positive delta with up-arrow and emerald tone', () => {
    render(
      <KpiCard
        label="X"
        kpi={{ value: 10, deltaPct: 12.5, spark: [] }}
      />,
    );
    const badge = screen.getByText('12.5%');
    expect(screen.getByRole('img', { name: 'tăng' })).toBeInTheDocument();
    expect(badge).toBeInTheDocument();
    expect(badge.className).toContain('emerald');
  });

  it('renders negative delta with down-arrow and rose tone', () => {
    render(
      <KpiCard
        label="X"
        kpi={{ value: 10, deltaPct: -3.2, spark: [] }}
      />,
    );
    const badge = screen.getByText('3.2%');
    expect(screen.getByRole('img', { name: 'giảm' })).toBeInTheDocument();
    expect(badge).toBeInTheDocument();
    expect(badge.className).toContain('rose');
  });

  it('renders em-dash when delta is null', () => {
    render(
      <KpiCard
        label="X"
        kpi={{ value: 10, deltaPct: null, spark: [] }}
      />,
    );
    expect(screen.getByText('—')).toBeInTheDocument();
  });

  it('does not render sparkline when spark is empty', () => {
    const { container } = render(
      <KpiCard
        label="X"
        kpi={{ value: 1, deltaPct: null, spark: [] }}
      />,
    );
    expect(container.querySelector('svg')).toBeNull();
  });

  it('renders sparkline when spark has data', () => {
    const { container } = render(
      <KpiCard
        label="X"
        kpi={{ value: 1, deltaPct: null, spark: [1, 2, 3, 4, 5, 6, 7] }}
      />,
    );
    expect(container.querySelector('svg')).not.toBeNull();
  });
});
