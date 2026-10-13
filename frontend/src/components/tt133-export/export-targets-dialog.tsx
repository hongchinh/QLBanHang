import { useState } from 'react';
import { X } from 'lucide-react';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';

export interface ExportTarget {
  id: string;
  label: string;
}

export type ExportTargetChoice = { ids: string[] } | { all: true };

interface Props {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  title: string;
  // The item the page is showing; without it the dialog starts at "Chọn nhiều".
  current?: ExportTarget | null;
  // The page's own picker; it calls `add` for every item the user picks.
  renderPicker: (add: (item: ExportTarget) => void) => React.ReactNode;
  onConfirm: (choice: ExportTargetChoice) => void;
}

type Mode = 'current' | 'many' | 'all';

function Body({ current, renderPicker, onConfirm, onCancel }: Omit<Props, 'open' | 'onOpenChange' | 'title'> & { onCancel: () => void }) {
  const [mode, setMode] = useState<Mode>(current ? 'current' : 'many');
  const [items, setItems] = useState<ExportTarget[]>([]);

  const add = (item: ExportTarget) =>
    setItems((prev) => (prev.some((p) => p.id === item.id) ? prev : [...prev, item]));
  const remove = (id: string) => setItems((prev) => prev.filter((p) => p.id !== id));

  const canConfirm = mode !== 'many' || items.length > 0;
  const confirm = () => {
    if (mode === 'all') onConfirm({ all: true });
    else if (mode === 'current' && current) onConfirm({ ids: [current.id] });
    else onConfirm({ ids: items.map((i) => i.id) });
  };

  const options: { value: Mode; label: string }[] = [
    ...(current ? [{ value: 'current' as const, label: `Đang xem: ${current.label}` }] : []),
    { value: 'many', label: 'Chọn nhiều' },
    { value: 'all', label: 'Tất cả có phát sinh trong kỳ' },
  ];

  return (
    <>
      <div className="space-y-3" role="radiogroup">
        {options.map((o) => (
          <label key={o.value} className="flex items-center gap-2 text-sm">
            <input type="radio" name="export-targets" checked={mode === o.value} onChange={() => setMode(o.value)} />
            {o.label}
          </label>
        ))}
      </div>
      {mode === 'many' && (
        <div className="space-y-2">
          {renderPicker(add)}
          {items.length > 0 && (
            <ul className="flex flex-wrap gap-2">
              {items.map((item) => (
                <li key={item.id} data-chip className="flex items-center gap-1 rounded-full border bg-muted px-2 py-1 text-xs">
                  <span>{item.label}</span>
                  <button
                    type="button"
                    aria-label={`Bỏ ${item.label}`}
                    className="rounded-full hover:text-destructive"
                    onClick={() => remove(item.id)}
                  >
                    <X className="h-3 w-3" />
                  </button>
                </li>
              ))}
            </ul>
          )}
        </div>
      )}
      <DialogFooter>
        <Button variant="outline" onClick={onCancel}>
          Hủy
        </Button>
        <Button disabled={!canConfirm} onClick={confirm}>
          Xuất
        </Button>
      </DialogFooter>
    </>
  );
}

// Which targets a multi-target form (S12, S06, S08) is exported for: the item being viewed, several picked ones,
// or everything with activity. The body is mounted only while open, so every opening starts fresh.
export function ExportTargetsDialog({ open, onOpenChange, title, current, renderPicker, onConfirm }: Props) {
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-lg">
        <DialogHeader>
          <DialogTitle>{title}</DialogTitle>
        </DialogHeader>
        <Body
          current={current}
          renderPicker={renderPicker}
          onConfirm={onConfirm}
          onCancel={() => onOpenChange(false)}
        />
      </DialogContent>
    </Dialog>
  );
}
