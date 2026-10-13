import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { AppShell } from '../app-shell';

// jsdom has no layout: these assert the structure that makes banner + app one viewport-high column.
// Only a browser can prove the pixels (the document must not scroll while the banner shows).
describe('AppShell', () => {
  it('is one viewport-high column: banner shrink-0 on top, the app takes the rest and may shrink', () => {
    render(
      <AppShell banner={<div>Bật thông báo</div>}>
        <div>app</div>
      </AppShell>,
    );

    const shell = screen.getByTestId('app-shell');
    expect(shell).toHaveClass('flex', 'h-screen', 'flex-col');
    const [bannerSlot, content] = Array.from(shell.children);
    expect(bannerSlot).toHaveClass('shrink-0');
    expect(bannerSlot).toContainElement(screen.getByText('Bật thông báo'));
    expect(content).toHaveClass('min-h-0', 'flex-1', 'flex-col');
    expect(content).toContainElement(screen.getByText('app'));
  });

  it('without a banner the app is the only child, so nothing changes', () => {
    render(
      <AppShell banner={null}>
        <div>app</div>
      </AppShell>,
    );

    const shell = screen.getByTestId('app-shell');
    expect(shell.children).toHaveLength(1);
    expect(shell.firstElementChild).toHaveClass('min-h-0', 'flex-1');
  });
});
