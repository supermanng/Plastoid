import { useEffect, useRef, useState } from 'react';
import { Link, NavLink, Outlet, useLocation } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import clsx from 'clsx';
import { Bell, ChevronDown, LogOut, Settings, ShieldCheck } from 'lucide-react';
import { Logo, LogoMark } from '../Logo';
import { Avatar } from '../ui';
import { homeFor, useAuth } from '../../lib/auth';
import { api } from '../../lib/api';
import { AppLink, hrefFor } from '../../lib/navigation';

/*
 * The signed-in shell every interface module renders: sidebar on desktop, top bar, bottom tabs on mobile (blueprint §14).
 *
 *   nav           { label, items: [{ to | href, label, icon, end?, mobile? }] }
 *                 `to` is a module-relative route (NavLink, active state); `href` is an absolute site path into
 *                 another module (plain anchor). `mobile: true` marks the (max five) bottom-tab items.
 *   areaSwitcher  { links: [{ label, to, current }], suggestions: [{ label, to, icon }] } — `to` are absolute site
 *                 paths; the module's AppShell builds them from MODULES and the user's roles.
 *
 * Profile, notifications and MFA set-up live in the learner module; the links below go through <AppLink> so they
 * stay client-side there and become full-page navigations from the sponsor and admin modules.
 */
export default function Shell({ nav, areaSwitcher }) {
  const auth = useAuth();
  const location = useLocation();

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
            <NavItem
              key={item.href || item.to}
              item={item}
              className={(isActive) => clsx('mb-0.5 flex min-h-11 items-center gap-3 rounded-input px-3 text-sm font-semibold transition-colors', isActive ? 'bg-royal-50 text-royal' : 'text-navy hover:bg-surface')}
            />
          ))}
        </nav>
        <AreaSwitcher {...areaSwitcher} />
      </aside>

      {/* Top bar */}
      <header className="sticky top-0 z-20 flex h-16 items-center justify-between gap-3 border-b border-line bg-white/95 px-4 backdrop-blur md:ml-64 md:px-8">
        <div className="flex items-center gap-3 md:hidden">
          <AppLink to={homeFor(auth.roles)} aria-label="Home"><LogoMark size={32} className="rounded-[10px]" /></AppLink>
          <span className="font-display text-lg font-extrabold">{nav.label}</span>
        </div>
        <div className="hidden text-sm text-muted md:block">{greeting()}, {auth.me?.displayName?.split(' ')[0] || 'there'}</div>
        <div className="flex items-center gap-1">
          <AppLink to="/learn/notifications" className="relative grid h-11 w-11 place-items-center rounded-full text-navy hover:bg-surface" aria-label={`Notifications${unread.data ? `, ${unread.data} unread` : ''}`}>
            <Bell className="h-5 w-5" />
            {unread.data ? <span className="absolute right-2 top-2 grid h-4 min-w-4 place-items-center rounded-chip bg-teal px-1 text-[10px] font-bold text-navy">{unread.data > 9 ? '9+' : unread.data}</span> : null}
          </AppLink>
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
          <NavItem
            key={item.href || item.to}
            item={item}
            className={(isActive) => clsx('flex min-h-14 flex-col items-center justify-center gap-0.5 text-[11px] font-semibold', isActive ? 'text-royal' : 'text-muted')}
          />
        ))}
      </nav>
    </div>
  );
}

/** A nav item: NavLink for a module route, plain anchor for an item that lives in another module. */
function NavItem({ item, className }) {
  const content = (
    <>
      <item.icon className="h-5 w-5 shrink-0" aria-hidden />
      {item.label}
    </>
  );
  if (item.href) return <a href={hrefFor(item.href)} className={className(false)}>{content}</a>;
  return (
    <NavLink to={item.to} end={item.end} className={({ isActive }) => className(isActive)}>
      {content}
    </NavLink>
  );
}

function greeting() {
  const h = new Date().getHours();
  return h < 12 ? 'Good morning' : h < 17 ? 'Good afternoon' : 'Good evening';
}

/** Lets multi-role accounts hop between areas (and modules); shows "Become a tutor / sponsor" links otherwise. */
function AreaSwitcher({ links = [], suggestions = [] }) {
  return (
    <div className="border-t border-line p-3">
      {links.length > 1 ? (
        <div className="mb-2">
          <div className="px-1 pb-1 text-xs font-semibold uppercase tracking-wide text-muted">Switch area</div>
          <div className="flex flex-wrap gap-1">
            {links.map((l) => (
              <AppLink key={l.to} to={l.to} className={clsx('rounded-chip border px-2.5 py-1 text-xs font-semibold', l.current ? 'border-royal bg-royal text-white' : 'border-line text-navy hover:bg-surface')}>
                {l.label}
              </AppLink>
            ))}
          </div>
        </div>
      ) : null}
      {suggestions.map((s) => (
        <AppLink key={s.to} to={s.to} className="flex items-center gap-2 px-1 py-1 text-xs font-semibold text-royal hover:underline"><s.icon className="h-3.5 w-3.5" /> {s.label}</AppLink>
      ))}
    </div>
  );
}

function UserMenu() {
  const auth = useAuth();
  const [open, setOpen] = useState(false);
  const ref = useRef(null);
  useEffect(() => {
    if (!open) return undefined;
    const onDoc = (e) => !ref.current?.contains(e.target) && setOpen(false);
    document.addEventListener('mousedown', onDoc);
    return () => document.removeEventListener('mousedown', onDoc);
  }, [open]);

  function signOut() {
    auth.logout();
    // The sign-in page is in the learner module; a full navigation also drops this module's in-memory state.
    window.location.assign('/auth/sign-in');
  }

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
          <MenuLink as={AppLink} to="/learn/profile" icon={Settings} onClick={() => setOpen(false)}>Profile & settings</MenuLink>
          <MenuLink as={AppLink} to="/learn/notifications" icon={Bell} onClick={() => setOpen(false)}>Notifications</MenuLink>
          {/* Every module mounts the MFA page at its own /auth/mfa, so this stays a module-relative route. */}
          {!auth.mfa ? <MenuLink to="/auth/mfa" icon={ShieldCheck} onClick={() => setOpen(false)}>Set up MFA</MenuLink> : null}
          <button type="button" role="menuitem" onClick={signOut} className="flex w-full items-center gap-2 rounded-input px-3 py-2 text-left text-danger hover:bg-danger-50">
            <LogOut className="h-4 w-4" /> Sign out
          </button>
        </div>
      ) : null}
    </div>
  );
}

function MenuLink({ as: L = Link, to, icon: Icon, children, onClick }) {
  return (
    <L to={to} role="menuitem" onClick={onClick} className="flex items-center gap-2 rounded-input px-3 py-2 text-navy hover:bg-surface">
      <Icon className="h-4 w-4 text-muted" /> {children}
    </L>
  );
}
