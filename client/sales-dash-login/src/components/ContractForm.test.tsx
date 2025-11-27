import React from 'react';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import '@testing-library/jest-dom';
import ContractForm from './ContractForm';
import * as contractService from '../services/contractService';

// Mock the contract service
jest.mock('../services/contractService');

const mockUsers = [
  { id: 'user-1', name: 'John Doe', email: 'john@example.com', role: 'user', isActive: true },
  { id: 'user-2', name: 'Jane Smith', email: 'jane@example.com', role: 'admin', isActive: true },
];

const mockGroups = [
  { id: 1, name: 'Group A', description: 'Test A', commission: 10, isActive: true },
  { id: 2, name: 'Group B', description: 'Test B', commission: 15, isActive: true },
];

const mockContract = {
  id: 1,
  contractNumber: 'C001',
  userId: 'user-1',
  userName: 'John Doe',
  totalAmount: 1000,
  groupId: 1,
  groupName: 'Group A',
  status: 'active' as const,
  contractStartDate: '2024-01-01T00:00:00Z',
  contractEndDate: null,
  isActive: true,
  createdAt: '2024-01-01T00:00:00Z',
  updatedAt: '2024-01-01T00:00:00Z',
};

describe('ContractForm', () => {
  const mockOnClose = jest.fn();
  const mockOnSuccess = jest.fn();

  beforeEach(() => {
    jest.clearAllMocks();
    (contractService.getUsers as jest.Mock).mockResolvedValue(mockUsers);
    (contractService.getGroups as jest.Mock).mockResolvedValue(mockGroups);
  });

  describe('Create Mode', () => {
    it('should render create form with all fields', async () => {
      render(<ContractForm onClose={mockOnClose} onSuccess={mockOnSuccess} />);

      await waitFor(() => {
        expect(screen.getByLabelText(/Número do Contrato/i)).toBeInTheDocument();
      }, { timeout: 3000 });

      expect(screen.getAllByText('Criar Contrato')[0]).toBeInTheDocument();
      expect(screen.getByLabelText(/Usuário/i)).toBeInTheDocument();
      expect(screen.getByLabelText(/Grupo/i)).toBeInTheDocument();
      expect(screen.getByLabelText(/Valor Total/i)).toBeInTheDocument();
      expect(screen.getByLabelText(/Status/i)).toBeInTheDocument();
      expect(screen.getByLabelText(/Data de Início/i)).toBeInTheDocument();
      expect(screen.getByLabelText(/Data de Término/i)).toBeInTheDocument();
    });

    it('should populate user and group dropdowns', async () => {
      render(<ContractForm onClose={mockOnClose} onSuccess={mockOnSuccess} />);

      await waitFor(() => {
        expect(contractService.getUsers).toHaveBeenCalled();
        expect(contractService.getGroups).toHaveBeenCalled();
      }, { timeout: 3000 });

      const userSelect = screen.getByLabelText(/Usuário/i) as HTMLSelectElement;
      expect(userSelect.options).toHaveLength(3); // "Selecione" + 2 users

      const groupSelect = screen.getByLabelText(/Grupo/i) as HTMLSelectElement;
      expect(groupSelect.options).toHaveLength(3); // "Selecione" + 2 groups
    });

    it('should validate required fields', async () => {
      render(<ContractForm onClose={mockOnClose} onSuccess={mockOnSuccess} />);

      await waitFor(() => {
        expect(screen.getByLabelText(/Número do Contrato/i)).toBeInTheDocument();
      }, { timeout: 3000 });

      const submitButton = screen.getAllByText('Criar Contrato')[1];
      fireEvent.click(submitButton);

      await waitFor(() => {
        expect(screen.getByText(/Contract number is required/i)).toBeInTheDocument();
      }, { timeout: 3000 });
    });

    it('should validate total amount minimum value', async () => {
      render(<ContractForm onClose={mockOnClose} onSuccess={mockOnSuccess} />);

      await waitFor(() => {
        expect(screen.getByLabelText(/Número do Contrato/i)).toBeInTheDocument();
      }, { timeout: 3000 });

      fireEvent.change(screen.getByLabelText(/Número do Contrato/i), {
        target: { value: 'C001' },
      });
      fireEvent.change(screen.getByLabelText(/Usuário/i), {
        target: { value: 'user-1' },
      });
      fireEvent.change(screen.getByLabelText(/Grupo/i), {
        target: { value: '1' },
      });
      fireEvent.change(screen.getByLabelText(/Valor Total/i), {
        target: { value: '0' },
      });
      fireEvent.change(screen.getByLabelText(/Data de Início/i), {
        target: { value: '2024-01-01' },
      });

      const submitButton = screen.getAllByText('Criar Contrato')[1];
      fireEvent.click(submitButton);

      await waitFor(() => {
        expect(screen.getByText(/Total amount must be at least 0.01/i)).toBeInTheDocument();
      }, { timeout: 3000 });
    });

    it('should submit valid create form', async () => {
      (contractService.createContract as jest.Mock).mockResolvedValue(mockContract);

      render(<ContractForm onClose={mockOnClose} onSuccess={mockOnSuccess} />);

      await waitFor(() => {
        expect(screen.getAllByText('Criar Contrato')[0]).toBeInTheDocument();
      }, { timeout: 3000 });

      fireEvent.change(screen.getByLabelText(/Número do Contrato/i), {
        target: { value: 'C001' },
      });
      fireEvent.change(screen.getByLabelText(/Usuário/i), {
        target: { value: 'user-1' },
      });
      fireEvent.change(screen.getByLabelText(/Grupo/i), {
        target: { value: '1' },
      });
      fireEvent.change(screen.getByLabelText(/Valor Total/i), {
        target: { value: '1000' },
      });
      fireEvent.change(screen.getByLabelText(/Data de Início/i), {
        target: { value: '2024-01-01' },
      });

      const submitButton = screen.getAllByText('Criar Contrato')[1];
      fireEvent.click(submitButton);

      await waitFor(() => {
        expect(contractService.createContract).toHaveBeenCalledWith({
          contractNumber: 'C001',
          userId: 'user-1',
          groupId: 1,
          totalAmount: 1000,
          status: 'active',
          contractStartDate: '2024-01-01',
          contractEndDate: null,
        });
        expect(mockOnSuccess).toHaveBeenCalled();
        expect(mockOnClose).toHaveBeenCalled();
      }, { timeout: 3000 });
    });
  });

  describe('Edit Mode', () => {
    it('should render edit form with pre-filled data', async () => {
      render(
        <ContractForm contract={mockContract} onClose={mockOnClose} onSuccess={mockOnSuccess} />
      );

      await waitFor(() => {
        expect(screen.getByText('Editar Contrato')).toBeInTheDocument();
      }, { timeout: 3000 });

      expect(screen.getByDisplayValue('C001')).toBeInTheDocument();
      expect(screen.getByDisplayValue('1000')).toBeInTheDocument();
      expect(screen.getByLabelText(/Contrato Ativo/i)).toBeInTheDocument();
    });

    it('should submit valid edit form', async () => {
      (contractService.updateContract as jest.Mock).mockResolvedValue(mockContract);

      render(
        <ContractForm contract={mockContract} onClose={mockOnClose} onSuccess={mockOnSuccess} />
      );

      await waitFor(() => {
        expect(screen.getByText('Editar Contrato')).toBeInTheDocument();
      }, { timeout: 3000 });

      fireEvent.change(screen.getByLabelText(/Valor Total/i), {
        target: { value: '1500' },
      });

      const submitButton = screen.getByText('Salvar Alterações');
      fireEvent.click(submitButton);

      await waitFor(() => {
        expect(contractService.updateContract).toHaveBeenCalledWith(1, expect.objectContaining({
          totalAmount: 1500,
        }));
        expect(mockOnSuccess).toHaveBeenCalled();
        expect(mockOnClose).toHaveBeenCalled();
      }, { timeout: 3000 });
    });
  });

  describe('User Interactions', () => {
    it('should close form when cancel button is clicked', async () => {
      render(<ContractForm onClose={mockOnClose} onSuccess={mockOnSuccess} />);

      await waitFor(() => {
        expect(screen.getAllByText('Criar Contrato')[0]).toBeInTheDocument();
      }, { timeout: 3000 });

      const cancelButton = screen.getByText('Cancelar');
      fireEvent.click(cancelButton);

      expect(mockOnClose).toHaveBeenCalled();
    });

    it('should close form when X button is clicked', async () => {
      render(<ContractForm onClose={mockOnClose} onSuccess={mockOnSuccess} />);

      await waitFor(() => {
        expect(screen.getAllByText('Criar Contrato')[0]).toBeInTheDocument();
      }, { timeout: 3000 });

      const closeButton = screen.getByText('✕');
      fireEvent.click(closeButton);

      expect(mockOnClose).toHaveBeenCalled();
    });

    it('should display error message on API failure', async () => {
      (contractService.createContract as jest.Mock).mockRejectedValue(
        new Error('Contract number already exists')
      );

      render(<ContractForm onClose={mockOnClose} onSuccess={mockOnSuccess} />);

      await waitFor(() => {
        expect(screen.getAllByText('Criar Contrato')[0]).toBeInTheDocument();
      }, { timeout: 3000 });

      fireEvent.change(screen.getByLabelText(/Número do Contrato/i), {
        target: { value: 'C001' },
      });
      fireEvent.change(screen.getByLabelText(/Usuário/i), {
        target: { value: 'user-1' },
      });
      fireEvent.change(screen.getByLabelText(/Grupo/i), {
        target: { value: '1' },
      });
      fireEvent.change(screen.getByLabelText(/Valor Total/i), {
        target: { value: '1000' },
      });
      fireEvent.change(screen.getByLabelText(/Data de Início/i), {
        target: { value: '2024-01-01' },
      });

      const submitButton = screen.getAllByText('Criar Contrato')[1];
      fireEvent.click(submitButton);

      await waitFor(() => {
        expect(screen.getByText(/Contract number already exists/i)).toBeInTheDocument();
      }, { timeout: 3000 });
    });
  });
});
