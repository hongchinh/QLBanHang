import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { AppShell } from '../app-shell';

// Every class of the content box is load-bearing. `flex flex-col` lets the routed page fill the column with
// flex-1 (in a block box AppLayout would grow past the window), `min-h-0` lets the box shrink below its content,
// and `overflow-y-auto` keeps a page that is still taller than the room (the login card on a short window)
// scrolling inside the shell instead of the document.
const CONTENT_CLASSES = ['flex', 'min-h-0', 'flex-1', 'flex-col', 'overflow-y-auto'];

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
    // The banner cannot be scrolled away, so the slot takes its own height only up to 30% of the window; a
    // banner that wraps to more lines than that (a phone) scrolls inside the slot.
    expect(bannerSlot).toHaveClass('max-h-[30vh]', 'shrink-0', 'overflow-y-auto');
    expect(bannerSlot).toContainElement(screen.getByText('Bật thông báo'));
    expect(content).toHaveClass(...CONTENT_CLASSES);
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
    expect(shell.firstElementChild).toHaveClass(...CONTENT_CLASSES);
  });
});
