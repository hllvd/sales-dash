import React from 'react';
import { Modal, Title, ModalProps } from '@mantine/core';

interface StyledModalProps extends Omit<ModalProps, 'title'> {
  title: string;
  children: React.ReactNode;
}

/**
 * Reusable styled modal component with light form area for better readability
 * Dark header + light form content + light footer for maximum contrast
 */
const StyledModal: React.FC<StyledModalProps> = ({ title, children, ...props }) => {
  return (
    <Modal
      {...props}
      title={
        <Title order={2} c="rgb(245, 245, 245)">
          {title}
        </Title>
      }
      styles={{
        header: {
          backgroundColor: '#1a1b1e',
          borderBottom: '1px solid #373A40',
          padding: '20px 24px',
        },
        body: {
          backgroundColor: '#1a1b1e',
          padding: '24px',
          color: '#fff',
        },
        content: {
          backgroundColor: '#1a1b1e',
        },
        close: {
          color: '#fff',
          backgroundColor: 'rgba(255, 255, 255, 0.1)',
          borderRadius: '6px',
          width: '36px',
          height: '36px',
          '&:hover': {
            backgroundColor: 'rgba(255, 255, 255, 0.2)',
            transform: 'scale(1.05)',
          },
          transition: 'all 0.2s ease',
        },
      }}
    >
      <div className="styled-form">
        {children}
      </div>
    </Modal>
  );
};

export default StyledModal;
