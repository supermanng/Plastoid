import { PageHeader } from '../../components/ui';

const PAGES = {
  about: {
    title: 'About Plastoids',
    body: [
      'Plastoids connects Nigerian learners with sponsored and paid skills training: cohort programmes funded by sponsors, self-paced courses and one-to-one tutoring from verified tutors.',
      'Sponsors fund seats and watch enrolment, attendance and completion live. Tutors are verified through identity, credential and subject reviews before they teach. Learners earn verifiable certificates.',
      'Every naira moves through a double-entry ledger with daily reconciliation, so sponsors, tutors and regulators can trust the numbers.',
    ],
  },
  privacy: {
    title: 'Privacy notice (NDPA)',
    body: [
      'We collect the minimum personal data needed to run the platform: your phone number, name and date of birth for your account; NIN and BVN only for tutors, verified through a licensed provider and stored encrypted.',
      'You can export everything we hold about you from Profile → Data export, and erase your account from Profile → Delete account. Learning records and ledger entries are kept in anonymised form as the law requires.',
      'Phone numbers, identifiers and dates of birth are encrypted at rest. They are never written to logs and never shared with sponsors beyond what you opt into.',
    ],
  },
  terms: {
    title: 'Terms of use',
    body: [
      'Learners must be 18 or older. Sponsored seats are offered under the sponsor\'s published eligibility rules; accepting an offer means accepting the code of conduct for the programme.',
      'Tutors are paid weekly for earnings that have cleared the hold period. Refunds follow the cancellation windows shown at checkout: full refund more than 24 hours before a session, 50% within 24 hours, 100% when the tutor cancels.',
      'Messages between learners and tutors stay on the platform; posts that share contact details or payment requests are screened.',
    ],
  },
};

export default function StaticPage({ page }) {
  const content = PAGES[page] || PAGES.about;
  return (
    <article className="mx-auto max-w-3xl px-4 py-10 sm:px-6">
      <PageHeader title={content.title} />
      <div className="space-y-4 text-base leading-7 text-navy">
        {content.body.map((p, i) => <p key={i}>{p}</p>)}
      </div>
    </article>
  );
}
