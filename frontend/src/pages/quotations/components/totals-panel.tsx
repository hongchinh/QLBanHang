import { useState } from 'react';
import { Link } from 'react-router-dom';
import { QrCode } from 'lucide-react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { computeTotals, type HeaderLike, type LineLike } from '@/pages/quotations/utils/compute-line';
import { formatMoneyForDisplay, parseMoneyInput } from '@/pages/quotations/utils/money-input';
import { buildPaymentQrHref } from '@/pages/quotations/utils/payment-qr-link';
import { useAuthStore } from '@/stores/auth-store';

interface Props {
  lines: LineLike[];
  header: HeaderLike;
  onHeaderChange: (patch: Partial<HeaderLike>) => void;
  quotationCode?: string;
}

const fmt = new Intl.NumberFormat('vi-VN');

export function TotalsPanel({ lines, header, onHeaderChange, quotationCode }: Props) {
  const totals = computeTotals(lines, header);
  const canViewCost = useAuthStore((s) => s.hasPermission('quotations.view_cost'));

  const renderQrLink = (amount: number) => {
    if (!quotationCode) return null;
    return (
      <Link
        to={buildPaymentQrHref(amount, quotationCode)}
        title="Tạo QR thanh toán"
        className="text-cyan-600 hover:text-cyan-700"
      >
        <QrCode className="h-3.5 w-3.5" />
      </Link>
    );
  };

  return (
    <Card>
      <CardHeader className="flex-row items-center border-b p-1 px-3 h-9 bg-blue-50">
        <CardTitle>Tổng cộng</CardTitle>
      </CardHeader>
      <CardContent className="space-y-1.5 px-3.5 pt-2.5 pb-3">

        {/* Tiền hàng */}
        <div className="flex items-center justify-between pb-1.5 border-b">
          <span className="text-sm text-muted-foreground">Tiền hàng</span>
          <span className="text-sm font-semibold tabular-nums">{fmt.format(totals.subtotal)}</span>
        </div>

        {/* CK + VC — inline labels */}
        <div className="grid grid-cols-[auto_1fr_auto_1fr] items-center gap-x-1.5 gap-y-0">
          <span className="text-[11px] text-muted-foreground">CK</span>
          <EditableMetric
            id="discount"
            value={header.discount}
            onChange={(value) => onHeaderChange({ discount: value })}
          />
          <span className="text-[11px] text-muted-foreground pl-1">VC</span>
          <EditableMetric
            id="freight"
            value={header.freight}
            onChange={(value) => onHeaderChange({ freight: value })}
          />
        </div>

        {/* Thuế % + Tiền thuế — inline labels */}
        <div className="grid grid-cols-[auto_60px_auto_1fr] items-center gap-x-1.5 gap-y-0">
          <span className="text-[11px] text-muted-foreground">Thuế %</span>
          <EditableMetric
            id="taxRate"
            value={header.taxRate}
            onChange={(value) => onHeaderChange({ taxRate: value })}
          />
          <span className="text-[11px] text-muted-foreground pl-1 text-right">Tiền thuế</span>
          <span className="text-sm tabular-nums text-right">{fmt.format(totals.taxAmount)}</span>
        </div>

        {/* Tổng cộng */}
        <div className="flex items-baseline justify-between border-t-2 border-foreground/70 pt-2 mt-1">
          <span className="text-sm font-bold">Tổng cộng</span>
          <span className="flex items-baseline gap-1.5">
            <span className="text-base font-extrabold tabular-nums">{fmt.format(totals.total)}</span>
            {renderQrLink(totals.total)}
          </span>
        </div>
        {canViewCost && (
          <div className="flex justify-end gap-3 text-[10.5px] text-muted-foreground tabular-nums -mt-0.5">
            <span>Giá vốn: {fmt.format(totals.totalCost)}</span>
            <span>LN gộp: {fmt.format(totals.grossProfit)}</span>
          </div>
        )}

        {/* Tạm ứng */}
        <div className="flex items-center justify-between border-t pt-1.5 mt-0.5">
          <span className="text-sm text-muted-foreground">Tạm ứng</span>
          <div className="flex items-center gap-1.5">
            <div className="w-[100px]">
              <EditableMetric
                id="advancePayment"
                value={header.advancePayment}
                onChange={(value) => onHeaderChange({ advancePayment: value })}
              />
            </div>
            {header.advancePayment > 0 && renderQrLink(header.advancePayment)}
          </div>
        </div>

        {header.advancePayment > 0 && (
          <div className="flex items-baseline justify-between">
            <span className="text-sm font-medium text-muted-foreground">Còn lại</span>
            <span className="flex items-baseline gap-1.5">
              <span
                className={[
                  'text-base font-bold tabular-nums',
                  totals.remainingBalance < 0 ? 'text-destructive' : '',
                ].join(' ')}
              >
                {fmt.format(totals.remainingBalance)}
              </span>
              {renderQrLink(totals.remainingBalance)}
            </span>
          </div>
        )}

      </CardContent>
    </Card>
  );
}

interface EditableMetricProps {
  id: string;
  value: number;
  onChange: (value: number) => void;
}

function EditableMetric({ id, value, onChange }: EditableMetricProps) {
  const [editing, setEditing] = useState(false);
  const [draft, setDraft] = useState('');
  return (
    <Input
      id={id}
      type="text"
      inputMode="decimal"
      autoComplete="off"
      value={editing ? draft : formatMoneyForDisplay(value)}
      onFocus={(e) => {
        setDraft(String(value ?? ''));
        setEditing(true);
        e.currentTarget.select();
      }}
      onBlur={() => setEditing(false)}
      onChange={(e) => {
        setDraft(e.target.value);
        const parsed = parseMoneyInput(e.target.value);
        onChange(parsed ?? 0);
      }}
      className="h-[26px] px-1.5 text-right tabular-nums"
    />
  );
}
