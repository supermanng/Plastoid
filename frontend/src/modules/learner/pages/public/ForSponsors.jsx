import { BarChart3, FileText, Landmark, ShieldCheck, Users } from 'lucide-react';
import { Button, Card, Stepper } from '../../../../shared/components/ui';
import { useAuth } from '../../../../shared/lib/auth';
import { formatMoney } from '../../../../shared/lib/format';
import { AppLink } from '../../../../shared/lib/navigation';

const STEPS = ['Objectives', 'Course', 'Seats & budget', 'Eligibility', 'Dates', 'Selection', 'Review & quote'];

/* Worked example of the quote composition (mirrors Sponsorship.Domain.QuoteLines): 150 seats × ₦60,000 tuition. */
const EXAMPLE = (() => {
  const seats = 150;
  const tuition = 60_000_00 * seats;
  const management = Math.max(Math.round(tuition * 0.12), 3_000_000_00);
  const seatFee = 12_000_00 * seats;
  const vat = Math.round((management + seatFee) * 0.075);
  return { seats, tuition, management, seatFee, vat, total: tuition + management + seatFee + vat };
})();

/** /for-sponsors — campaign stepper overview, quote composition, live funnel and impact report. */
export default function ForSponsors() {
  const auth = useAuth();
  const cta = auth.isAuthenticated ? '/sponsor/organisation/new' : '/auth/sign-in?mode=signup';
  return (
    <div>
      <section className="bg-navy text-white">
        <div className="mx-auto grid max-w-[1280px] gap-8 px-4 py-14 sm:px-6 lg:grid-cols-2 lg:py-20">
          <div>
            <h1 className="text-4xl leading-tight sm:text-5xl">Fund skills. <span className="text-teal">See every seat filled.</span></h1>
            <p className="mt-4 max-w-lg text-white/80">
              Build a campaign in seven steps, approve a transparent quote, and watch applications, attendance and completion live. Every naira is on a ledger you can audit.
            </p>
            <div className="mt-8 flex flex-wrap gap-3">
              <Button as={AppLink} to={cta} size="lg" variant="teal">Register your organisation</Button>
              <Button to="/opportunities" size="lg" variant="ghost" className="text-white hover:bg-white/10">See live programmes</Button>
            </div>
          </div>
          <ul className="grid gap-3 self-center sm:grid-cols-2">
            {[
              [Users, 'Lawful eligibility rules', 'Age, residency, education and employment only; protected characteristics are refused.'],
              [BarChart3, 'Live funnel', 'Applied → accepted → active → completed → certified, refreshed from events.'],
              [Landmark, 'Ledger-backed budget', 'Budget vs spend read from your programme sub-account; unfilled seats refunded at close.'],
              [FileText, 'Impact report', 'PDF with funnel, attendance, completion and scores, plus a CSV of the funnel.'],
            ].map(([Icon, t, d]) => (
              <li key={t} className="flex gap-3 rounded-card bg-white/5 p-4"><Icon className="h-5 w-5 shrink-0 text-teal" aria-hidden /><div><div className="font-semibold">{t}</div><div className="mt-1 text-sm text-white/70">{d}</div></div></li>
            ))}
          </ul>
        </div>
      </section>

      <section className="mx-auto max-w-[1280px] px-4 py-14 sm:px-6">
        <h2 className="text-2xl">The campaign builder</h2>
        <p className="mt-1 text-sm text-muted">Save a draft at any step; request the quote when you are ready. Programmes publish once the quote is approved and the invoice paid.</p>
        <Stepper steps={STEPS} current={-1} className="mt-6" />
        <div className="mt-8 grid gap-6 lg:grid-cols-2">
          <Card>
            <h3 className="text-lg">What a quote contains</h3>
            <p className="mt-1 text-sm text-muted">Example: {EXAMPLE.seats} seats on a {formatMoney(60_000_00)} course.</p>
            <dl className="mt-4 divide-y divide-line text-sm">
              <Row k="Tuition (seats × price)" v={formatMoney(EXAMPLE.tuition)} />
              <Row k="Management fee (12%, minimum ₦3,000,000)" v={formatMoney(EXAMPLE.management)} />
              <Row k="Seat fee (₦12,000 per seat)" v={formatMoney(EXAMPLE.seatFee)} />
              <Row k="VAT 7.5% on fees" v={formatMoney(EXAMPLE.vat)} />
              <Row k="Total" v={formatMoney(EXAMPLE.total)} bold />
            </dl>
            <p className="mt-3 text-xs text-muted">Partial sponsorship (for example 70%) lowers tuition and sets a learner co-payment per seat. Budget checks compare against the ex-VAT total.</p>
          </Card>
          <Card>
            <h3 className="text-lg">After you publish</h3>
            <ul className="mt-3 space-y-3 text-sm text-navy">
              <li className="flex gap-3"><ShieldCheck className="h-5 w-5 shrink-0 text-teal" /> Plastoids reviews the rules and copy, then publishes the opportunity page.</li>
              <li className="flex gap-3"><Users className="h-5 w-5 shrink-0 text-teal" /> Applicants are scored on your rubric and an optional entry assessment; you sign off the shortlist or delegate selection.</li>
              <li className="flex gap-3"><BarChart3 className="h-5 w-5 shrink-0 text-teal" /> Offers expire after five days; waitlisted applicants are backfilled automatically.</li>
              <li className="flex gap-3"><FileText className="h-5 w-5 shrink-0 text-teal" /> Monthly fees and tutor earnings post as the programme runs; the final report and certificates arrive at close.</li>
            </ul>
            <Button as={AppLink} to={cta} className="mt-5 w-full" size="lg">Start a campaign</Button>
          </Card>
        </div>
      </section>
    </div>
  );
}

function Row({ k, v, bold }) {
  return (
    <div className={`flex items-center justify-between gap-4 py-2 ${bold ? 'font-bold text-navy' : 'text-muted'}`}>
      <dt>{k}</dt>
      <dd className="tabular text-navy">{v}</dd>
    </div>
  );
}
