import { useLocation } from 'react-router-dom';
import { Lock } from 'lucide-react';
import { Button, EmptyState } from '../../components/ui';
import { homeFor, useAuth } from '../../lib/auth';
import { AppLink } from '../../lib/navigation';

// Absolute site paths: the tutor onboarding and the sign-in page are in the learner module, organisation registration in the sponsor module.
const HOW = {
  tutor: { text: 'Apply to teach to unlock the tutor area.', to: '/tutor/onboarding', label: 'Apply to teach' },
  sponsor: { text: 'Register your organisation to unlock the sponsor area.', to: '/sponsor/organisation/new', label: 'Register organisation' },
  staff: { text: 'The back office is for Plastoids staff. A staff member can propose a role grant for you.', to: null },
  learner: { text: 'Sign in with your phone number to learn.', to: '/auth/sign-in', label: 'Sign in' },
};

export default function NoAccess() {
  const auth = useAuth();
  const { role } = useLocation().state || {};
  const how = HOW[role] || {};
  return (
    <div className="mx-auto max-w-lg px-4 py-16 sm:px-6">
      <EmptyState
        icon={Lock}
        title={`You do not have the ${role || 'required'} role`}
        description={how.text || 'Your account cannot open that page.'}
        action={
          <div className="flex gap-2">
            {how.to ? <Button as={AppLink} to={how.to}>{how.label}</Button> : null}
            <Button variant="secondary" as={AppLink} to={auth.isAuthenticated ? homeFor(auth.roles) : '/'}>Go to my dashboard</Button>
          </div>
        }
      />
    </div>
  );
}
