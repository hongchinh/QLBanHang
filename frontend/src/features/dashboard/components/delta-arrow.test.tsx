import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { DeltaArrow } from './delta-arrow';

describe('DeltaArrow', () => {
  it('up is the trendUp icon named "tăng"', () => {
    render(<DeltaArrow direction="up" />);
    expect(screen.getByRole('img', { name: 'tăng' })).toHaveAttribute('data-icon', 'trendUp');
  });

  it('down is the trendDown icon named "giảm"', () => {
    render(<DeltaArrow direction="down" />);
    expect(screen.getByRole('img', { name: 'giảm' })).toHaveAttribute('data-icon', 'trendDown');
  });
});
