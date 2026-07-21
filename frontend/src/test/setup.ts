import '@testing-library/jest-dom/vitest';
import { cleanup } from '@testing-library/react';
import type * as ReactModule from 'react';
import type * as RechartsModule from 'recharts';
import { type ReactNode } from 'react';
import { afterEach, vi } from 'vitest';

if (typeof globalThis.ResizeObserver === 'undefined') {
  globalThis.ResizeObserver = class {
    observe = vi.fn();
    unobserve = vi.fn();
    disconnect = vi.fn();
  } as unknown as typeof ResizeObserver;
}

// jsdom doesn't implement the Pointer Capture API; Radix UI's Select (and other
// pointer-driven primitives) call these during interaction, so stub them as no-ops.
if (typeof Element.prototype.hasPointerCapture === 'undefined') {
  Element.prototype.hasPointerCapture = () => false;
}
if (typeof Element.prototype.setPointerCapture === 'undefined') {
  Element.prototype.setPointerCapture = () => {};
}
if (typeof Element.prototype.releasePointerCapture === 'undefined') {
  Element.prototype.releasePointerCapture = () => {};
}
if (typeof Element.prototype.scrollIntoView === 'undefined') {
  Element.prototype.scrollIntoView = () => {};
}

// Recharts ResponsiveContainer relies on layout measurement, which jsdom does not provide.
// Inject explicit width/height into the child chart so it renders during tests.
vi.mock('recharts', async () => {
  const React = await vi.importActual<typeof ReactModule>('react');
  const actual = await vi.importActual<typeof RechartsModule>('recharts');
  const MockResponsive = ({ children }: { children: ReactNode }) => {
    const child = React.Children.only(children) as React.ReactElement<{ width?: number; height?: number }>;
    return React.cloneElement(child, { width: 400, height: 200 });
  };
  return { ...actual, ResponsiveContainer: MockResponsive };
});

afterEach(() => {
  cleanup();
});
