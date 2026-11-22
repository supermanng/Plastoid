import { useCallback, useEffect, useMemo, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useToast } from '../components/ui';

export function useDebounce(value, delay = 300) {
  const [debounced, setDebounced] = useState(value);
  useEffect(() => {
    const t = setTimeout(() => setDebounced(value), delay);
    return () => clearTimeout(t);
  }, [value, delay]);
  return debounced;
}

/** URL-backed filter state: returns [values, setValue(key, value), setMany(obj)]. */
export function useQueryParams(defaults = {}) {
  const [params, setParams] = useSearchParams();
  const values = useMemo(() => {
    const out = { ...defaults };
    for (const [k, v] of params.entries()) out[k] = v;
    return out;
  }, [params, defaults]);
  const setValue = useCallback((key, value) => {
    setParams((prev) => {
      const next = new URLSearchParams(prev);
      if (value === undefined || value === null || value === '' || value === defaults[key]) next.delete(key);
      else next.set(key, String(value));
      return next;
    }, { replace: true });
  }, [setParams, defaults]);
  const setMany = useCallback((obj) => {
    setParams((prev) => {
      const next = new URLSearchParams(prev);
      for (const [k, v] of Object.entries(obj)) {
        if (v === undefined || v === null || v === '' || v === defaults[k]) next.delete(k);
        else next.set(k, String(v));
      }
      return next;
    }, { replace: true });
  }, [setParams, defaults]);
  return [values, setValue, setMany];
}

/**
 * useMutation with toast + cache invalidation baked in.
 * useAction(fn, { success: 'Saved', invalidate: [['key'], ...], onSuccess })
 */
export function useAction(mutationFn, { success, invalidate = [], onSuccess, onError, silent = false } = {}) {
  const qc = useQueryClient();
  const toast = useToast();
  return useMutation({
    mutationFn,
    onSuccess: async (data, variables, ctx) => {
      await Promise.all(invalidate.map((key) => qc.invalidateQueries({ queryKey: Array.isArray(key) ? key : [key] })));
      if (success && !silent) toast.success(typeof success === 'function' ? success(data, variables) : success);
      onSuccess?.(data, variables, ctx);
    },
    onError: (error, variables, ctx) => {
      if (!silent) toast.error(error);
      onError?.(error, variables, ctx);
    },
  });
}

/** Simple controlled form state: const { values, set, bind, reset } = useForm(initial). */
export function useForm(initial) {
  const [values, setValues] = useState(initial);
  const set = useCallback((key, value) => setValues((v) => ({ ...v, [key]: value })), []);
  const bind = useCallback((key, type) => ({
    name: key,
    value: values[key] ?? '',
    onChange: (e) => {
      const target = e?.target;
      if (!target) return set(key, e);
      if (type === 'number') return set(key, target.value === '' ? '' : Number(target.value));
      if (target.type === 'checkbox') return set(key, target.checked);
      return set(key, target.value);
    },
  }), [values, set]);
  const reset = useCallback((next) => setValues(next ?? initial), [initial]);
  return { values, set, bind, reset, setValues };
}

/** Cursor pagination helper for `{ items, nextCursor }` responses. */
export function useCursor() {
  const [stack, setStack] = useState([null]);
  const cursor = stack[stack.length - 1];
  return {
    cursor,
    canBack: stack.length > 1,
    next: (nextCursor) => nextCursor && setStack((s) => [...s, nextCursor]),
    back: () => setStack((s) => (s.length > 1 ? s.slice(0, -1) : s)),
    reset: () => setStack([null]),
  };
}

/** Builds the list + cursor out of the API's various paging shapes. */
export function pageOf(data) {
  if (!data) return { items: [], nextCursor: null, total: null };
  if (Array.isArray(data)) return { items: data, nextCursor: null, total: data.length };
  return {
    items: data.items || data.results || data.data || [],
    nextCursor: data.nextCursor ?? data.next ?? data.cursor ?? null,
    total: data.total ?? data.totalCount ?? null,
  };
}
