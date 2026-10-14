import { AppIcon } from '@/components/icons';

// The rise / fall arrow of a delta; named for screen readers.
export function DeltaArrow({ direction }: { direction: 'up' | 'down' }) {
  return <AppIcon name={direction === 'up' ? 'trendUp' : 'trendDown'} size={12} label={direction === 'up' ? 'tăng' : 'giảm'} />;
}
