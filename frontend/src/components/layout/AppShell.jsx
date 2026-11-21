import { useEffect, useRef, useState } from 'react';
import { Link, NavLink, Outlet, useLocation, useNavigate } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import clsx from 'clsx';
import {
  Award, BarChart3, Bell, BookOpen, Briefcase, Building2, CalendarDays, ChevronDown, ClipboardList, Compass, CreditCard,
  FolderKanban, GraduationCap, Home, Landmark, LayoutDashboard, LogOut, MessageSquare, Settings, ShieldCheck,
  Sparkles, User, Users, Wallet, Flag, History, Receipt, Percent, KeyRound, Mail,
} from 'lucide-react';
import { Logo, LogoMark } from '../Logo';
import { Avatar } from '../ui';
import { ROLES, homeFor, useAuth } from '../../lib/auth';
import { api } from '../../lib/api';

/*
 * Navigation per area. `mobile: true` marks the (max five) items shown in the bottom tab bar (blueprint §14).
 * Items are plain route links; pages decide what to render.
 */
const NAVS = {
  learn: {
    label: 'Learner',
    role: ROLES.learner,
    items: [
      { to: '/learn', label: 'Home', icon: Home, end: true, mobile: true },
      { to: '/learn/courses', label: 'Learn', icon: BookOpen, mobile: true },
      { to: '/courses', label: 'Explore', icon: Compass, mobile: true },
      { to: '/learn/applications', label: 'Applications', icon: ClipboardList },
      { to: '/learn/bookings', label: 'Tutor sessions', icon: CalendarDays },
      { to: '/learn/certificates', label: 'Certificates', icon: Award },
      { to: '/learn/payments', label: 'Payments', icon: CreditCard },
      { to: '/learn/messages', label: 'Messages', icon: MessageSquare, mobile: true },
      { to: '/learn/profile', label: 'Profile', icon: User, mobile: true },
    ],
  },
  tutor: {
    label: 'Tutor',
    role: ROLES.tutor,
    items: [
      { to: '/tutor', label: 'Home', icon: Home, end: true, mobile: true },
      { to: '/tutor/sessions', label: 'Classes', icon: CalendarDays, mobile: true },
      { to: '/tutor/courses', label: 'Courses', icon: BookOpen },
      { to: '/tutor/cohorts', label: 'Students', icon: Users, mobile: true },
      { to: '/tutor/earnings', label: 'Earnings', icon: Wallet, mobile: true },
      { to: '/tutor/profile', label: 'Profile', icon: User, mobile: true },
    ],
  },
  sponsor: {
    label: 'Sponsor',
    role: ROLES.sponsor,
    items: [
      { to: '/sponsor', label: 'Overview', icon: LayoutDashboard, end: true, mobile: true },
      { to: '/sponsor/programmes', label: 'Programmes', icon: FolderKanban, mobile: true },
      { to: '/sponsor/organisation', label: 'Organisation', icon: Building2, mobile: true },
      { to: '/sponsor/invoices', label: 'Invoices', icon: Receipt, mobile: true },
      { to: '/learn/profile', label: 'Profile', icon: User, mobile: true },
    ],
  },
  admin: {
    label: 'Back office',
    role: ROLES.staff,
    items: [
      { to: '/admin', label: 'Dashboard', icon: BarChart3, end: true, mobile: true },
      { to: '/admin/queues', label: 'Queues', icon: ClipboardList, mobile: true },
      { to: '/admin/users', label: 'Users', icon: Users, mobile: true },
      { to: '/admin/tutors', label: 'Tutors', icon: GraduationCap },
      { to: '/admin/sponsors', label: 'Sponsors', icon: Briefcase },
      { to: '/admin/programmes', label: 'Programmes', icon: FolderKanban },
      { to: '/admin/catalogue', label: 'Course moderation', icon: BookOpen },
      { to: '/admin/finance', label: 'Finance', icon: Landmark, mobile: true },
      { to: '/admin/payouts', label: 'Payouts', icon: Wallet },
      { to: '/admin/fee-rules', label: 'Fee rules', icon: Percent },
      { to: '/admin/role-grants', label: 'Role grants', icon: KeyRound },
      { to: '/admin/templates', label: 'Templates', icon: Mail },
      { to: '/admin/feature-flags', label: 'Feature flags', icon: Flag },
      { to: '/admin/compliance', label: 'Compliance', icon: ShieldCheck },
      { to: '/admin/audit', label: 'Audit log', icon: History, mobile: true },
    ],
  },
};

export function areaOf(pathname) {
  const first = pathname.split('/')[1];
  return NAVS[first] ? first : null;
}

export default function AppShell({ area }) {
  const auth = useAuth();
  const location = useLocation();
  const nav = NAVS[area] || NAVS.learn;

  const unread = useQuery({
    queryKey: ['notifications', 'unread-count'],
    queryFn: () => api.get('/notifications/me', { unreadOnly: true, limit: 20 }),
    enabled: auth.isAuthenticated,
    refetchInterval: 60_000,
    select: (d) => (Array.isArray(d?.items) ? d.items.length : Array.isArray(d) ? d.length : 0),
  });

  const mobileItems = nav.items.filter((i) => i.mobile).slice(0, 5);

  return (
    <div className="min-h-dvh bg-surface text-navy">
      {/* Desktop / tablet sidebar */}
      <aside className="fixed inset-y-0 left-0 z-30 hidden w-64 flex-col border-r border-line bg-white md:flex">
        <div className="flex h-16 items-center px-5">
          <Logo />
        </div>
        <div className="px-4 pb-2 text-xs font-semibold uppercase tracking-wide text-muted">{nav.label}</div>
        <nav className="flex-1 overflow-y-auto px-3" aria-label={`${nav.label} navigation`}>
          {nav.items.map((item) => (
            <NavLink
              key={item.to}
              to={item.to}
              end={item.end}
              className={({ isActive }) => clsx('mb-0.5 flex min-h-11 items-center gap-3 rounded-input px-3 text-sm font-semibold transition-colors', isActive ? 'bg-royal-50 text-royal' : 'text-navy hover:bg-surface')}
            >
              <item.icon className="h-5 w-5 shrink-0" aria-hidden />
              {item.label}
            </NavLink>
          ))}
        </nav>
        <AreaSwitcher current={area} />
      </aside>

      {/* Top bar */}
      <header className="sticky top-0 z-20 flex h-16 items-center justify-between gap-3 border-b border-line bg-white/95 px-4 backdrop-blur md:ml-64 md:px-8">
        <div className="flex items-center gap-3 md:hidden">
          <Link to={homeFor(auth.roles)} aria-label="Home"><LogoMark size={32} className="rounded-[10px]" /></Link>
          <span className="font-display text-lg font-extrabold">{nav.label}</span>
        </div>
        <div className="hidden text-sm text-muted md:block">{greeting()}, {auth.me?.displayName?.split(' ')[0] || 'there'}</div>
        <div className="flex items-center gap-1">
          <Link to="/learn/notifications" className="relative grid h-11 w-11 place-items-center rounded-full text-navy hover:bg-surface" aria-label={`Notifications${unread.data ? `, ${unread.data} unread` : ''}`}>
            <Bell className="h-5 w-5" />
            {unread.data ? <span className="absolute right-2 top-2 grid h-4 min-w-4 place-items-center rounded-chip bg-teal px-1 text-[10px] font-bold text-navy">{unread.data > 9 ? '9+' : unread.data}</span> : null}
          </Link>
          <UserMenu />
        </div>
      </header>

      <main className="px-4 pb-24 pt-6 md:ml-64 md:px-8 md:pb-10">
        <div className="mx-auto max-w-[1280px]" key={location.pathname}>
          <Outlet />
        </div>
      </main>

      {/* Mobile bottom tabs */}
      <nav className="fixed inset-x-0 bottom-0 z-30 grid border-t border-line bg-white pb-[env(safe-area-inset-bottom)] md:hidden" style={{ gridTemplateColumns: `repeat(${mobileItems.length}, minmax(0, 1fr))` }} aria-label="Bottom navigation">
        {mobileItems.map((item) => (
          <NavLink key={item.to} to={item.to} end={item.end} className={({ isActive }) => clsx('flex min-h-14 flex-col items-center justify-center gap-0.5 text-[11px] font-semibold', isActive ? 'text-royal' : 'text-muted')}>
            <item.icon className="h-5 w-5" aria-hidden />
            {item.label}
          </NavLink>
        ))}
      </nav>
    </div>
  );
}

function greeting() {
  const h = new Date().getHours();
  return h < 12 ? 'Good morning' : h < 17 ? 'Good afternoon' : 'Good evening';
}

/** Lets multi-role accounts hop between areas; shows "Become a tutor / sponsor" links otherwise. */
function AreaSwitcher({ current }) {
  const auth = useAuth();
  const areas = Object.entries(NAVS).filter(([, n]) => auth.hasRole(n.role));
  return (
    <div className="border-t border-line p-3">
      {areas.length > 1 ? (
        <div className="mb-2">
          <div className="px-1 pb-1 text-xs font-semibold uppercase tracking-wide text-muted">Switch area</div>
          <div className="flex flex-wrap gap-1">
            {areas.map(([key, n]) => (
              <Link key={key} to={`/${key}`} className={clsx('rounded-chip border px-2.5 py-1 text-xs font-semibold', key === current ? 'border-royal bg-royal text-white' : 'border-line text-navy hover:bg-surface')}>
                {n.label}
              </Link>
            ))}
          </div>
        </div>
      ) : null}
      {!auth.hasRole(ROLES.tutor) ? <Link to="/become-a-tutor" className="flex items-center gap-2 px-1 py-1 text-xs font-semibold text-royal hover:underline"><Sparkles className="h-3.5 w-3.5" /> Become a tutor</Link> : null}
      {!auth.hasRole(ROLES.sponsor) ? <Link to="/for-sponsors" className="flex items-center gap-2 px-1 py-1 text-xs font-semibold text-royal hover:underline"><Briefcase className="h-3.5 w-3.5" /> Sponsor a programme</Link> : null}
    </div>
  );
}

function UserMenu() {
  const auth = useAuth();
  const navigate = useNavigate();
  const [open, setOpen] = useState(false);
  const ref = useRef(null);
  useEffect(() => {
    if (!open) return undefined;
    const onDoc = (e) => !ref.current?.contains(e.target) && setOpen(false);
    document.addEventListener('mousedown', onDoc);
    return () => document.removeEventListener('mousedown', onDoc);
  }, [open]);

  return (
    <div className="relative" ref={ref}>
      <button type="button" onClick={() => setOpen((v) => !v)} aria-haspopup="menu" aria-expanded={open} className="flex min-h-11 items-center gap-2 rounded-chip pl-1 pr-2 hover:bg-surface">
        <Avatar name={auth.me?.displayName} size={32} />
        <ChevronDown className="h-4 w-4 text-muted" aria-hidden />
      </button>
      {open ? (
        <div role="menu" className="card absolute right-0 mt-2 w-60 p-1 text-sm">
          <div className="px-3 py-2">
            <div className="truncate font-semibold">{auth.me?.displayName}</div>
            <div className="truncate text-xs text-muted">{auth.me?.email || auth.me?.phoneMasked}</div>
            <div className="mt-1 flex flex-wrap gap-1">
              {auth.roles.map((r) => <span key={r} className="rounded-chip bg-surface px-2 py-0.5 text-[11px] font-semibold capitalize text-muted">{r}</span>)}
              {auth.mfa ? <span className="rounded-chip bg-teal-50 px-2 py-0.5 text-[11px] font-semibold text-teal-600">MFA</span> : null}
            </div>
          </div>
          <MenuLink to="/learn/profile" icon={Settings} onClick={() => setOpen(false)}>Profile & settings</MenuLink>
          <MenuLink to="/learn/notifications" icon={Bell} onClick={() => setOpen(false)}>Notifications</MenuLink>
          {!auth.mfa ? <MenuLink to="/auth/mfa" icon={ShieldCheck} onClick={() => setOpen(false)}>Set up MFA</MenuLink> : null}
          <button type="button" role="menuitem" onClick={() => { auth.logout(); navigate('/'); }} className="flex w-full items-center gap-2 rounded-input px-3 py-2 text-left text-danger hover:bg-danger-50">
            <LogOut className="h-4 w-4" /> Sign out
          </button>
        </div>
      ) : null}
    </div>
  );
}

function MenuLink({ to, icon: Icon, children, onClick }) {
  return (
    <Link to={to} role="menuitem" onClick={onClick} className="flex items-center gap-2 rounded-input px-3 py-2 text-navy hover:bg-surface">
      <Icon className="h-4 w-4 text-muted" /> {children}
    </Link>
  );
}

export { NAVS };
