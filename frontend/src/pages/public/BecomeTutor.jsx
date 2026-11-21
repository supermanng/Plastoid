import { BadgeCheck, Banknote, CalendarCheck, FileCheck, Fingerprint, Video, Wallet } from 'lucide-react';
import { Button, Card } from '../../components/ui';
import { useAuth } from '../../lib/auth';

const STAGES = [
  { icon: Fingerprint, title: '1. Identity', text: 'NIN and BVN checked through a licensed provider with a liveness selfie. Encrypted; never shown to learners.' },
  { icon: FileCheck, title: '2. Credentials', text: 'Upload your degree, certificates or work history. A reviewer approves each one.' },
  { icon: Video, title: '3. Demo lesson', text: 'Record a five-minute lesson. A subject reviewer checks clarity, accuracy and pace.' },
  { icon: CalendarCheck, title: '4. Availability & pricing', text: 'Set weekly windows in your time zone and your hourly rate per format.' },
  { icon: Wallet, title: '5. Payout account', text: 'Your bank account is resolved with the gateway and the name must match your identity check.' },
];

/** /become-a-tutor — marketing page with the verification stages and payout terms. */
export default function BecomeTutor() {
  const auth = useAuth();
  const cta = auth.isAuthenticated ? '/tutor/onboarding' : '/auth/sign-in?mode=signup';
  return (
    <div>
      <section className="bg-navy text-white">
        <div className="mx-auto grid max-w-[1280px] gap-8 px-4 py-14 sm:px-6 lg:grid-cols-2 lg:py-20">
          <div>
            <h1 className="text-4xl leading-tight sm:text-5xl">Teach what you know. <span className="text-teal">Get paid every week.</span></h1>
            <p className="mt-4 max-w-lg text-white/80">
              Verified tutors sell self-paced courses, run cohorts and take one-to-one bookings. Sponsors invite tutors with strong ratings onto funded programmes.
            </p>
            <div className="mt-8 flex flex-wrap gap-3">
              <Button to={cta} size="lg" variant="teal">Apply to teach</Button>
              <Button to="/tutors" size="lg" variant="ghost" className="text-white hover:bg-white/10">See tutors</Button>
            </div>
          </div>
          <ul className="grid gap-3 self-center sm:grid-cols-2">
            {[
              ['Weekly payouts', 'Earnings clear after a short hold, then land in your bank every week.'],
              ['Your prices', 'Hourly rate plus per-format prices. Ten-session packages discounted 10%.'],
              ['Protected sessions', 'Learners pay before a slot is confirmed. Late cancellations still pay you 50%.'],
              ['Verified badge', 'Learners see your verification and rating before they book.'],
            ].map(([t, d]) => (
              <li key={t} className="rounded-card bg-white/5 p-4"><div className="font-semibold">{t}</div><div className="mt-1 text-sm text-white/70">{d}</div></li>
            ))}
          </ul>
        </div>
      </section>

      <section className="mx-auto max-w-[1280px] px-4 py-14 sm:px-6">
        <h2 className="text-2xl">Five steps to your first class</h2>
        <p className="mt-1 text-sm text-muted">Most tutors finish in a few days; reviews have a published SLA.</p>
        <ol className="mt-6 grid gap-4 md:grid-cols-5">
          {STAGES.map((s) => (
            <li key={s.title}>
              <Card className="h-full">
                <span className="grid h-10 w-10 place-items-center rounded-full bg-royal-50 text-royal"><s.icon className="h-5 w-5" aria-hidden /></span>
                <h3 className="mt-3 text-base">{s.title}</h3>
                <p className="mt-1 text-sm text-muted">{s.text}</p>
              </Card>
            </li>
          ))}
        </ol>
      </section>

      <section className="bg-surface">
        <div className="mx-auto grid max-w-[1280px] gap-8 px-4 py-14 sm:px-6 lg:grid-cols-2">
          <div>
            <h2 className="text-2xl">How you get paid</h2>
            <ul className="mt-4 space-y-3 text-sm text-navy">
              <li className="flex gap-3"><Banknote className="h-5 w-5 shrink-0 text-teal" /> Every sale, booking and sponsored month posts to your earnings balance on a double-entry ledger.</li>
              <li className="flex gap-3"><Banknote className="h-5 w-5 shrink-0 text-teal" /> Course sales clear after a seven-day hold (refund window); session fees after the session ends.</li>
              <li className="flex gap-3"><Banknote className="h-5 w-5 shrink-0 text-teal" /> Weekly payout runs pay everything cleared above ₦5,000. A disputed session holds only that amount.</li>
              <li className="flex gap-3"><Banknote className="h-5 w-5 shrink-0 text-teal" /> Platform commission is deducted at sale and shown on every statement; download a CSV any time.</li>
            </ul>
          </div>
          <Card>
            <div className="flex items-center gap-2 text-base font-bold"><BadgeCheck className="h-5 w-5 text-teal" /> What we expect</div>
            <ul className="mt-3 list-disc space-y-1 pl-5 text-sm text-muted">
              <li>Keep conversations and payments on the platform.</li>
              <li>Start sessions on time; a tutor cancellation refunds the learner 100% and counts as a strike.</li>
              <li>Mark assignments within the course's stated turnaround.</li>
              <li>Agree to the code of conduct when you apply.</li>
            </ul>
            <Button to={cta} className="mt-5 w-full" size="lg">Start your application</Button>
          </Card>
        </div>
      </section>
    </div>
  );
}
