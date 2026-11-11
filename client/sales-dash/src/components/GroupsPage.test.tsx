import React from 'react';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import '@testing-library/jest-dom';
import { MantineProvider } from '@mantine/core';
import GroupsPage, { sortGroupsBySegmentAndName } from './GroupsPage';
import * as contractService from '../services/contractService';

jest.mock('../services/contractService');
jest.mock('./Menu', () => ({ children }: { children?: React.ReactNode }) => (
  <div data-testid="menu">{children}</div>
));
jest.mock('@mantine/notifications', () => ({
  notifications: {
    show: jest.fn(),
  },
}));

const mockGroups = [
  { id: 1, name: 'Grupo Lar', description: 'Grupo do lar', commission: 10, segment: 1, isActive: true },
  { id: 2, name: 'Grupo Motores', description: 'Grupo automotivo', commission: 15, segment: 2, isActive: true },
  { id: 3, name: 'Grupo Sem Segmento', description: 'Pendente', commission: 5, segment: null, isActive: true },
  { id: 4, name: 'Grupo Inativo', description: 'Antigo', commission: 5, segment: 3, isActive: false },
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
    expect(screen.getByText('Importar')).toBeInTheDocument();

    await waitFor(() => {
      expect(screen.getByText('Grupo Lar')).toBeInTheDocument();
      expect(screen.getByText('Grupo Motores')).toBeInTheDocument();
      expect(screen.getByText('Grupo Sem Segmento')).toBeInTheDocument();
      expect(screen.getByText('Lar')).toBeInTheDocument();
      expect(screen.getByText('Motores')).toBeInTheDocument();
      expect(screen.getByText('Não Definido')).toBeInTheDocument();
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

  it('opens import modal when Importar is clicked', async () => {
    render(
      <MantineProvider>
        <GroupsPage />
      </MantineProvider>
    );

    fireEvent.click(screen.getByText('Importar'));

    await waitFor(() => {
      expect(screen.getByText('Importar Grupos via Excel (.xlsx)')).toBeInTheDocument();
      expect(screen.getByText('Baixar Modelo (.xlsx)')).toBeInTheDocument();
    });
  });

  it('exports xlsx with active filter and search query when Exportar XLSX is clicked', async () => {
    const mockBlob = new Blob(['xlsx'], {
      type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
    });
    (contractService.exportGroupsXlsx as jest.Mock).mockResolvedValue(mockBlob);

    const createObjectURL = jest.fn(() => 'blob:mock-url');
    const revokeObjectURL = jest.fn();
    window.URL.createObjectURL = createObjectURL;
    window.URL.revokeObjectURL = revokeObjectURL;

    render(
      <MantineProvider>
        <GroupsPage />
      </MantineProvider>
    );

    await waitFor(() => {
      expect(screen.getByText('Grupo Lar')).toBeInTheDocument();
    });

    const searchInput = screen.getByPlaceholderText('Buscar por nome, descrição ou segmento...');
    fireEvent.change(searchInput, { target: { value: 'Lar' } });

    const exportButton = screen.getByText('Exportar XLSX');
    fireEvent.click(exportButton);

    await waitFor(() => {
      expect(contractService.exportGroupsXlsx).toHaveBeenCalledWith('active', 'Lar');
      expect(createObjectURL).toHaveBeenCalledWith(mockBlob);
      expect(revokeObjectURL).toHaveBeenCalledWith('blob:mock-url');
    });
  });

  describe('sortGroupsBySegmentAndName', () => {
    it('sorts null segments first, then by segment value, then by name', () => {
      const items = [
        { id: 1, name: 'Zeta', segment: 2, isActive: true },
        { id: 2, name: 'Beta', segment: null, isActive: true },
        { id: 3, name: 'Alfa', segment: null, isActive: true },
        { id: 4, name: 'Gama', segment: 1, isActive: true },
        { id: 5, name: 'Delta', segment: 1, isActive: true },
      ] as any[];

      const sorted = [...items].sort(sortGroupsBySegmentAndName);

      expect(sorted.map((s) => s.name)).toEqual([
        'Alfa', // null segment, name 'Alfa'
        'Beta', // null segment, name 'Beta'
        'Delta', // segment 1, name 'Delta'
        'Gama', // segment 1, name 'Gama'
        'Zeta', // segment 2, name 'Zeta'
      ]);
    });
  });
});
