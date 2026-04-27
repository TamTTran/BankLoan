import React, { useState, useEffect, useCallback } from 'react';
import { notificationsApi } from '../features/notifications/api/notificationsApi';
import { useNavigate } from 'react-router-dom';
import { useT } from '../i18n/useT';
import { Card, CardHeader, CardBody } from '../components/ui/Card';
import { Button } from '../components/ui/Button';
import { useNotifications } from '../context/NotificationContext';


export default function Notifications() {
  const { t } = useT();
  const { refreshCount } = useNotifications();
  const [notifications, setNotifications] = useState([]);

  const [unreadOnly, setUnreadOnly] = useState(false);
  const [page, setPage] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const [loading, setLoading] = useState(false);
  const [markingAll, setMarkingAll] = useState(false);
  const pageSize = 20;
  const navigate = useNavigate();

  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const resp = await notificationsApi.getMy(unreadOnly, page, pageSize);
      setNotifications(resp.items || []);
      setTotalCount(resp.totalCount || 0);
    } catch (e) {
      console.error('Failed to load notifications', e);
    } finally {
      setLoading(false);
    }
  }, [unreadOnly, page]);

  useEffect(() => { load(); }, [load]);
  useEffect(() => { setPage(1); }, [unreadOnly]);

  const handleRead = async (id, deeplinkUrl) => {
    try {
      await notificationsApi.markAsRead(id);
      load();
      refreshCount(); // Update bell immediately
      if (deeplinkUrl) navigate(deeplinkUrl);
    } catch (e) {
      console.error('markAsRead failed', e);
    }
  };

  const handleMarkAllRead = async () => {
    setMarkingAll(true);
    try {
      await notificationsApi.markAllAsRead();
      load();
      refreshCount(); // Update bell immediately
    } catch (e) {
      console.error('markAllAsRead failed', e);
    } finally {
      setMarkingAll(false);
    }
  };

  return (
    <div className="page">
      <div className="page-header">
        <div>
          <h2>{t('notifications.title')}</h2>
          <p className="subtitle" style={{ marginTop: '0.2rem' }}>
            {t('notifications.subtitle')}
            {totalCount > 0 && (
              <span style={{ marginLeft: '0.5rem', color: 'var(--blue)', fontWeight: 700 }}>
                ({t('notifications.count', { count: totalCount })})
              </span>
            )}
          </p>
        </div>
        <div style={{ display: 'flex', gap: '0.75rem', alignItems: 'center' }}>
          <Button
            variant="ghost"
            onClick={handleMarkAllRead}
            disabled={markingAll || notifications.every(n => n.isRead)}
          >
            {markingAll ? t('common.processing') : `✓ ${t('notifications.markAllRead')}`}
          </Button>
          <div style={{ display: 'flex', background: 'var(--bg-muted)', borderRadius: '12px', padding: '3px', gap: '2px' }}>
            {[
              { key: false, label: t('notifications.all') },
              { key: true,  label: t('notifications.unreadOnly') },
            ].map(({ key, label }) => (
              <button
                key={String(key)}
                onClick={() => setUnreadOnly(key)}
                style={{
                  padding: '0.38rem 0.9rem', borderRadius: '10px', border: 'none', fontFamily: 'inherit',
                  fontSize: '0.82rem', fontWeight: 700, cursor: 'pointer', transition: 'all 0.18s',
                  background: unreadOnly === key ? 'var(--blue)' : 'transparent',
                  color: unreadOnly === key ? '#fff' : 'var(--text-muted)',
                  boxShadow: unreadOnly === key ? '0 2px 8px rgba(31,95,191,0.18)' : 'none',
                }}
              >
                {label}
              </button>
            ))}
          </div>
        </div>
      </div>

      <Card>
        <CardBody>
          {loading ? (
            <div style={{ padding: '3rem 0', textAlign: 'center', color: 'var(--text-muted)' }}>
              <div className="spinner" style={{ width: 22, height: 22, margin: '0 auto 0.75rem', borderColor: 'var(--border)', borderTopColor: 'var(--blue)' }} />
              {t('notifications.loading')}
            </div>
          ) : notifications.length === 0 ? (
            <div className="empty-state" style={{ padding: '3rem 0' }}>
              <span className="empty-icon">🔔</span>
              <p>{t('notifications.noNotifications')}</p>
            </div>
          ) : (
            <div style={{ display: 'flex', flexDirection: 'column', gap: '0.6rem' }}>
              {notifications.map(n => (
                <div
                  key={n.id}
                  style={{
                    display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start',
                    padding: '1rem 1.15rem', borderRadius: '14px', transition: 'all 0.18s',
                    border: n.isRead ? '1px solid var(--border)' : '1px solid rgba(33,96,196,0.25)',
                    background: n.isRead ? 'transparent' : 'var(--bg-accent-soft)',
                  }}
                >
                  <div style={{ flex: 1, minWidth: 0 }}>
                    <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', marginBottom: '0.25rem' }}>
                      {!n.isRead && (
                        <span style={{ width: 8, height: 8, borderRadius: '50%', background: 'var(--blue)', flexShrink: 0, display: 'block' }} />
                      )}
                      <h3 style={{
                        fontWeight: n.isRead ? 600 : 800, fontSize: '0.9rem', margin: 0,
                        color: n.isRead ? 'var(--text-secondary)' : 'var(--navy)',
                        overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap',
                      }}>
                        {n.title}
                      </h3>
                    </div>
                    <p style={{ margin: 0, fontSize: '0.84rem', color: n.isRead ? 'var(--text-muted)' : 'var(--text-secondary)', lineHeight: 1.5 }}>
                      {n.message}
                    </p>
                    <p style={{ margin: '0.4rem 0 0', fontSize: '0.75rem', color: 'var(--text-muted)' }}>
                      {new Date(n.createdAt).toLocaleString(undefined, { dateStyle: 'medium', timeStyle: 'short' })}
                    </p>
                  </div>
                  <div style={{ marginLeft: '1rem', flexShrink: 0, display: 'flex', gap: '0.5rem' }}>
                    {!n.isRead && (
                      <Button variant="outline" onClick={() => handleRead(n.id, n.deeplinkUrl)}
                        style={{ fontSize: '0.78rem', padding: '0.32rem 0.7rem' }}>
                        {t('notifications.markRead')}
                      </Button>
                    )}
                    {n.isRead && n.deeplinkUrl && (
                      <Button variant="ghost" onClick={() => navigate(n.deeplinkUrl)}
                        style={{ fontSize: '0.78rem', padding: '0.32rem 0.7rem' }}>
                        {t('notifications.viewDetail')}
                      </Button>
                    )}
                  </div>
                </div>
              ))}
            </div>
          )}

          {/* Pagination */}
          {totalPages > 1 && (
            <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginTop: '1.5rem', paddingTop: '1rem', borderTop: '1px solid var(--border)' }}>
              <p style={{ fontSize: '0.82rem', color: 'var(--text-muted)' }}>
                {t('notifications.pageInfo', { page, total: totalPages, count: notifications.length, totalCount })}
              </p>
              <div style={{ display: 'flex', gap: '0.4rem' }}>
                <Button variant="ghost" onClick={() => setPage(p => Math.max(1, p - 1))} disabled={page <= 1}
                  style={{ fontSize: '0.82rem', padding: '0.38rem 0.75rem' }}>
                  {t('notifications.prev')}
                </Button>
                {Array.from({ length: Math.min(5, totalPages) }, (_, i) => {
                  const start = Math.max(1, Math.min(page - 2, totalPages - 4));
                  const pageNum = start + i;
                  if (pageNum > totalPages) return null;
                  return (
                    <Button key={pageNum} variant={pageNum === page ? 'primary' : 'ghost'}
                      onClick={() => setPage(pageNum)}
                      style={{ fontSize: '0.82rem', padding: '0.38rem 0.65rem', minWidth: '2rem' }}>
                      {pageNum}
                    </Button>
                  );
                })}
                <Button variant="ghost" onClick={() => setPage(p => Math.min(totalPages, p + 1))} disabled={page >= totalPages}
                  style={{ fontSize: '0.82rem', padding: '0.38rem 0.75rem' }}>
                  {t('notifications.next')}
                </Button>
              </div>
            </div>
          )}
        </CardBody>
      </Card>
    </div>
  );
}
