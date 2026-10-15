import { useNavigate } from 'react-router-dom';
import { AppIcon } from '@/components/icons';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { Avatar, AvatarFallback, AvatarImage } from '@/components/ui/avatar';
import type { CurrentUser } from '@/stores/auth-store';

interface HeaderUserMenuProps {
  user: CurrentUser | null;
  onLogout: () => void;
  triggerRef?: React.Ref<HTMLButtonElement>;
  onOpenHelp?: () => void;
}

export function HeaderUserMenu({ user, onLogout, triggerRef, onOpenHelp }: HeaderUserMenuProps) {
  const navigate = useNavigate();

  if (!user) return null;

  const initials = getInitials(user.fullName);

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <button
          type="button"
          ref={triggerRef}
          aria-label="Mở menu người dùng"
          className="flex items-center gap-2 rounded-md px-2 py-1 text-header-fg hover:bg-header-active focus:outline-none focus-visible:ring-2 focus-visible:ring-white/70"
        >
          <Avatar className="h-8 w-8">
            <AvatarImage src={undefined} alt={user.fullName} />
            <AvatarFallback className="bg-white/15 text-sm font-semibold text-header-fg">
              {initials}
            </AvatarFallback>
          </Avatar>
          <span className="hidden max-w-[160px] truncate text-sm font-medium lg:inline">
            {user.fullName}
          </span>
          <AppIcon name="chevronDown" className="hidden lg:inline" />
        </button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" className="w-64">
        <DropdownMenuLabel>
          <div className="font-semibold leading-tight">{user.fullName}</div>
          <div className="text-xs font-normal text-muted-foreground">{user.username}</div>
        </DropdownMenuLabel>
        <DropdownMenuSeparator />
        {onOpenHelp && (
          <DropdownMenuItem className="md:hidden" onSelect={onOpenHelp}>
            <AppIcon name="help" /> Hướng dẫn màn hình này
          </DropdownMenuItem>
        )}
        <DropdownMenuItem onSelect={() => navigate('/settings/my-quotation-settings')}>
          Cài đặt của tôi
        </DropdownMenuItem>
        <DropdownMenuSeparator />
        <DropdownMenuItem
          onSelect={onLogout}
          className="text-[hsl(var(--header-danger))] focus:text-[hsl(var(--header-danger))]"
        >
          Đăng xuất
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}

function getInitials(fullName: string): string {
  const parts = fullName.trim().split(/\s+/).filter(Boolean);
  if (parts.length === 0) return '?';
  if (parts.length === 1) return parts[0].slice(0, 2).toUpperCase();
  const first = parts[0][0];
  const last = parts[parts.length - 1][0];
  return (first + last).toUpperCase();
}
