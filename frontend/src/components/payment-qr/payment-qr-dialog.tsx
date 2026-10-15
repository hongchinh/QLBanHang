import { useRef, type ReactNode } from 'react';
import { Link } from 'react-router-dom';
import { QRCodeCanvas } from 'qrcode.react';
import { AppIcon } from '@/components/icons';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogDescription, DialogTitle } from '@/components/ui/dialog';
import { Sheet, SheetContent } from '@/components/ui/sheet';
import { useVoucherPaymentQr } from '@/features/voucher-payment-qr/hooks';
import type { PaymentQrKind, VoucherPaymentQr } from '@/features/voucher-payment-qr/types';
import { formatApiErrorDetails, getApiError } from '@/lib/api-client';
import { toast } from '@/lib/use-toast';
import { cn, formatCurrencyVnd } from '@/lib/utils';
import { useAuthStore } from '@/stores/auth-store';
import { buildQrInfoText, copyText, shareOrDownload } from './qr-actions';
import { canvasToBlob, composeQrImage } from './qr-image';
import { useMinWidth } from './use-min-width';

export interface PaymentQrDialogProps {
  kind: PaymentQrKind;
  id: string;
  voucherCode: string;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

const QR_SIZE = 264;
const UNAVAILABLE_CODE = 'PAYMENT_QR_UNAVAILABLE';

// The two server messages that the fund setup (Quỹ screen) can fix.
function isFundSetupMessage(message: string): boolean {
  return message.includes('chưa khai ngân hàng') || message.includes('chưa có quỹ ngân hàng mặc định');
}

// The part of the dialog that scrolls; the title above it and the actions below it stay in place.
function ScrollBody({ children, className }: { children: ReactNode; className?: string }) {
  return (
    <div data-testid="payment-qr-scroll" className={cn('min-h-0 flex-1 overflow-y-auto px-6 pb-4', className)}>
      {children}
    </div>
  );
}

function DetailRow({ label, value, copyValue }: { label: string; value: ReactNode; copyValue?: string }) {
  return (
    <div className="flex items-start gap-3 py-1.5 text-sm">
      <span className="w-28 shrink-0 text-muted-foreground">{label}</span>
      <span className="min-w-0 flex-1 break-words font-medium">{value}</span>
      {copyValue !== undefined && (
        <Button
          type="button"
          variant="ghost"
          size="icon"
          aria-label={`Sao chép ${label.toLowerCase()}`}
          title="Sao chép"
          onClick={() => void copyText(copyValue)}
        >
          <AppIcon name="copy" size={14} />
        </Button>
      )}
    </div>
  );
}

function PaymentQrDetails({ qr, voucherCode }: { qr: VoucherPaymentQr; voucherCode: string }) {
  const qrRef = useRef<HTMLCanvasElement>(null);

  const download = async () => {
    const qrCanvas = qrRef.current;
    if (!qrCanvas) return;
    try {
      const picture = composeQrImage(qrCanvas, {
        amount: qr.amount,
        content: qr.content,
        bankLine: `${qr.bankShortName} · ${qr.accountNumber}`,
      });
      await shareOrDownload(await canvasToBlob(picture), `QR_${voucherCode}.png`);
    } catch {
      toast({ title: 'Không tạo được ảnh QR', variant: 'destructive' });
    }
  };

  return (
    <>
      <ScrollBody className="space-y-3">
        <div className="flex justify-center">
          <div className="rounded-lg border bg-white p-1">
            <QRCodeCanvas
              ref={qrRef}
              value={qr.payload}
              size={QR_SIZE}
              level="M"
              boostLevel={false}
              marginSize={4}
              bgColor="#ffffff"
              role="img"
              aria-label="Mã QR chuyển khoản"
            />
          </div>
        </div>
        <div className="text-center">
          <div className="text-3xl font-bold tabular-nums">
            {formatCurrencyVnd(qr.amount)} <small className="text-base font-medium text-muted-foreground">đ</small>
          </div>
          {qr.cashPaid > 0 && (
            <p className="text-sm text-muted-foreground">
              {`Tổng phiếu ${formatCurrencyVnd(qr.total)} − đã trả tiền mặt ${formatCurrencyVnd(qr.cashPaid)}`}
            </p>
          )}
          {qr.sourceStockVoucherCode && (
            <p className="text-sm text-muted-foreground">{`Phiếu thu tự động theo ${qr.sourceStockVoucherCode}`}</p>
          )}
        </div>
        <div className="divide-y rounded-md border px-3">
          <DetailRow label="Ngân hàng" value={qr.bankName} />
          <DetailRow label="Số tài khoản" value={<span className="font-mono">{qr.accountNumber}</span>} copyValue={qr.accountNumber} />
          <DetailRow label="Chủ tài khoản" value={qr.accountHolderName} />
          <DetailRow label="Nội dung" value={<span className="font-mono">{qr.content}</span>} copyValue={qr.content} />
          <DetailRow label="Quỹ nhận" value={`${qr.fundName} (${qr.fundCode})`} />
        </div>
      </ScrollBody>
      {/* Outside the scrolling body, so the actions stay visible on a short screen. */}
      <div
        data-testid="payment-qr-actions"
        className="flex shrink-0 flex-col gap-2 border-t bg-background px-6 pb-[max(1rem,env(safe-area-inset-bottom))] pt-4 sm:flex-row sm:justify-end"
      >
        <Button type="button" variant="outline" onClick={() => void copyText(buildQrInfoText(qr))}>
          <AppIcon name="copy" />
          Sao chép thông tin
        </Button>
        <Button type="button" variant="outline" onClick={() => void download()}>
          <AppIcon name="download" />
          Tải ảnh QR
        </Button>
      </div>
    </>
  );
}

function PaymentQrBody({
  query,
  voucherCode,
}: {
  query: ReturnType<typeof useVoucherPaymentQr>;
  voucherCode: string;
}) {
  const canManageFunds = useAuthStore((s) => s.hasPermission('cash.catalogs.manage'));

  // isFetching, not isLoading: the dialog stays mounted while closed, so on a reopening the old answer is still there.
  if (query.isFetching) {
    return (
      <ScrollBody>
        <div role="status" className="flex items-center justify-center py-16 text-muted-foreground">
          <AppIcon name="loading" size={24} label="Đang tải" />
        </div>
      </ScrollBody>
    );
  }

  if (query.isError) {
    const apiError = getApiError(query.error);
    if (apiError?.code === UNAVAILABLE_CODE) {
      return (
        <ScrollBody>
          <div role="alert" className="space-y-2 rounded-md border border-destructive/30 bg-destructive/10 p-3 text-sm text-destructive">
            <p>{apiError.message}</p>
            {canManageFunds && isFundSetupMessage(apiError.message) && (
              <Link to="/funds" className="inline-block font-medium text-primary underline-offset-4 hover:underline">
                Mở màn Quỹ
              </Link>
            )}
          </div>
        </ScrollBody>
      );
    }
    return (
      <ScrollBody>
        <div role="alert" className="rounded-md border border-destructive/30 bg-destructive/10 p-3 text-sm text-destructive">
          <div>{formatApiErrorDetails(query.error)}</div>
          <Button type="button" variant="outline" size="sm" className="mt-2" onClick={() => void query.refetch()}>
            Thử lại
          </Button>
        </div>
      </ScrollBody>
    );
  }

  if (!query.data) return null;
  return <PaymentQrDetails qr={query.data} voucherCode={voucherCode} />;
}

// The transfer QR of a voucher: a dialog on a wide screen, a bottom sheet on a phone. It asks the server on every
// opening, because the QR depends on the live fund and voucher.
export function PaymentQrDialog({ kind, id, voucherCode, open, onOpenChange }: PaymentQrDialogProps) {
  const isWide = useMinWidth(640);
  const query = useVoucherPaymentQr(kind, id, { enabled: open });

  // Title and description are the Radix dialog parts, which a sheet shares.
  const header = (
    <div className="shrink-0 space-y-1 px-6 pb-3 pt-6">
      <DialogTitle>QR chuyển khoản</DialogTitle>
      <DialogDescription>{voucherCode}</DialogDescription>
    </div>
  );
  const body = <PaymentQrBody query={query} voucherCode={voucherCode} />;

  if (isWide) {
    return (
      <Dialog open={open} onOpenChange={onOpenChange}>
        <DialogContent data-testid="payment-qr-dialog" className="flex max-h-[90dvh] max-w-md flex-col gap-0 overflow-hidden p-0">
          {header}
          {body}
        </DialogContent>
      </Dialog>
    );
  }

  return (
    <Sheet open={open} onOpenChange={onOpenChange}>
      <SheetContent
        side="bottom"
        data-testid="payment-qr-sheet"
        className="flex max-h-[90dvh] flex-col gap-0 overflow-hidden rounded-t-xl p-0"
      >
        {header}
        {body}
      </SheetContent>
    </Sheet>
  );
}
