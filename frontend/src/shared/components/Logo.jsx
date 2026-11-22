import clsx from 'clsx';
import { Link } from 'react-router-dom';

export function LogoMark({ size = 32, className }) {
  return (
    <svg width={size} height={size} viewBox="0 0 64 64" className={className} aria-hidden>
      <defs>
        <linearGradient id="plastoids-g" x1="0" y1="0" x2="1" y2="1">
          <stop offset="0" stopColor="#003B7A" />
          <stop offset="0.4" stopColor="#0866C6" />
          <stop offset="0.75" stopColor="#00B8D9" />
          <stop offset="1" stopColor="#11CDB5" />
        </linearGradient>
      </defs>
      <rect width="64" height="64" rx="14" fill="#fff" />
      <path d="M18 54V10h16c10 0 17 6 17 15s-7 15-17 15h-7v14z M27 32h7c5 0 8-3 8-7s-3-7-8-7h-7z" fill="url(#plastoids-g)" fillRule="evenodd" />
    </svg>
  );
}

export function Logo({ to = '/', className, light = false, size = 32 }) {
  return (
    <Link to={to} className={clsx('inline-flex items-center gap-2 font-display text-xl font-extrabold tracking-tight', light ? 'text-white' : 'text-navy', className)} aria-label="Plastoids home">
      <LogoMark size={size} className="shrink-0 rounded-[10px] shadow-sm" />
      <span>Plastoids</span>
    </Link>
  );
}
