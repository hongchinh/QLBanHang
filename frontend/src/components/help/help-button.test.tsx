import { createRef } from 'react';
import { act, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it } from 'vitest';
import { HelpButton } from './help-button';
import { useUiStore } from '@/stores/ui-store';

const NAME = 'Hướng dẫn màn hình này (F1)';

describe('HelpButton', () => {
  afterEach(() => {
    act(() => useUiStore.setState({ helpOpen: false }));
  });

  it('is named, desktop-only and collapsed by default', () => {
    render(<HelpButton showDot={false} />);
    const button = screen.getByRole('button', { name: NAME });
    expect(button).toHaveClass('hidden');
    expect(button).toHaveClass('md:flex');
    expect(button).toHaveAttribute('aria-expanded', 'false');
  });

  it('toggles the panel on click', async () => {
    render(<HelpButton showDot={false} />);
    await userEvent.click(screen.getByRole('button', { name: NAME }));
    expect(useUiStore.getState().helpOpen).toBe(true);
    expect(screen.getByRole('button', { name: NAME })).toHaveAttribute('aria-expanded', 'true');
  });

  it('shows the hint dot only when asked to', () => {
    const { rerender } = render(<HelpButton showDot />);
    expect(screen.getByTestId('help-hint-dot')).toBeInTheDocument();
    rerender(<HelpButton showDot={false} />);
    expect(screen.queryByTestId('help-hint-dot')).not.toBeInTheDocument();
  });

  it('forwards the ref to the button', () => {
    const ref = createRef<HTMLButtonElement>();
    render(<HelpButton ref={ref} showDot={false} />);
    expect(ref.current).toBe(screen.getByRole('button', { name: NAME }));
  });
});
