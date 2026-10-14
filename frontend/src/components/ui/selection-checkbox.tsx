import { useEffect, useRef } from 'react';

interface SelectionCheckboxProps {
  checked: boolean;
  indeterminate?: boolean;
  onCheckedChange: (checked: boolean) => void;
  'aria-label': string;
}

/** Native checkbox for list row selection; its click never reaches the row. */
export function SelectionCheckbox({
  checked,
  indeterminate = false,
  onCheckedChange,
  'aria-label': ariaLabel,
}: SelectionCheckboxProps) {
  const ref = useRef<HTMLInputElement>(null);
  useEffect(() => {
    if (ref.current) ref.current.indeterminate = indeterminate;
  }, [indeterminate]);

  return (
    <input
      ref={ref}
      type="checkbox"
      className="h-4 w-4 rounded border-input accent-primary"
      checked={checked}
      aria-label={ariaLabel}
      onClick={(e) => e.stopPropagation()}
      onChange={(e) => onCheckedChange(e.target.checked)}
    />
  );
}
