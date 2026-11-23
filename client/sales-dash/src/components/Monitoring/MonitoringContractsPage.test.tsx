import React from 'react';
import { render as rtlRender, screen, fireEvent, waitFor } from '@testing-library/react';
import '@testing-library/jest-dom';
import { MantineProvider } from '@mantine/core';
import MonitoringContractsPage from './MonitoringContractsPage';
import * as contractService from '../../services/contractService';

const render = (ui: React.ReactElement) => {
  return rtlRender(<MantineProvider>{ui}</MantineProvider>);
};

// Mock the services and contexts
jest.mock('../../services/contractService');

jest.mock('../../contexts/ReferenceDataContext', () => ({
  useReferenceData: () => ({
    fetchTeams: jest.fn().mockResolvedValue([
      { id: 1, name: 'Equipe Alpha' },
      { id: 2, name: 'Equipe Beta' },
    ]),
    fetchContractFilterUsers: jest.fn().mockResolvedValue([
      { id: 'user-1', name: 'João Silva', email: 'joao@example.com' },
    ]),
  }),
}));

jest.mock('../Menu', () => ({ children }: any) => <div data-testid="mock-menu">{children}</div>);

describe('MonitoringContractsPage', () => {
  const mockSummary: contractService.ContractUpdateSummary = {
    moreThan1Hour: 5,
    moreThan3Hours: 12,
    moreThan6Hours: 20,
    moreThan1Day: 35,
    moreThan3Days: 50,
    moreThan1Week: 15,
    total: 95,
  };

  const mockContracts: contractService.Contract[] = [
    {
      id: 1,
      contractNumber: 'CTR-1001',
      userId: 'user-1',
      userName: 'João Silva',
      totalAmount: 15000,
      groupId: 10,
      groupName: 'Grupo Imóveis',
      status: contractService.ContractStatus.Active,
      contractStartDate: '2026-01-15T00:00:00Z',
      isActive: true,
      createdAt: '2026-01-15T00:00:00Z',
      updatedAt: new Date(Date.now() - 30 * 60 * 1000).toISOString(), // 30 mins ago
      matriculaNumber: '12345',
      teamName: 'Equipe Alpha',
      customerName: 'Maria Santos',
      quota: 42,
      isAwaitingPayment: false,
    },
    {
      id: 2,
      contractNumber: 'CTR-1002',
      userId: 'user-1',
      userName: 'João Silva',
      totalAmount: 25000,
      groupId: 10,
      groupName: 'Grupo Imóveis',
      status: contractService.ContractStatus.Active,
      contractStartDate: '2026-02-10T00:00:00Z',
      isActive: true,
      createdAt: '2026-02-10T00:00:00Z',
      updatedAt: new Date(Date.now() - 10 * 24 * 60 * 60 * 1000).toISOString(), // 10 days ago
      matriculaNumber: '67890',
      teamName: 'Equipe Beta',
      customerName: 'Carlos Oliveira',
      quota: 88,
      isAwaitingPayment: true,
    },
  ];

  beforeEach(() => {
    jest.clearAllMocks();
    (contractService.getContractUpdateSummary as jest.Mock).mockResolvedValue(mockSummary);
    (contractService.getMonitoringContracts as jest.Mock).mockResolvedValue({
      contracts: mockContracts,
      totalCount: mockContracts.length,
      page: 1,
      pageSize: 100,
    });
  });

  it('renders page header, summary cards with counts, and contract table', async () => {
    render(<MonitoringContractsPage />);

    expect(screen.getByText('Monitoramento de Contratos')).toBeInTheDocument();
    expect(
      screen.getByText('Acompanhe a recência de atualização dos contratos para validar a cobertura do scrape')
    ).toBeInTheDocument();

    // Verify summary counts are rendered in the cards
    await waitFor(() => {
      expect(screen.getByTestId('card-filter-moreThan1Hour')).toBeInTheDocument();
      expect(screen.getByTestId('card-filter-moreThan1Hour')).toHaveTextContent('5');
      expect(screen.getByTestId('card-filter-moreThan3Hours')).toHaveTextContent('12');
      expect(screen.getByTestId('card-filter-moreThan1Week')).toHaveTextContent('15');
    });

    // Verify contract rows are rendered
    await waitFor(() => {
      expect(screen.getByText('CTR-1001')).toBeInTheDocument();
      expect(screen.getByText('CTR-1002')).toBeInTheDocument();
      expect(screen.getByText('12345')).toBeInTheDocument();
      expect(screen.getAllByText('Equipe Alpha').length).toBeGreaterThanOrEqual(1);
      expect(screen.getByText('Maria Santos')).toBeInTheDocument();
    });
  });

  it('filters contracts when a summary card is clicked', async () => {
    render(<MonitoringContractsPage />);

    await waitFor(() => {
      expect(screen.getByTestId('card-filter-moreThan1Hour')).toBeInTheDocument();
    });

    // Click the "> 1 hora" card
    fireEvent.click(screen.getByTestId('card-filter-moreThan1Hour'));

    await waitFor(() => {
      expect(contractService.getMonitoringContracts).toHaveBeenCalledWith(
        expect.objectContaining({
          updateFilter: 'moreThan1Hour',
          page: 1,
        })
      );
    });

    // The card should now have the active class
    expect(screen.getByTestId('card-filter-moreThan1Hour')).toHaveClass('active');
  });

  it('clears filters when clear button is clicked', async () => {
    render(<MonitoringContractsPage />);

    await waitFor(() => {
      expect(screen.getByTestId('card-filter-moreThan3Hours')).toBeInTheDocument();
    });

    // Click card to activate a filter
    fireEvent.click(screen.getByTestId('card-filter-moreThan3Hours'));

    await waitFor(() => {
      expect(screen.getByText('Limpar Filtros')).toBeInTheDocument();
    });

    // Click clear filters
    fireEvent.click(screen.getByText('Limpar Filtros'));

    await waitFor(() => {
      expect(contractService.getMonitoringContracts).toHaveBeenCalledWith(
        expect.objectContaining({
          updateFilter: undefined,
          page: 1,
        })
      );
    });
  });
});
