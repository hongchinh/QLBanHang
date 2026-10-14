import type { ExecutiveParams, ExecutiveTab } from '@/features/executive-dashboard/types';

interface OverviewTabProps {
  params: ExecutiveParams;
  // The other tabs on the bar; the "→" buttons open one of them.
  openTabs: ExecutiveTab[];
  onOpenTab(tab: ExecutiveTab): void;
}

// The Tổng quan tab. Its tiles come with the next task of the plan.
export function OverviewTab(_props: OverviewTabProps) {
  return null;
}
