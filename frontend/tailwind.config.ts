import type { Config } from 'tailwindcss';
import animate from 'tailwindcss-animate';

export default {
  darkMode: ['class'],
  content: ['./index.html', './src/**/*.{ts,tsx}'],
  theme: {
    container: {
      center: true,
      padding: '2rem',
      screens: { '2xl': '1400px' },
    },
    extend: {
      colors: {
        icon: {
          create: 'hsl(var(--icon-create))', edit: 'hsl(var(--icon-edit))', danger: 'hsl(var(--icon-danger))',
          confirm: 'hsl(var(--icon-confirm))', info: 'hsl(var(--icon-info))', export: 'hsl(var(--icon-export))',
          print: 'hsl(var(--icon-print))', accent: 'hsl(var(--icon-accent))', muted: 'hsl(var(--icon-muted))',
          warn: 'hsl(var(--icon-warn))',
        },
        border: 'hsl(var(--border))',
        input: 'hsl(var(--input))',
        ring: 'hsl(var(--ring))',
        background: 'hsl(var(--background))',
        foreground: 'hsl(var(--foreground))',
        primary: {
          DEFAULT: 'hsl(var(--primary))',
          foreground: 'hsl(var(--primary-foreground))',
        },
        secondary: {
          DEFAULT: 'hsl(var(--secondary))',
          foreground: 'hsl(var(--secondary-foreground))',
        },
        destructive: {
          DEFAULT: 'hsl(var(--destructive))',
          foreground: 'hsl(var(--destructive-foreground))',
        },
        muted: {
          DEFAULT: 'hsl(var(--muted))',
          foreground: 'hsl(var(--muted-foreground))',
        },
        accent: {
          DEFAULT: 'hsl(var(--accent))',
          foreground: 'hsl(var(--accent-foreground))',
        },
        card: {
          DEFAULT: 'hsl(var(--card))',
          foreground: 'hsl(var(--card-foreground))',
        },
        popover: {
          DEFAULT: 'hsl(var(--popover))',
          foreground: 'hsl(var(--popover-foreground))',
        },
        header: {
          bg: 'hsl(var(--header-bg))',
          fg: 'hsl(var(--header-fg))',
          active: 'hsl(var(--header-active))',
          'brand-bg': 'hsl(var(--header-brand-bg))',
          danger: 'hsl(var(--header-danger))',
        },
        nav: {
          sales: 'hsl(var(--nav-sales))',
          inventory: 'hsl(var(--nav-inventory))',
          cash: 'hsl(var(--nav-cash))',
          debt: 'hsl(var(--nav-debt))',
          reports: 'hsl(var(--nav-reports))',
          catalogs: 'hsl(var(--nav-catalogs))',
          system: 'hsl(var(--nav-system))',
          'group-bg': 'hsl(var(--nav-group-bg))',
          'pin-bg': 'hsl(var(--nav-pin-bg))',
          'pin-fg': 'hsl(var(--nav-pin-fg))',
          'pin-border': 'hsl(var(--nav-pin-border))',
          match: 'hsl(var(--nav-match))',
        },
      },
      borderRadius: {
        lg: 'var(--radius)',
        md: 'calc(var(--radius) - 2px)',
        sm: 'calc(var(--radius) - 4px)',
      },
    },
  },
  plugins: [animate],
} satisfies Config;
