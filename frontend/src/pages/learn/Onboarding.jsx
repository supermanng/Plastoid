import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { ArrowRight, Check } from 'lucide-react';
import { Button, Card, Chip, ErrorAlert, Field, Input, Skeleton, Stepper } from '../../components/ui';
import { useAuth } from '../../lib/auth';
import { useAction } from '../../lib/hooks';
import { CourseCard, OpportunityCard } from '../public/Home';
import { applications, catalogue, identity, keys } from '../../api/learn';

const INTERESTS = ['Software development', 'Data analysis', 'Product design', 'Digital marketing', 'Business & finance', 'Cybersecurity', 'Cloud & DevOps', 'Content creation', 'Project management', 'Trades & technical', 'Health & care', 'Teaching'];
const LEVELS = [
  ['beginner', 'Beginner', 'Starting from scratch'],
  ['intermediate', 'Intermediate', 'Some experience, want to go deeper'],
  ['advanced', 'Advanced', 'Experienced, want specialist skills'],
];
const STEPS = ['Your goal', 'Interests', 'Level'];

/** Three-step setup (blueprint learner journey step 2): goal, interests, level → PUT /identity/me/goals → recommendations. */
export default function Onboarding() {
  const auth = useAuth();
  const navigate = useNavigate();
  const [step, setStep] = useState(0);
  const [goal, setGoal] = useState(auth.me?.careerGoal || '');
  const [interests, setInterests] = useState(auth.me?.interests || []);
  const [level, setLevel] = useState('beginner');
  const [done, setDone] = useState(false);

  const save = useAction(() => identity.updateGoals({ careerGoal: goal.trim(), interests: [...interests, `level:${level}`] }), {
    success: 'Profile saved',
    onSuccess: async () => {
      await auth.refreshMe();
      setDone(true);
    },
  });

  const toggle = (i) => setInterests((list) => (list.includes(i) ? list.filter((x) => x !== i) : list.length < 5 ? [...list, i] : list));

  if (done) return <Recommendations goal={goal} interests={interests} level={level} onFinish={() => navigate('/learn')} />;

  return (
    <div className="mx-auto max-w-2xl">
      <h1 className="text-2xl sm:text-3xl">Let’s set you up</h1>
      <p className="mt-1 text-sm text-muted">Three quick questions. You can change these any time in your profile.</p>
      <Stepper steps={STEPS} current={step} className="mt-5" onSelect={(i) => i < step && setStep(i)} />
      <Card className="mt-6">
        <ErrorAlert error={save.error} className="mb-4" />
        {step === 0 ? (
          <div className="space-y-4">
            <Field label="What do you want to achieve?" hint="One sentence is enough, e.g. “Get my first job as a data analyst”." required>
              {(p) => <Input {...p} value={goal} onChange={(e) => setGoal(e.target.value)} placeholder="Your career goal" maxLength={200} autoFocus />}
            </Field>
            <div className="flex flex-wrap gap-2">
              {['Get my first tech job', 'Switch careers', 'Grow my business', 'Earn a certificate', 'Start freelancing'].map((s) => (
                <Chip key={s} active={goal === s} onClick={() => setGoal(s)}>{s}</Chip>
              ))}
            </div>
          </div>
        ) : null}
        {step === 1 ? (
          <div>
            <h2 className="text-lg">What are you interested in?</h2>
            <p className="text-sm text-muted">Pick up to five.</p>
            <div className="mt-4 flex flex-wrap gap-2">
              {INTERESTS.map((i) => (
                <Chip key={i} active={interests.includes(i)} onClick={() => toggle(i)}>
                  {interests.includes(i) ? <Check className="h-3.5 w-3.5" /> : null}{i}
                </Chip>
              ))}
            </div>
          </div>
        ) : null}
        {step === 2 ? (
          <div>
            <h2 className="text-lg">Where are you starting from?</h2>
            <div className="mt-4 grid gap-3">
              {LEVELS.map(([value, label, desc]) => (
                <button key={value} type="button" onClick={() => setLevel(value)} aria-pressed={level === value} className={`flex min-h-14 items-center justify-between rounded-card border px-4 py-3 text-left ${level === value ? 'border-royal bg-royal-50' : 'border-line hover:bg-surface'}`}>
                  <div>
                    <div className="font-semibold text-navy">{label}</div>
                    <div className="text-sm text-muted">{desc}</div>
                  </div>
                  {level === value ? <Check className="h-5 w-5 text-royal" /> : null}
                </button>
              ))}
            </div>
          </div>
        ) : null}
        <div className="mt-6 flex items-center justify-between gap-2">
          <Button variant="ghost" onClick={() => navigate('/learn')}>Skip for now</Button>
          <div className="flex gap-2">
            {step > 0 ? <Button variant="secondary" onClick={() => setStep(step - 1)}>Back</Button> : null}
            {step < 2 ? (
              <Button onClick={() => setStep(step + 1)} disabled={(step === 0 && !goal.trim()) || (step === 1 && !interests.length)}>Next <ArrowRight className="h-4 w-4" /></Button>
            ) : (
              <Button onClick={() => save.mutate()} loading={save.isPending}>Finish</Button>
            )}
          </div>
        </div>
      </Card>
    </div>
  );
}

function Recommendations({ goal, interests, level, onFinish }) {
  const q = interests[0] || goal;
  const courses = useQuery({ queryKey: keys.courses({ q, level, limit: 6 }), queryFn: () => catalogue.search({ q, level, limit: 6 }) });
  const opportunities = useQuery({ queryKey: [...keys.opportunities, 'onboarding'], queryFn: () => applications.opportunities({ limit: 3 }) });
  const courseItems = courses.data?.items?.length ? courses.data.items : courses.data?.suggestions?.popular || [];
  return (
    <div className="mx-auto max-w-4xl">
      <h1 className="text-2xl sm:text-3xl">You’re all set</h1>
      <p className="mt-1 text-sm text-muted">Here is where to start, based on “{goal}”.</p>
      <h2 className="mt-8 text-lg">Sponsored seats open now</h2>
      {opportunities.isPending ? <Skeleton className="mt-3 h-32" /> : null}
      <div className="mt-3 grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        {(opportunities.data?.items || []).map((o) => <OpportunityCard key={o.programmeId} o={o} />)}
      </div>
      {opportunities.data && !opportunities.data.items?.length ? <p className="text-sm text-muted">None open right now; we will notify you when one matches.</p> : null}
      <h2 className="mt-8 text-lg">Courses for you</h2>
      {courses.isPending ? <Skeleton className="mt-3 h-32" /> : null}
      <div className="mt-3 grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        {courseItems.map((c) => <CourseCard key={c.courseId} c={c} />)}
      </div>
      {courses.data && !courseItems.length ? <p className="text-sm text-muted">No matching courses yet. <a href="/courses">Browse everything.</a></p> : null}
      <div className="mt-8 flex gap-2">
        <Button onClick={onFinish}>Go to my dashboard</Button>
        <Button to="/courses" variant="secondary">Browse courses</Button>
      </div>
    </div>
  );
}
