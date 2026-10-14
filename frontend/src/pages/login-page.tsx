import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Navigate, useLocation, useNavigate } from 'react-router-dom';
import { useLogin } from '@/features/auth/hooks';
import { useAuthStore } from '@/stores/auth-store';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { getErrorMessage } from '@/lib/api-client';
import { canAccessRoute } from '@/lib/route-permissions';
import { toast } from '@/lib/use-toast';

const schema = z.object({
  username: z.string().min(1, 'Vui lòng nhập username'),
  password: z.string().min(1, 'Vui lòng nhập mật khẩu'),
});

type FormValues = z.infer<typeof schema>;

function pickPostLoginTarget(
  from: string | undefined,
  perms: readonly string[],
  roles: readonly string[],
): string {
  if (!from || from.startsWith('/login') || from.startsWith('/403')) return '/';
  return canAccessRoute(from, perms, roles) ? from : '/';
}

export function LoginPage() {
  const isAuthenticated = useAuthStore((s) => s.isAuthenticated());
  const location = useLocation() as { state?: { from?: { pathname?: string } } };
  const navigate = useNavigate();
  const login = useLogin();

  const form = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { username: '', password: '' },
  });

  if (isAuthenticated) {
    const currentUser = useAuthStore.getState().user;
    const target = pickPostLoginTarget(
      location.state?.from?.pathname,
      currentUser?.permissions ?? [],
      currentUser?.roles ?? [],
    );
    return <Navigate to={target} replace />;
  }

  const onSubmit = (values: FormValues) => {
    login.mutate(values, {
      onSuccess: (data) => {
        toast({ variant: 'success', title: 'Đăng nhập thành công' });
        const target = pickPostLoginTarget(
          location.state?.from?.pathname,
          data.user.permissions,
          data.user.roles,
        );
        navigate(target, { replace: true });
      },
      onError: (err) => {
        toast({
          variant: 'destructive',
          title: 'Đăng nhập thất bại',
          description: getErrorMessage(err),
        });
      },
    });
  };

  return (
    <div className="flex flex-1 items-center justify-center bg-muted/30 p-4">
      <Card className="w-full max-w-md">
        <CardHeader>
          <CardTitle>Đăng nhập</CardTitle>
          <CardDescription>Phần mềm Quản lý Đơn hàng</CardDescription>
        </CardHeader>
        <CardContent>
          <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4" noValidate>
            <div className="space-y-2">
              <Label htmlFor="username">Tên đăng nhập</Label>
              <Input id="username" autoComplete="username" {...form.register('username')} />
              {form.formState.errors.username && (
                <p className="text-sm text-destructive">{form.formState.errors.username.message}</p>
              )}
            </div>
            <div className="space-y-2">
              <Label htmlFor="password">Mật khẩu</Label>
              <Input id="password" type="password" autoComplete="current-password" {...form.register('password')} />
              {form.formState.errors.password && (
                <p className="text-sm text-destructive">{form.formState.errors.password.message}</p>
              )}
            </div>
            <Button type="submit" className="w-full" disabled={login.isPending}>
              {login.isPending ? 'Đang đăng nhập...' : 'Đăng nhập'}
            </Button>
          </form>
        </CardContent>
      </Card>
    </div>
  );
}
