import React from 'react';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import '@testing-library/jest-dom';
import { MantineProvider } from '@mantine/core';
import ContractForm from './ContractForm';
import * as contractService from '../services/contractService';
import { ContractStatus } from '../services/contractService';

// Wrap with MantineProvider
const renderWithMantine = (ui: React.ReactNode) => {
  return render(<MantineProvider>{ui}</MantineProvider>);
};

// Mock the contract service
jest.mock('../services/contractService');

// Mock contexts
jest.mock('../contexts/CurrentUserContext', () => ({
  useCurrentUser: () => ({
    currentUser: { id: 'admin-1', name: 'Admin', role: 'admin' },
    setCurrentUser: jest.fn(),
  }),
}));

jest.mock('../contexts/ContractsContext', () => ({
  useContractsContext: () => ({
    users: [],
    groups: [],
    contracts: [],
    setUsers: jest.fn(),
    setGroups: jest.fn(),
    setContracts: jest.fn(),
  }),
}));

jest.mock('../contexts/ReferenceDataContext', () => ({
  useReferenceData: () => ({
    fetchPVs: jest.fn().mockResolvedValue([]),
  }),
}));

const mockUsers = [
  {
    id: 'user-1',
    name: 'John Doe',
    email: 'john@example.com',
    role: 'user',
    isActive: true,
    activeMatriculas: [
      {
        id: 101,
        matriculaNumber: 'MAT-001',
        isOwner: true,
        status: 'active',
        startDate: '2024-01-01',
        endDate: null,
      },
    ],
  },
  {
    id: 'user-2',
    name: 'Jane Smith',
    email: 'jane@example.com',
    role: 'admin',
    isActive: true,
    activeMatriculas: [],
  },
];

const mockGroups = [
  { id: 0, name: 'Padrão', description: 'Default', commission: 0, isActive: true },
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
  status: ContractStatus.Active,
  contractStartDate: '2024-01-01T00:00:00Z',
  isActive: true,
  createdAt: '2024-01-01T00:00:00Z',
  updatedAt: '2024-01-01T00:00:00Z',
  customerName: 'Cliente Teste',
  matriculaNumber: 'MAT-001',
};

describe('ContractForm', () => {
  const mockOnClose = jest.fn();
  const mockOnSuccess = jest.fn();

  beforeAll(() => {
    window.HTMLElement.prototype.scrollIntoView = jest.fn();
  });

  beforeEach(() => {
    jest.clearAllMocks();
    (contractService.getUsers as jest.Mock).mockResolvedValue(mockUsers);
    (contractService.getGroups as jest.Mock).mockResolvedValue(mockGroups);
  });

  describe('Create Mode', () => {
    it('should render create form with all fields', async () => {
      renderWithMantine(<ContractForm onClose={mockOnClose} onSuccess={mockOnSuccess} />);

      await waitFor(() => {
        expect(screen.getByLabelText(/Número do Contrato/i)).toBeInTheDocument();
      }, { timeout: 3000 });

      expect(screen.getAllByText('Criar Contrato')[0]).toBeInTheDocument();
      expect(screen.getByRole('textbox', { name: 'Número do Contrato' })).toBeInTheDocument();
      expect(screen.getByRole('textbox', { name: 'Valor Total' })).toBeInTheDocument();
      expect(screen.getByRole('textbox', { name: 'Nome do Cliente' })).toBeInTheDocument();
      expect(screen.getByLabelText('Data de Início')).toBeInTheDocument();
    });

    it('should validate required fields', async () => {
      renderWithMantine(<ContractForm onClose={mockOnClose} onSuccess={mockOnSuccess} />);

      await waitFor(() => {
        expect(screen.getByLabelText(/Número do Contrato/i)).toBeInTheDocument();
      }, { timeout: 3000 });

      const submitButton = screen.getAllByText('Criar Contrato')[1];
      fireEvent.click(submitButton);

      await waitFor(() => {
        expect(screen.getByText('Número do contrato é obrigatório')).toBeInTheDocument();
      }, { timeout: 3000 });
    });

    it('should validate total amount minimum value', async () => {
      renderWithMantine(<ContractForm onClose={mockOnClose} onSuccess={mockOnSuccess} />);

      await waitFor(() => {
        expect(screen.getByLabelText(/Número do Contrato/i)).toBeInTheDocument();
      }, { timeout: 3000 });

      fireEvent.change(screen.getByLabelText(/Número do Contrato/i), {
        target: { value: 'C001' },
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
        expect(screen.getByText('Valor total deve ser pelo menos 0.01')).toBeInTheDocument();
      }, { timeout: 3000 });
    });

    it('should validate customer name with numbers and show direct instruction', async () => {
      renderWithMantine(<ContractForm onClose={mockOnClose} onSuccess={mockOnSuccess} />);

      await waitFor(() => {
        expect(screen.getByLabelText(/Número do Contrato/i)).toBeInTheDocument();
      }, { timeout: 3000 });

      fireEvent.change(screen.getByLabelText(/Número do Contrato/i), {
        target: { value: 'C001' },
      });
      fireEvent.change(screen.getByLabelText(/Valor Total/i), {
        target: { value: '1000' },
      });
      fireEvent.change(screen.getByLabelText(/Data de Início/i), {
        target: { value: '2024-01-01' },
      });
      fireEvent.change(screen.getByLabelText(/Nome do Cliente/i), {
        target: { value: 'Cliente 123 Silva' },
      });

      const submitButton = screen.getAllByText('Criar Contrato')[1];
      fireEvent.click(submitButton);

      await waitFor(() => {
        expect(
          screen.getByText(
            'O campo Nome do Cliente não pode conter números. É necessário remover os números do campo cliente para poder salvar.'
          )
        ).toBeInTheDocument();
      }, { timeout: 3000 });
    });

    it('should submit valid create form', async () => {
      (contractService.createContract as jest.Mock).mockResolvedValue(mockContract);

      renderWithMantine(<ContractForm onClose={mockOnClose} onSuccess={mockOnSuccess} />);

      await waitFor(() => {
        expect(screen.getAllByText('Criar Contrato')[0]).toBeInTheDocument();
      }, { timeout: 3000 });

      fireEvent.change(screen.getByLabelText(/Número do Contrato/i), {
        target: { value: 'C001' },
      });
      fireEvent.change(screen.getByLabelText(/Valor Total/i), {
        target: { value: '1000' },
      });
      fireEvent.change(screen.getByLabelText(/Data de Início/i), {
        target: { value: '2024-01-01' },
      });
      fireEvent.change(screen.getByLabelText(/Nome do Cliente/i), {
        target: { value: 'João da Silva' },
      });

      const submitButton = screen.getAllByText('Criar Contrato')[1];
      fireEvent.click(submitButton);

      await waitFor(() => {
        expect(contractService.createContract).toHaveBeenCalledWith(
          expect.objectContaining({
            contractNumber: 'C001',
            totalAmount: 1000,
            contractStartDate: '2024-01-01',
            customerName: 'João da Silva',
          })
        );
        expect(mockOnSuccess).toHaveBeenCalled();
        expect(mockOnClose).toHaveBeenCalled();
      }, { timeout: 3000 });
    });
  });

  describe('Edit Mode', () => {
    it('should render edit form with pre-filled data', async () => {
      renderWithMantine(
        <ContractForm contract={mockContract} onClose={mockOnClose} onSuccess={mockOnSuccess} />
      );

      await waitFor(() => {
        expect(screen.getByText('Editar Contrato')).toBeInTheDocument();
      }, { timeout: 3000 });

      expect(screen.getByDisplayValue('C001')).toBeInTheDocument();
      expect(screen.getByRole('textbox', { name: 'Valor Total' })).toBeInTheDocument();
      expect(screen.getByDisplayValue('Cliente Teste')).toBeInTheDocument();
    });

    it('should submit valid edit form', async () => {
      (contractService.updateContract as jest.Mock).mockResolvedValue(mockContract);

      renderWithMantine(
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
        expect(contractService.updateContract).toHaveBeenCalledWith(
          1,
          expect.objectContaining({
            totalAmount: 1500,
          })
        );
        expect(mockOnSuccess).toHaveBeenCalled();
        expect(mockOnClose).toHaveBeenCalled();
      }, { timeout: 3000 });
    });
  });

  describe('User Interactions and Errors', () => {
    it('should close form when cancel button is clicked', async () => {
      renderWithMantine(<ContractForm onClose={mockOnClose} onSuccess={mockOnSuccess} />);

      await waitFor(() => {
        expect(screen.getAllByText('Criar Contrato')[0]).toBeInTheDocument();
      }, { timeout: 3000 });

      const cancelButton = screen.getByText('Cancelar');
      fireEvent.click(cancelButton);

      expect(mockOnClose).toHaveBeenCalled();
    });

    it('should display error message on API failure', async () => {
      (contractService.createContract as jest.Mock).mockRejectedValue(
        new Error('Número de contrato já existe')
      );

      renderWithMantine(<ContractForm onClose={mockOnClose} onSuccess={mockOnSuccess} />);

      await waitFor(() => {
        expect(screen.getAllByText('Criar Contrato')[0]).toBeInTheDocument();
      }, { timeout: 3000 });

      fireEvent.change(screen.getByLabelText(/Número do Contrato/i), {
        target: { value: 'C001' },
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
        expect(screen.getByText('Número de contrato já existe')).toBeInTheDocument();
      }, { timeout: 3000 });
    });
  });
});
