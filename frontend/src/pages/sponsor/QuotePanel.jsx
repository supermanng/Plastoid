import { Alert, Card, CardHeader } from '../../components/ui';
import { formatMoney, koboOf } from '../../lib/format';
import { computeQuote } from '../../api/sponsor';

/** Live quote preview (same maths as the API) with a budget check ex-VAT. */
export function QuotePreview({ seats, tuitionPerSeatKobo, sponsorSharePercent, budgetKobo, durationMonths }) {
  const q = computeQuote({ seats, tuitionPerSeatKobo, sponsorSharePercent });
  if (!q) {
    return <Alert tone="info">Enter seats, tuition per seat and the sponsor share to preview the quote.</Alert>;
  }
  const overBudget = budgetKobo > 0 && q.subtotal > budgetKobo;
  return (
    <Card className="bg-surface">
      <CardHeader title="Quote preview" description="Tuition + management fee (12%, min ₦3,000,000) + seat fee (₦12,000 per seat); VAT 7.5% on the fees only." />
      <QuoteLines
        lines={[
          ['Tuition', `${seats} seats × ${formatMoney(q.sharePerSeat)} (${sponsorSharePercent}% of ${formatMoney(tuitionPerSeatKobo)})`, q.tuition],
          ['Management fee', q.managementFee === 3_000_000 * 100 && q.tuition * 0.12 < q.managementFee ? 'Minimum fee applies' : '12% of tuition', q.managementFee],
          ['Seat fee', `${seats} × ₦12,000`, q.seatFee],
        ]}
        subtotal={q.subtotal}
        vat={q.vat}
        total={q.total}
      />
      <div className="mt-3 flex flex-wrap justify-between gap-2 text-xs text-muted">
        <span>Learner co-pay per seat: <span className="font-semibold text-navy">{q.coPayPerSeat ? formatMoney(q.coPayPerSeat) : 'none (fully sponsored)'}</span></span>
        {durationMonths ? <span>Fees are posted monthly over {durationMonths} month{durationMonths === 1 ? '' : 's'}.</span> : null}
      </div>
      {overBudget ? (
        <Alert tone="danger" className="mt-3" title="Quote exceeds budget">
          Before VAT the quote is {formatMoney(q.subtotal)} but the budget is {formatMoney(budgetKobo)}. Reduce seats or the sponsor share, or raise the budget, or the quote request will be refused.
        </Alert>
      ) : budgetKobo > 0 ? (
        <Alert tone="success" className="mt-3">Within budget: {formatMoney(budgetKobo - q.subtotal)} headroom before VAT.</Alert>
      ) : null}
    </Card>
  );
}

/** Renders quote lines from either the local preview or the API's QuoteView / QuoteResponse. */
export function QuoteLines({ lines, subtotal, vat, total }) {
  return (
    <table className="w-full text-sm">
      <tbody>
        {lines.map(([label, detail, amount]) => (
          <tr key={label} className="border-b border-line">
            <td className="py-2">
              <div className="font-semibold text-navy">{label}</div>
              {detail ? <div className="text-xs text-muted">{detail}</div> : null}
            </td>
            <td className="py-2 text-right tabular">{formatMoney(amount)}</td>
          </tr>
        ))}
        <tr><td className="py-2 text-muted">Subtotal (ex VAT)</td><td className="py-2 text-right tabular">{formatMoney(subtotal)}</td></tr>
        <tr><td className="py-2 text-muted">VAT 7.5% on fees</td><td className="py-2 text-right tabular">{formatMoney(vat)}</td></tr>
        <tr className="border-t border-line"><td className="py-2 text-base font-extrabold text-navy">Total</td><td className="py-2 text-right text-base font-extrabold text-navy tabular">{formatMoney(total)}</td></tr>
      </tbody>
    </table>
  );
}

/** Lines from a ProgrammeView.quote (QuoteView). */
export function quoteViewLines(quote) {
  if (!quote) return null;
  return {
    lines: [
      ['Tuition', `${quote.seats} seats × ${formatMoney(quote.tuitionPerSeatSponsorShare)}`, quote.tuition],
      ['Management fee', null, quote.managementFee],
      ['Seat fee', `${quote.seats} × ₦12,000`, quote.seatFee],
    ],
    subtotal: (koboOf(quote.tuition) || 0) + (koboOf(quote.managementFee) || 0) + (koboOf(quote.seatFee) || 0),
    vat: quote.vat,
    total: quote.total,
  };
}
