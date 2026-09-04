import { API_BASE_URL } from '../config';
import { PagedNotifications, DomainRequest } from '../types/Notification';

class NotificationApiService {
  private getHeaders(): HeadersInit {
    const token = localStorage.getItem('token');
    return {
      'Content-Type': 'application/json',
      ...(token ? { Authorization: `Bearer ${token}` } : {})
    };
  }

  public async getNotifications(limit = 20, cursor?: string): Promise<PagedNotifications> {
    const params = new URLSearchParams();
    params.append('limit', limit.toString());
    if (cursor) params.append('cursor', cursor);

    const res = await fetch(`${API_BASE_URL}/api/notifications?${params.toString()}`, {
      headers: this.getHeaders()
    });

    const json = await res.json();
    if (!res.ok || !json.success) {
      throw new Error(json.message || 'Falha ao carregar notificações');
    }
    return json.data;
  }

  public async getUnreadCount(): Promise<number> {
    const res = await fetch(`${API_BASE_URL}/api/notifications/unread-count`, {
      headers: this.getHeaders()
    });
    const json = await res.json();
    if (!res.ok || !json.success) {
      return 0;
    }
    return json.data.unreadCount;
  }

  public async markAsRead(sk: string): Promise<void> {
    const encodedSk = encodeURIComponent(sk);
    const res = await fetch(`${API_BASE_URL}/api/notifications/${encodedSk}/read`, {
      method: 'POST',
      headers: this.getHeaders()
    });
    const json = await res.json();
    if (!res.ok || !json.success) {
      throw new Error(json.message || 'Falha ao marcar notificação como lida');
    }
  }

  public async markAllAsRead(): Promise<void> {
    const res = await fetch(`${API_BASE_URL}/api/notifications/read-all`, {
      method: 'POST',
      headers: this.getHeaders()
    });
    const json = await res.json();
    if (!res.ok || !json.success) {
      throw new Error(json.message || 'Falha ao marcar todas como lidas');
    }
  }

  public async getPendingRequests(): Promise<DomainRequest[]> {
    const res = await fetch(`${API_BASE_URL}/api/requests/pending`, {
      headers: this.getHeaders()
    });
    const json = await res.json();
    if (!res.ok || !json.success) {
      throw new Error(json.message || 'Falha ao carregar solicitações pendentes');
    }
    return json.data;
  }

  public async acceptRequest(sk: string, comment?: string): Promise<void> {
    const encodedSk = encodeURIComponent(sk);
    const res = await fetch(`${API_BASE_URL}/api/requests/${encodedSk}/accept`, {
      method: 'POST',
      headers: this.getHeaders(),
      body: JSON.stringify({ comment })
    });
    const json = await res.json();
    if (!res.ok || !json.success) {
      throw new Error(json.message || 'Falha ao aceitar solicitação');
    }
  }

  public async declineRequest(sk: string, comment?: string): Promise<void> {
    const encodedSk = encodeURIComponent(sk);
    const res = await fetch(`${API_BASE_URL}/api/requests/${encodedSk}/decline`, {
      method: 'POST',
      headers: this.getHeaders(),
      body: JSON.stringify({ comment })
    });
    const json = await res.json();
    if (!res.ok || !json.success) {
      throw new Error(json.message || 'Falha ao recusar solicitação');
    }
  }
}

export const notificationService = new NotificationApiService();
