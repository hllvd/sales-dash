import React from 'react';
import { Button } from '@mantine/core';

interface ExportButtonProps {
  onExport: () => void;
  isExporting: boolean;
  label?: string;
  disabled?: boolean;
}

/**
 * Dumb export button component.
 * Renders an idle "Exportar XLSX" button or a disabled "Preparando..." spinner during export.
 */
const ExportButton: React.FC<ExportButtonProps> = ({
  onExport,
  isExporting,
  label = 'Exportar XLSX',
  disabled = false,
}) => {
  return (
    <Button
      id="btn-export-xlsx"
      onClick={onExport}
      disabled={isExporting || disabled}
      variant="outline"
      color="green"
      size="sm"
      leftSection={
        isExporting ? (
          <span
            style={{
              display: 'inline-block',
              width: 14,
              height: 14,
              border: '2px solid currentColor',
              borderTopColor: 'transparent',
              borderRadius: '50%',
              animation: 'spin 0.7s linear infinite',
            }}
          />
        ) : (
          <span style={{ fontSize: 14 }}>⬇</span>
        )
      }
      styles={{
        root: {
          transition: 'opacity 0.2s',
          opacity: isExporting ? 0.7 : 1,
        },
      }}
    >
      {isExporting ? 'Preparando...' : label}
      <style>{`@keyframes spin { to { transform: rotate(360deg); } }`}</style>
    </Button>
  );
};

export default ExportButton;
