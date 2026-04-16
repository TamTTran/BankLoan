import { httpClient } from '../../../shared/api/httpClient';

export const notificationsApi = {
  getMy: (unreadOnly = false, page = 1, pageSize = 50) =>
    httpClient('GET', `/notifications?unreadOnly=${unreadOnly}&page=${page}&pageSize=${pageSize}`),
  getUnreadCount: () => httpClient('GET', '/notifications/unread-count'),
  markAsRead: (id) => httpClient('POST', `/notifications/${id}/read`)
};
