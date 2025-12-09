import React, { useState, useEffect } from 'react';
import './ContractsPage.css';
import Menu from './Menu';
import ContractForm from './ContractForm';
import BulkImportModal from './BulkImportModal';
import {
  Contract,
  User,
  Group,
  getContracts,
  deleteContract,
  getUsers,
  getGroups,
} from '../services/contractService';

const ContractsPage: React.FC = () => {
  const [contracts, setContracts] = useState<Contract[]>([]);
  const [users, setUsers] = useState<User[]>([]);
  const [groups, setGroups] = useState<Group[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [showForm, setShowForm] = useState(false);
  const [editingContract, setEditingContract] = useState<Contract | null>(null);
  const [deleteConfirm, setDeleteConfirm] = useState<number | null>(null);
  const [showImportModal, setShowImportModal] = useState(false);

  // Filters
  const [filterUserId, setFilterUserId] = useState('');
  const [filterGroupId, setFilterGroupId] = useState('');
  const [filterStartDate, setFilterStartDate] = useState('');
  const [filterEndDate, setFilterEndDate] = useState('');

  useEffect(() => {
    loadFilters();
  }, []);

  const loadFilters = async () => {
    try {
      const [usersData, groupsData] = await Promise.all([getUsers(), getGroups()]);
      setUsers(usersData);
      setGroups(groupsData);
    } catch (err) {
      console.error('Failed to load filter options:', err);
    }
  };

  const loadContracts = async () => {
    setLoading(true);
    setError('');

    try {
      const data = await getContracts(
        filterUserId || undefined,
        filterGroupId ? parseInt(filterGroupId) : undefined,
        filterStartDate || undefined,
        filterEndDate || undefined
      );
      setContracts(data);
    } catch (err: any) {
      setError(err.message || 'Failed to load contracts');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadContracts();
  }, [filterUserId, filterGroupId, filterStartDate, filterEndDate]);

  const handleCreateClick = () => {
    setEditingContract(null);
    setShowForm(true);
  };

  const handleEditClick = (contract: Contract) => {
    setEditingContract(contract);
    setShowForm(true);
  };

  const handleDeleteClick = (id: number) => {
    setDeleteConfirm(id);
  };

  const handleDeleteConfirm = async () => {
    if (deleteConfirm === null) return;

    try {
      await deleteContract(deleteConfirm);
      setDeleteConfirm(null);
      loadContracts();
    } catch (err: any) {
      setError(err.message || 'Failed to delete contract');
      setDeleteConfirm(null);
    }
  };

  const handleFormSuccess = () => {
    loadContracts();
  };

  const formatCurrency = (amount: number): string => {
    return new Intl.NumberFormat('pt-BR', {
      style: 'currency',
      currency: 'BRL',
    }).format(amount);
  };

  const formatDate = (dateString: string | null): string => {
    if (!dateString) return '-';
    const date = new Date(dateString);
    return date.toLocaleDateString('pt-BR');
  };

  const getStatusBadgeClass = (status: string): string => {
    switch (status) {
      case 'active':
        return 'status-active';
      case 'delinquent':
        return 'status-delinquent';
      case 'paid_off':
        return 'status-paid-off';
      default:
        return '';
    }
  };

  const getStatusLabel = (status: string): string => {
    switch (status) {
      case 'active':
        return 'Ativo';
      case 'delinquent':
        return 'Inadimplente';
      case 'paid_off':
        return 'Quitado';
      default:
        return status;
    }
  };

  return (
    <div className="contracts-layout">
      <Menu />
      <div className="contracts-content">
        <div className="contracts-page">
          <div className="contracts-header">
            <h1>Gerenciamento de Contratos</h1>
            <div style={{ display: 'flex', gap: '10px' }}>
              <button className="create-contract-btn" onClick={() => setShowImportModal(true)}>
                ⬆️ Importar Contratos
              </button>
              <button className="create-contract-btn" onClick={handleCreateClick}>
                + Criar Contrato
              </button>
            </div>
          </div>

          {error && <div className="contracts-error">{error}</div>}

          <div className="contracts-filters">
        <div className="filter-group">
          <label htmlFor="filterUser">Usuário</label>
          <select
            id="filterUser"
            value={filterUserId}
            onChange={(e) => setFilterUserId(e.target.value)}
          >
            <option value="">Todos</option>
            {users.map((user) => (
              <option key={user.id} value={user.id}>
                {user.name}
              </option>
            ))}
          </select>
        </div>

        <div className="filter-group">
          <label htmlFor="filterGroup">Grupo</label>
          <select
            id="filterGroup"
            value={filterGroupId}
            onChange={(e) => setFilterGroupId(e.target.value)}
          >
            <option value="">Todos</option>
            {groups.map((group) => (
              <option key={group.id} value={group.id}>
                {group.name}
              </option>
            ))}
          </select>
        </div>

        <div className="filter-group">
          <label htmlFor="filterStartDate">Data Início</label>
          <input
            type="date"
            id="filterStartDate"
            value={filterStartDate}
            onChange={(e) => setFilterStartDate(e.target.value)}
          />
        </div>

        <div className="filter-group">
          <label htmlFor="filterEndDate">Data Fim</label>
          <input
            type="date"
            id="filterEndDate"
            value={filterEndDate}
            onChange={(e) => setFilterEndDate(e.target.value)}
          />
        </div>

        {(filterUserId || filterGroupId || filterStartDate || filterEndDate) && (
          <button
            className="clear-filters-btn"
            onClick={() => {
              setFilterUserId('');
              setFilterGroupId('');
              setFilterStartDate('');
              setFilterEndDate('');
            }}
          >
            Limpar Filtros
          </button>
        )}
      </div>

      {loading ? (
        <div className="contracts-loading">
          <div className="spinner"></div>
          <p>Carregando contratos...</p>
        </div>
      ) : contracts.length === 0 ? (
        <div className="contracts-empty">
          <p>Nenhum contrato encontrado.</p>
          <button className="create-contract-btn" onClick={handleCreateClick}>
            Criar Primeiro Contrato
          </button>
        </div>
      ) : (
        <div className="contracts-table-container">
          <table className="contracts-table">
            <thead>
              <tr>
                <th>Número</th>
                <th>Usuário</th>
                <th>Grupo</th>
                <th>Cliente</th>
                <th>Valor Total</th>
                <th>Status</th>
                <th>Data Início</th>
                <th>Ações</th>
              </tr>
            </thead>
            <tbody>
              {contracts.map((contract) => (
                <tr key={contract.id}>
                  <td>{contract.contractNumber}</td>
                  <td>{contract.userName}</td>
                  <td>{contract.groupName}</td>
                  <td>{contract.customerName || '-'}</td>
                  <td>{formatCurrency(contract.totalAmount)}</td>
                  <td>
                    <span className={`status-badge ${getStatusBadgeClass(contract.status)}`}>
                      {getStatusLabel(contract.status)}
                    </span>
                  </td>
                  <td>{formatDate(contract.contractStartDate)}</td>
                  <td className="actions-cell">
                    <button
                      className="action-btn edit-btn"
                      onClick={() => handleEditClick(contract)}
                      title="Editar"
                    >
                      ✏️
                    </button>
                    <button
                      className="action-btn delete-btn"
                      onClick={() => handleDeleteClick(contract.id)}
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
        <ContractForm
          contract={editingContract}
          onClose={() => setShowForm(false)}
          onSuccess={handleFormSuccess}
        />
      )}

      {showImportModal && (
        <BulkImportModal
          onClose={() => setShowImportModal(false)}
          onSuccess={() => {
            setShowImportModal(false);
            loadContracts();
          }}
          templateId={2}
          title="Importar Contratos em Lote"
        />
      )}

      {deleteConfirm !== null && (
        <div className="delete-confirm-overlay" onClick={() => setDeleteConfirm(null)}>
          <div className="delete-confirm-modal" onClick={(e) => e.stopPropagation()}>
            <h3>Confirmar Exclusão</h3>
            <p>Tem certeza que deseja excluir este contrato?</p>
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

export default ContractsPage;
