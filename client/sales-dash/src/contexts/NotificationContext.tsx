import React, { createContext, useContext, useState, useEffect, useCallback, ReactNode } from 'react';
import { NotificationItem, DomainRequest, AnimationKey } from '../types/Notification';
import { notificationService } from '../services/notificationService';
import { sseService } from '../services/sseService';
import { notifications } from '@mantine/notifications';
import { ANIMATION_MAP } from '../utils/animationMap';

interface NotificationContextType {
  notificationsList: NotificationItem[];
  unreadCount: number;
  loading: boolean;
  activeAnimation: AnimationKey | null;
  clearActiveAnimation: () => void;
  loadNotifications: () => Promise<void>;
  markAsRead: (sk: string) => Promise<void>;
  markAllAsRead: () => Promise<void>;
  acceptRequest: (sk: string) => Promise<void>;
  declineRequest: (sk: string) => Promise<void>;
}

const NotificationContext = createContext<NotificationContextType | undefined>(undefined);

export const NotificationProvider: React.FC<{ children: ReactNode }> = ({ children }) => {
  const [notificationsList, setNotificationsList] = useState<NotificationItem[]>([]);
  const [unreadCount, setUnreadCount] = useState<number>(0);
  const [loading, setLoading] = useState<boolean>(false);
  const [activeAnimation, setActiveAnimation] = useState<AnimationKey | null>(null);

  const clearActiveAnimation = useCallback(() => {
    setActiveAnimation(null);
  }, []);

  const loadNotifications = useCallback(async () => {
    const token = localStorage.getItem('token');
    if (!token) return;

    try {
      setLoading(true);
      const data = await notificationService.getNotifications(30);
      setNotificationsList(data.items);
      setUnreadCount(data.unreadCount);
    } catch (err) {
      console.error('Error loading notifications:', err);
    } finally {
      setLoading(false);
    }
  }, []);

  const markAsRead = useCallback(async (sk: string) => {
    try {
      await notificationService.markAsRead(sk);
      setNotificationsList(prev =>
        prev.map(item => (item.sk === sk ? { ...item, unread: false } : item))
      );
      setUnreadCount(prev => Math.max(0, prev - 1));
    } catch (err) {
      console.error('Error marking as read:', err);
    }
  }, []);

  const markAllAsRead = useCallback(async () => {
    try {
      await notificationService.markAllAsRead();
      setNotificationsList(prev => prev.map(item => ({ ...item, unread: false })));
      setUnreadCount(0);
    } catch (err) {
      console.error('Error marking all as read:', err);
    }
  }, []);

  const acceptRequest = useCallback(async (sk: string) => {
    try {
      await notificationService.acceptRequest(sk);
      // Optimistically update notifications linked to this request
      setNotificationsList(prev =>
        prev.map(item => (item.relatedSK === sk ? { ...item, unread: false } : item))
      );
      setUnreadCount(prev => Math.max(0, prev - 1));
      notifications.show({
        title: 'Sucesso',
        message: 'Solicitação aceita com sucesso.',
        color: 'green'
      });
    } catch (err: any) {
      notifications.show({
        title: 'Erro',
        message: err.message || 'Falha ao aceitar solicitação.',
        color: 'red'
      });
    }
  }, []);

  const declineRequest = useCallback(async (sk: string) => {
    try {
      await notificationService.declineRequest(sk);
      setNotificationsList(prev =>
        prev.map(item => (item.relatedSK === sk ? { ...item, unread: false } : item))
      );
      setUnreadCount(prev => Math.max(0, prev - 1));
      notifications.show({
        title: 'Recusada',
        message: 'Solicitação recusada com sucesso.',
        color: 'gray'
      });
    } catch (err: any) {
      notifications.show({
        title: 'Erro',
        message: err.message || 'Falha ao recusar solicitação.',
        color: 'red'
      });
    }
  }, []);

  // Set up real-time SSE listener
  useEffect(() => {
    const token = localStorage.getItem('token');
    if (!token) return;

    loadNotifications();
    sseService.connect();

    const unsubscribe = sseService.subscribe((event, data) => {
      if (event === 'notification') {
        const newNotif = data as NotificationItem;

        setNotificationsList(prev => [newNotif, ...prev]);
        setUnreadCount(prev => prev + 1);

        // Check priority for real-time toast
        if (newNotif.priority === 'HIGH' || newNotif.priority === 'CRITICAL' || newNotif.priority === 'NORMAL') {
          notifications.show({
            title: newNotif.title,
            message: newNotif.message,
            color: newNotif.priority === 'CRITICAL' ? 'red' : newNotif.priority === 'HIGH' ? 'orange' : 'blue',
            autoClose: newNotif.priority === 'CRITICAL' ? false : 6000
          });
        }

        // Trigger animation if configured
        if (newNotif.animation && newNotif.animation !== 'NONE' && ANIMATION_MAP[newNotif.animation]) {
          setActiveAnimation(newNotif.animation);
        }
      } else if (event === 'unread_count') {
        if (typeof data?.unreadCount === 'number') {
          setUnreadCount(data.unreadCount);
        }
      } else if (event === 'request_resolved') {
        loadNotifications();
      }
    });

    return () => {
      unsubscribe();
      sseService.disconnect();
    };
  }, [loadNotifications]);

  return (
    <NotificationContext.Provider
      value={{
        notificationsList,
        unreadCount,
        loading,
        activeAnimation,
        clearActiveAnimation,
        loadNotifications,
        markAsRead,
        markAllAsRead,
        acceptRequest,
        declineRequest
      }}
    >
      {children}
    </NotificationContext.Provider>
  );
};

export const useNotifications = () => {
  const context = useContext(NotificationContext);
  if (!context) {
    throw new Error('useNotifications must be used within a NotificationProvider');
  }
  return context;
};
