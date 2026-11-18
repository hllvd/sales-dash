import React from 'react';
import { render, screen, waitFor } from '@testing-library/react';
import '@testing-library/jest-dom';
import PreferencesPage from './PreferencesPage';
import { apiService } from '../services/apiService';
import { MantineProvider } from '@mantine/core';

jest.mock('../services/apiService');

jest.mock('../contexts/BuildInfoContext', () => ({
  BuildInfoProvider: ({ children }: { children: React.ReactNode }) => <>{children}</>,
  useBuildInfo: () => ({ buildInfo: { buildId: 'test-build' } }),
}));

describe('PreferencesPage', () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  it('renders preferences options when loaded', async () => {
    (apiService.getUserPreferences as jest.Mock).mockResolvedValue({
      success: true,
      data: {
        autoAssignNewContractsToMatriculaOwner: true,
        treatUnpaidActiveAsAwaitingPayment: true,
        includeInactiveUsersInFilter: false,
      },
      message: 'ok',
    });

    render(
      <MantineProvider>
        <PreferencesPage />
      </MantineProvider>
    );

    await waitFor(() => {
      expect(screen.getByText(/Atribuir novos contratos do Ava Pro ao dono da matrícula/i)).toBeInTheDocument();
      expect(screen.getByText(/Tratar cotas ativas sem pagamento como aguardando pagamento/i)).toBeInTheDocument();
      expect(screen.getByText(/Incluir usuários inativos nos filtros/i)).toBeInTheDocument();
    });
  });
});
