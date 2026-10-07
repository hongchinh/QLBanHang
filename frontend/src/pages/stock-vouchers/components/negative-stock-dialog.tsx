import { AlertTriangle } from 'lucide-react';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';

export interface NegativeStockShortage {
  // "{productCode}@{warehouseCode}" (D17).
  key: string;
  messages: string[];
}

export function toShortages(details?: Record<string, string[]>): NegativeStockShortage[] {
  return Object.entries(details ?? {}).map(([key, messages]) => ({ key, messages }));
}

function formatKey(key: string): string {
  const at = key.lastIndexOf('@');
  return at < 0 ? key : `${key.slice(0, at)} @ ${key.slice(at + 1)}`;
}

interface Props {
  open: boolean;
  blocked: boolean;
  shortages: NegativeStockShortage[];
  confirmLabel?: string;
  onConfirm: () => void;
  onClose: () => void;
}

export function NegativeStockDialog({
  open,
  blocked,
  shortages,
  confirmLabel = 'Vẫn lưu',
  onConfirm,
  onClose,
}: Props) {
  return (
    <Dialog open={open} onOpenChange={(next) => !next && onClose()}>
      <DialogContent showClose={false} className="max-w-lg">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <AlertTriangle className={blocked ? 'h-5 w-5 text-red-600' : 'h-5 w-5 text-amber-600'} />
            {blocked ? 'Không đủ tồn kho' : 'Cảnh báo xuất âm kho'}
          </DialogTitle>
          <DialogDescription>
            {blocked
              ? 'Các mặt hàng sau không đủ tồn kho, không thể thực hiện thao tác:'
              : 'Các mặt hàng sau sẽ bị âm kho. Bạn có muốn tiếp tục?'}
          </DialogDescription>
        </DialogHeader>
        <ul className="max-h-72 divide-y overflow-auto rounded-md border text-sm">
          {shortages.map((s) => (
            <li key={s.key} className="px-3 py-2">
              <div className="font-medium">{formatKey(s.key)}</div>
              {s.messages.map((m) => (
                <div key={m} className="text-muted-foreground">
                  {m}
                </div>
              ))}
            </li>
          ))}
        </ul>
        <DialogFooter>
          {blocked ? (
            <Button variant="outline" onClick={onClose}>
              Đóng
            </Button>
          ) : (
            <>
              <Button variant="outline" onClick={onClose}>
                Quay lại
              </Button>
              <Button variant="destructive" onClick={onConfirm}>
                {confirmLabel}
              </Button>
            </>
          )}
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
