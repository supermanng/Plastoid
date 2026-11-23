import { Suspense, lazy } from 'react';
import { Route, Routes, useLocation } from 'react-router-dom';
import AppShell from './layout/AppShell';
import { RequireAuth, ROLES } from '../../shared/lib/auth';
import { Loading } from '../../shared/components/ui';

/*
 * Sponsor module (ADR 0020): the organisation portal (sponsors and providers), router at basename `/sponsor`.
 * Routes are relative to that base (`/sponsor/seats` is `seats` here). The shared sign-in, MFA and
 * no-access pages are mounted under `auth/` so RequireAuth can redirect inside this module.
 */

// Auth (shared)
const SignIn = lazy(() => import('../../shared/pages/auth/SignIn'));
const Mfa = lazy(() => import('../../shared/pages/auth/Mfa'));
const NoAccess = lazy(() => import('../../shared/pages/auth/NoAccess'));
const NotFound = lazy(() => import('../../shared/pages/NotFound'));

// Sponsor
const SponsorDashboard = lazy(() => import('./pages/Dashboard'));
const SponsorOrganisation = lazy(() => import('./pages/Organisation'));
const RegisterOrganisation = lazy(() => import('./pages/RegisterOrganisation'));
const SponsorSeats = lazy(() => import('./pages/Seats'));
const BuySeats = lazy(() => import('./pages/BuySeats'));
const SeatBlock = lazy(() => import('./pages/SeatBlock'));
const SponsorWallet = lazy(() => import('./pages/Wallet'));
const SponsorCourses = lazy(() => import('./pages/Courses'));
const SponsorCourseEditor = lazy(() => import('./pages/CourseEditor'));
const SponsorCourseTutors = lazy(() => import('./pages/CourseTutors'));
const SponsorPayouts = lazy(() => import('./pages/Payouts'));

export default function App() {
  const location = useLocation();
  return (
    <Suspense fallback={<Loading className="min-h-dvh" />} key={location.pathname.split('/')[1]}>
      <Routes>
        <Route path="auth/sign-in" element={<SignIn />} />
        <Route path="auth/mfa" element={<Mfa />} />
        <Route path="auth/no-access" element={<NoAccess />} />

        {/* Registering an organisation is open to any signed-in account */}
        <Route path="organisation/new" element={<RequireAuth><AppShell /></RequireAuth>}>
          <Route index element={<RegisterOrganisation />} />
        </Route>
        <Route element={<RequireAuth role={ROLES.sponsor} mfa><AppShell /></RequireAuth>}>
          <Route index element={<SponsorDashboard />} />
          <Route path="organisation" element={<SponsorOrganisation />} />
          <Route path="seats" element={<SponsorSeats />} />
          <Route path="seats/new" element={<BuySeats />} />
          <Route path="seats/:blockId" element={<SeatBlock />} />
          <Route path="wallet" element={<SponsorWallet />} />
          <Route path="courses" element={<SponsorCourses />} />
          <Route path="courses/new" element={<SponsorCourseEditor />} />
          <Route path="courses/:courseId" element={<SponsorCourseEditor />} />
          <Route path="courses/:courseId/tutors" element={<SponsorCourseTutors />} />
          <Route path="payouts" element={<SponsorPayouts />} />
        </Route>

        <Route path="*" element={<NotFound />} />
      </Routes>
    </Suspense>
  );
}
