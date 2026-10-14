import { Component, type ErrorInfo, type ReactNode } from 'react';
import { Button } from '@/components/ui/button';
import { cn } from '@/lib/utils';

interface Props {
  children: ReactNode;
  /**
   * Set it where a parent already gives the fallback its height (inside AppShell): the fallback fills that
   * box. Without it the fallback claims the viewport height itself, which is right only where it replaces
   * the whole app.
   */
  fill?: boolean;
}

interface State {
  error: Error | null;
  componentStack: string | null;
}

export class ErrorBoundary extends Component<Props, State> {
  state: State = { error: null, componentStack: null };

  static getDerivedStateFromError(error: Error): Partial<State> {
    return { error };
  }

  componentDidCatch(error: Error, info: ErrorInfo) {
    console.error('Unhandled UI error:', error, info);
    this.setState({ componentStack: info.componentStack ?? null });
  }

  reset = () => this.setState({ error: null, componentStack: null });

  render() {
    if (this.state.error) {
      return (
        <div
          className={cn(
            'flex',
            this.props.fill ? 'flex-1' : 'min-h-screen',
            'flex-col items-center justify-center gap-3 p-6 text-center',
          )}
        >
          <h1 className="text-2xl font-bold">Đã có lỗi xảy ra</h1>
          <p className="max-w-md text-sm text-muted-foreground">
            {this.state.error.message || 'Lỗi không xác định.'}
          </p>
          <div className="flex gap-2">
            <Button onClick={this.reset}>Thử lại</Button>
            <Button variant="outline" onClick={() => window.location.assign('/')}>
              Về trang chủ
            </Button>
          </div>
          {this.state.componentStack && (
            <details className="mt-4 max-w-2xl text-left">
              <summary className="cursor-pointer text-xs text-muted-foreground">Chi tiết lỗi (dành cho debug)</summary>
              <pre className="mt-2 max-h-64 overflow-auto rounded border bg-muted p-3 text-xs text-muted-foreground">
                {this.state.componentStack}
              </pre>
            </details>
          )}
        </div>
      );
    }
    return this.props.children;
  }
}
