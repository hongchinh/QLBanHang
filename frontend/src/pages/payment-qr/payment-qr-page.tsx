import { useEffect, useRef, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { Controller, useForm, type Resolver } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { QRCodeCanvas } from 'qrcode.react';
import { Card, CardContent } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { ButtonLoader } from '@/components/ui/button-loader';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { toast } from '@/lib/use-toast';
import { getErrorMessage } from '@/lib/api-client';
import { formatCurrencyVnd } from '@/lib/utils';
import { useBanks } from '@/features/banks/hooks';
import { useMyBankAccounts } from '@/features/bank-accounts/hooks';
import { useGenerateQr } from '@/features/payment-qr/hooks';
import { paymentQrSchema, type PaymentQrFormParsed, type PaymentQrFormValues } from '@/features/payment-qr/schema';

export function PaymentQrPage() {
  const [searchParams] = useSearchParams();
  const { data: banks } = useBanks();
  const { data: savedAccounts } = useMyBankAccounts();
  const generateQr = useGenerateQr();
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const [payload, setPayload] = useState<string | null>(null);
  const prefilledFromDefaultRef = useRef(false);

  const form = useForm<PaymentQrFormValues, unknown, PaymentQrFormParsed>({
    resolver: zodResolver(paymentQrSchema) as unknown as Resolver<PaymentQrFormValues, unknown, PaymentQrFormParsed>,
    defaultValues: {
      bankId: '',
      accountNumber: '',
      accountName: '',
      amount: Number(searchParams.get('amount')) || undefined,
      content: searchParams.get('content') ?? undefined,
    },
  });

  const selectedBank = banks?.find((b) => b.id === form.watch('bankId'));

  useEffect(() => {
    if (prefilledFromDefaultRef.current) return;
    if (!savedAccounts) return;
    prefilledFromDefaultRef.current = true;

    const defaultAccount = savedAccounts.find((a) => a.isDefault);
    if (defaultAccount && !form.getValues('bankId')) {
      form.reset({
        ...form.getValues(),
        bankId: defaultAccount.bankId,
        accountNumber: defaultAccount.accountNumber,
        accountName: defaultAccount.accountName,
      });
    }
  }, [savedAccounts, form]);

  const onSubmit = form.handleSubmit(async (values) => {
    try {
      const result = await generateQr.mutateAsync(values);
      setPayload(result.payload);
    } catch (err) {
      toast({ title: 'Tạo QR thất bại', description: getErrorMessage(err), variant: 'destructive' });
    }
  });

  const handleDownload = () => {
    const canvas = canvasRef.current;
    if (!canvas) return;
    const url = canvas.toDataURL('image/png');
    const a = document.createElement('a');
    a.href = url;
    a.download = 'qr-thanh-toan.png';
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
  };

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold">Tạo QR thanh toán</h1>
        <p className="text-sm text-muted-foreground">
          Tạo mã QR VietQR để nhận chuyển khoản qua ứng dụng ngân hàng.
        </p>
      </div>

      <Card>
        <CardContent className="space-y-4 pt-6">
          <form className="space-y-4" onSubmit={onSubmit}>
            <div className="space-y-2">
              <Label htmlFor="bankId">Ngân hàng</Label>
              <Controller
                control={form.control}
                name="bankId"
                render={({ field }) => (
                  <Select value={field.value} onValueChange={field.onChange}>
                    <SelectTrigger id="bankId" aria-label="Ngân hàng">
                      <SelectValue placeholder="Chọn ngân hàng" />
                    </SelectTrigger>
                    <SelectContent>
                      {banks?.map((bank) => (
                        <SelectItem key={bank.id} value={bank.id}>{bank.name}</SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                )}
              />
              {form.formState.errors.bankId && (
                <p className="text-sm text-destructive">{form.formState.errors.bankId.message}</p>
              )}
            </div>

            <div className="space-y-2">
              <Label htmlFor="accountNumber">Số tài khoản</Label>
              <Input id="accountNumber" {...form.register('accountNumber')} />
              {form.formState.errors.accountNumber && (
                <p className="text-sm text-destructive">{form.formState.errors.accountNumber.message}</p>
              )}
            </div>

            <div className="space-y-2">
              <Label htmlFor="accountName">Chủ tài khoản</Label>
              <Input id="accountName" {...form.register('accountName')} />
              {form.formState.errors.accountName && (
                <p className="text-sm text-destructive">{form.formState.errors.accountName.message}</p>
              )}
            </div>

            <div className="space-y-2">
              <Label htmlFor="amount">Số tiền (VNĐ)</Label>
              <Input id="amount" type="number" {...form.register('amount')} />
              {form.formState.errors.amount && (
                <p className="text-sm text-destructive">{form.formState.errors.amount.message}</p>
              )}
            </div>

            <div className="space-y-2">
              <Label htmlFor="content">Nội dung chuyển khoản</Label>
              <Input id="content" {...form.register('content')} />
            </div>

            <Button type="submit" disabled={generateQr.isPending}>
              {generateQr.isPending && <ButtonLoader className="mr-2" />}
              Tạo QR
            </Button>
          </form>
        </CardContent>
      </Card>

      {payload && (
        <Card>
          <CardContent className="flex flex-col items-center gap-4 pt-6">
            <QRCodeCanvas ref={canvasRef} value={payload} size={280} />
            <div className="w-full max-w-sm space-y-1 text-sm">
              <p><strong>Ngân hàng:</strong> {selectedBank?.name}</p>
              <p><strong>Số tài khoản:</strong> {form.getValues('accountNumber')}</p>
              <p><strong>Chủ tài khoản:</strong> {form.getValues('accountName')}</p>
              <p><strong>Số tiền:</strong> {formatCurrencyVnd(form.getValues('amount'))} đ</p>
              {form.getValues('content') && <p><strong>Nội dung:</strong> {form.getValues('content')}</p>}
            </div>
            <Button variant="outline" onClick={handleDownload}>
              Tải ảnh QR
            </Button>
          </CardContent>
        </Card>
      )}
    </div>
  );
}
