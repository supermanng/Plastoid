import { useState } from 'react';
import { Link, NavLink, Outlet } from 'react-router-dom';
import clsx from 'clsx';
import { Menu, X } from 'lucide-react';
import { Logo } from '../Logo';
import { Button } from '../ui';
import { homeFor, useAuth } from '../../lib/auth';

const NAV = [
  { to: '/courses', label: 'Courses' },
  { to: '/opportunities', label: 'Sponsored seats' },
  { to: '/tutors', label: 'Find a tutor' },
  { to: '/become-a-tutor', label: 'Teach' },
  { to: '/for-sponsors', label: 'Sponsor' },
];

export default function PublicLayout() {
  const auth = useAuth();
  const [open, setOpen] = useState(false);

  return (
    <div className="flex min-h-dvh flex-col bg-white">
      <header className="sticky top-0 z-40 border-b border-line bg-white/90 backdrop-blur">
        <div className="mx-auto flex h-16 max-w-[1280px] items-center justify-between gap-4 px-4 sm:px-6">
          <Logo />
          <nav className="hidden items-center gap-1 lg:flex" aria-label="Primary">
            {NAV.map((n) => (
              <NavLink
                key={n.to}
                to={n.to}
                className={({ isActive }) => clsx('rounded-input px-3 py-2 text-sm font-semibold transition-colors', isActive ? 'bg-royal-50 text-royal' : 'text-navy hover:bg-surface')}
              >
                {n.label}
              </NavLink>
            ))}
          </nav>
          <div className="hidden items-center gap-2 lg:flex">
            {auth.isAuthenticated ? (
              <Button to={homeFor(auth.roles)} size="sm">My dashboard</Button>
            ) : (
              <>
                <Button to="/auth/sign-in" variant="ghost" size="sm">Sign in</Button>
                <Button to="/auth/sign-in?mode=signup" size="sm">Get started</Button>
              </>
            )}
          </div>
          <button type="button" className="grid h-11 w-11 place-items-center rounded-input text-navy lg:hidden" aria-label={open ? 'Close menu' : 'Open menu'} aria-expanded={open} onClick={() => setOpen((v) => !v)}>
            {open ? <X className="h-6 w-6" /> : <Menu className="h-6 w-6" />}
          </button>
        </div>
        {open ? (
          <nav className="border-t border-line bg-white px-4 py-3 lg:hidden" aria-label="Mobile">
            <div className="flex flex-col">
              {NAV.map((n) => (
                <NavLink key={n.to} to={n.to} onClick={() => setOpen(false)} className={({ isActive }) => clsx('rounded-input px-3 py-3 text-base font-semibold', isActive ? 'bg-royal-50 text-royal' : 'text-navy')}>
                  {n.label}
                </NavLink>
              ))}
              <div className="mt-2 flex gap-2 border-t border-line pt-3">
                {auth.isAuthenticated ? (
                  <Button to={homeFor(auth.roles)} className="flex-1" onClick={() => setOpen(false)}>My dashboard</Button>
                ) : (
                  <>
                    <Button to="/auth/sign-in" variant="secondary" className="flex-1" onClick={() => setOpen(false)}>Sign in</Button>
                    <Button to="/auth/sign-in?mode=signup" className="flex-1" onClick={() => setOpen(false)}>Get started</Button>
                  </>
                )}
              </div>
            </div>
          </nav>
        ) : null}
      </header>

      <main className="flex-1">
        <Outlet />
      </main>

      <footer className="border-t border-line bg-surface">
        <div className="mx-auto grid max-w-[1280px] gap-8 px-4 py-10 sm:px-6 md:grid-cols-4">
          <div className="md:col-span-1">
            <Logo />
            <p className="mt-3 text-sm text-muted">Sponsored and paid skills training for Nigeria. Learn, teach, sponsor.</p>
          </div>
          <FooterCol title="Learn" links={[['/courses', 'Browse courses'], ['/opportunities', 'Sponsored opportunities'], ['/tutors', 'Find a tutor'], ['/verify', 'Verify a certificate']]} />
          <FooterCol title="Partner" links={[['/become-a-tutor', 'Become a tutor'], ['/for-sponsors', 'Sponsor a programme'], ['/auth/staff', 'Staff sign-in']]} />
          <FooterCol title="Company" links={[['/about', 'About'], ['/privacy', 'Privacy (NDPA)'], ['/terms', 'Terms']]} />
        </div>
        <div className="border-t border-line py-4 text-center text-xs text-muted">© {new Date().getFullYear()} Plastoids. All amounts in Nigerian naira (₦).</div>
      </footer>
    </div>
  );
}

function FooterCol({ title, links }) {
  return (
    <div>
      <h4 className="text-sm font-bold text-navy">{title}</h4>
      <ul className="mt-3 space-y-2 text-sm">
        {links.map(([to, label]) => (
          <li key={to}>
            <Link to={to} className="text-muted hover:text-navy">{label}</Link>
          </li>
        ))}
      </ul>
    </div>
  );
}
