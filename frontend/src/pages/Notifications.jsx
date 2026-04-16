import React, { useState, useEffect, useCallback } from 'react';
import { notificationsApi } from '../features/notifications/api/notificationsApi';
import { useNavigate } from 'react-router-dom';

export default function Notifications() {
  const [notifications, setNotifications] = useState([]);
  const [unreadOnly, setUnreadOnly] = useState(false);
  const [page, setPage] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const [loading, setLoading] = useState(false);
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
      console.error("Failed to load notifications", e);
    } finally {
      setLoading(false);
    }
  }, [unreadOnly, page]);

  useEffect(() => {
    load();
  }, [load]);

  // Reset to page 1 when switching filter
  useEffect(() => {
    setPage(1);
  }, [unreadOnly]);

  const handleRead = async (id, deeplinkUrl) => {
    try {
      await notificationsApi.markAsRead(id);
      load();
      if (deeplinkUrl) navigate(deeplinkUrl);
    } catch (e) {
      alert(e.message || "Failed to mark as read");
    }
  };

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex justify-between items-center bg-[#1E1E2D] p-5 rounded-xl shadow-lg border border-gray-800">
        <div>
          <h2 className="text-2xl font-bold text-white">Thông báo</h2>
          <p className="text-gray-400 text-sm mt-1">
            Quản lý các nhắc nhở và thông báo hệ thống
            {totalCount > 0 && <span className="ml-2 text-blue-400">({totalCount} thông báo)</span>}
          </p>
        </div>
        <div className="flex bg-[#2A2A3C] p-1 rounded-lg">
          <button
            onClick={() => setUnreadOnly(false)}
            className={`px-4 py-2 rounded-md text-sm font-medium transition-all ${!unreadOnly ? 'bg-blue-600 text-white shadow' : 'text-gray-400 hover:text-white'}`}
          >
            Tất cả
          </button>
          <button
            onClick={() => setUnreadOnly(true)}
            className={`px-4 py-2 rounded-md text-sm font-medium transition-all ${unreadOnly ? 'bg-blue-600 text-white shadow' : 'text-gray-400 hover:text-white'}`}
          >
            Chưa đọc
          </button>
        </div>
      </div>

      {/* List */}
      <div className="bg-[#1E1E2D] rounded-xl border border-gray-800 p-4">
        {loading ? (
          <div className="text-center text-gray-500 py-8">
            <div className="inline-block animate-spin rounded-full h-6 w-6 border-2 border-gray-600 border-t-blue-500 mr-2"></div>
            Đang tải...
          </div>
        ) : notifications.length === 0 ? (
          <div className="text-center text-gray-500 py-8">Không có thông báo nào</div>
        ) : (
          <div className="space-y-3">
            {notifications.map(n => (
              <div
                key={n.id}
                className={`flex justify-between items-start p-4 rounded-lg border transition-all hover:border-blue-900/50 ${n.isRead ? 'bg-[#1E1E2D] border-gray-800' : 'bg-[#2A2A3C]/50 border-blue-900/30'}`}
              >
                <div className="flex-1 min-w-0">
                  <div className="flex items-center space-x-2">
                    {!n.isRead && <span className="w-2 h-2 rounded-full bg-blue-500 flex-shrink-0"></span>}
                    <h3 className={`font-semibold truncate ${n.isRead ? 'text-gray-400' : 'text-white'}`}>{n.title}</h3>
                  </div>
                  <p className={`mt-1 text-sm ${n.isRead ? 'text-gray-500' : 'text-gray-300'}`}>{n.message}</p>
                  <p className="mt-2 text-xs text-gray-500">{new Date(n.createdAt).toLocaleString('vi-VN')}</p>
                </div>
                <div className="ml-4 flex-shrink-0">
                  {!n.isRead && (
                    <button
                      onClick={() => handleRead(n.id, n.deeplinkUrl)}
                      className="text-sm px-3 py-1 bg-blue-600/20 text-blue-400 rounded hover:bg-blue-600/40 transition-colors"
                    >
                      Đánh dấu đã đọc
                    </button>
                  )}
                  {n.isRead && n.deeplinkUrl && (
                    <button
                      onClick={() => navigate(n.deeplinkUrl)}
                      className="text-sm px-3 py-1 bg-gray-800 text-gray-400 rounded hover:bg-gray-700 transition-colors"
                    >
                      Xem chi tiết
                    </button>
                  )}
                </div>
              </div>
            ))}
          </div>
        )}

        {/* Pagination */}
        {totalPages > 1 && (
          <div className="flex items-center justify-between mt-6 pt-4 border-t border-gray-800">
            <p className="text-sm text-gray-500">
              Trang {page} / {totalPages} • Hiển thị {notifications.length} / {totalCount}
            </p>
            <div className="flex space-x-2">
              <button
                onClick={() => setPage(p => Math.max(1, p - 1))}
                disabled={page <= 1}
                className="px-3 py-1.5 rounded-md text-sm font-medium bg-[#2A2A3C] text-gray-300 hover:bg-[#3A3A4C] transition-colors disabled:opacity-40 disabled:cursor-not-allowed"
              >
                ← Trước
              </button>
              {/* Page number buttons */}
              {Array.from({ length: Math.min(5, totalPages) }, (_, i) => {
                const start = Math.max(1, Math.min(page - 2, totalPages - 4));
                const pageNum = start + i;
                if (pageNum > totalPages) return null;
                return (
                  <button
                    key={pageNum}
                    onClick={() => setPage(pageNum)}
                    className={`px-3 py-1.5 rounded-md text-sm font-medium transition-colors ${
                      pageNum === page
                        ? 'bg-blue-600 text-white shadow'
                        : 'bg-[#2A2A3C] text-gray-300 hover:bg-[#3A3A4C]'
                    }`}
                  >
                    {pageNum}
                  </button>
                );
              })}
              <button
                onClick={() => setPage(p => Math.min(totalPages, p + 1))}
                disabled={page >= totalPages}
                className="px-3 py-1.5 rounded-md text-sm font-medium bg-[#2A2A3C] text-gray-300 hover:bg-[#3A3A4C] transition-colors disabled:opacity-40 disabled:cursor-not-allowed"
              >
                Sau →
              </button>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
