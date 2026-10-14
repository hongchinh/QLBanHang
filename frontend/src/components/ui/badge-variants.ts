import { cva } from 'class-variance-authority';

export const badgeVariants = cva(
  'qldh-badge-counter inline-flex items-center rounded-full border font-semibold transition-colors',
  {
    variants: {
      variant: {
        default: 'border-transparent bg-primary text-primary-foreground',
        secondary: 'border-transparent bg-secondary text-secondary-foreground',
        destructive: 'border-transparent bg-destructive text-destructive-foreground',
        outline: 'text-foreground',
        success: 'border-transparent bg-emerald-500/15 text-emerald-700 dark:text-emerald-300',
        warning: 'border-transparent bg-amber-500/15 text-amber-700 dark:text-amber-300',
        info: 'border-transparent bg-sky-500/15 text-sky-700 dark:text-sky-300',
      },
    },
    defaultVariants: { variant: 'default' },
  },
);
