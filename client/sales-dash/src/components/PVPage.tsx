import React, { useState, useEffect } from 'react';
import { Title, Button, Table, ActionIcon, Group } from '@mantine/core';
import { IconEdit, IconTrash, IconPlus, IconUpload } from '@tabler/icons-react';
import './PVPage.css';
import Menu from './Menu';
import PVForm from './PVForm';
import PVImportModal from './PVImportModal';
import { apiService, PV } from '../services/apiService';

const PVPage: React.FC = () => {
  const [pvs, setPVs] = useState<PV[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [showForm, setShowForm] = useState(false);
  const [showImportModal, setShowImportModal] = useState(false);
  const [editingPV, setEditingPV] = useState<PV | null>(null);
  const [deleteConfirm, setDeleteConfirm] = useState<number | null>(null);

  useEffect(() => {
    loadPVs();
  }, []);

  const loadPVs = async () => {
    setLoading(true);
    setError('');

    try {
      const response = await apiService.getPVs();
      if (response.success && response.data) {
        setPVs(response.data);
      }
    } catch (err: any) {
      setError(err.message || 'Failed to load PVs');
    } finally {
      setLoading(false);
    }
  };

  const handleCreateClick = () => {
    setEditingPV(null);
    setShowForm(true);
  };

  const handleEditClick = (pv: PV) => {
    setEditingPV(pv);
    setShowForm(true);
  };

  const handleDeleteClick = (id: number) => {
    setDeleteConfirm(id);
  };

  const handleDeleteConfirm = async () => {
    if (deleteConfirm === null) return;

    try {
      await apiService.deletePV(deleteConfirm);
      setDeleteConfirm(null);
      loadPVs();
    } catch (err: any) {
      setError(err.message || 'Failed to delete PV');
      setDeleteConfirm(null);
    }
  };

  const handleFormSubmit = async (pvData: { id: number; name: string }) => {
    if (editingPV) {
      await apiService.updatePV(editingPV.id, pvData);
    } else {
      await apiService.createPV(pvData);
    }
    setShowForm(false);
    loadPVs();
  };

  const formatDate = (dateString: string): string => {
    const date = new Date(dateString);
    return date.toLocaleDateString('pt-BR');
  };

  return (
    <Menu>
      <div className="pv-page">
          <div className="pv-header">
            <Title order={2} size="h2" className="page-title-break">Gerenciamento de PV</Title>
            <div className="pv-header-actions">
              <Button onClick={() => setShowImportModal(true)} leftSection={<IconUpload size={16} />}>
                Importar
              </Button>
              <Button onClick={handleCreateClick} leftSection={<IconPlus size={16} />}>
                Criar
              </Button>
            </div>
          </div>

          {error && <div className="pv-error">{error}</div>}

          {loading ? (
            <div className="pv-loading">
              <div className="spinner"></div>
              <p>Carregando pontos de venda...</p>
            </div>
          ) : pvs.length === 0 ? (
            <div className="pv-empty">
              <p>Nenhum ponto de venda encontrado.</p>
              <Button onClick={handleCreateClick}>
                Criar Primeiro Ponto de Venda
              </Button>
            </div>
          ) : (
            <div className="pv-table-container">
              <Table striped highlightOnHover>
                <Table.Thead>
                  <Table.Tr>
                    <Table.Th>ID</Table.Th>
                    <Table.Th>Nome</Table.Th>
                    <Table.Th>Criado em</Table.Th>
                    <Table.Th>Atualizado em</Table.Th>
                    <Table.Th>Ações</Table.Th>
                  </Table.Tr>
                </Table.Thead>
                <Table.Tbody>
                  {pvs.map((pv) => (
                    <Table.Tr key={pv.id}>
                      <Table.Td>{pv.id}</Table.Td>
                      <Table.Td>{pv.name}</Table.Td>
                      <Table.Td>{formatDate(pv.createdAt)}</Table.Td>
                      <Table.Td>{formatDate(pv.updatedAt)}</Table.Td>
                      <Table.Td>
                        <Group gap="xs">
                          <ActionIcon
                            variant="subtle"
                            color="blue"
                            onClick={() => handleEditClick(pv)}
                            title="Editar"
                          >
                            <IconEdit size={16} />
                          </ActionIcon>
                          <ActionIcon
                            variant="subtle"
                            color="red"
                            onClick={() => handleDeleteClick(pv.id)}
                            title="Excluir"
                          >
                            <IconTrash size={16} />
                          </ActionIcon>
                        </Group>
                      </Table.Td>
                    </Table.Tr>
                  ))}
                </Table.Tbody>
              </Table>
            </div>
          )}
        

      {showForm && (
        <PVForm
          pv={editingPV || undefined}
          onSubmit={handleFormSubmit}
          onCancel={() => setShowForm(false)}
          isEdit={!!editingPV}
        />
      )}

      {showImportModal && (
        <PVImportModal
          onClose={() => setShowImportModal(false)}
          onSuccess={() => {
            loadPVs();
            setShowImportModal(false);
          }}
        />
      )}

      {deleteConfirm !== null && (
        <div className="delete-confirm-overlay" onClick={() => setDeleteConfirm(null)}>
          <div className="delete-confirm-modal" onClick={(e) => e.stopPropagation()}>
            <h3>Confirmar Exclusão</h3>
            <p>Tem certeza que deseja excluir este ponto de venda?</p>
            <div className="delete-confirm-actions">
              <button onClick={() => setDeleteConfirm(null)}>Cancelar</button>
              <button onClick={handleDeleteConfirm} className="delete-confirm-btn">
                Excluir
              </button>
            </div>
          </div>
        </div>
      )}
      </div>
    </Menu>
  );
};

export default PVPage;
