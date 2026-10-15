import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { SalesLeaderboard } from './sales-leaderboard';

describe('SalesLeaderboard', () => {
  it('shows the voucher count and no conversion rate', () => {
    render(
      <MemoryRouter>
        <SalesLeaderboard
          items={[{ userId: 'u1', fullName: 'An', voucherCount: 3, revenue: 1000, deltaPct: null }]}
        />
      </MemoryRouter>,
    );

    expect(screen.getByText('3 phiếu')).toBeInTheDocument();
    expect(screen.queryByText(/CR /)).toBeNull();
    expect(screen.queryByText(/BG/)).toBeNull();
  });
});
