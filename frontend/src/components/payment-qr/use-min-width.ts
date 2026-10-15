import { useEffect, useState } from 'react';

function matches(px: number): boolean {
  // Without matchMedia (old browsers, jsdom) assume a desktop screen.
  if (typeof window === 'undefined' || typeof window.matchMedia !== 'function') return true;
  return window.matchMedia(`(min-width: ${px}px)`).matches;
}

export function useMinWidth(px: number): boolean {
  const [value, setValue] = useState(() => matches(px));

  useEffect(() => {
    if (typeof window.matchMedia !== 'function') return;
    const list = window.matchMedia(`(min-width: ${px}px)`);
    const update = () => setValue(list.matches);
    update();
    list.addEventListener('change', update);
    return () => list.removeEventListener('change', update);
  }, [px]);

  return value;
}
