import { useState } from 'react';
import { formatMoneyForDisplay, parseMoneyInput } from '@/lib/money-input';

export interface MoneyCellProps {
  id: string;
  label: string;
  value: number | undefined;
  onCommit: (value: number | undefined) => void;
  disabled?: boolean;
  error?: string;
  errorId?: string;
}

// Formatted when idle; raw text while focused. Every keystroke commits the parsed value.
export function MoneyCell({ id, label, value, onCommit, disabled, error, errorId }: MoneyCellProps) {
  const [editing, setEditing] = useState<string | null>(null);
  return (
    <input
      id={id}
      className="cell-input cell-number tabular-nums"
      type="text"
      inputMode="decimal"
      aria-label={label}
      aria-invalid={error ? true : undefined}
      aria-describedby={error ? errorId : undefined}
      title={error}
      disabled={disabled}
      value={editing ?? formatMoneyForDisplay(value)}
      onFocus={(e) => {
        const el = e.currentTarget;
        setEditing(value === undefined ? '' : String(value));
        requestAnimationFrame(() => el.select());
      }}
      onChange={(e) => {
        setEditing(e.target.value);
        onCommit(parseMoneyInput(e.target.value));
      }}
      onBlur={() => setEditing(null)}
    />
  );
}

export function CellError({ id, message }: { id: string; message?: string }) {
  if (!message) return null;
  return (
    <div id={id} className="px-1 text-[11px] leading-tight text-destructive">
      {message}
    </div>
  );
}
