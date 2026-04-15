import { useState, useCallback, useEffect, useRef } from 'react';
import { loanApi } from '../api/loanApi';
import { mapLoanList } from '../mappers/loanMapper';

export function useLoans() {
  const [data, setData]       = useState(null);
  const [page, setPage]       = useState(1);
  const [search, setSearch]   = useState('');
  const [status, setStatus]   = useState('');
  const [loading, setLoading] = useState(true);

  // Keep refs to current values so refresh() never uses stale closures
  const stateRef = useRef({ page: 1, search: '', status: '' });
  stateRef.current = { page, search, status };

  const load = useCallback(async (p=1, q='', s='') => {
    setLoading(true);
    try {
      const params = { page: p, pageSize: 15 };
      if (q) params.search = q;
      if (s) params.status = s;
      const res = await loanApi.list(params);
      setData(mapLoanList(res));
    } catch (e) {
      console.error(e);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { load(); }, [load]);
  
  const doSearch = useCallback(() => {
    setPage(1);
    load(1, stateRef.current.search, stateRef.current.status);
  }, [load]);
  
  const refresh = useCallback(() => {
    const { page: p, search: q, status: s } = stateRef.current;
    load(p, q, s);
  }, [load]);

  return {
    data,
    page, setPage,
    search, setSearch,
    status, setStatus,
    loading,
    load, doSearch, refresh
  };
}
