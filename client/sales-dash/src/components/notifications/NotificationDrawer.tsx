import React, { useState } from 'react';
import {
  Drawer,
  Stack,
  Group,
  Text,
  Button,
  Tabs,
  ScrollArea,
  Center,
  Loader
} from '@mantine/core';
import { IconChecklist, IconBellOff } from '@tabler/icons-react';
import { useNotifications } from '../../contexts/NotificationContext';
import { NotificationCard } from './NotificationCard';
import { NotificationCategory } from '../../types/Notification';

interface Props {
  opened: boolean;
  onClose: () => void;
}

export const NotificationDrawer: React.FC<Props> = ({ opened, onClose }) => {
  const { notificationsList, unreadCount, loading, markAllAsRead } = useNotifications();
  const [activeTab, setActiveTab] = useState<string>('all');

  const filteredNotifications = notificationsList.filter(item => {
    if (activeTab === 'all') return true;
    if (activeTab === 'unread') return item.unread;
    if (activeTab === 'requests') return item.category === 'SOLICITACOES';
    return item.category === activeTab;
  });

  return (
    <Drawer
      opened={opened}
      onClose={onClose}
      position="right"
      size="md"
      title={
        <Group justify="space-between" style={{ width: '100%' }}>
          <Group gap="xs">
            <Text fw={700} size="lg">
              Notificações
            </Text>
            {unreadCount > 0 && (
              <Text size="sm" c="dimmed">
                ({unreadCount} não lidas)
              </Text>
            )}
          </Group>
          {unreadCount > 0 && (
            <Button
              variant="subtle"
              size="xs"
              leftSection={<IconChecklist size={14} />}
              onClick={markAllAsRead}
            >
              Marcar todas como lidas
            </Button>
          )}
        </Group>
      }
    >
      <Stack gap="md" style={{ height: 'calc(100vh - 80px)' }}>
        <Tabs value={activeTab} onChange={val => setActiveTab(val || 'all')}>
          <Tabs.List>
            <Tabs.Tab value="all">Todas</Tabs.Tab>
            <Tabs.Tab value="unread">Não Lidas</Tabs.Tab>
            <Tabs.Tab value="requests">Solicitações</Tabs.Tab>
            <Tabs.Tab value="CONQUISTAS">Conquistas</Tabs.Tab>
          </Tabs.List>
        </Tabs>

        <ScrollArea style={{ flex: 1 }} offsetScrollbars>
          {loading && notificationsList.length === 0 ? (
            <Center h={200}>
              <Loader size="md" />
            </Center>
          ) : filteredNotifications.length === 0 ? (
            <Center h={250}>
              <Stack align="center" gap="xs">
                <IconBellOff size={40} color="#adb5bd" stroke={1.5} />
                <Text size="sm" c="dimmed">
                  Nenhuma notificação encontrada
                </Text>
              </Stack>
            </Center>
          ) : (
            <Stack gap="xs">
              {filteredNotifications.map(notification => (
                <NotificationCard key={notification.sk} notification={notification} />
              ))}
            </Stack>
          )}
        </ScrollArea>
      </Stack>
    </Drawer>
  );
};
