import { useCallback, useEffect, useState } from 'react';

/** Selection of the rows of one list page; resets when `resetKey` changes. */
export function useRowSelection(pageIds: readonly string[], resetKey: string) {
  const [selected, setSelected] = useState<Set<string>>(() => new Set());

  useEffect(() => {
    setSelected(new Set());
  }, [resetKey]);

  const selectedIds = pageIds.filter((id) => selected.has(id));
  const allSelected = pageIds.length > 0 && selectedIds.length === pageIds.length;
  const someSelected = selectedIds.length > 0 && !allSelected;

  const isSelected = useCallback((id: string) => selected.has(id), [selected]);

  const toggle = useCallback((id: string, checked: boolean) => {
    setSelected((prev) => {
      const next = new Set(prev);
      if (checked) next.add(id);
      else next.delete(id);
      return next;
    });
  }, []);

  const toggleAll = (checked: boolean) => {
    setSelected(checked ? new Set(pageIds) : new Set());
  };

  const clear = useCallback(() => setSelected(new Set()), []);

  return { selectedIds, isSelected, toggle, toggleAll, allSelected, someSelected, clear };
}
