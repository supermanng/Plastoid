import { useEffect, useState } from 'react';
import { Link, useLocation, useParams } from 'react-router-dom';
import { CheckCircle2, Clock, XCircle } from 'lucide-react';
import { Button, Card, ErrorAlert, Loading } from '../../components/ui';
import { payments } from '../../api/learn';

/*
 * Gateway return page. The API builds the callback as `${Payments:CallbackBaseUrl}/{paymentIntentId:N}` (32 hex chars,
 * no dashes). Point `Payments:CallbackBaseUrl` at this route (e.g. http://localhost:5173/learn/payments/return) and the
 * intent id arrives as the last path segment; `?intent=`, `?paymentIntentId=`, `?reference=` and `?trxref=` are also read.
 */
export default function PaymentReturn() {
  const location = useLocation();
  const params = useParams();
  const [state, setState] = useState({ status: 'verifying' });

  useEffect(() => {
    const q = new URLSearchParams(location.search);
    const raw = params.paymentIntentId || q.get('intent') || q.get('paymentIntentId') || q.get('payment_intent') || lastSegment(location.pathname) || refToId(q.get('reference') || q.get('trxref'));
    const id = toGuid(raw);
    if (!id) {
      setState({ status: 'missing' });
      return;
    }
    payments
      .verify(id)
      .then((r) => setState({ status: r.status, id, gatewayStatus: r.gatewayStatus }))
      .catch((error) => setState({ status: 'error', id, error }));
  }, [location, params]);

  return (
    <div className="mx-auto max-w-md pt-8">
      <Card className="text-center">
        {state.status === 'verifying' ? <Loading label="Confirming your payment with the gateway…" /> : null}
        {state.status === 'succeeded' ? (
          <>
            <CheckCircle2 className="mx-auto h-12 w-12 text-teal" />
            <h1 className="mt-3 text-2xl">Payment confirmed</h1>
            <p className="mt-1 text-sm text-muted">Thank you. Your enrolment, booking or seat is now active.</p>
          </>
        ) : null}
        {state.status === 'pending' ? (
          <>
            <Clock className="mx-auto h-12 w-12 text-warn" />
            <h1 className="mt-3 text-2xl">Still processing</h1>
            <p className="mt-1 text-sm text-muted">The gateway reports “{state.gatewayStatus}”. Bank transfers can take a few minutes; we will notify you.</p>
          </>
        ) : null}
        {state.status === 'failed' || state.status === 'expired' ? (
          <>
            <XCircle className="mx-auto h-12 w-12 text-danger" />
            <h1 className="mt-3 text-2xl">Payment {state.status}</h1>
            <p className="mt-1 text-sm text-muted">Nothing was charged. You can try again from the course, booking or offer page.</p>
          </>
        ) : null}
        {state.status === 'missing' ? (
          <>
            <XCircle className="mx-auto h-12 w-12 text-muted" />
            <h1 className="mt-3 text-2xl">No payment reference</h1>
            <p className="mt-1 text-sm text-muted">We could not tell which payment you returned from. Find it in your payments list.</p>
          </>
        ) : null}
        {state.status === 'error' ? <ErrorAlert error={state.error} title="Could not verify" className="text-left" /> : null}
        <div className="mt-6 flex flex-wrap justify-center gap-2">
          {state.id ? <Button to={`/learn/payments/${state.id}`}>Payment details</Button> : null}
          <Button to="/learn/payments" variant="secondary">All payments</Button>
          <Button to="/learn" variant="ghost">Dashboard</Button>
        </div>
        {state.status !== 'verifying' ? <p className="mt-4 text-xs text-muted"><Link to="/learn/courses">My courses</Link> · <Link to="/learn/bookings">Tutor sessions</Link> · <Link to="/learn/applications">Applications</Link></p> : null}
      </Card>
    </div>
  );
}

function lastSegment(pathname) {
  const seg = pathname.replace(/\/+$/, '').split('/').pop();
  return seg === 'return' ? null : seg;
}

/** Fake/Paystack references look like "ps_<guid-N>" or "<guid>"; pull out 32 hex chars. */
function refToId(ref) {
  if (!ref) return null;
  const m = ref.replace(/-/g, '').match(/[0-9a-f]{32}/i);
  return m ? m[0] : null;
}

function toGuid(raw) {
  if (!raw) return null;
  const hex = String(raw).replace(/-/g, '');
  if (!/^[0-9a-f]{32}$/i.test(hex)) return /^[0-9a-f-]{36}$/i.test(raw) ? raw : null;
  return `${hex.slice(0, 8)}-${hex.slice(8, 12)}-${hex.slice(12, 16)}-${hex.slice(16, 20)}-${hex.slice(20)}`.toLowerCase();
}
