import { httpClient } from '../../../shared/api/httpClient';

export const adminApi = {
  // User management
  getUsers:       ()              => httpClient('GET',    '/admin/users'),
  createUser:     (body)          => httpClient('POST',   '/admin/users', body),
  changeRole:     (id, role)      => httpClient('PUT',    `/admin/users/${id}/role`, { role }),
  changeStatus:   (id, status)    => httpClient('PUT',    `/admin/users/${id}/status?status=${status}`),
  lockAccount:    (id, minutes)   => httpClient('PUT',    `/admin/users/${id}/lock?minutes=${minutes}`),
  unlockAccount:  (id)            => httpClient('PUT',    `/admin/users/${id}/unlock`),
  getSessions:    (id)            => httpClient('GET',    `/admin/users/${id}/sessions`),
  revokeAllSessions: (id)         => httpClient('DELETE', `/admin/users/${id}/sessions`),
  revokeSession:  (userId, sid)   => httpClient('DELETE', `/admin/users/${userId}/sessions/${sid}`),
};
