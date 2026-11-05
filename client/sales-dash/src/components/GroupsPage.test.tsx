import React from 'react';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import '@testing-library/jest-dom';
import { MantineProvider } from '@mantine/core';
import GroupsPage from './GroupsPage';
import * as contractService from '../services/contractService';

jest.mock('../services/contractService');
jest.mock('./Menu', () => () => <div data-testid="menu">Menu</div>);
jest.mock('@mantine/notifications', () => ({
  notifications: {
    show: jest.fn(),
  },
}));

const mockGroups = [
  { id: 1, name: 'Grupo Lar', description: 'Grupo do lar', commission: 10, segment: 1, isActive: true },
  { id: 2, name: 'Grupo Motores', description: 'Grupo automotivo', commission: 15, segment: 2, isActive: true },
  { id: 3, name: 'Grupo Inativo', description: 'Antigo', commission: 5, segment: 3, isActive: false },
];

describe('GroupsPage', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    (contractService.getGroups as jest.Mock).mockResolvedValue(mockGroups);
  });

  it('renders groups page with header and groups list', async () => {
    render(
      <MantineProvider>
        <GroupsPage />
      </MantineProvider>
    );

    expect(screen.getByText('Gerenciamento de Grupos')).toBeInTheDocument();
    expect(screen.getByText('Novo Grupo')).toBeInTheDocument();

    await waitFor(() => {
      expect(screen.getByText('Grupo Lar')).toBeInTheDocument();
      expect(screen.getByText('Grupo Motores')).toBeInTheDocument();
      expect(screen.getByText('Lar')).toBeInTheDocument();
      expect(screen.getByText('Motores')).toBeInTheDocument();
    });
  });

  it('opens create modal when Novo Grupo is clicked', async () => {
    render(
      <MantineProvider>
        <GroupsPage />
      </MantineProvider>
    );

    fireEvent.click(screen.getByText('Novo Grupo'));

    await waitFor(() => {
      expect(screen.getByText('Cadastrar')).toBeInTheDocument();
      expect(screen.getByPlaceholderText('Ex: 101, Grupo Alfa...')).toBeInTheDocument();
    });
  });
});
