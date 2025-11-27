import React from 'react';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import '@testing-library/jest-dom';
import ContractsPage from './ContractsPage';
import * as contractService from '../services/contractService';

// Mock the contract service
jest.mock('../services/contractService');
// Mock ContractForm component
jest.mock('./ContractForm', () => {
  return function MockContractForm({ onClose, onSuccess }: any) {
    return (
      <div data-testid="contract-form">
        <button onClick={onSuccess}>Mock Submit</button>
        <button onClick={onClose}>Mock Close</button>
      </div>
    );
  };
});
// Mock Menu component
jest.mock('./Menu', () => () => <div data-testid="menu">Menu</div>);

const mockContracts = [
  {
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
  },
  {
    id: 2,
    contractNumber: 'C002',
    userId: 'user-2',
    userName: 'Jane Smith',
    totalAmount: 2000,
    groupId: 2,
    groupName: 'Group B',
    status: 'delinquent' as const,
    contractStartDate: '2024-02-01T00:00:00Z',
    contractEndDate: '2024-12-31T00:00:00Z',
    isActive: true,
    createdAt: '2024-02-01T00:00:00Z',
    updatedAt: '2024-02-01T00:00:00Z',
  },
];

const mockUsers = [
  { id: 'user-1', name: 'John Doe', email: 'john@example.com', role: 'user', isActive: true },
  { id: 'user-2', name: 'Jane Smith', email: 'jane@example.com', role: 'admin', isActive: true },
];

const mockGroups = [
  { id: 1, name: 'Group A', description: 'Test A', commission: 10, isActive: true },
  { id: 2, name: 'Group B', description: 'Test B', commission: 15, isActive: true },
];

describe('ContractsPage', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    (contractService.getContracts as jest.Mock).mockResolvedValue(mockContracts);
    (contractService.getUsers as jest.Mock).mockResolvedValue(mockUsers);
    (contractService.getGroups as jest.Mock).mockResolvedValue(mockGroups);
  });

  describe('Rendering', () => {
    it('should render contracts page with header', async () => {
      render(<ContractsPage />);

      expect(screen.getByText('Gerenciamento de Contratos')).toBeInTheDocument();
      expect(screen.getByText('+ Criar Contrato')).toBeInTheDocument();
    });

    it('should display loading state initially', () => {
      render(<ContractsPage />);

      expect(screen.getByText('Carregando contratos...')).toBeInTheDocument();
    });

    it('should display contracts list after loading', async () => {
      render(<ContractsPage />);

      await waitFor(() => {
        expect(screen.getByText('C001')).toBeInTheDocument();
        expect(screen.getByText('C002')).toBeInTheDocument();
        expect(screen.getAllByText('John Doe')[0]).toBeInTheDocument();
        expect(screen.getAllByText('Jane Smith')[0]).toBeInTheDocument();
      }, { timeout: 3000 });
    });

    it('should display empty state when no contracts', async () => {
      (contractService.getContracts as jest.Mock).mockResolvedValue([]);

      render(<ContractsPage />);

      await waitFor(() => {
        expect(screen.getByText('Nenhum contrato encontrado.')).toBeInTheDocument();
        expect(screen.getByText('Criar Primeiro Contrato')).toBeInTheDocument();
      }, { timeout: 3000 });
    });
  });

  describe('Status Badges', () => {
    it('should display correct status badges with colors', async () => {
      render(<ContractsPage />);

      await waitFor(() => {
        const activeBadge = screen.getByText('Ativo');
        expect(activeBadge).toHaveClass('status-active');

        const delinquentBadge = screen.getByText('Inadimplente');
        expect(delinquentBadge).toHaveClass('status-delinquent');
      }, { timeout: 3000 });
    });
  });

  describe('Filtering', () => {
    it('should render filter controls', async () => {
      render(<ContractsPage />);

      await waitFor(() => {
        expect(screen.getByLabelText('Usuário')).toBeInTheDocument();
        expect(screen.getByLabelText('Grupo')).toBeInTheDocument();
        expect(screen.getByLabelText('Data Início')).toBeInTheDocument();
        expect(screen.getByLabelText('Data Fim')).toBeInTheDocument();
      }, { timeout: 3000 });
    });

    it('should call getContracts with filters when filter changes', async () => {
      render(<ContractsPage />);

      await waitFor(() => {
        expect(screen.getByLabelText('Usuário')).toBeInTheDocument();
      }, { timeout: 3000 });

      const userFilter = screen.getByLabelText('Usuário');
      fireEvent.change(userFilter, { target: { value: 'user-1' } });

      await waitFor(() => {
        expect(contractService.getContracts).toHaveBeenCalledWith(
          'user-1',
          undefined,
          undefined,
          undefined
        );
      }, { timeout: 3000 });
    });

    it('should show clear filters button when filters are active', async () => {
      render(<ContractsPage />);

      await waitFor(() => {
        expect(screen.getByLabelText('Usuário')).toBeInTheDocument();
      }, { timeout: 3000 });

      const userFilter = screen.getByLabelText('Usuário');
      fireEvent.change(userFilter, { target: { value: 'user-1' } });

      await waitFor(() => {
        expect(screen.getByText('Limpar Filtros')).toBeInTheDocument();
      }, { timeout: 3000 });
    });

    it('should clear all filters when clear button is clicked', async () => {
      render(<ContractsPage />);

      await waitFor(() => {
        expect(screen.getByLabelText('Usuário')).toBeInTheDocument();
      }, { timeout: 3000 });

      const userFilter = screen.getByLabelText('Usuário') as HTMLSelectElement;
      fireEvent.change(userFilter, { target: { value: 'user-1' } });

      await waitFor(() => {
        expect(screen.getByText('Limpar Filtros')).toBeInTheDocument();
      }, { timeout: 3000 });

      const clearButton = screen.getByText('Limpar Filtros');
      fireEvent.click(clearButton);

      expect(userFilter.value).toBe('');
    });
  });

  describe('CRUD Actions', () => {
    it('should open create form when create button is clicked', async () => {
      render(<ContractsPage />);

      await waitFor(() => {
        expect(screen.getByText('+ Criar Contrato')).toBeInTheDocument();
      }, { timeout: 3000 });

      const createButton = screen.getAllByText('+ Criar Contrato')[0];
      fireEvent.click(createButton);

      expect(screen.getByTestId('contract-form')).toBeInTheDocument();
    });

    it('should open edit form when edit button is clicked', async () => {
      render(<ContractsPage />);

      await waitFor(() => {
        expect(screen.getByText('C001')).toBeInTheDocument();
      }, { timeout: 3000 });

      const editButtons = screen.getAllByTitle('Editar');
      fireEvent.click(editButtons[0]);

      expect(screen.getByTestId('contract-form')).toBeInTheDocument();
    });

    it('should show delete confirmation when delete button is clicked', async () => {
      render(<ContractsPage />);

      await waitFor(() => {
        expect(screen.getByText('C001')).toBeInTheDocument();
      }, { timeout: 3000 });

      const deleteButtons = screen.getAllByTitle('Excluir');
      fireEvent.click(deleteButtons[0]);

      expect(screen.getByText('Confirmar Exclusão')).toBeInTheDocument();
      expect(screen.getByText('Tem certeza que deseja excluir este contrato?')).toBeInTheDocument();
    });

    it('should delete contract when confirmed', async () => {
      (contractService.deleteContract as jest.Mock).mockResolvedValue(undefined);

      render(<ContractsPage />);

      await waitFor(() => {
        expect(screen.getByText('C001')).toBeInTheDocument();
      }, { timeout: 3000 });

      const deleteButtons = screen.getAllByTitle('Excluir');
      fireEvent.click(deleteButtons[0]);

      const confirmButton = screen.getByText('Excluir');
      fireEvent.click(confirmButton);

      await waitFor(() => {
        expect(contractService.deleteContract).toHaveBeenCalledWith(1);
        expect(contractService.getContracts).toHaveBeenCalledTimes(2); // Initial + after delete
      }, { timeout: 3000 });
    });

    it('should cancel delete when cancel button is clicked', async () => {
      render(<ContractsPage />);

      await waitFor(() => {
        expect(screen.getByText('C001')).toBeInTheDocument();
      }, { timeout: 3000 });

      const deleteButtons = screen.getAllByTitle('Excluir');
      fireEvent.click(deleteButtons[0]);

      const cancelButton = screen.getByText('Cancelar');
      fireEvent.click(cancelButton);

      await waitFor(() => {
        expect(screen.queryByText('Confirmar Exclusão')).not.toBeInTheDocument();
      }, { timeout: 3000 });
    });

    it('should reload contracts after successful form submission', async () => {
      render(<ContractsPage />);

      await waitFor(() => {
        expect(screen.getByText('+ Criar Contrato')).toBeInTheDocument();
      }, { timeout: 3000 });

      const createButton = screen.getAllByText('+ Criar Contrato')[0];
      fireEvent.click(createButton);

      const mockSubmitButton = screen.getByText('Mock Submit');
      fireEvent.click(mockSubmitButton);

      await waitFor(() => {
        expect(contractService.getContracts).toHaveBeenCalledTimes(2); // Initial + after submit
      }, { timeout: 3000 });
    });
  });

  describe('Currency and Date Formatting', () => {
    it('should format currency values correctly', async () => {
      render(<ContractsPage />);

      await waitFor(() => {
        expect(screen.getByText(/R\$\s*1\.000,00/)).toBeInTheDocument();
        expect(screen.getByText(/R\$\s*2\.000,00/)).toBeInTheDocument();
      }, { timeout: 3000 });
    });

    it('should format dates correctly', async () => {
      render(<ContractsPage />);

      await waitFor(() => {
        // Handle potential timezone differences (UTC vs Local)
        // 2024-01-01T00:00:00Z -> 01/01/2024 or 31/12/2023
        const date1 = screen.queryByText('01/01/2024') || screen.queryByText('31/12/2023');
        expect(date1).toBeInTheDocument();

        // 2024-02-01T00:00:00Z -> 01/02/2024 or 31/01/2024
        const date2 = screen.queryByText('01/02/2024') || screen.queryByText('31/01/2024');
        expect(date2).toBeInTheDocument();
      }, { timeout: 3000 });
    });
  });

  describe('Error Handling', () => {
    it('should display error message when loading fails', async () => {
      (contractService.getContracts as jest.Mock).mockRejectedValue(
        new Error('Failed to load contracts')
      );

      render(<ContractsPage />);

      await waitFor(() => {
        expect(screen.getByText(/Failed to load contracts/i)).toBeInTheDocument();
      }, { timeout: 3000 });
    });
  });
});
