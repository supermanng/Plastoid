import { Briefcase, Sparkles } from 'lucide-react';
import { ROLES } from '../../lib/auth';
import { MODULES } from '../../lib/navigation';

/** Areas a signed-in account can switch to (absolute site paths, each in its module). */
const AREAS = [
  { key: 'learn', label: 'Learner', role: ROLES.learner, to: `${MODULES.learner}learn` },
  { key: 'tutor', label: 'Tutor', role: ROLES.tutor, to: `${MODULES.learner}tutor` },
  { key: 'sponsor', label: 'Sponsor', role: ROLES.sponsor, to: MODULES.sponsor },
  { key: 'admin', label: 'Back office', role: ROLES.staff, to: MODULES.admin },
];

/**
 * Builds the Shell's `areaSwitcher` prop for the module's AppShell: one link per area the user has the role for
 * (the current area highlighted), plus "Become a tutor / Sponsor a programme" pointers for roles they lack.
 */
export function areaSwitcherFor(auth, current) {
  return {
    links: AREAS.filter((a) => auth.hasRole(a.role)).map((a) => ({ label: a.label, to: a.to, current: a.key === current })),
    suggestions: [
      !auth.hasRole(ROLES.tutor) && { label: 'Become a tutor', to: `${MODULES.learner}become-a-tutor`, icon: Sparkles },
      !auth.hasRole(ROLES.sponsor) && { label: 'Sponsor a programme', to: `${MODULES.learner}for-sponsors`, icon: Briefcase },
    ].filter(Boolean),
  };
}
