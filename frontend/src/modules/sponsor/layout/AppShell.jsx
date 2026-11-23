import { Armchair, Banknote, BookOpen, Building2, Coins, LayoutDashboard, User } from 'lucide-react';
import Shell from '../../../shared/components/layout/Shell';
import { areaSwitcherFor } from '../../../shared/components/layout/areaSwitcher';
import { useAuth } from '../../../shared/lib/auth';
import { MODULES } from '../../../shared/lib/navigation';

/*
 * Sponsor module navigation. `to` are routes relative to this module's basename (/sponsor); `href` items point into
 * the learner module (profile lives there) and are full-page navigations.
 */
export const NAVS = {
  sponsor: {
    label: 'Sponsor',
    items: [
      { to: '/', label: 'Overview', icon: LayoutDashboard, end: true, mobile: true },
      { to: '/seats', label: 'Seats', icon: Armchair, mobile: true },
      { to: '/courses', label: 'Courses', icon: BookOpen, mobile: true },
      { to: '/organisation', label: 'Organisation', icon: Building2 },
      { to: '/wallet', label: 'Wallet', icon: Coins },
      { to: '/payouts', label: 'Payouts', icon: Banknote },
      { href: `${MODULES.learner}learn/profile`, label: 'Profile', icon: User, mobile: true },
    ],
  },
};

export default function AppShell() {
  const auth = useAuth();
  return <Shell nav={NAVS.sponsor} areaSwitcher={areaSwitcherFor(auth, 'sponsor')} />;
}
