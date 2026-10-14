import { render } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { AppIcon } from './app-icon';

const svg = (c: HTMLElement) => c.querySelector('svg')!;

describe('AppIcon', () => {
  it('defaults to 16px, stroke 2, decorative, tagged with its name', () => {
    const { container } = render(<AppIcon name="add" />);
    expect(svg(container)).toHaveAttribute('width', '16');
    expect(svg(container)).toHaveAttribute('stroke-width', '2');
    expect(svg(container)).toHaveAttribute('aria-hidden', 'true');
    expect(svg(container)).toHaveAttribute('data-icon', 'add');
  });
  it.each([[8, '2'], [12, '2'], [14, '2'], [18, '1.85'], [20, '1.85'], [24, '1.75']] as const)(
    'size %i draws stroke %s', (size, stroke) => {
      const { container } = render(<AppIcon name="add" size={size} />);
      expect(svg(container)).toHaveAttribute('width', String(size));
      expect(svg(container)).toHaveAttribute('stroke-width', stroke);
    });
  it('applies the default tone class, an explicit tone, or none', () => {
    expect(svg(render(<AppIcon name="add" />).container)).toHaveClass('text-icon-create');
    expect(svg(render(<AppIcon name="add" tone="danger" />).container)).toHaveClass('text-icon-danger');
    expect(svg(render(<AppIcon name="add" tone="none" />).container).getAttribute('class')).not.toMatch(/text-icon-/);
  });
  it('keeps extra classes and spins the loading icon', () => {
    const { container } = render(<AppIcon name="loading" className="mr-2" />);
    expect(svg(container)).toHaveClass('mr-2', 'animate-spin');
  });
  it('is an image with a name when labelled', () => {
    const { getByRole } = render(<AppIcon name="trendUp" label="tăng" />);
    expect(getByRole('img', { name: 'tăng' })).toBeInTheDocument();
  });
});
