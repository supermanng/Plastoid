import { Suspense, lazy } from 'react';
import { Navigate, Route, Routes, useLocation } from 'react-router-dom';
import PublicLayout from './components/layout/PublicLayout';
import AppShell from './components/layout/AppShell';
import { RequireAuth, ROLES, homeFor, useAuth } from './lib/auth';
import { Loading } from './components/ui';

/*
 * Route map. Pages are lazy so each area ships as its own chunk.
 * Areas: public (/), auth (/auth/*), learner (/learn/*), tutor (/tutor/*), sponsor (/sponsor/*), back office (/admin/*).
 */

// Public
const Home = lazy(() => import('./pages/public/Home'));
const Courses = lazy(() => import('./pages/public/Courses'));
const CourseDetail = lazy(() => import('./pages/public/CourseDetail'));
const Compare = lazy(() => import('./pages/public/Compare'));
const Opportunities = lazy(() => import('./pages/public/Opportunities'));
const OpportunityDetail = lazy(() => import('./pages/public/OpportunityDetail'));
const Tutors = lazy(() => import('./pages/public/Tutors'));
const TutorProfile = lazy(() => import('./pages/public/TutorProfile'));
const BecomeTutor = lazy(() => import('./pages/public/BecomeTutor'));
const ForSponsors = lazy(() => import('./pages/public/ForSponsors'));
const Verify = lazy(() => import('./pages/public/Verify'));
const StaticPage = lazy(() => import('./pages/public/StaticPage'));
const NotFound = lazy(() => import('./pages/public/NotFound'));

// Auth
const SignIn = lazy(() => import('./pages/auth/SignIn'));
const StaffSignIn = lazy(() => import('./pages/auth/StaffSignIn'));
const Mfa = lazy(() => import('./pages/auth/Mfa'));
const NoAccess = lazy(() => import('./pages/auth/NoAccess'));

// Learner
const LearnDashboard = lazy(() => import('./pages/learn/Dashboard'));
const LearnOnboarding = lazy(() => import('./pages/learn/Onboarding'));
const MyCourses = lazy(() => import('./pages/learn/MyCourses'));
const CoursePlayer = lazy(() => import('./pages/learn/CoursePlayer'));
const MyApplications = lazy(() => import('./pages/learn/Applications'));
const ApplicationDetail = lazy(() => import('./pages/learn/ApplicationDetail'));
const Apply = lazy(() => import('./pages/learn/Apply'));
const MyBookings = lazy(() => import('./pages/learn/Bookings'));
const BookingDetail = lazy(() => import('./pages/learn/BookingDetail'));
const MyCertificates = lazy(() => import('./pages/learn/Certificates'));
const MyPayments = lazy(() => import('./pages/learn/Payments'));
const MyWallet = lazy(() => import('./pages/learn/Wallet'));
const PaymentDetail = lazy(() => import('./pages/learn/PaymentDetail'));
const PaymentReturn = lazy(() => import('./pages/learn/PaymentReturn'));
const Messages = lazy(() => import('./pages/learn/Messages'));
const Notifications = lazy(() => import('./pages/learn/Notifications'));
const NotificationPreferences = lazy(() => import('./pages/learn/NotificationPreferences'));
const Profile = lazy(() => import('./pages/learn/Profile'));
const LiveSession = lazy(() => import('./pages/learn/LiveSession'));

// Tutor
const TutorDashboard = lazy(() => import('./pages/tutor/Dashboard'));
const TutorOnboarding = lazy(() => import('./pages/tutor/Onboarding'));
const TutorProfileSettings = lazy(() => import('./pages/tutor/Profile'));
const TutorCourses = lazy(() => import('./pages/tutor/Courses'));
const CourseBuilder = lazy(() => import('./pages/tutor/CourseBuilder'));
const Gradebook = lazy(() => import('./pages/tutor/Gradebook'));
const TutorCohorts = lazy(() => import('./pages/tutor/Cohorts'));
const CohortDetail = lazy(() => import('./pages/tutor/CohortDetail'));
const TutorSessions = lazy(() => import('./pages/tutor/Sessions'));
const Earnings = lazy(() => import('./pages/tutor/Earnings'));

// Sponsor
const SponsorDashboard = lazy(() => import('./pages/sponsor/Dashboard'));
const SponsorOrganisation = lazy(() => import('./pages/sponsor/Organisation'));
const RegisterOrganisation = lazy(() => import('./pages/sponsor/RegisterOrganisation'));
const SponsorProgrammes = lazy(() => import('./pages/sponsor/Programmes'));
const CampaignBuilder = lazy(() => import('./pages/sponsor/CampaignBuilder'));
const ProgrammeDetail = lazy(() => import('./pages/sponsor/ProgrammeDetail'));
const SponsorInvoices = lazy(() => import('./pages/sponsor/Invoices'));
const SponsorWallet = lazy(() => import('./pages/sponsor/Wallet'));
const InvoiceDetail = lazy(() => import('./pages/sponsor/InvoiceDetail'));

// Back office
const AdminDashboard = lazy(() => import('./pages/admin/Dashboard'));
const AdminQueues = lazy(() => import('./pages/admin/Queues'));
const AdminUsers = lazy(() => import('./pages/admin/Users'));
const AdminUserDetail = lazy(() => import('./pages/admin/UserDetail'));
const AdminTutors = lazy(() => import('./pages/admin/Tutors'));
const AdminSponsors = lazy(() => import('./pages/admin/Sponsors'));
const AdminProgrammes = lazy(() => import('./pages/admin/Programmes'));
const AdminProgrammeDetail = lazy(() => import('./pages/admin/ProgrammeDetail'));
const AdminCatalogue = lazy(() => import('./pages/admin/Catalogue'));
const AdminFinance = lazy(() => import('./pages/admin/Finance'));
const AdminPayouts = lazy(() => import('./pages/admin/Payouts'));
const AdminPayoutBatch = lazy(() => import('./pages/admin/PayoutBatch'));
const AdminFeeRules = lazy(() => import('./pages/admin/FeeRules'));
const AdminUnitRates = lazy(() => import('./pages/admin/UnitRates'));
const AdminRoleGrants = lazy(() => import('./pages/admin/RoleGrants'));
const AdminTemplates = lazy(() => import('./pages/admin/Templates'));
const AdminFeatureFlags = lazy(() => import('./pages/admin/FeatureFlags'));
const AdminCompliance = lazy(() => import('./pages/admin/Compliance'));
const AdminAudit = lazy(() => import('./pages/admin/Audit'));

function RoleHome() {
  const auth = useAuth();
  if (auth.status === 'loading') return <Loading />;
  if (!auth.isAuthenticated) return <Navigate to="/auth/sign-in" replace />;
  return <Navigate to={homeFor(auth.roles)} replace />;
}

export default function App() {
  const location = useLocation();
  return (
    <Suspense fallback={<Loading className="min-h-dvh" />} key={location.pathname.split('/')[1]}>
      <Routes>
        {/* Public site */}
        <Route element={<PublicLayout />}>
          <Route index element={<Home />} />
          <Route path="courses" element={<Courses />} />
          <Route path="courses/compare" element={<Compare />} />
          <Route path="courses/:slug" element={<CourseDetail />} />
          <Route path="opportunities" element={<Opportunities />} />
          <Route path="opportunities/:programmeId" element={<OpportunityDetail />} />
          <Route path="tutors" element={<Tutors />} />
          <Route path="tutors/:tutorId" element={<TutorProfile />} />
          <Route path="become-a-tutor" element={<BecomeTutor />} />
          <Route path="for-sponsors" element={<ForSponsors />} />
          <Route path="verify" element={<Verify />} />
          <Route path="verify/:publicCode" element={<Verify />} />
          <Route path="about" element={<StaticPage page="about" />} />
          <Route path="privacy" element={<StaticPage page="privacy" />} />
          <Route path="terms" element={<StaticPage page="terms" />} />

          <Route path="auth/sign-in" element={<SignIn />} />
          <Route path="auth/staff" element={<StaffSignIn />} />
          <Route path="auth/mfa" element={<Mfa />} />
          <Route path="auth/no-access" element={<NoAccess />} />
          <Route path="dashboard" element={<RoleHome />} />
        </Route>

        {/* Learner area: any signed-in account (profile, inbox and messages live here for every role) */}
        <Route path="learn" element={<RequireAuth><AppShell area="learn" /></RequireAuth>}>
          <Route index element={<LearnDashboard />} />
          <Route path="onboarding" element={<LearnOnboarding />} />
          <Route path="courses" element={<MyCourses />} />
          <Route path="courses/:enrolmentId" element={<CoursePlayer />} />
          <Route path="courses/:enrolmentId/lessons/:lessonId" element={<CoursePlayer />} />
          <Route path="applications" element={<MyApplications />} />
          <Route path="applications/:applicationId" element={<ApplicationDetail />} />
          <Route path="apply/:programmeId" element={<Apply />} />
          <Route path="bookings" element={<MyBookings />} />
          <Route path="bookings/:bookingId" element={<BookingDetail />} />
          <Route path="certificates" element={<MyCertificates />} />
          <Route path="payments" element={<MyPayments />} />
          <Route path="wallet" element={<MyWallet />} />
          <Route path="payments/return" element={<PaymentReturn />} />
          <Route path="payments/return/:paymentIntentId" element={<PaymentReturn />} />
          <Route path="payments/:paymentIntentId" element={<PaymentDetail />} />
          <Route path="messages" element={<Messages />} />
          <Route path="messages/:threadId" element={<Messages />} />
          <Route path="notifications" element={<Notifications />} />
          <Route path="notifications/preferences" element={<NotificationPreferences />} />
          <Route path="profile" element={<Profile />} />
          <Route path="live/:liveSessionId" element={<LiveSession />} />
        </Route>

        {/* Tutor area: onboarding is open to any signed-in account; the rest needs the tutor role + MFA */}
        <Route path="tutor">
          <Route path="onboarding" element={<RequireAuth><AppShell area="tutor" /></RequireAuth>}>
            <Route index element={<TutorOnboarding />} />
          </Route>
          <Route element={<RequireAuth role={ROLES.tutor} mfa><AppShell area="tutor" /></RequireAuth>}>
            <Route index element={<TutorDashboard />} />
            <Route path="profile" element={<TutorProfileSettings />} />
            <Route path="courses" element={<TutorCourses />} />
            <Route path="courses/:courseId" element={<CourseBuilder />} />
            <Route path="courses/:courseId/gradebook" element={<Gradebook />} />
            <Route path="cohorts" element={<TutorCohorts />} />
            <Route path="cohorts/:cohortId" element={<CohortDetail />} />
            <Route path="sessions" element={<TutorSessions />} />
            <Route path="earnings" element={<Earnings />} />
          </Route>
        </Route>

        {/* Sponsor area: registering an organisation is open to any signed-in account */}
        <Route path="sponsor">
          <Route path="organisation/new" element={<RequireAuth><AppShell area="sponsor" /></RequireAuth>}>
            <Route index element={<RegisterOrganisation />} />
          </Route>
          <Route element={<RequireAuth role={ROLES.sponsor} mfa><AppShell area="sponsor" /></RequireAuth>}>
            <Route index element={<SponsorDashboard />} />
            <Route path="organisation" element={<SponsorOrganisation />} />
            <Route path="programmes" element={<SponsorProgrammes />} />
            <Route path="programmes/new" element={<CampaignBuilder />} />
            <Route path="programmes/:programmeId" element={<ProgrammeDetail />} />
            <Route path="programmes/:programmeId/edit" element={<CampaignBuilder />} />
            <Route path="invoices" element={<SponsorInvoices />} />
            <Route path="wallet" element={<SponsorWallet />} />
            <Route path="invoices/:invoiceId" element={<InvoiceDetail />} />
          </Route>
        </Route>

        {/* Back office */}
        <Route path="admin" element={<RequireAuth role={ROLES.staff} mfa><AppShell area="admin" /></RequireAuth>}>
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

        <Route path="*" element={<PublicLayout />}>
          <Route path="*" element={<NotFound />} />
        </Route>
      </Routes>
    </Suspense>
  );
}
