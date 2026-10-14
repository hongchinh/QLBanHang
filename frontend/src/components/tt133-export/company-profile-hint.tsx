import { Link } from 'react-router-dom';
import { useCompanyProfile } from '@/features/company-profile/hooks';
import { useAuthStore } from '@/stores/auth-store';

// Shown next to print/export buttons when the company profile is still blank.
export function CompanyProfileHint() {
  const { data: company } = useCompanyProfile();
  const canEditCompany = useAuthStore((s) => s.hasPermission('settings.company'));

  if (company === undefined || company.name?.trim()) return null;

  return (
    <span className="text-xs text-muted-foreground">
      Chưa có thông tin đơn vị
      {canEditCompany && (
        <>
          {' '}
          <Link to="/settings/company" className="text-primary underline-offset-4 hover:underline">
            Cài đặt
          </Link>
        </>
      )}
    </span>
  );
}
