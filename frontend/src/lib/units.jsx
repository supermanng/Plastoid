import { useQuery } from '@tanstack/react-query';
import { api } from './api';
import { formatMoney, koboOf } from './format';

/*
 * Units (ADR 0018): a prepaid balance priced by an admin-approved rate. Prices stay in naira in the API; the UI shows
 * the whole units a price costs (rounded up, which is what the API charges when paying with units).
 */

export const unitRateKey = ['wallet', 'rate'];

/** GET /wallet/rate → { rateKobo, rateValue, version, effectiveFrom, source } */
export function useUnitRate() {
  return useQuery({
    queryKey: unitRateKey,
    queryFn: () => api.get('/wallet/rate', undefined, { auth: false }),
    staleTime: 5 * 60_000,
  });
}

/** Whole units needed for a Money (or kobo) at a rate: ceil(kobo / rateKobo). */
export function unitsFor(money, rateKobo) {
  const kobo = koboOf(money);
  if (kobo === null || !rateKobo || rateKobo <= 0) return null;
  if (kobo <= 0) return 0;
  return Math.ceil(kobo / rateKobo);
}

/** Naira value of a number of units at a rate, as Money. */
export function valueOf(units, rateKobo) {
  return { amount: Math.round((Number(units) || 0) * (rateKobo || 0)), currency: 'NGN' };
}

export function formatUnits(n, { fallback = '—' } = {}) {
  if (n === null || n === undefined || Number.isNaN(Number(n))) return fallback;
  const v = Number(n);
  return `${new Intl.NumberFormat('en-NG').format(v)} ${Math.abs(v) === 1 ? 'unit' : 'units'}`;
}

/**
 * "45 units" with the naira equivalent beneath. Falls back to naira alone while the rate loads.
 * `size`: 'sm' | 'md' | 'lg'. `inline` renders both on one line ("45 units · ₦45,000").
 */
export function UnitPrice({ money, size = 'md', inline = false, className = '', prefix = '', free = 'Free' }) {
  const rate = useUnitRate();
  const kobo = koboOf(money);
  if (kobo === null || kobo === undefined) return <span className={className}>—</span>;
  if (kobo === 0 && free) return <span className={`font-bold text-navy ${className}`}>{free}</span>;
  const units = unitsFor(money, rate.data?.rateKobo);
  const sizes = { sm: 'text-sm', md: 'text-base', lg: 'text-lg' };
  if (units === null) {
    return <span className={`font-bold text-navy tabular ${sizes[size]} ${className}`}>{prefix}{formatMoney(money)}</span>;
  }
  if (inline) {
    return (
      <span className={`tabular ${className}`}>
        <span className={`font-bold text-navy ${sizes[size]}`}>{prefix}{formatUnits(units)}</span>
        <span className="text-xs text-muted"> · {formatMoney(money)}</span>
      </span>
    );
  }
  return (
    <span className={`inline-flex flex-col leading-tight tabular ${className}`}>
      <span className={`font-bold text-navy ${sizes[size]}`}>{prefix}{formatUnits(units)}</span>
      <span className="text-xs font-normal text-muted">{formatMoney(money)}</span>
    </span>
  );
}
