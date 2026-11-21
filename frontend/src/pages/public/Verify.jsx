import { useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { Award, Search } from 'lucide-react';
import { Button, Card, ErrorAlert, Field, Input, KeyValue, Loading, PageHeader, StatusPill } from '../../components/ui';
import { api } from '../../lib/api';
import { formatDate } from '../../lib/format';

/** Public certificate verification: GET /verify/{publicCode} → valid | revoked | unknown. */
export default function Verify() {
  const { publicCode } = useParams();
  const navigate = useNavigate();
  const [code, setCode] = useState(publicCode || '');

  const query = useQuery({
    queryKey: ['verify', publicCode],
    queryFn: () => api.get(`/verify/${encodeURIComponent(publicCode)}`, undefined, { auth: false }),
    enabled: Boolean(publicCode),
    retry: false,
  });

  return (
    <div className="mx-auto max-w-2xl px-4 py-10 sm:px-6">
      <PageHeader title="Verify a certificate" description="Every Plastoids certificate carries a public code. Enter it to confirm the holder, course and issue date." />
      <Card>
        <form
          className="flex flex-col gap-3 sm:flex-row sm:items-end"
          onSubmit={(e) => {
            e.preventDefault();
            if (code.trim()) navigate(`/verify/${encodeURIComponent(code.trim())}`);
          }}
        >
          <Field label="Certificate code" className="flex-1">
            {(p) => <Input {...p} value={code} onChange={(e) => setCode(e.target.value)} placeholder="e.g. PLT-7F3K-9Q2M" autoFocus />}
          </Field>
          <Button type="submit" icon={Search} disabled={!code.trim()}>Verify</Button>
        </form>
      </Card>

      {publicCode ? (
        <Card className="mt-6">
          {query.isPending ? <Loading /> : null}
          {query.isError ? (
            query.error?.status === 404 ? (
              <div className="flex items-start gap-3">
                <StatusPill status="unknown" label="Not found" />
                <p className="text-sm text-muted">No certificate matches <code>{publicCode}</code>. Check the code and try again.</p>
              </div>
            ) : (
              <ErrorAlert error={query.error} />
            )
          ) : null}
          {query.data ? <Result data={query.data} /> : null}
        </Card>
      ) : null}
    </div>
  );
}

function Result({ data }) {
  const status = data.status || data.state || (data.valid === false ? 'revoked' : 'valid');
  return (
    <div>
      <div className="flex items-center gap-3">
        <span className="grid h-12 w-12 place-items-center rounded-full bg-teal-50 text-teal-600"><Award className="h-6 w-6" /></span>
        <div>
          <StatusPill status={status} />
          <div className="mt-1 text-xs text-muted">Code {data.publicCode || data.code}</div>
        </div>
      </div>
      <KeyValue
        className="mt-5"
        items={[
          ['Holder', data.learnerName || data.holderName || data.displayName],
          ['Course', data.courseTitle || data.course],
          ['Issued', formatDate(data.issuedAt)],
          data.revokedAt ? ['Revoked', formatDate(data.revokedAt)] : null,
          data.revocationReason || data.reason ? ['Reason', data.revocationReason || data.reason] : null,
          data.tutorName ? ['Tutor', data.tutorName] : null,
          data.grade || data.score ? ['Result', data.grade || `${data.score}%`] : null,
          data.hash ? ['Integrity hash', <code key="hash" className="text-xs break-all">{data.hash}</code>] : null,
        ]}
      />
    </div>
  );
}
