import React from 'react';
import { Modal, Title } from '@mantine/core';
import { UserProfile } from './UserProfile';

interface UserProfileModalProps {
  userId: string | null;
  opened: boolean;
  onClose: () => void;
}

export const UserProfileModal: React.FC<UserProfileModalProps> = ({ userId, opened, onClose }) => {
  if (!userId) return null;

  return (
    <Modal
      opened={opened}
      onClose={onClose}
      title={<Title order={3} style={{ color: '#1c1c1e', fontWeight: 700 }}>Perfil do Usuário</Title>}
      size="85%"
      centered
      overlayProps={{
        opacity: 0,
        blur: 0,
      }}
      styles={{
        content: {
          border: '1px solid #e9ecef',
          borderRadius: '12px',
          boxShadow: '0 10px 40px rgba(0, 0, 0, 0.08)',
        },
        header: {
          borderBottom: '1px solid #f3f4f6',
          paddingBottom: '12px',
          marginBottom: '16px',
        }
      }}
    >
      <UserProfile userId={userId} mode="modal" onClose={onClose} />
    </Modal>
  );
};
export default UserProfileModal;
