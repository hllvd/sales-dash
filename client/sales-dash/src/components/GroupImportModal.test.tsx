import React from 'react';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import '@testing-library/jest-dom';
import { MantineProvider } from '@mantine/core';
import GroupImportModal from './GroupImportModal';
import * as contractService from '../services/contractService';

jest.mock('../services/contractService');

describe('GroupImportModal', () => {
  const mockOnClose = jest.fn();
  const mockOnSuccess = jest.fn();

  beforeEach(() => {
    jest.clearAllMocks();
  });

  it('renders modal with title, instructions and download template button', () => {
    render(
      <MantineProvider>
        <GroupImportModal
          opened={true}
          onClose={mockOnClose}
          onSuccess={mockOnSuccess}
        />
      </MantineProvider>
    );

    expect(screen.getByText('Importar Grupos via Excel (.xlsx)')).toBeInTheDocument();
    expect(screen.getByText('Baixar Modelo (.xlsx)')).toBeInTheDocument();
    expect(screen.getByText('Importar')).toBeInTheDocument();
    expect(screen.getByText('Cancelar')).toBeInTheDocument();
  });

  it('calls downloadGroupImportTemplate when template button is clicked', async () => {
    const mockBlob = new Blob(['dummy content'], { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' });
    (contractService.downloadGroupImportTemplate as jest.Mock).mockResolvedValue(mockBlob);

    // Mock URL methods
    window.URL.createObjectURL = jest.fn(() => 'blob:mock-url');
    window.URL.revokeObjectURL = jest.fn();
    const clickSpy = jest.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {});

    render(
      <MantineProvider>
        <GroupImportModal
          opened={true}
          onClose={mockOnClose}
          onSuccess={mockOnSuccess}
        />
      </MantineProvider>
    );

    fireEvent.click(screen.getByText('Baixar Modelo (.xlsx)'));

    await waitFor(() => {
      expect(contractService.downloadGroupImportTemplate).toHaveBeenCalled();
    });
    clickSpy.mockRestore();
  });

  it('handles successful file import and displays result badges', async () => {
    (contractService.importGroups as jest.Mock).mockResolvedValue({
      totalRows: 3,
      createdCount: 2,
      updatedCount: 1,
      errorCount: 0,
      errors: [],
    });

    render(
      <MantineProvider>
        <GroupImportModal
          opened={true}
          onClose={mockOnClose}
          onSuccess={mockOnSuccess}
        />
      </MantineProvider>
    );

    const file = new File(['dummy xlsx content'], 'grupos.xlsx', {
      type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
    });

    const fileInput = document.querySelector('input[type="file"]') as HTMLInputElement;
    fireEvent.change(fileInput, { target: { files: [file] } });

    fireEvent.click(screen.getByRole('button', { name: /importar/i }));

    await waitFor(() => {
      expect(contractService.importGroups).toHaveBeenCalledWith(file);
      expect(mockOnSuccess).toHaveBeenCalled();
      expect(screen.getByText('2 criados')).toBeInTheDocument();
      expect(screen.getByText('1 atualizados')).toBeInTheDocument();
      expect(screen.getByText('Importação Concluída')).toBeInTheDocument();
    });
  });

  it('displays errors list when import response contains errors', async () => {
    (contractService.importGroups as jest.Mock).mockResolvedValue({
      totalRows: 2,
      createdCount: 1,
      updatedCount: 0,
      errorCount: 1,
      errors: [
        {
          rowNumber: 3,
          groupName: '1099',
          error: "Segmento 'X' inválido",
        },
      ],
    });

    render(
      <MantineProvider>
        <GroupImportModal
          opened={true}
          onClose={mockOnClose}
          onSuccess={mockOnSuccess}
        />
      </MantineProvider>
    );

    const file = new File(['dummy xlsx content'], 'grupos.xlsx', {
      type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
    });

    const fileInput = document.querySelector('input[type="file"]') as HTMLInputElement;
    fireEvent.change(fileInput, { target: { files: [file] } });

    fireEvent.click(screen.getByRole('button', { name: /importar/i }));

    await waitFor(() => {
      expect(screen.getByText('1 erros')).toBeInTheDocument();
      expect(screen.getByText(/Linha 3 \(Grupo "1099"\): Segmento 'X' inválido/i)).toBeInTheDocument();
    });
  });
});
