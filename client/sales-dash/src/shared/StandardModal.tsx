import React from 'react';
import { Modal, Title, Group } from '@mantine/core';

interface StandardModalProps {
  isOpen: boolean;
  onClose: () => void;
  title: string;
  children: React.ReactNode;
  footer?: React.ReactNode;
  size?: 'sm' | 'md' | 'lg' | 'xl';
  className?: string; // Optional class for the body/form container
  headerActions?: React.ReactNode; // Optional actions to place in the header next to close button
}

const StandardModal: React.FC<StandardModalProps> = ({
  isOpen,
  onClose,
  title,
  children,
  footer,
  size = 'md',
  className = 'premium-light-form',
  headerActions,
}) => {
  return (
    <Modal
      opened={isOpen}
      onClose={onClose}
      title={
        <Group justify="space-between" style={{ width: '100%' }}>
          <Title order={3} style={{ color: '#1c1c1e', fontWeight: 700 }}>
            {title}
          </Title>
          {headerActions && <div style={{ marginRight: '16px' }}>{headerActions}</div>}
        </Group>
      }
      size={size}
      styles={{
        content: {
          border: '1px solid #e9ecef',
          borderRadius: '12px',
          boxShadow: '0 10px 40px rgba(0, 0, 0, 0.08)',
          backgroundColor: '#ffffff',
        },
        header: {
          borderBottom: '1px solid #f3f4f6',
          paddingBottom: '12px',
          marginBottom: '16px',
          backgroundColor: '#ffffff',
        },
        body: {
          backgroundColor: '#ffffff',
          color: '#1c1c1e',
          padding: '24px',
        }
      }}
    >
      <div className={className}>
        {children}
        
        {footer && (
          <Group justify="flex-end" mt="xl" style={{ 
            paddingTop: '16px', 
            borderTop: '1px solid #f3f4f6',
            marginTop: '24px'
          }}>
            {footer}
          </Group>
        )}
      </div>
    </Modal>
  );
};

export default StandardModal;
