import React from 'react';
import { Modal, Title, ModalProps } from '@mantine/core';

interface StyledModalProps extends Omit<ModalProps, 'title'> {
  title: string;
  children: React.ReactNode;
}

/**
 * Reusable styled modal component with white title
 * Used across the application for consistent modal styling
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
      className="styled-form"
    >
      {children}
    </Modal>
  );
};

export default StyledModal;
