import { Suspense, lazy, useEffect } from 'react';
import { Route, Routes, useLocation, useNavigate } from 'react-router-dom';
import PublicLayout from './layout/PublicLayout';
import AppShell from './layout/AppShell';
import { RequireAuth, ROLES, homeFor, useAuth } from '../../shared/lib/auth';
import { navigateTo, signInPath } from '../../shared/lib/navigation';
import { Loading } from '../../shared/components/ui';

/*
 * Learner module (ADR 0020): router at basename `/`. Pages are lazy so each area ships as its own chunk.
 * Areas: public (/), auth (/auth/*), learner (/learn/*), tutor (/tutor/*). The sponsor (/sponsor/*) and back
 * office (/admin/*) areas are separate modules with their own entries; links into them are full-page navigations.
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
const NotFound = lazy(() => import('../../shared/pages/NotFound'));

// Auth
const SignIn = lazy(() => import('../../shared/pages/auth/SignIn'));
const StaffSignIn = lazy(() => import('../../shared/pages/auth/StaffSignIn'));
const Mfa = lazy(() => import('../../shared/pages/auth/Mfa'));
const NoAccess = lazy(() => import('../../shared/pages/auth/NoAccess'));

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
const TutorOffers = lazy(() => import('./pages/tutor/Offers'));

/** /dashboard: send the account to its home area, which may be in another module (sponsor, back office). */
function RoleHome() {
  const auth = useAuth();
  const navigate = useNavigate();
  useEffect(() => {
    if (auth.status === 'loading') return;
    navigateTo(auth.isAuthenticated ? homeFor(auth.roles) : signInPath(), navigate, { replace: true });
  }, [auth.status, auth.isAuthenticated, auth.roles, navigate]);
  return <Loading />;
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
            <Route path="offers" element={<TutorOffers />} />
          </Route>
        </Route>

        <Route path="*" element={<PublicLayout />}>
          <Route path="*" element={<NotFound />} />
        </Route>
      </Routes>
    </Suspense>
  );
}
