import React from 'react';
import { Card, Text, Group, Badge, Button, Stack, ActionIcon, Box } from '@mantine/core';
import {
  IconCheck,
  IconX,
  IconTrophy,
  IconTrendingUp,
  IconAlertTriangle,
  IconUserPlus,
  IconClipboardCheck,
  IconFlame
} from '@tabler/icons-react';
import { NotificationItem, NotificationCategory } from '../../types/Notification';
import { useNotifications } from '../../contexts/NotificationContext';

interface Props {
  notification: NotificationItem;
}

const getCategoryIcon = (category: NotificationCategory) => {
  switch (category) {
    case 'CONQUISTAS':
      return <IconTrophy size={18} color="#e59900" />;
    case 'PROGRESSO':
      return <IconTrendingUp size={18} color="#2b8a3e" />;
    case 'URGENCIA':
      return <IconAlertTriangle size={18} color="#d9480f" />;
    case 'SOLICITACOES':
      return <IconUserPlus size={18} color="#1971c2" />;
    case 'TAREFAS':
      return <IconClipboardCheck size={18} color="#495057" />;
    case 'OPORTUNIDADES':
      return <IconFlame size={18} color="#f03e3e" />;
    default:
      return null;
  }
};

const getCategoryLabel = (category: NotificationCategory) => {
  switch (category) {
    case 'CONQUISTAS':
      return 'Conquista';
    case 'PROGRESSO':
      return 'Progresso';
    case 'URGENCIA':
      return 'Urgência';
    case 'SOLICITACOES':
      return 'Solicitação';
    case 'TAREFAS':
      return 'Tarefa';
    case 'OPORTUNIDADES':
      return 'Oportunidade';
    default:
      return category;
  }
};

export const NotificationCard: React.FC<Props> = ({ notification }) => {
  const { markAsRead, acceptRequest, declineRequest } = useNotifications();

  const handleCardClick = () => {
    if (notification.unread) {
      markAsRead(notification.sk);
    }
  };

  const handleAction = async (e: React.MouseEvent, type: string) => {
    e.stopPropagation();
    if (!notification.relatedSK) return;

    if (type.startsWith('ACCEPT_')) {
      await acceptRequest(notification.relatedSK);
    } else if (type.startsWith('DECLINE_')) {
      await declineRequest(notification.relatedSK);
    }
  };

  return (
    <Card
      withBorder
      shadow={notification.unread ? 'xs' : undefined}
      radius="md"
      p="sm"
      onClick={handleCardClick}
      style={{
        cursor: 'pointer',
        backgroundColor: notification.unread ? 'rgba(231, 245, 255, 0.4)' : undefined,
        borderLeft: notification.unread ? '4px solid #228be6' : undefined,
        transition: 'background-color 0.2s ease'
      }}
    >
      <Stack gap="xs">
        <Group justify="space-between" wrap="nowrap">
          <Group gap="xs">
            {getCategoryIcon(notification.category)}
            <Text size="xs" c="dimmed" fw={500}>
              {getCategoryLabel(notification.category)}
            </Text>
          </Group>
          <Group gap="xs">
            {notification.unread && (
              <Badge size="xs" color="blue" variant="filled">
                Nova
              </Badge>
            )}
            <Text size="xs" c="dimmed">
              {new Date(notification.createdAt).toLocaleTimeString([], {
                hour: '2-digit',
                minute: '2-digit'
              })}
            </Text>
          </Group>
        </Group>

        <Box>
          <Text size="sm" fw={600}>
            {notification.title}
          </Text>
          <Text size="xs" c="dimmed" mt={2}>
            {notification.message}
          </Text>
        </Box>

        {notification.actions && notification.actions.length > 0 && (
          <Group gap="xs" mt="xs">
            {notification.actions.map(action => {
              const isAccept = action.type.startsWith('ACCEPT_');
              return (
                <Button
                  key={action.type}
                  size="xs"
                  variant={isAccept ? 'filled' : 'light'}
                  color={isAccept ? 'blue' : 'gray'}
                  leftSection={isAccept ? <IconCheck size={14} /> : <IconX size={14} />}
                  onClick={e => handleAction(e, action.type)}
                >
                  {action.label}
                </Button>
              );
            })}
          </Group>
        )}
      </Stack>
    </Card>
  );
};
