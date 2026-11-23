/*
 * Units wallet API (ADR 0018). Shapes mirror the C# records in src/Modules/Wallet:
 *   WalletView            { walletId, ownerType, ownerId, balance, balanceValue: Money, rateKobo, lifetimePurchased, lifetimeRedeemed,
 *                           openLots[{ lotId, unitsTotal, unitsRemaining, valueRemaining, kind, acquiredAt }],
 *                           recentTransactions[{ transactionId, kind, units, value, balanceAfter, purpose, referenceId, description, occurredAt }],
 *                           pendingPurchases[{ purchaseId, paymentIntentId, units, amount, status, createdAt }] }
 *   PurchaseUnitsResponse { purchaseId, walletId, paymentIntentId, units, rateKobo, amount, checkoutUrl, status }
 *   UnitRatesView         { effectiveRateKobo, effectiveRateValue, effectiveSource, rates[UnitRateVersionView] }
 *   UnitRateVersionView   { rateId, rateKobo, rateValue, version, effectiveFrom, status, rationale, proposedBy, proposedAt, decidedBy, decidedAt, decisionNote }
 */
import { api } from '../lib/api';

export const walletKeys = {
  rate: ['wallet', 'rate'],
  me: ['wallet', 'me'],
  organisation: (orgId) => ['wallet', 'organisation', orgId],
  transactions: (ownerKey, cursor) => ['wallet', 'transactions', ownerKey, cursor ?? null],
  adminRates: ['admin', 'wallet', 'rates'],
  adminLookup: (ownerType, ownerId) => ['admin', 'wallet', ownerType, ownerId],
};

export const walletApi = {
  rate: () => api.get('/wallet/rate', undefined, { auth: false }),
  me: () => api.get('/wallet/me'),
  organisation: (orgId) => api.get(`/wallet/organisations/${orgId}`),
  myTransactions: (cursor, limit = 50) => api.get('/wallet/me/transactions', { cursor, limit }),
  organisationTransactions: (orgId, cursor, limit = 50) => api.get(`/wallet/organisations/${orgId}/transactions`, { cursor, limit }),
  /** POST /wallet/purchases { units, organisationId? } → hosted checkout; units land after the gateway confirms. */
  purchase: (units, organisationId) => api.post('/wallet/purchases', { units: Number(units), organisationId: organisationId ?? null }),

  // Staff
  rates: () => api.get('/admin/wallet/rates'),
  proposeRate: (body) => api.post('/admin/wallet/rates', body),
  decideRate: (rateId, decision, note) => api.post(`/admin/wallet/rates/${rateId}/decision`, { decision, note: note || null }),
  lookup: (ownerType, ownerId) => api.get(`/admin/wallet/${ownerType}/${ownerId}`),
  adjust: (ownerType, ownerId, units, reason) => api.post(`/admin/wallet/${ownerType}/${ownerId}/adjustments`, { units: Number(units), reason }),
};

export const PURCHASE_PRESETS = [10, 50, 100, 500];
