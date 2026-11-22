import { Suspense, lazy } from 'react';
import { Route, Routes, useLocation } from 'react-router-dom';
import AppShell from './layout/AppShell';
import { RequireAuth, ROLES } from '../../shared/lib/auth';
import { Loading } from '../../shared/components/ui';

/*
 * Administration module (ADR 0020): the back office, router at basename `/admin`. Routes are relative to that base
 * (`/admin/users` is `users` here). The shared staff sign-in, MFA and no-access pages are mounted under `auth/`.
 */

// Auth (shared)
const StaffSignIn = lazy(() => import('../../shared/pages/auth/StaffSignIn'));
const Mfa = lazy(() => import('../../shared/pages/auth/Mfa'));
const NoAccess = lazy(() => import('../../shared/pages/auth/NoAccess'));
const NotFound = lazy(() => import('../../shared/pages/NotFound'));

// Back office
const AdminDashboard = lazy(() => import('./pages/Dashboard'));
const AdminQueues = lazy(() => import('./pages/Queues'));
const AdminUsers = lazy(() => import('./pages/Users'));
const AdminUserDetail = lazy(() => import('./pages/UserDetail'));
const AdminTutors = lazy(() => import('./pages/Tutors'));
const AdminSponsors = lazy(() => import('./pages/Sponsors'));
const AdminProgrammes = lazy(() => import('./pages/Programmes'));
const AdminProgrammeDetail = lazy(() => import('./pages/ProgrammeDetail'));
const AdminCatalogue = lazy(() => import('./pages/Catalogue'));
const AdminFinance = lazy(() => import('./pages/Finance'));
const AdminPayouts = lazy(() => import('./pages/Payouts'));
const AdminPayoutBatch = lazy(() => import('./pages/PayoutBatch'));
const AdminFeeRules = lazy(() => import('./pages/FeeRules'));
const AdminUnitRates = lazy(() => import('./pages/UnitRates'));
const AdminRoleGrants = lazy(() => import('./pages/RoleGrants'));
const AdminTemplates = lazy(() => import('./pages/Templates'));
const AdminFeatureFlags = lazy(() => import('./pages/FeatureFlags'));
const AdminCompliance = lazy(() => import('./pages/Compliance'));
const AdminAudit = lazy(() => import('./pages/Audit'));

export default function App() {
  const location = useLocation();
  return (
    <Suspense fallback={<Loading className="min-h-dvh" />} key={location.pathname.split('/')[1]}>
      <Routes>
        <Route path="auth/staff" element={<StaffSignIn />} />
        <Route path="auth/mfa" element={<Mfa />} />
        <Route path="auth/no-access" element={<NoAccess />} />

        <Route element={<RequireAuth role={ROLES.staff} mfa><AppShell /></RequireAuth>}>
          <Route index element={<AdminDashboard />} />
          <Route path="queues" element={<AdminQueues />} />
          <Route path="users" element={<AdminUsers />} />
          <Route path="users/:userId" element={<AdminUserDetail />} />
          <Route path="tutors" element={<AdminTutors />} />
          <Route path="sponsors" element={<AdminSponsors />} />
          <Route path="programmes" element={<AdminProgrammes />} />
          <Route path="programmes/:programmeId" element={<AdminProgrammeDetail />} />
          <Route path="catalogue" element={<AdminCatalogue />} />
          <Route path="finance" element={<AdminFinance />} />
          <Route path="payouts" element={<AdminPayouts />} />
          <Route path="payouts/:batchId" element={<AdminPayoutBatch />} />
          <Route path="fee-rules" element={<AdminFeeRules />} />
          <Route path="unit-rates" element={<AdminUnitRates />} />
          <Route path="role-grants" element={<AdminRoleGrants />} />
          <Route path="templates" element={<AdminTemplates />} />
          <Route path="feature-flags" element={<AdminFeatureFlags />} />
          <Route path="compliance" element={<AdminCompliance />} />
          <Route path="audit" element={<AdminAudit />} />
        </Route>

        <Route path="*" element={<NotFound />} />
      </Routes>
    </Suspense>
  );
}
