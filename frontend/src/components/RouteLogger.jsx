/**
 * RouteLogger — tự động log mỗi lần route thay đổi.
 * Đặt bên trong <BrowserRouter> để useLocation hoạt động.
 */
import { useEffect, useRef } from 'react';
import { useLocation } from 'react-router-dom';
import { logger } from '../shared/utils/logger';

export function RouteLogger() {
  const location = useLocation();
  const prev     = useRef(null);

  useEffect(() => {
    const to   = location.pathname + (location.search || '');
    const from = prev.current ?? '(start)';
    if (from !== to) {
      logger.nav(from, to);
    }
    prev.current = to;
  }, [location]);

  return null; // render nothing
}
