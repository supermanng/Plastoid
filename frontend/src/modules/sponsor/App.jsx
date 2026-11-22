import { Suspense, lazy } from 'react';
import { Route, Routes, useLocation } from 'react-router-dom';
import AppShell from './layout/AppShell';
import { RequireAuth, ROLES } from '../../shared/lib/auth';
import { Loading } from '../../shared/components/ui';

/*
 * Sponsor module (ADR 0020): the organisation portal (sponsors and providers), router at basename `/sponsor`.
 * Routes are relative to that base (`/sponsor/programmes` is `programmes` here). The shared sign-in, MFA and
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
const SponsorProgrammes = lazy(() => import('./pages/Programmes'));
const CampaignBuilder = lazy(() => import('./pages/CampaignBuilder'));
const ProgrammeDetail = lazy(() => import('./pages/ProgrammeDetail'));
const SponsorInvoices = lazy(() => import('./pages/Invoices'));
const SponsorWallet = lazy(() => import('./pages/Wallet'));
const InvoiceDetail = lazy(() => import('./pages/InvoiceDetail'));
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
          <Route path="programmes" element={<SponsorProgrammes />} />
          <Route path="programmes/new" element={<CampaignBuilder />} />
          <Route path="programmes/:programmeId" element={<ProgrammeDetail />} />
          <Route path="programmes/:programmeId/edit" element={<CampaignBuilder />} />
          <Route path="invoices" element={<SponsorInvoices />} />
          <Route path="wallet" element={<SponsorWallet />} />
          <Route path="invoices/:invoiceId" element={<InvoiceDetail />} />
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
