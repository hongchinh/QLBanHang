import { Link } from 'react-router-dom';
import { FileText, PanelLeftClose, PanelLeftOpen } from 'lucide-react';
import { cn } from '@/lib/utils';
import { logoUrl } from '@/features/branding/api';
import { useBrandingMeta } from '@/features/branding/hooks';

interface BrandBlockProps {
  collapsed: boolean;
  onToggleCollapse: () => void;
}

export function BrandBlock({ collapsed, onToggleCollapse }: BrandBlockProps) {
  const { data: meta } = useBrandingMeta();
  const version = meta?.updatedAt ?? '';
  const hasFull = meta?.hasLogoFull ?? false;
  const hasMark = meta?.hasLogoMark ?? false;

  const fullLogo = hasFull ? (
    <img
      src={logoUrl('full', version)}
      alt="Logo công ty"
      className="h-8 max-w-[200px] object-contain"
    />
  ) : (
    <>
      <FileText className="h-6 w-6 shrink-0" />
      <span className="truncate text-base font-bold">QLBanHang</span>
    </>
  );

  const markLogo = hasMark ? (
    <img
      src={logoUrl('mark', version)}
      alt="Logo công ty"
      className="h-8 w-8 object-contain"
    />
  ) : (
    <FileText className="h-6 w-6 shrink-0" />
  );

  return (
    <>
      {/* Mobile (<768px): 56px logo mark only, so the branch switcher and bell fit on a 360px screen. */}
      <div className="flex h-16 w-14 shrink-0 items-center border-r bg-header-brand-bg text-foreground md:hidden">
        <Link
          to="/"
          aria-label="Trang chủ"
          className="flex h-full w-full items-center justify-center overflow-hidden text-primary"
        >
          {markLogo}
        </Link>
      </div>

      {/* Desktop (≥768px): 240px expanded (logo+text+toggle) or 64px collapsed (logo mark + toggle). */}
      <div
        className={cn(
          'hidden h-16 shrink-0 items-center border-r bg-header-brand-bg text-foreground md:flex',
          'transition-[width] duration-200 ease-in-out',
          collapsed ? 'md:w-16' : 'md:w-60',
        )}
      >
        {collapsed ? (
          <button
            type="button"
            onClick={onToggleCollapse}
            aria-label="Mở rộng menu"
            className="mx-auto flex h-10 w-10 items-center justify-center rounded-md text-muted-foreground hover:bg-accent hover:text-foreground"
          >
            {hasMark ? markLogo : <PanelLeftOpen className="h-5 w-5" />}
          </button>
        ) : (
          <>
            <Link
              to="/"
              className="flex h-full flex-1 items-center gap-2 overflow-hidden px-4 text-primary"
            >
              {fullLogo}
            </Link>
            <button
              type="button"
              onClick={onToggleCollapse}
              aria-label="Thu gọn menu"
              className="mr-1 flex h-10 w-10 items-center justify-center rounded-md text-muted-foreground hover:bg-accent hover:text-foreground"
            >
              <PanelLeftClose className="h-5 w-5" />
            </button>
          </>
        )}
      </div>
    </>
  );
}
