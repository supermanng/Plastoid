import {
  Award, BookOpen, CalendarDays, ClipboardList, Coins, Compass, CreditCard, Handshake, Home, MessageSquare, User, Users, Wallet,
} from 'lucide-react';
import Shell from '../../../shared/components/layout/Shell';
import { areaSwitcherFor } from '../../../shared/components/layout/areaSwitcher';
import { useAuth } from '../../../shared/lib/auth';

/*
 * Learner module navigation (learn and tutor areas). `to` are module-relative routes (this module's basename is `/`,
 * so they are also the site paths). `mobile: true` marks the (max five) items shown in the bottom tab bar.
 */
export const NAVS = {
  learn: {
    label: 'Learner',
    items: [
      { to: '/learn', label: 'Home', icon: Home, end: true, mobile: true },
      { to: '/learn/courses', label: 'Learn', icon: BookOpen, mobile: true },
      { to: '/courses', label: 'Explore', icon: Compass, mobile: true },
      { to: '/learn/applications', label: 'Applications', icon: ClipboardList },
      { to: '/learn/bookings', label: 'Tutor sessions', icon: CalendarDays },
      { to: '/learn/certificates', label: 'Certificates', icon: Award },
      { to: '/learn/payments', label: 'Payments', icon: CreditCard },
      { to: '/learn/wallet', label: 'Wallet', icon: Coins },
      { to: '/learn/messages', label: 'Messages', icon: MessageSquare, mobile: true },
      { to: '/learn/profile', label: 'Profile', icon: User, mobile: true },
    ],
  },
  tutor: {
    label: 'Tutor',
    items: [
      { to: '/tutor', label: 'Home', icon: Home, end: true, mobile: true },
      { to: '/tutor/sessions', label: 'Classes', icon: CalendarDays, mobile: true },
      { to: '/tutor/courses', label: 'Courses', icon: BookOpen },
      { to: '/tutor/offers', label: 'Offers', icon: Handshake },
      { to: '/tutor/cohorts', label: 'Students', icon: Users, mobile: true },
      { to: '/tutor/earnings', label: 'Earnings', icon: Wallet, mobile: true },
      { to: '/learn/wallet', label: 'Wallet', icon: Coins },
      { to: '/tutor/profile', label: 'Profile', icon: User, mobile: true },
    ],
  },
};

export default function AppShell({ area }) {
  const auth = useAuth();
  const nav = NAVS[area] || NAVS.learn;
  return <Shell nav={nav} areaSwitcher={areaSwitcherFor(auth, area)} />;
}
