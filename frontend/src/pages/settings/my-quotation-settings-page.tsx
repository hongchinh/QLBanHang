import { useRef, useState } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { toast } from '@/lib/use-toast';
import { getErrorMessage } from '@/lib/api-client';
import { useDeleteTemplate, useMySettings, useUploadTemplate, useUploadHandoverTemplate, useDeleteHandoverTemplate } from '@/features/me-settings/hooks';
import { meSettingsApi, HandoverTemplateType } from '@/features/me-settings/api';
import { useAuthStore } from '@/stores/auth-store';
import { BrandingTab } from '@/features/branding/branding-tab';
import { BankAccountsTab } from '@/features/bank-accounts/bank-accounts-tab';

const MAX_BYTES = 5 * 1024 * 1024;

const LOCK_LABELS: Record<string, string> = {
  Sent: 'Đã gửi',
  Confirmed: 'Đã xác nhận',
};

interface HandoverTemplateCardProps {
  type: HandoverTemplateType;
  title: string;
  description: string;
  settings: ReturnType<typeof useMySettings>['data'];
}

function HandoverTemplateCard({ type, title, description, settings }: HandoverTemplateCardProps) {
  const upload = useUploadHandoverTemplate(type);
  const remove = useDeleteHandoverTemplate(type);
  const inputRef = useRef<HTMLInputElement>(null);
  const [downloading, setDownloading] = useState(false);
  const [downloadingDefault, setDownloadingDefault] = useState(false);
  const [downloadingEffective, setDownloadingEffective] = useState(false);

  const isWithPrice = type === 'handover-with-price';
  const templateFileName = isWithPrice
    ? settings?.handoverWithPriceTemplateFileName
    : settings?.handoverNoPriceTemplateFileName;
  const templateOriginalName = isWithPrice
    ? settings?.handoverWithPriceTemplateOriginalName
    : settings?.handoverNoPriceTemplateOriginalName;
  const templateUploadedAt = isWithPrice
    ? settings?.handoverWithPriceTemplateUploadedAt
    : settings?.handoverNoPriceTemplateUploadedAt;

  const hasTemplate = !!templateFileName;

  const handlePick = () => inputRef.current?.click();

  const handleUpload = async (file: File) => {
    if (file.size > MAX_BYTES) {
      toast({ title: 'File quá lớn', description: 'Tối đa 5MB.', variant: 'destructive' });
      return;
    }
    try {
      await upload.mutateAsync(file);
      toast({ title: 'Đã tải template lên', description: file.name });
    } catch (err) {
      toast({ title: 'Tải lên thất bại', description: getErrorMessage(err), variant: 'destructive' });
    } finally {
      if (inputRef.current) inputRef.current.value = '';
    }
  };

  const handleDelete = async () => {
    try {
      await remove.mutateAsync();
      toast({ title: 'Đã xoá template', description: 'Sẽ dùng template mặc định.' });
    } catch (err) {
      toast({ title: 'Xoá thất bại', description: getErrorMessage(err), variant: 'destructive' });
    }
  };

  const handleDownload = async () => {
    if (!templateFileName) return;
    setDownloading(true);
    try {
      const blob = await meSettingsApi.downloadHandoverTemplate(type);
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = templateOriginalName ?? templateFileName;
      document.body.appendChild(a);
      a.click();
      document.body.removeChild(a);
      URL.revokeObjectURL(url);
    } catch (err) {
      toast({ title: 'Tải về thất bại', description: getErrorMessage(err), variant: 'destructive' });
    } finally {
      setDownloading(false);
    }
  };

  const handleDownloadDefault = async () => {
    setDownloadingDefault(true);
    try {
      const blob = await meSettingsApi.downloadDefaultTemplate(type);
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = type === 'handover-with-price' ? 'templete_bbbg.xlsx' : 'templete_bbbg_sl.xlsx';
      document.body.appendChild(a);
      a.click();
      document.body.removeChild(a);
      URL.revokeObjectURL(url);
    } catch (err) {
      toast({ title: 'Tải về thất bại', description: getErrorMessage(err), variant: 'destructive' });
    } finally {
      setDownloadingDefault(false);
    }
  };

  const handleDownloadEffective = async () => {
    setDownloadingEffective(true);
    try {
      const blob = await meSettingsApi.downloadEffectiveTemplate(type);
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = templateOriginalName ?? templateFileName ?? (type === 'handover-with-price' ? 'templete_bbbg.xlsx' : 'templete_bbbg_sl.xlsx');
      document.body.appendChild(a);
      a.click();
      document.body.removeChild(a);
      URL.revokeObjectURL(url);
    } catch (err) {
      toast({ title: 'Tải về thất bại', description: getErrorMessage(err), variant: 'destructive' });
    } finally {
      setDownloadingEffective(false);
    }
  };

  return (
    <Card>
      <CardHeader>
        <CardTitle>{title}</CardTitle>
        <CardDescription>{description}</CardDescription>
      </CardHeader>
      <CardContent className="space-y-3">
        {hasTemplate ? (
          <>
            <p className="text-sm">
              <strong>{templateOriginalName ?? templateFileName}</strong>
              {templateUploadedAt && (
                <span className="ml-2 text-muted-foreground">
                  (cập nhật {new Date(templateUploadedAt).toLocaleString('vi-VN')})
                </span>
              )}
            </p>
            <div className="flex gap-2">
              <Button variant="outline" onClick={handleDownload} disabled={downloading}>
                Tải về
              </Button>
              <Button variant="outline" onClick={handlePick}>Tải lên thay thế</Button>
              <Button variant="destructive" onClick={handleDelete} disabled={remove.isPending}>
                Xoá
              </Button>
            </div>
          </>
        ) : (
          <>
            <p className="text-sm text-muted-foreground">Đang dùng template mặc định của hệ thống.</p>
            <Button onClick={handlePick}>Tải lên template riêng</Button>
          </>
        )}
        <div className="flex gap-2 pt-1">
          <Button variant="outline" size="sm" onClick={handleDownloadEffective} disabled={downloadingEffective}>
            Tải template hiện tại
          </Button>
          <Button variant="ghost" size="sm" onClick={handleDownloadDefault} disabled={downloadingDefault}>
            Tải template mặc định
          </Button>
        </div>
        <input
          ref={inputRef}
          type="file"
          accept=".xlsx,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
          className="hidden"
          onChange={(e) => {
            const file = e.target.files?.[0];
            if (file) void handleUpload(file);
          }}
        />
      </CardContent>
    </Card>
  );
}

function QuotationSettingsTabContent() {
  const { data: settings, isLoading } = useMySettings();
  const upload = useUploadTemplate();
  const remove = useDeleteTemplate();
  const inputRef = useRef<HTMLInputElement>(null);
  const [downloading, setDownloading] = useState(false);
  const [downloadingDefault, setDownloadingDefault] = useState(false);
  const [downloadingEffective, setDownloadingEffective] = useState(false);

  const handlePick = () => inputRef.current?.click();

  const handleUpload = async (file: File) => {
    if (file.size > MAX_BYTES) {
      toast({ title: 'File quá lớn', description: 'Tối đa 5MB.', variant: 'destructive' });
      return;
    }
    try {
      await upload.mutateAsync(file);
      toast({ title: 'Đã tải template lên', description: file.name });
    } catch (err) {
      toast({ title: 'Tải lên thất bại', description: getErrorMessage(err), variant: 'destructive' });
    } finally {
      if (inputRef.current) inputRef.current.value = '';
    }
  };

  const handleDelete = async () => {
    try {
      await remove.mutateAsync();
      toast({ title: 'Đã xoá template', description: 'Sẽ dùng template mặc định.' });
    } catch (err) {
      toast({ title: 'Xoá thất bại', description: getErrorMessage(err), variant: 'destructive' });
    }
  };

  const handleDownload = async () => {
    if (!settings?.templateFileName) return;
    setDownloading(true);
    try {
      const blob = await meSettingsApi.downloadTemplate();
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = settings.templateOriginalName ?? settings.templateFileName;
      document.body.appendChild(a);
      a.click();
      document.body.removeChild(a);
      URL.revokeObjectURL(url);
    } catch (err) {
      toast({ title: 'Tải về thất bại', description: getErrorMessage(err), variant: 'destructive' });
    } finally {
      setDownloading(false);
    }
  };

  const handleDownloadDefault = async () => {
    setDownloadingDefault(true);
    try {
      const blob = await meSettingsApi.downloadDefaultTemplate('quotation');
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = 'template_baogia.xlsx';
      document.body.appendChild(a);
      a.click();
      document.body.removeChild(a);
      URL.revokeObjectURL(url);
    } catch (err) {
      toast({ title: 'Tải về thất bại', description: getErrorMessage(err), variant: 'destructive' });
    } finally {
      setDownloadingDefault(false);
    }
  };

  const handleDownloadEffective = async () => {
    setDownloadingEffective(true);
    try {
      const blob = await meSettingsApi.downloadEffectiveTemplate('quotation');
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = settings?.templateOriginalName ?? settings?.templateFileName ?? 'template_baogia.xlsx';
      document.body.appendChild(a);
      a.click();
      document.body.removeChild(a);
      URL.revokeObjectURL(url);
    } catch (err) {
      toast({ title: 'Tải về thất bại', description: getErrorMessage(err), variant: 'destructive' });
    } finally {
      setDownloadingEffective(false);
    }
  };

  if (isLoading) return <div>Đang tải…</div>;

  const hasTemplate = !!settings?.templateFileName;
  const lockLabel = settings?.lockAtStatus ? LOCK_LABELS[settings.lockAtStatus] : null;

  return (
    <div className="space-y-6">
      <Card>
        <CardHeader>
          <CardTitle>Template Excel</CardTitle>
          <CardDescription>
            File .xlsx dùng khi xuất báo giá. Nếu chưa upload, hệ thống dùng template mặc định.
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-3">
          {hasTemplate ? (
            <>
              <p className="text-sm">
                <strong>{settings!.templateOriginalName ?? settings!.templateFileName}</strong>
                {settings!.templateUploadedAt && (
                  <span className="ml-2 text-muted-foreground">
                    (cập nhật {new Date(settings!.templateUploadedAt).toLocaleString('vi-VN')})
                  </span>
                )}
              </p>
              <div className="flex gap-2">
                <Button variant="outline" onClick={handleDownload} disabled={downloading}>
                  Tải về
                </Button>
                <Button variant="outline" onClick={handlePick}>Tải lên thay thế</Button>
                <Button variant="destructive" onClick={handleDelete} disabled={remove.isPending}>
                  Xoá
                </Button>
              </div>
            </>
          ) : (
            <>
              <p className="text-sm text-muted-foreground">Đang dùng template mặc định của hệ thống.</p>
              <Button onClick={handlePick}>Tải lên template riêng</Button>
            </>
          )}
          <div className="flex gap-2 pt-1">
            <Button variant="outline" size="sm" onClick={handleDownloadEffective} disabled={downloadingEffective}>
              Tải template hiện tại
            </Button>
            <Button variant="ghost" size="sm" onClick={handleDownloadDefault} disabled={downloadingDefault}>
              Tải template mặc định
            </Button>
          </div>
          <input
            ref={inputRef}
            type="file"
            accept=".xlsx,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
            className="hidden"
            onChange={(e) => {
              const file = e.target.files?.[0];
              if (file) void handleUpload(file);
            }}
          />
        </CardContent>
      </Card>

      <HandoverTemplateCard
        type="handover-with-price"
        title="Template Biên bản bàn giao (có tiền)"
        description="File .xlsx dùng khi xuất biên bản bàn giao kèm giá. Nếu chưa upload, hệ thống dùng template mặc định."
        settings={settings}
      />

      <HandoverTemplateCard
        type="handover-no-price"
        title="Template Biên bản bàn giao (không tiền)"
        description="File .xlsx dùng khi xuất biên bản bàn giao không kèm giá. Nếu chưa upload, hệ thống dùng template mặc định."
        settings={settings}
      />

      <Card>
        <CardHeader>
          <CardTitle>Khoá theo trạng thái</CardTitle>
          <CardDescription>Cấu hình do quản trị viên thiết lập (chỉ xem).</CardDescription>
        </CardHeader>
        <CardContent>
          {lockLabel ? (
            <p className="text-sm">
              Báo giá từ trạng thái <strong>{lockLabel}</strong> trở lên sẽ không cho phép bạn chỉnh sửa.
            </p>
          ) : (
            <p className="text-sm text-muted-foreground">Không có giới hạn nào (chưa cấu hình).</p>
          )}
        </CardContent>
      </Card>
    </div>
  );
}

export function MyQuotationSettingsPage() {
  const canManageBranding = useAuthStore((s) => s.hasPermission('user_settings.manage'));

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold">Cài đặt của tôi</h1>
        <p className="text-sm text-muted-foreground">
          Quản lý template Excel, cấu hình khoá trạng thái{canManageBranding ? ' và logo công ty' : ''}.
        </p>
      </div>

      <Tabs defaultValue="quotation" className="space-y-4">
        <TabsList>
          <TabsTrigger value="quotation">Cài đặt báo giá</TabsTrigger>
          <TabsTrigger value="bank-accounts">Tài khoản ngân hàng</TabsTrigger>
          {canManageBranding && <TabsTrigger value="branding">Logo công ty</TabsTrigger>}
        </TabsList>
        <TabsContent value="quotation">
          <QuotationSettingsTabContent />
        </TabsContent>
        <TabsContent value="bank-accounts">
          <BankAccountsTab />
        </TabsContent>
        {canManageBranding && (
          <TabsContent value="branding">
            <BrandingTab />
          </TabsContent>
        )}
      </Tabs>
    </div>
  );
}
