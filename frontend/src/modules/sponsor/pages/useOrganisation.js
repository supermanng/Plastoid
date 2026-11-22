import { useCallback, useEffect, useMemo, useState } from 'react';
import { useAuth } from '../../../shared/lib/auth';

const STORAGE_KEY = 'plastoids.sponsor.org';

function readStored() {
  try {
    return localStorage.getItem(STORAGE_KEY);
  } catch {
    return null;
  }
}

/**
 * Selected sponsor organisation. Organisations come from GET /identity/me → organisations[{ id, name, type, role, verificationStatus }].
 * Roles: owner | finance | author | reviewer | viewer | member. Owner-only: publish, members, shortlist approval.
 * Finance (or owner): approve quote, invoices, payout account. Author (or owner): courses and tutor engagements (ADR 0019).
 */
export function useOrganisation() {
  const auth = useAuth();
  const organisations = useMemo(() => auth.organisations || [], [auth.organisations]);
  const [selectedId, setSelectedId] = useState(readStored);

  useEffect(() => {
    if (!organisations.length) return;
    if (!organisations.some((o) => o.id === selectedId)) {
      setSelectedId(organisations[0].id);
    }
  }, [organisations, selectedId]);

  const select = useCallback((id) => {
    setSelectedId(id);
    try {
      localStorage.setItem(STORAGE_KEY, id);
    } catch {
      /* ignore */
    }
  }, []);

  const organisation = organisations.find((o) => o.id === selectedId) || organisations[0] || null;
  const role = String(organisation?.role || '').toLowerCase();
  const verified = String(organisation?.verificationStatus || '').toLowerCase() === 'verified';

  return {
    organisations,
    organisation,
    organisationId: organisation?.id || null,
    role,
    isOwner: role === 'owner',
    isFinance: role === 'owner' || role === 'finance',
    isAuthor: role === 'owner' || role === 'author',
    verified,
    verificationStatus: organisation?.verificationStatus,
    select,
    refresh: auth.refreshMe,
  };
}
