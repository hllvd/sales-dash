import React from 'react';
import { Modal, Title, ModalProps } from '@mantine/core';

interface StyledModalProps extends Omit<ModalProps, 'title'> {
  title: string;
  children: React.ReactNode;
}

/**
 * Reusable styled modal component with a premium light theme
 * Clean header + white content area + subtle shadows for maximum contrast and readability
 */
const StyledModal: React.FC<StyledModalProps> = ({ title, children, ...props }) => {
  return (
    <Modal
      {...props}
      title={
        <Title order={3} style={{ color: '#1c1c1e', fontWeight: 700 }}>
          {title}
        </Title>
      }
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
      <div className="premium-light-form">
        {children}
      </div>
    </Modal>
  );
};

export default StyledModal;
