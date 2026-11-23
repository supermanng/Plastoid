import { Armchair, CalendarDays, Coins, Landmark, ShieldCheck, Undo2, UserPlus, Users } from 'lucide-react';
import { Button, Card, Stepper } from '../../../../shared/components/ui';
import { useAuth } from '../../../../shared/lib/auth';
import { AppLink } from '../../../../shared/lib/navigation';

const STEPS = ['Register', 'Buy units', 'Pick a cohort', 'Buy seats', 'Give seats'];

/** /for-sponsors — seat sponsorship (ADR 0021): buy seats in a cohort, give them to your people, no applications. */
export default function ForSponsors() {
  const auth = useAuth();
  const cta = auth.isAuthenticated ? '/sponsor/organisation/new' : '/auth/sign-in?mode=signup';
  return (
    <div>
      <section className="bg-navy text-white">
        <div className="mx-auto grid max-w-[1280px] gap-8 px-4 py-14 sm:px-6 lg:grid-cols-2 lg:py-20">
          <div>
            <h1 className="text-4xl leading-tight sm:text-5xl">Fund skills. <span className="text-teal">Choose who gets the seat.</span></h1>
            <p className="mt-4 max-w-lg text-white/80">
              Pick a course cohort, pay for as many seats as you need, and give each one to a person you name by phone number or email. No open call, no applications, no shortlist: your staff, members or beneficiaries are enrolled the moment you give them a seat.
            </p>
            <div className="mt-8 flex flex-wrap gap-3">
              <Button as={AppLink} to={cta} size="lg" variant="teal">Register your organisation</Button>
              <Button to="/courses?format=cohort" size="lg" variant="ghost" className="text-white hover:bg-white/10">See upcoming cohorts</Button>
            </div>
          </div>
          <ul className="grid gap-3 self-center sm:grid-cols-2">
            {[
              [Armchair, 'Seats, not campaigns', 'Buy one seat or a hundred in any upcoming cohort; a seat costs the cohort price, nothing more.'],
              [UserPlus, 'You pick the learners', 'Give a seat by phone or email. People with an account start at once; others are enrolled when they sign up.'],
              [Undo2, 'Unused seats come back', 'Release seats you do not need at any time; open seats are refunded automatically when the cohort ends.'],
              [Landmark, 'Every unit on a ledger', 'Seats are paid for in prepaid units from your organisation wallet, with every movement auditable.'],
            ].map(([Icon, t, d]) => (
              <li key={t} className="flex gap-3 rounded-card bg-white/5 p-4"><Icon className="h-5 w-5 shrink-0 text-teal" aria-hidden /><div><div className="font-semibold">{t}</div><div className="mt-1 text-sm text-white/70">{d}</div></div></li>
            ))}
          </ul>
        </div>
      </section>

      <section className="mx-auto max-w-[1280px] px-4 py-14 sm:px-6">
        <h2 className="text-2xl">How sponsoring works</h2>
        <p className="mt-1 text-sm text-muted">Five short steps. Only an owner or finance member of your organisation can spend units.</p>
        <Stepper steps={STEPS} current={-1} className="mt-6" />
        <div className="mt-8 grid gap-6 lg:grid-cols-2">
          <Card>
            <h3 className="text-lg">What you pay</h3>
            <ul className="mt-3 space-y-3 text-sm text-navy">
              <li className="flex gap-3"><Coins className="h-5 w-5 shrink-0 text-teal" /> Each seat costs the cohort's published price per seat, paid in units from your organisation wallet at the day's rate.</li>
              <li className="flex gap-3"><ShieldCheck className="h-5 w-5 shrink-0 text-teal" /> No management fee, seat fee or VAT line. Plastoids earns its usual commission from the course price when a seat is taken up.</li>
              <li className="flex gap-3"><Undo2 className="h-5 w-5 shrink-0 text-teal" /> Seats you release come back to the wallet as units, in proportion to what you paid.</li>
              <li className="flex gap-3"><CalendarDays className="h-5 w-5 shrink-0 text-teal" /> Seats can be bought until the cohort ends; anything still open at the end is released for you.</li>
            </ul>
          </Card>
          <Card>
            <h3 className="text-lg">What your learners get</h3>
            <ul className="mt-3 space-y-3 text-sm text-navy">
              <li className="flex gap-3"><Users className="h-5 w-5 shrink-0 text-teal" /> A place in the cohort with live classes, lessons, quizzes and the tutor's gradebook.</li>
              <li className="flex gap-3"><Armchair className="h-5 w-5 shrink-0 text-teal" /> "Sponsored by your organisation" on their course, and a certificate anyone can verify at the end.</li>
              <li className="flex gap-3"><UserPlus className="h-5 w-5 shrink-0 text-teal" /> A seat that waits for them: if they have no account yet, it is theirs as soon as they sign up with the same phone number or email.</li>
            </ul>
            <Button as={AppLink} to={cta} className="mt-5 w-full" size="lg">Start sponsoring</Button>
          </Card>
        </div>
      </section>
    </div>
  );
}
