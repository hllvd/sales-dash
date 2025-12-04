import React, { useState, useEffect } from 'react';
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
    <div className="pv-layout">
      <Menu />
      <div className="pv-content">
        <div className="pv-page">
          <div className="pv-header">
            <h1>Gerenciamento de Pontos de Venda</h1>
            <div className="pv-header-actions">
              <button className="import-pv-btn" onClick={() => setShowImportModal(true)}>
                📁 Importar CSV
              </button>
              <button className="create-pv-btn" onClick={handleCreateClick}>
                + Criar Ponto de Venda
              </button>
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
              <button className="create-pv-btn" onClick={handleCreateClick}>
                Criar Primeiro Ponto de Venda
              </button>
            </div>
          ) : (
            <div className="pv-table-container">
              <table className="pv-table">
                <thead>
                  <tr>
                    <th>ID</th>
                    <th>Nome</th>
                    <th>Criado em</th>
                    <th>Atualizado em</th>
                    <th>Ações</th>
                  </tr>
                </thead>
                <tbody>
                  {pvs.map((pv) => (
                    <tr key={pv.id}>
                      <td>{pv.id}</td>
                      <td>{pv.name}</td>
                      <td>{formatDate(pv.createdAt)}</td>
                      <td>{formatDate(pv.updatedAt)}</td>
                      <td className="actions-cell">
                        <button
                          className="action-btn edit-btn"
                          onClick={() => handleEditClick(pv)}
                          title="Editar"
                        >
                          ✏️
                        </button>
                        <button
                          className="action-btn delete-btn"
                          onClick={() => handleDeleteClick(pv.id)}
                          title="Excluir"
                        >
                          🗑️
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      </div>

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
  );
};

export default PVPage;
