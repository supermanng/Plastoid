/* Formatting helpers. Money from the API is { amountKobo | kobo, currency } or a plain kobo integer. */

const ngn = new Intl.NumberFormat('en-NG', { style: 'currency', currency: 'NGN', maximumFractionDigits: 0 });
const ngnPrecise = new Intl.NumberFormat('en-NG', { style: 'currency', currency: 'NGN', minimumFractionDigits: 2, maximumFractionDigits: 2 });

export function koboOf(money) {
  if (money === null || money === undefined) return null;
  if (typeof money === 'number') return money;
  if (typeof money === 'string') return Number(money);
  if (typeof money === 'object') {
    for (const key of ['amountKobo', 'kobo', 'amount_kobo', 'amount', 'value']) {
      if (typeof money[key] === 'number') return money[key];
    }
  }
  return null;
}

export function formatMoney(money, { precise = false, fallback = '—' } = {}) {
  const kobo = koboOf(money);
  if (kobo === null || Number.isNaN(kobo)) return fallback;
  const naira = kobo / 100;
  return (precise || naira % 1 !== 0 ? ngnPrecise : ngn).format(naira);
}

export function nairaToKobo(naira) {
  const n = Number(String(naira).replace(/[^\d.]/g, ''));
  return Number.isFinite(n) ? Math.round(n * 100) : 0;
}

export function formatDate(value, options = {}) {
  if (!value) return '—';
  const d = value instanceof Date ? value : new Date(value);
  if (Number.isNaN(d.getTime())) return '—';
  return d.toLocaleDateString('en-NG', { day: 'numeric', month: 'short', year: 'numeric', ...options });
}

export function formatDateTime(value) {
  if (!value) return '—';
  const d = new Date(value);
  if (Number.isNaN(d.getTime())) return '—';
  return d.toLocaleString('en-NG', { day: 'numeric', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit' });
}

export function formatTime(value) {
  if (!value) return '—';
  const d = new Date(value);
  return d.toLocaleTimeString('en-NG', { hour: '2-digit', minute: '2-digit' });
}

export function relativeTime(value) {
  if (!value) return '';
  const diff = (new Date(value).getTime() - Date.now()) / 1000;
  const abs = Math.abs(diff);
  const rtf = new Intl.RelativeTimeFormat('en', { numeric: 'auto' });
  if (abs < 60) return rtf.format(Math.round(diff), 'second');
  if (abs < 3600) return rtf.format(Math.round(diff / 60), 'minute');
  if (abs < 86400) return rtf.format(Math.round(diff / 3600), 'hour');
  return rtf.format(Math.round(diff / 86400), 'day');
}

export function formatPercent(value, digits = 0) {
  if (value === null || value === undefined || Number.isNaN(Number(value))) return '—';
  const n = Number(value);
  return `${(n <= 1 ? n * 100 : n).toFixed(digits)}%`;
}

export function titleCase(value) {
  if (!value) return '';
  return String(value)
    .replace(/[_-]+/g, ' ')
    .replace(/([a-z])([A-Z])/g, '$1 $2')
    .toLowerCase()
    .replace(/(^|\s)\w/g, (c) => c.toUpperCase());
}

export function initials(name) {
  return String(name || '?')
    .split(/\s+/)
    .slice(0, 2)
    .map((p) => p[0]?.toUpperCase() || '')
    .join('');
}

export function toDateInput(value) {
  if (!value) return '';
  const d = new Date(value);
  return Number.isNaN(d.getTime()) ? '' : d.toISOString().slice(0, 10);
}

export function pluralise(count, singular, plural = `${singular}s`) {
  return `${count} ${count === 1 ? singular : plural}`;
}
