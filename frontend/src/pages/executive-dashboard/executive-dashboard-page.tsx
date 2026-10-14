import type { ComponentType } from 'react';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { LiveStatus } from '@/features/dashboard/components/live-status';
import { RangePicker } from '@/features/dashboard/components/range-picker';
import { previousPeriod } from '@/features/executive-dashboard/period';
import { EXECUTIVE_PRESETS } from '@/features/executive-dashboard/presets';
import type { ExecutiveParams, ExecutiveTab } from '@/features/executive-dashboard/types';
import { useExecutiveAccess } from '@/features/executive-dashboard/use-executive-access';
import { useExecutiveLive } from '@/features/executive-dashboard/use-executive-live';
import { useExecutiveParams } from '@/features/executive-dashboard/use-executive-params';
import { useExecutiveScope } from '@/features/executive-dashboard/use-executive-scope';
import { formatDateOnly } from '@/lib/date-only';
import { ScopeSelect } from './components/scope-select';
import { CashTab } from './tabs/cash-tab';
import { DebtTab } from './tabs/debt-tab';
import { InventoryTab } from './tabs/inventory-tab';
import { OverviewTab } from './tabs/overview-tab';
import { SalesTab } from './tabs/sales-tab';

interface TabPanelProps {
  params: ExecutiveParams;
  openTabs: ExecutiveTab[];
  onOpenTab(tab: ExecutiveTab): void;
}

const TAB_LABELS: Record<ExecutiveTab, string> = {
  overview: 'Tổng quan',
  sales: 'Kinh doanh',
  cash: 'Tiền',
  debt: 'Công nợ',
  inventory: 'Kho',
};

// The tabs that exist. The bar shows those the user may open (useExecutiveAccess), in the order of access.tabs.
const PANELS: Partial<Record<ExecutiveTab, ComponentType<TabPanelProps>>> = {
  overview: OverviewTab,
  sales: SalesTab,
  cash: CashTab,
  debt: DebtTab,
  inventory: InventoryTab,
};

// Bảng điều hành: sales, cash, debt and inventory of the whole company or one branch, kept live.
export function ExecutiveDashboardPage() {
  const { from, to, tab, setRange, setPreset, setTab } = useExecutiveParams();
  const scope = useExecutiveScope();
  const access = useExecutiveAccess();
  const live = useExecutiveLive(scope.branchId);

  const params: ExecutiveParams = { from, to, branchId: scope.branchId };
  const previous = previousPeriod(from, to);
  const panels = access.tabs.flatMap((key) => {
    const Panel = PANELS[key];
    return Panel ? [{ key, Panel }] : [];
  });
  const listed = panels.map((p) => p.key);
  const activeTab: ExecutiveTab = listed.includes(tab) ? tab : 'overview';
  const openTabs = listed.filter((key) => key !== 'overview');

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div className="space-y-1">
          <div className="flex flex-wrap items-center gap-x-3 gap-y-1">
            <h1 className="text-2xl font-semibold tracking-tight">Điều hành</h1>
            <LiveStatus status={live.status} updatedAt={live.updatedAt} />
          </div>
          <p className="text-xs text-muted-foreground">
            {`So với kỳ trước: ${formatDateOnly(previous.from)} – ${formatDateOnly(previous.to)}`}
          </p>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          <ScopeSelect />
          <RangePicker from={from} to={to} onChange={setRange} onPreset={setPreset} presets={EXECUTIVE_PRESETS} />
        </div>
      </div>

      {/* Only the active panel is mounted (TabsContent default), so the queries of the other tabs do not run. */}
      <Tabs value={activeTab} onValueChange={(value) => setTab(value as ExecutiveTab)}>
        {/* Five tabs are wider than a phone: the bar scrolls sideways, from its first tab. */}
        <TabsList className="max-w-full justify-start overflow-x-auto">
          {panels.map(({ key }) => (
            <TabsTrigger key={key} value={key}>
              {TAB_LABELS[key]}
            </TabsTrigger>
          ))}
        </TabsList>
        {panels.map(({ key, Panel }) => (
          <TabsContent key={key} value={key} className="mt-4">
            <Panel params={params} openTabs={openTabs} onOpenTab={setTab} />
          </TabsContent>
        ))}
      </Tabs>
    </div>
  );
}
