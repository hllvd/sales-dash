import React, { useState } from 'react';
import { ActionIcon, Indicator, Tooltip } from '@mantine/core';
import { IconBell } from '@tabler/icons-react';
import { useNotifications } from '../../contexts/NotificationContext';
import { NotificationDrawer } from './NotificationDrawer';

export const NotificationBell: React.FC = () => {
  const { unreadCount } = useNotifications();
  const [opened, setOpened] = useState(false);

  return (
    <>
      <Tooltip label="Notificações" withArrow position="bottom">
        <Indicator
          disabled={unreadCount === 0}
          label={unreadCount > 99 ? '99+' : unreadCount}
          size={18}
          offset={4}
          color="red"
          withBorder
        >
          <ActionIcon
            variant="subtle"
            size="lg"
            radius="md"
            onClick={() => setOpened(true)}
            aria-label="Abrir notificações"
          >
            <IconBell size={22} stroke={1.5} />
          </ActionIcon>
        </Indicator>
      </Tooltip>

      <NotificationDrawer opened={opened} onClose={() => setOpened(false)} />
    </>
  );
};
