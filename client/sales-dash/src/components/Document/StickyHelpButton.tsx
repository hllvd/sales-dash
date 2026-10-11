import React, { useState, useEffect } from 'react';
import { ActionIcon, Tooltip } from '@mantine/core';
import { IconHelp } from '@tabler/icons-react';
import { HelpdeskModal } from './HelpdeskModal';

export const StickyHelpButton: React.FC = () => {
  const [modalOpen, setModalOpen] = useState(false);
  const [userRole, setUserRole] = useState<string>('');

  useEffect(() => {
    const updateRole = () => {
      const userJson = localStorage.getItem('user');
      if (userJson) {
        try {
          const u = JSON.parse(userJson);
          if (u.role) {
            setUserRole(u.role);
            return;
          }
        } catch (e) {
          console.error(e);
        }
      }

      const token = localStorage.getItem('token');
      if (token) {
        try {
          const payload = token.split('.')[1];
          const base64 = payload.replace(/-/g, '+').replace(/_/g, '/');
          const jsonPayload = decodeURIComponent(atob(base64).split('').map(c => 
            '%' + ('00' + c.charCodeAt(0).toString(16)).slice(-2)
          ).join(''));
          const decoded = JSON.parse(jsonPayload);
          const roleFromToken = decoded.role || decoded['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'] || '';
          if (roleFromToken) {
            setUserRole(roleFromToken);
          }
        } catch (e) {
          console.error(e);
        }
      }
    };

    updateRole();

    window.addEventListener('storage', updateRole);
    return () => {
      window.removeEventListener('storage', updateRole);
    };
  }, []);

  // Show only for admin or superadmin
  const role = userRole.toLowerCase();
  const canAsk = role === 'admin' || role === 'superadmin';

  if (!canAsk) {
    return null;
  }

  return (
    <>
      <div
        style={{
          position: 'fixed',
          top: 18,
          right: 22,
          zIndex: 1000,
          transition: 'all 0.3s ease',
          boxShadow: '0 4px 12px rgba(9, 105, 218, 0.3)',
          borderRadius: '50%'
        }}
      >
        <Tooltip label="Fazer uma pergunta sobre esta tela" position="left" withArrow>
          <ActionIcon
            size={42}
            radius="xl"
            color="blue"
            variant="filled"
            onClick={() => setModalOpen(true)}
            aria-label="Ajuda e FAQ"
          >
            <IconHelp size={24} />
          </ActionIcon>
        </Tooltip>
      </div>

      <HelpdeskModal
        opened={modalOpen}
        onClose={() => setModalOpen(false)}
        questionId={null}
      />
    </>
  );
};
