import {
  Armchair, BarChart3, BookOpen, Briefcase, ClipboardList, Coins, Flag, GraduationCap, History, KeyRound, Landmark, Mail, Percent,
  ShieldCheck, Users, Wallet,
} from 'lucide-react';
import Shell from '../../../shared/components/layout/Shell';
import { areaSwitcherFor } from '../../../shared/components/layout/areaSwitcher';
import { useAuth } from '../../../shared/lib/auth';

/** Back-office navigation. `to` are routes relative to this module's basename (/admin). */
export const NAVS = {
  admin: {
    label: 'Back office',
    items: [
      { to: '/', label: 'Dashboard', icon: BarChart3, end: true, mobile: true },
      { to: '/queues', label: 'Queues', icon: ClipboardList, mobile: true },
      { to: '/users', label: 'Users', icon: Users, mobile: true },
      { to: '/tutors', label: 'Tutors', icon: GraduationCap },
      { to: '/sponsors', label: 'Sponsors', icon: Briefcase },
      { to: '/seat-blocks', label: 'Seat blocks', icon: Armchair },
      { to: '/catalogue', label: 'Course moderation', icon: BookOpen },
      { to: '/finance', label: 'Finance', icon: Landmark, mobile: true },
      { to: '/payouts', label: 'Payouts', icon: Wallet },
      { to: '/fee-rules', label: 'Fee rules', icon: Percent },
      { to: '/unit-rates', label: 'Unit rate', icon: Coins },
      { to: '/role-grants', label: 'Role grants', icon: KeyRound },
      { to: '/templates', label: 'Templates', icon: Mail },
      { to: '/feature-flags', label: 'Feature flags', icon: Flag },
      { to: '/compliance', label: 'Compliance', icon: ShieldCheck },
      { to: '/audit', label: 'Audit log', icon: History, mobile: true },
    ],
  },
};

export default function AppShell() {
  const auth = useAuth();
  return <Shell nav={NAVS.admin} areaSwitcher={areaSwitcherFor(auth, 'admin')} />;
}
