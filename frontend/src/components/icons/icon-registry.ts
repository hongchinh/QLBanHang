import {
  AlarmClock, AlertTriangle, ArrowDown, ArrowDownToLine, ArrowLeft, ArrowLeftRight, ArrowRight, ArrowRightLeft,
  ArrowUp, ArrowUpFromLine, BadgeCheck, BadgeDollarSign, Ban, Banknote, BarChart3, Bell, BookOpen, BookText, Boxes,
  Building2, Calculator, CalendarDays, CarFront, Check, CheckCircle2, ChevronDown, ChevronLeft, ChevronRight, Circle,
  CircleHelp, CirclePlus, ClipboardList, Clock, Combine, Container, Copy, CreditCard, Download, Eraser, ExternalLink, FileCheck, FileSpreadsheet,
  FileText, Files, Gauge, HandCoins, Hash, History, Hourglass, Keyboard, Landmark, LayoutDashboard, LayoutList,
  Library, Lightbulb, Link2, Link2Off, ListChecks, ListOrdered, Loader2, Lock, Menu, MoreHorizontal, MoreVertical, Package,
  PanelLeftClose, PanelLeftOpen, Pencil, PiggyBank, Play, Plus, Presentation, Printer, QrCode, ReceiptText, RotateCcw,
  Save, Scale, ScrollText, Search, SearchX, Send, Settings, Settings2, Share2, ShieldCheck, ShoppingCart, Sigma,
  SlidersHorizontal, Sparkles, Star, Tag, Target, Trash2, TrendingDown, TrendingUp, Truck, Undo2, UserCog,
  UserPlus, UserRound, Users, Users2, Wallet, Warehouse, X, type LucideIcon,
} from 'lucide-react';

// The only file that maps business names to lucide-react drawings; lucide-react may be imported only inside components/icons (enforced by no-direct-lucide-imports.test.ts and ESLint).
// Names describe the business meaning, not the drawing, so a later icon set can map them differently.

export const ICON_TONES = ['create', 'edit', 'danger', 'confirm', 'info', 'export', 'print', 'accent', 'muted', 'warn'] as const;
export type IconTone = (typeof ICON_TONES)[number];

export interface IconDef {
  icon: LucideIcon;
  // Default colour; a caller can pass tone="none" (icon on a filled or coloured surface).
  tone?: IconTone;
  // Always spins (loading indicators).
  spin?: boolean;
}

export const ICONS = {
  // Utility: no tone, the colour comes from the surrounding text.
  menu: { icon: Menu },
  sidebarCollapse: { icon: PanelLeftClose },
  sidebarExpand: { icon: PanelLeftOpen },
  chevronDown: { icon: ChevronDown },
  chevronRight: { icon: ChevronRight },
  chevronLeft: { icon: ChevronLeft },
  arrowLeft: { icon: ArrowLeft },
  arrowRight: { icon: ArrowRight },
  trendUp: { icon: TrendingUp },
  trendDown: { icon: TrendingDown },
  sortAsc: { icon: ArrowUp },
  sortDesc: { icon: ArrowDown },
  more: { icon: MoreHorizontal },
  moreVertical: { icon: MoreVertical },
  check: { icon: Check },
  radioIndicator: { icon: Circle },
  help: { icon: CircleHelp },
  video: { icon: Play },
  externalLink: { icon: ExternalLink },
  close: { icon: X },
  loading: { icon: Loader2, spin: true },
  pin: { icon: Star },
  searchEmpty: { icon: SearchX },
  keyboard: { icon: Keyboard },
  gridSettings: { icon: Settings2 },
  listPicker: { icon: LayoutList },
  notifications: { icon: Bell },
  brandFallback: { icon: FileText },
  transferOwnership: { icon: ArrowRightLeft },
  applyMatching: { icon: Check },
  time: { icon: Clock },
  calendar: { icon: CalendarDays },
  paymentQr: { icon: QrCode },
  copy: { icon: Copy },
  download: { icon: Download },
  share: { icon: Share2 },

  // Actions (tone = the per-action colour of the conventions).
  search: { icon: Search, tone: 'muted' },
  back: { icon: ArrowLeft, tone: 'muted' },
  closeForm: { icon: X, tone: 'muted' },
  remove: { icon: X, tone: 'danger' },
  add: { icon: Plus, tone: 'create' },
  addUser: { icon: UserPlus, tone: 'create' },
  edit: { icon: Pencil, tone: 'edit' },
  delete: { icon: Trash2, tone: 'danger' },
  cancelVoucher: { icon: Ban, tone: 'danger' },
  restore: { icon: RotateCcw, tone: 'confirm' },
  discardChanges: { icon: Undo2, tone: 'muted' },
  unsettle: { icon: Undo2, tone: 'danger' },
  save: { icon: Save, tone: 'edit' },
  send: { icon: Send, tone: 'create' },
  confirm: { icon: CheckCircle2, tone: 'confirm' },
  accountingConfirm: { icon: BadgeCheck, tone: 'confirm' },
  clone: { icon: Copy, tone: 'accent' },
  print: { icon: Printer, tone: 'print' },
  exportExcel: { icon: FileSpreadsheet, tone: 'export' },
  exportPdf: { icon: FileText, tone: 'print' },
  autoMatch: { icon: Sparkles, tone: 'accent' },
  clearMatches: { icon: Eraser, tone: 'danger' },
  warning: { icon: AlertTriangle, tone: 'warn' },
  helpHint: { icon: Lightbulb, tone: 'warn' },
  recalcCost: { icon: Calculator, tone: 'edit' },
  openingBalance: { icon: Wallet, tone: 'confirm' },

  // Document activity history (activity-history.tsx, quotation-form-page.tsx).
  activityCreated: { icon: CirclePlus, tone: 'create' },
  activityUpdated: { icon: Save, tone: 'edit' },
  activitySent: { icon: Send, tone: 'create' },
  activityConfirmed: { icon: CheckCircle2, tone: 'confirm' },
  activityAccountingConfirmed: { icon: CheckCircle2, tone: 'info' },
  activityCancelled: { icon: Ban, tone: 'danger' },
  activityRestored: { icon: RotateCcw, tone: 'confirm' },
  activityDeleted: { icon: Trash2, tone: 'danger' },
  activitySettlementRemoved: { icon: Link2Off, tone: 'warn' },
  activityAutoVoucherChanged: { icon: ReceiptText, tone: 'print' },
  activityOwnerTransferred: { icon: ArrowRightLeft, tone: 'accent' },
  activityCloned: { icon: Copy, tone: 'accent' },

  // Sidebar (nav-config.ts): groups, then items. Drawings must stay unique within the sidebar.
  overview: { icon: LayoutDashboard },
  sales: { icon: ShoppingCart },
  quotation: { icon: FileText },
  customers: { icon: Users },
  inventory: { icon: Warehouse },
  stockIn: { icon: ArrowDownToLine },
  stockOut: { icon: ArrowUpFromLine },
  openingStock: { icon: ClipboardList },
  cash: { icon: Wallet },
  receipt: { icon: HandCoins },
  payment: { icon: Banknote },
  fundTransfer: { icon: ArrowLeftRight },
  debt: { icon: Scale },
  debtMatching: { icon: Link2 },
  debtOffset: { icon: Combine },
  openingDebt: { icon: History },
  creditLimit: { icon: Gauge },
  reports: { icon: BarChart3 },
  executiveDashboard: { icon: Presentation },
  revenueReport: { icon: TrendingUp },
  salesRevenueReport: { icon: BadgeDollarSign },
  vehicleRevenueReport: { icon: CarFront },
  salesPerformanceReport: { icon: Target },
  stockOnHandReport: { icon: Boxes },
  stockCardReport: { icon: ScrollText },
  stockSummaryReport: { icon: Sigma },
  cashBookReport: { icon: PiggyBank },
  debtBalanceReport: { icon: BookOpen },
  debtLedgerReport: { icon: BookText },
  debtByDocumentReport: { icon: Files },
  debtAgingReport: { icon: Hourglass },
  debtOverdueReport: { icon: AlarmClock },
  debtBySalespersonReport: { icon: UserRound },
  reconciliationReport: { icon: FileCheck },
  catalogs: { icon: Library },
  product: { icon: Package },
  productGroup: { icon: Tag },
  supplier: { icon: Truck },
  warehouse: { icon: Container },
  stockReason: { icon: ListChecks },
  cashReason: { icon: ListOrdered },
  paymentMethod: { icon: CreditCard },
  fund: { icon: Landmark },
  system: { icon: Settings },
  mySettings: { icon: UserCog },
  systemConfig: { icon: SlidersHorizontal },
  users: { icon: Users2 },
  roles: { icon: ShieldCheck },

  // Settings hub cards (settings-hub-page.tsx); the hub's other cards use system, mySettings, users.
  settingsCompany: { icon: Building2 },
  settingsBranches: { icon: Building2 },
  settingsPeriodLock: { icon: Lock },
  settingsNumbering: { icon: Hash },
  settingsInventory: { icon: SlidersHorizontal },
  settingsRecalcCost: { icon: Calculator },
  settingsCash: { icon: HandCoins },
  branch: { icon: Building2 },

  // Home page KPI labels (dashboard-page.tsx).
  kpiRevenueToday: { icon: TrendingUp },
  kpiRevenueRange: { icon: BarChart3 },
  kpiQuotations: { icon: FileText },
  kpiCancelled: { icon: Ban },
} satisfies Record<string, IconDef>;

export type IconName = keyof typeof ICONS;

export function iconDef(name: IconName): IconDef {
  return ICONS[name];
}
