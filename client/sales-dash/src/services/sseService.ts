import { API_BASE_URL } from '../config';

export type SseEventHandler = (event: string, data: any) => void;

class SseService {
  private eventSource: EventSource | null = null;
  private listeners: Set<SseEventHandler> = new Set();
  private reconnectTimeout: any = null;

  public connect(): void {
    const token = localStorage.getItem('token');
    if (!token) return;

    if (this.eventSource) {
      this.disconnect();
    }

    const streamUrl = `${API_BASE_URL}/api/notifications/stream?token=${encodeURIComponent(token)}`;
    this.eventSource = new EventSource(streamUrl);

    this.eventSource.addEventListener('connected', (e: MessageEvent) => {
      try {
        const data = JSON.parse(e.data);
        this.notifyListeners('connected', data);
      } catch (err) {
        // ignore malformed
      }
    });

    this.eventSource.addEventListener('notification', (e: MessageEvent) => {
      try {
        const data = JSON.parse(e.data);
        this.notifyListeners('notification', data);
      } catch (err) {
        console.error('Failed to parse incoming notification SSE:', err);
      }
    });

    this.eventSource.addEventListener('unread_count', (e: MessageEvent) => {
      try {
        const data = JSON.parse(e.data);
        this.notifyListeners('unread_count', data);
      } catch (err) {
        console.error('Failed to parse incoming unread_count SSE:', err);
      }
    });

    this.eventSource.addEventListener('request_resolved', (e: MessageEvent) => {
      try {
        const data = JSON.parse(e.data);
        this.notifyListeners('request_resolved', data);
      } catch (err) {
        console.error('Failed to parse request_resolved SSE:', err);
      }
    });

    this.eventSource.onerror = () => {
      // EventSource natively auto-reconnects, but if state is closed, schedule manual retry
      if (this.eventSource?.readyState === EventSource.CLOSED) {
        this.disconnect();
        this.reconnectTimeout = setTimeout(() => this.connect(), 5000);
      }
    };
  }

  public subscribe(handler: SseEventHandler): () => void {
    this.listeners.add(handler);
    return () => {
      this.listeners.delete(handler);
    };
  }

  private notifyListeners(event: string, data: any) {
    this.listeners.forEach(listener => {
      try {
        listener(event, data);
      } catch (err) {
        console.error('Error in SSE listener handler:', err);
      }
    });
  }

  public disconnect(): void {
    if (this.reconnectTimeout) {
      clearTimeout(this.reconnectTimeout);
      this.reconnectTimeout = null;
    }
    if (this.eventSource) {
      this.eventSource.close();
      this.eventSource = null;
    }
  }
}

export const sseService = new SseService();
