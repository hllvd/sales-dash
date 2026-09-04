export type NotificationCategory =
  | 'CONQUISTAS'
  | 'PROGRESSO'
  | 'URGENCIA'
  | 'SOLICITACOES'
  | 'TAREFAS'
  | 'OPORTUNIDADES';

export type NotificationPriority = 'LOW' | 'NORMAL' | 'HIGH' | 'CRITICAL';

export type RequestStatus = 'PENDING' | 'ACCEPTED' | 'DECLINED' | 'EXPIRED';

export type AnimationKey =
  | 'NONE'
  | 'LEVEL_UP'
  | 'BADGE_UNLOCKED'
  | 'TROPHY'
  | 'TARGET_REACHED'
  | 'URGENT_ALERT';

export interface NotificationAction {
  type: string;
  label: string;
}

export interface NotificationItem {
  id: string;
  sk: string;
  type: string;
  category: NotificationCategory;
  priority: NotificationPriority;
  title: string;
  message: string;
  animation: AnimationKey;
  actions: NotificationAction[];
  relatedPK?: string;
  relatedSK?: string;
  unread: boolean;
  createdAt: string;
}

export interface PagedNotifications {
  items: NotificationItem[];
  nextCursor?: string;
  unreadCount: number;
}

export interface DomainRequest {
  sk: string;
  recipientUserId: string;
  requesterUserId: string;
  requestType: string;
  status: RequestStatus;
  payloadJson: string;
  createdAt: string;
  resolvedAt?: string;
}
