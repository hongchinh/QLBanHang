import { Link } from 'react-router-dom';
import { AppIcon, type IconName } from '@/components/icons';
import { Card, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import type { Permission } from '@/lib/permissions';
import { useAuthStore } from '@/stores/auth-store';

const INVENTORY_CARDS: {
  to: string;
  permission?: Permission;
  // Shown when the user has at least one of these.
  anyPermission?: Permission[];
  icon: IconName;
  title: string;
  description: string;
}[] = [
  {
    to: '/settings/company',
    permission: 'settings.company',
    icon: 'settingsCompany',
    title: 'Thông tin đơn vị',
    description: 'Tên, mã số thuế, người ký trên sổ sách.',
  },
  {
    to: '/settings/branches',
    permission: 'branches.manage',
    icon: 'settingsBranches',
    title: 'Chi nhánh',
    description: 'Thêm, sửa, xóa chi nhánh.',
  },
  {
    to: '/settings/period-lock',
    permission: 'period_lock.manage',
    icon: 'settingsPeriodLock',
    title: 'Khóa sổ',
    description: 'Khóa chứng từ kho đến một ngày theo từng chi nhánh.',
  },
  {
    to: '/settings/numbering',
    anyPermission: ['inventory.settings', 'cash.settings'],
    icon: 'settingsNumbering',
    title: 'Đánh số chứng từ',
    description: 'Ký hiệu, độ dài và mẫu số phiếu kho, thu chi, chuyển quỹ, bù trừ công nợ.',
  },
  {
    to: '/settings/inventory',
    permission: 'inventory.settings',
    icon: 'settingsInventory',
    title: 'Cấu hình kho',
    description: 'Phương pháp tính giá vốn, kỳ tính giá, chính sách xuất âm.',
  },
  {
    to: '/settings/recalc-cost',
    permission: 'inventory.recalc_cost',
    icon: 'settingsRecalcCost',
    title: 'Tính lại giá vốn',
    description: 'Tính lại giá vốn bình quân từ một kỳ đến nay.',
  },
  {
    to: '/settings/cash',
    permission: 'cash.settings',
    icon: 'settingsCash',
    title: 'Cấu hình tiền & công nợ',
    description: 'Chính sách khi vượt hạn mức nợ và khi chi âm quỹ.',
  },
];

export function SettingsHubPage() {
  const hasPermission = useAuthStore((s) => s.hasPermission);
  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold">Cấu hình</h1>
        <p className="text-sm text-muted-foreground">
          Điểm vào cho các cài đặt cá nhân và quản trị.
        </p>
      </div>

      <div className="grid gap-4 md:grid-cols-2">
        {hasPermission('system.manage_settings') && (
          <Link to="/settings/quotation" className="block">
            <Card className="h-full transition hover:border-primary">
              <CardHeader>
                <div className="flex items-center gap-2">
                  <AppIcon name="system" size={20} />
                  <CardTitle>Cấu hình hệ thống báo giá</CardTitle>
                </div>
                <CardDescription>
                  Chọn trường ngày dùng trong báo cáo doanh thu và dashboard.
                </CardDescription>
              </CardHeader>
            </Card>
          </Link>
        )}
        <Link to="/settings/my-quotation-settings" className="block">
          <Card className="h-full transition hover:border-primary">
            <CardHeader>
              <div className="flex items-center gap-2">
                <AppIcon name="mySettings" size={20} />
                <CardTitle>Cài đặt báo giá của tôi</CardTitle>
              </div>
              <CardDescription>
                Upload template Excel cá nhân, xem cấu hình khoá trạng thái.
              </CardDescription>
            </CardHeader>
          </Card>
        </Link>

        {hasPermission('user_settings.manage') && (
          <Link to="/admin/users" className="block">
            <Card className="h-full transition hover:border-primary">
              <CardHeader>
                <div className="flex items-center gap-2">
                  <AppIcon name="users" size={20} />
                  <CardTitle>Quản lý người dùng</CardTitle>
                </div>
                <CardDescription>
                  Cấu hình lock-at theo từng user và chuyển nhượng báo giá hàng loạt.
                </CardDescription>
              </CardHeader>
            </Card>
          </Link>
        )}

        {INVENTORY_CARDS.filter(
          (c) =>
            (!c.permission || hasPermission(c.permission)) &&
            (!c.anyPermission || c.anyPermission.some((p) => hasPermission(p))),
        ).map(({ to, icon, title, description }) => (
          <Link key={to} to={to} className="block">
            <Card className="h-full transition hover:border-primary">
              <CardHeader>
                <div className="flex items-center gap-2">
                  <AppIcon name={icon} size={20} />
                  <CardTitle>{title}</CardTitle>
                </div>
                <CardDescription>{description}</CardDescription>
              </CardHeader>
            </Card>
          </Link>
        ))}
      </div>
    </div>
  );
}
