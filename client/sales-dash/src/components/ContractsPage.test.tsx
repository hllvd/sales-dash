import React from 'react';
import { render as rtlRender, screen, fireEvent, waitFor } from '@testing-library/react';
import '@testing-library/jest-dom';
import { MantineProvider } from '@mantine/core';
import ContractsPage from './ContractsPage';
import * as contractService from '../services/contractService';
import { apiService } from '../services/apiService';

const render = (ui: React.ReactElement) => {
  return rtlRender(<MantineProvider>{ui}</MantineProvider>);
};

// Mock the contract service
jest.mock('../services/contractService');

// Mock apiService
jest.mock('../services/apiService', () => ({
  apiService: {
    getUserPreferences: jest.fn(),
    updateUserPreferences: jest.fn(),
    startContractExport: jest.fn(),
    contractExportPollUrl: jest.fn(),
    contractExportDownloadUrl: jest.fn(),
  },
}));

// Mock contexts
jest.mock('../contexts/CurrentUserContext', () => ({
  useCurrentUser: () => ({
    currentUser: { id: 'admin-1', name: 'Admin', role: 'admin' },
    setCurrentUser: jest.fn(),
  }),
}));

const mockSetUsers = jest.fn();
const mockSetGroups = jest.fn();
const mockSetContracts = jest.fn();

jest.mock('../contexts/ContractsContext', () => ({
  useContractsContext: () => ({
    users: [],
    groups: [],
    contracts: [],
    setUsers: mockSetUsers,
    setGroups: mockSetGroups,
    setContracts: mockSetContracts,
  }),
}));

const mockUsers = [
  { id: 'user-1', name: 'John Doe', email: 'john@example.com', role: 'user', isActive: true },
  { id: 'user-2', name: 'Jane Smith', email: 'jane@example.com', role: 'admin', isActive: true },
];

const mockFetchTeams = jest.fn();
const mockFetchContractFilterUsers = jest.fn();
const mockFetchPVs = jest.fn();

jest.mock('../contexts/ReferenceDataContext', () => ({
  useReferenceData: () => ({
    fetchTeams: mockFetchTeams,
    fetchContractFilterUsers: mockFetchContractFilterUsers,
    fetchPVs: mockFetchPVs,
  }),
}));
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
jest.mock('./Menu', () => ({ children }: any) => <div data-testid="menu">{children}</div>);
// Mock HistoricProduction component
jest.mock('../shared/HistoricProduction', () => () => <div data-testid="historic-production" />);
// Mock BulkImportModal component
jest.mock('./BulkImportModal', () => {
  return function MockBulkImportModal({ onClose, onSuccess, templateId, title }: any) {
    return (
      <div data-testid="bulk-import-modal">
        <div data-testid="modal-title">{title}</div>
        <div data-testid="template-id">{templateId}</div>
        <button onClick={onSuccess}>Mock Import Success</button>
        <button onClick={onClose}>Mock Import Close</button>
      </div>
    );
  };
});

const mockContracts = [
  {
    id: 1,
    contractNumber: 'C001',
    userId: 'user-1',
    userName: 'John Doe',
    totalAmount: 1000,
    groupId: 1,
    groupName: 'Group A',
    segment: 1,
    segmentName: 'Lar',
    customerName: 'Maria Santos',
    status: contractService.ContractStatus.Active,
    contractStartDate: '2024-01-01T00:00:00Z',
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
    segment: 2,
    segmentName: 'Motores',
    customerName: 'Carlos Oliveira',
    status: contractService.ContractStatus.Delinquent,
    contractStartDate: '2024-02-01T00:00:00Z',
    isActive: true,
    createdAt: '2024-02-01T00:00:00Z',
    updatedAt: '2024-02-01T00:00:00Z',
  },
];

const mockGroups = [
  { id: 0, name: 'Padrão', description: 'Default', commission: 0, isActive: true },
  { id: 1, name: 'Group A', description: 'Test A', commission: 10, segment: 1, isActive: true },
  { id: 2, name: 'Group B', description: 'Test B', commission: 15, segment: 2, isActive: true },
];

describe('ContractsPage', () => {
  beforeEach(() => {
    localStorage.clear();
    jest.clearAllMocks();
    mockFetchTeams.mockResolvedValue([]);
    mockFetchContractFilterUsers.mockResolvedValue(mockUsers);
    mockFetchPVs.mockResolvedValue([]);
    (apiService.getUserPreferences as jest.Mock).mockResolvedValue({
      success: true,
      data: {
        treatUnpaidActiveAsAwaitingPayment: true,
        includeInactiveUsersInFilter: true,
      },
    });
    (apiService.updateUserPreferences as jest.Mock).mockResolvedValue({ success: true });
    (contractService.getContracts as jest.Mock).mockResolvedValue({
      contracts: mockContracts,
      totalCount: mockContracts.length,
    });
    (contractService.getUsers as jest.Mock).mockResolvedValue(mockUsers);
    (contractService.getGroups as jest.Mock).mockResolvedValue(mockGroups);
  });

  describe('Rendering', () => {
    it('should render contracts page with header', async () => {
      render(<ContractsPage />);

      expect(screen.getByText('Gerenciamento de Contratos')).toBeInTheDocument();
      expect(screen.getByText('Criar')).toBeInTheDocument();
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
      (contractService.getContracts as jest.Mock).mockResolvedValue({
        contracts: [],
        totalCount: 0,
      });

      render(<ContractsPage />);

      await waitFor(() => {
        expect(screen.getByText('Nenhum contrato encontrado.')).toBeInTheDocument();
        expect(screen.getByText('Criar Primeiro Contrato')).toBeInTheDocument();
      }, { timeout: 3000 });
    });
  });

  describe('Status Badges', () => {
    it('should display correct status badges', async () => {
      render(<ContractsPage />);

      await waitFor(() => {
        expect(screen.getByText('Ativo')).toBeInTheDocument();
        expect(screen.getByText('Inadimplente')).toBeInTheDocument();
      }, { timeout: 3000 });
    });
  });

  describe('Filtering', () => {
    it('should render filter controls including Segmento and Cliente', async () => {
      render(<ContractsPage />);

      await waitFor(() => {
        expect(screen.getByLabelText('Usuários')).toBeInTheDocument();
        expect(screen.getByLabelText('Equipe')).toBeInTheDocument();
        expect(screen.getByLabelText('Segmento')).toBeInTheDocument();
        expect(screen.getByLabelText('Cliente')).toBeInTheDocument();
        expect(screen.getByLabelText('Data Início')).toBeInTheDocument();
        expect(screen.getByLabelText('Data Fim')).toBeInTheDocument();
      }, { timeout: 3000 });
    });

    it('should filter by customerName after debounce delay when typing in Cliente input', async () => {
      render(<ContractsPage />);

      await waitFor(() => {
        expect(screen.getByLabelText('Cliente')).toBeInTheDocument();
      }, { timeout: 3000 });

      const customerInput = screen.getByLabelText('Cliente') as HTMLInputElement;
      fireEvent.change(customerInput, { target: { value: 'Maria' } });

      await waitFor(() => {
        const lastCall = (contractService.getContracts as jest.Mock).mock.calls.slice(-1)[0];
        expect(lastCall[16]).toBe('Maria');
      }, { timeout: 3000 });
    });

    it('should filter by segment when a segment option is selected', async () => {
      render(<ContractsPage />);

      await waitFor(() => {
        expect(screen.getByLabelText('Segmento')).toBeInTheDocument();
      }, { timeout: 3000 });

      const segmentInput = screen.getByLabelText('Segmento');
      fireEvent.click(segmentInput);

      const larOption = await screen.findByRole('option', { name: 'Lar', hidden: true });
      fireEvent.click(larOption);

      await waitFor(() => {
        const lastCall = (contractService.getContracts as jest.Mock).mock.calls.slice(-1)[0];
        expect(lastCall[15]).toEqual([1]);
      }, { timeout: 3000 });
    });

    it('should show clear filters button when filters are active and reset all filters on click', async () => {
      render(<ContractsPage />);

      await waitFor(() => {
        expect(screen.getByLabelText('Cliente')).toBeInTheDocument();
      }, { timeout: 3000 });

      const customerInput = screen.getByLabelText('Cliente') as HTMLInputElement;
      fireEvent.change(customerInput, { target: { value: 'Carlos' } });

      await waitFor(() => {
        expect(screen.getByText('Limpar Filtros')).toBeInTheDocument();
      }, { timeout: 3000 });

      const clearButton = screen.getByText('Limpar Filtros');
      fireEvent.click(clearButton);

      expect(customerInput.value).toBe('');
    });
  });

  describe('CRUD Actions', () => {
    it('should open create form when create button is clicked', async () => {
      render(<ContractsPage />);

      await waitFor(() => {
        expect(screen.getByText('Criar')).toBeInTheDocument();
      }, { timeout: 3000 });

      const createButton = screen.getAllByText('Criar')[0];
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

      await waitFor(() => {
        expect(screen.getByText('Confirmar Exclusão')).toBeInTheDocument();
        expect(screen.getByText('Tem certeza que deseja excluir este contrato?')).toBeInTheDocument();
      }, { timeout: 3000 });
    });

    it('should delete contract when confirmed', async () => {
      (contractService.deleteContract as jest.Mock).mockResolvedValue(undefined);

      render(<ContractsPage />);

      await waitFor(() => {
        expect(screen.getByText('C001')).toBeInTheDocument();
      }, { timeout: 3000 });

      const deleteButtons = screen.getAllByTitle('Excluir');
      fireEvent.click(deleteButtons[0]);

      const confirmButton = await screen.findByText('Excluir', { selector: 'button .mantine-Button-label' });
      fireEvent.click(confirmButton);

      await waitFor(() => {
        expect(contractService.deleteContract).toHaveBeenCalledWith(1);
      }, { timeout: 3000 });
    });

    it('should cancel delete when cancel button is clicked', async () => {
      render(<ContractsPage />);

      await waitFor(() => {
        expect(screen.getByText('C001')).toBeInTheDocument();
      }, { timeout: 3000 });

      const deleteButtons = screen.getAllByTitle('Excluir');
      fireEvent.click(deleteButtons[0]);

      const cancelButton = await screen.findByText('Cancelar');
      fireEvent.click(cancelButton);

      await waitFor(() => {
        expect(screen.queryByText('Confirmar Exclusão')).not.toBeInTheDocument();
      }, { timeout: 3000 });
    });

    it('should reload contracts after successful form submission', async () => {
      render(<ContractsPage />);

      await waitFor(() => {
        expect(screen.getByText('Criar')).toBeInTheDocument();
      }, { timeout: 3000 });

      const createButton = screen.getAllByText('Criar')[0];
      fireEvent.click(createButton);

      const initialCalls = (contractService.getContracts as jest.Mock).mock.calls.length;
      const mockSubmitButton = screen.getByText('Mock Submit');
      fireEvent.click(mockSubmitButton);

      await waitFor(() => {
        expect((contractService.getContracts as jest.Mock).mock.calls.length).toBeGreaterThan(initialCalls);
      }, { timeout: 3000 });
    });
  });

  describe('Import Functionality', () => {
    it('should open import modal when import button is clicked', async () => {
      render(<ContractsPage />);

      await waitFor(() => {
        expect(screen.getByText('Importar')).toBeInTheDocument();
      }, { timeout: 3000 });

      const importButton = screen.getByText('Importar');
      fireEvent.click(importButton);

      expect(screen.getByTestId('bulk-import-modal')).toBeInTheDocument();
      expect(screen.getByTestId('modal-title')).toHaveTextContent('Importar Contratos em Lote');
      expect(screen.getByTestId('template-id')).toHaveTextContent('3');
    });

    it('should reload contracts after successful import', async () => {
      render(<ContractsPage />);

      await waitFor(() => {
        expect(screen.getByText('Importar')).toBeInTheDocument();
      }, { timeout: 3000 });

      const importButton = screen.getByText('Importar');
      fireEvent.click(importButton);

      const initialCalls = (contractService.getContracts as jest.Mock).mock.calls.length;
      const successButton = screen.getByText('Mock Import Success');
      fireEvent.click(successButton);

      await waitFor(() => {
        expect((contractService.getContracts as jest.Mock).mock.calls.length).toBeGreaterThan(initialCalls);
      }, { timeout: 3000 });
    });

    it('should close modal when close button is clicked', async () => {
      render(<ContractsPage />);

      await waitFor(() => {
        expect(screen.getByText('Importar')).toBeInTheDocument();
      }, { timeout: 3000 });

      const importButton = screen.getByText('Importar');
      fireEvent.click(importButton);

      const closeButton = screen.getByText('Mock Import Close');
      fireEvent.click(closeButton);

      await waitFor(() => {
        expect(screen.queryByTestId('bulk-import-modal')).not.toBeInTheDocument();
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
        const date1 = screen.queryByText('01/01/2024') || screen.queryByText('31/12/2023');
        expect(date1).toBeInTheDocument();

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
