import { useEffect, useState } from 'react';

export type ToastVariant = 'default' | 'success' | 'destructive';

export interface ToastItem {
  id: string;
  title?: string;
  description?: string;
  variant?: ToastVariant;
  durationMs?: number;
  // A button in the toast (e.g. navigate to a settings page); the toast closes after it runs.
  action?: ToastAction;
}

export interface ToastAction {
  label: string;
  onClick: () => void;
}

type Listener = (toasts: ToastItem[]) => void;

const listeners = new Set<Listener>();
let toasts: ToastItem[] = [];

function emit() {
  for (const l of listeners) l(toasts);
}

function uid() {
  return Math.random().toString(36).slice(2);
}

export function toast(input: Omit<ToastItem, 'id'>) {
  const item: ToastItem = { id: uid(), durationMs: 4000, variant: 'default', ...input };
  toasts = [...toasts, item];
  emit();
  return item.id;
}

export function dismissToast(id: string) {
  toasts = toasts.filter((t) => t.id !== id);
  emit();
}

export function useToasts(): ToastItem[] {
  const [state, setState] = useState<ToastItem[]>(toasts);
  useEffect(() => {
    const listener: Listener = (next) => setState(next);
    listeners.add(listener);
    return () => {
      listeners.delete(listener);
    };
  }, []);
  return state;
}
