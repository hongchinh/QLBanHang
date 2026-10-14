import type { LucideProps } from 'lucide-react';
import { cn } from '@/lib/utils';
import { ICONS, type IconDef, type IconName, type IconTone } from './icon-registry';

export type IconSize = 8 | 12 | 14 | 16 | 18 | 20 | 24;

const STROKE: Record<IconSize, number> = { 8: 2, 12: 2, 14: 2, 16: 2, 18: 1.85, 20: 1.85, 24: 1.75 };

// Literal class names so Tailwind generates them.
const TONE_CLASS: Record<IconTone, string> = {
  create: 'text-icon-create', edit: 'text-icon-edit', danger: 'text-icon-danger', confirm: 'text-icon-confirm',
  info: 'text-icon-info', export: 'text-icon-export', print: 'text-icon-print', accent: 'text-icon-accent',
  muted: 'text-icon-muted', warn: 'text-icon-warn',
};

export interface AppIconProps extends Omit<LucideProps, 'ref' | 'size' | 'strokeWidth' | 'color'> {
  name: IconName;
  size?: IconSize;
  // Defaults to the registry tone; "none" keeps the surrounding text colour.
  tone?: IconTone | 'none';
  // Makes the icon an image with this accessible name; without it the icon is decorative.
  label?: string;
}

export function AppIcon({ name, size = 16, tone, label, className, ...rest }: AppIconProps) {
  const def: IconDef = ICONS[name];
  const Icon = def.icon;
  const effective = tone ?? def.tone;
  return (
    <Icon
      {...rest}
      width={size}
      height={size}
      strokeWidth={STROKE[size]}
      data-icon={name}
      className={cn('shrink-0', effective && effective !== 'none' && TONE_CLASS[effective], def.spin && 'animate-spin', className)}
      {...(label ? { role: 'img', 'aria-label': label } : { 'aria-hidden': true })}
    />
  );
}
