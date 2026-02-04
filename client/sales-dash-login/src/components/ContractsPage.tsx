import React, { useState, useEffect } from 'react';
import { Title, Button, Table, ActionIcon, Group, Badge } from '@mantine/core';
import { IconEdit, IconTrash, IconPlus, IconUpload } from '@tabler/icons-react';
import './ContractsPage.css';
import Menu from './Menu';
import ContractForm from './ContractForm';
import BulkImportModal from './BulkImportModal';
import AggregationSummary from '../shared/AggregationSummary';
import HistoricProduction from '../shared/HistoricProduction';
import Pagination from './Pagination';
import { useContractsContext } from '../contexts/ContractsContext';
import { toast } from '../utils/toast';
import {
  Contract,
  ContractStatus,
  User,
  Group as ContractGroup,
  ContractAggregation,
  getContracts,
  deleteContract,
  getUsers,
  getGroups,
} from '../services/contractService';

const ContractsPage: React.FC = () => {
  // Get context for caching
  const { setContracts: setCachedContracts, setUsers: setCachedUsers, setGroups: setCachedGroups } = useContractsContext();
  
  const [contracts, setContracts] = useState<Contract[]>([]);
  const [users, setUsers] = useState<User[]>([]);
  const [groups, setGroups] = useState<ContractGroup[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [showForm, setShowForm] = useState(false);
  const [editingContract, setEditingContract] = useState<Contract | null>(null);
  const [deleteConfirm, setDeleteConfirm] = useState<number | null>(null);
  const [showImportModal, setShowImportModal] = useState(false);
  const [aggregation, setAggregation] = useState<ContractAggregation | null>(null);

  // Filters
  const [filterUserId, setFilterUserId] = useState('');
  const [filterGroupId, setFilterGroupId] = useState('');
  const [filterStartDate, setFilterStartDate] = useState('');
  const [filterEndDate, setFilterEndDate] = useState('');
  const [filterContractNumber, setFilterContractNumber] = useState('');
  const [debouncedContractNumber, setDebouncedContractNumber] = useState('');

  // Pagination state
  const [currentPage, setCurrentPage] = useState(1);
  const [pageSize, setPageSize] = useState(() => {
    const saved = localStorage.getItem('contracts_pageSize');
    return saved ? parseInt(saved) : 50;
  });

  // Load saved filters from localStorage
  useEffect(() => {
    const savedStartDate = localStorage.getItem('contracts_filterStartDate');
    if (savedStartDate) {
      setFilterStartDate(savedStartDate);
    }
    loadFilters();
  }, []);

  const loadFilters = async () => {
    try {
      const [usersData, groupsData] = await Promise.all([getUsers(), getGroups()]);
      setUsers(usersData);
      setGroups(groupsData);
      // Cache the data in context for use by ContractForm
      setCachedUsers(usersData);
      setCachedGroups(groupsData);
    } catch (err: any) {
      console.error('Failed to load filter options:', err);
      toast.error(err.message || 'Falha ao carregar opções de filtro');
    }
  };
  
  // Debounce contract number search
  useEffect(() => {
    const timer = setTimeout(() => {
      setDebouncedContractNumber(filterContractNumber);
    }, 3000); // 3-second debounce as requested

    return () => clearTimeout(timer);
  }, [filterContractNumber]);

  const loadContracts = async () => {
    setLoading(true);
    setError('');

    try {
      const { contracts: data, aggregation: aggData } = await getContracts(
        filterUserId || undefined,
        filterGroupId ? parseInt(filterGroupId) : undefined,
        filterStartDate || undefined,
        filterEndDate || undefined,
        debouncedContractNumber || undefined
      );
      setContracts(data);
      setAggregation(aggData || null);
      // Cache contracts in context
      setCachedContracts(data);
    } catch (err: any) {
      const errorMessage = err.message || 'Falha ao carregar contratos';
      setError(errorMessage);
      toast.error(errorMessage);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadContracts();
  }, [filterUserId, filterGroupId, filterStartDate, filterEndDate, debouncedContractNumber]);

  // Reset to page 1 when filters change
  useEffect(() => {
    setCurrentPage(1);
  }, [filterUserId, filterGroupId, filterStartDate, filterEndDate, debouncedContractNumber]);

  // Calculate pagination
  const totalPages = Math.ceil(contracts.length / pageSize);
  const startIndex = (currentPage - 1) * pageSize;
  const endIndex = startIndex + pageSize;
  const paginatedContracts = contracts.slice(startIndex, endIndex);

  const handlePageSizeChange = (newSize: number) => {
    setPageSize(newSize);
    setCurrentPage(1);
    localStorage.setItem('contracts_pageSize', newSize.toString());
  };

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
      toast.success('Contrato excluído com sucesso');
      loadContracts();
    } catch (err: any) {
      const errorMessage = err.message || 'Falha ao excluir contrato';
      setError(errorMessage);
      toast.error(errorMessage);
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
    switch (status.toLowerCase()) {
      case 'active':
        return 'Ativo';
      case 'late1':
        return 'Atrasado 1';
      case 'late2':
        return 'Atrasado 2';
      case 'late3':
        return 'Atrasado 3';
      case 'defaulted':
        return 'Cancelado';
      case 'transferred':
        return 'Transferido';
      case 'paid_off':
        return 'Quitado';
      case 'delinquent':
        return 'Inadimplente';
      default:
        return status;
    }
  };

  return (
    <Menu>
      <div className="contracts-page">
          <div className="contracts-header">
            <Title order={2} size="h2" className="page-title-break">Gerenciamento de Contratos</Title>
            <div style={{ display: 'flex', gap: '10px' }}>
              <Button onClick={() => setShowImportModal(true)} leftSection={<IconUpload size={16} />}>
                Importar
              </Button>
              <Button onClick={handleCreateClick} leftSection={<IconPlus size={16} />}>
                Criar
              </Button>
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
          <label htmlFor="filterContractNumber">Número do Contrato</label>
          <input
            type="text"
            id="filterContractNumber"
            value={filterContractNumber}
            onChange={(e) => setFilterContractNumber(e.target.value)}
            placeholder="Buscar por número..."
          />
        </div>

        <div className="filter-group">
          <label htmlFor="filterStartDate">Data Início</label>
          <input
            type="date"
            id="filterStartDate"
            value={filterStartDate}
            onChange={(e) => {
              const value = e.target.value;
              setFilterStartDate(value);
              if (value) {
                localStorage.setItem('contracts_filterStartDate', value);
              } else {
                localStorage.removeItem('contracts_filterStartDate');
              }
            }}
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

        {(filterUserId || filterGroupId || filterStartDate || filterEndDate || filterContractNumber) && (
          <button
            className="clear-filters-btn"
            onClick={() => {
              setFilterUserId('');
              setFilterGroupId('');
              setFilterStartDate('');
              setFilterEndDate('');
              setFilterContractNumber('');
              setDebouncedContractNumber('');
              localStorage.removeItem('contracts_filterStartDate');
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
        <>
          <Pagination
            currentPage={currentPage}
            totalPages={totalPages}
            pageSize={pageSize}
            totalItems={contracts.length}
            onPageChange={setCurrentPage}
            onPageSizeChange={handlePageSizeChange}
            showBottomControls={false}
          />

          <div className="contracts-table-container">
            <Table striped highlightOnHover>
            <Table.Thead>
              <Table.Tr>
                <Table.Th>Número do Contrato</Table.Th>
                <Table.Th>Usuário</Table.Th>
                <Table.Th>Matrícula</Table.Th>
                <Table.Th>Grupo</Table.Th>
                <Table.Th>Cliente</Table.Th>
                <Table.Th>Valor Total</Table.Th>
                <Table.Th>Status</Table.Th>
                <Table.Th>Data Início</Table.Th>
                <Table.Th>Ações</Table.Th>
              </Table.Tr>
            </Table.Thead>
            <Table.Tbody>
              {paginatedContracts.map((contract) => (
                <Table.Tr key={contract.id}>
                  <Table.Td>{contract.contractNumber}</Table.Td>
                  <Table.Td>{contract.userName}</Table.Td>
                  <Table.Td>{contract.matriculaNumber || '-'}</Table.Td>
                  <Table.Td>{contract.groupName}</Table.Td>
                  <Table.Td>{contract.customerName || '-'}</Table.Td>
                  <Table.Td>{formatCurrency(contract.totalAmount)}</Table.Td>
                  <Table.Td>
                    <Badge 
                      color={
                        contract.status.toLowerCase() === 'active' ? 'teal' :
                        contract.status.toLowerCase() === 'late1' ? 'yellow' :
                        contract.status.toLowerCase() === 'late2' ? 'orange' :
                        contract.status.toLowerCase() === 'late3' ? 'red' :
                        contract.status.toLowerCase() === 'defaulted' ? 'dark' :
                        contract.status.toLowerCase() === 'transferred' ? 'blue' :
                        contract.status.toLowerCase() === 'paid_off' ? 'green' :
                        'gray'
                      }
                      title={getStatusLabel(contract.status)}
                      style={{ cursor: 'help' }}
                    >
                      {getStatusLabel(contract.status)}
                    </Badge>
                  </Table.Td>
                  <Table.Td>{formatDate(contract.contractStartDate)}</Table.Td>
                  <Table.Td>
                    <Group gap="xs">
                      <ActionIcon
                        variant="subtle"
                        color="blue"
                        onClick={() => handleEditClick(contract)}
                        title="Editar"
                      >
                        <IconEdit size={16} />
                      </ActionIcon>
                      <ActionIcon
                        variant="subtle"
                        color="red"
                        onClick={() => handleDeleteClick(contract.id)}
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

        <Pagination
          currentPage={currentPage}
          totalPages={totalPages}
          pageSize={pageSize}
          totalItems={contracts.length}
          onPageChange={setCurrentPage}
          onPageSizeChange={handlePageSizeChange}
          showTopControls={false}
        />
      </>
      )}

      {/* Aggregation Summary */}
      {aggregation && contracts.length > 0 && (
        <AggregationSummary
          total={aggregation?.total || 0}
          totalCancel={aggregation?.totalCancel || 0}
          totalActive={aggregation?.totalActive || 0}
          totalLate={aggregation?.totalLate || 0}
          retention={aggregation?.retention || 0}
        />
      )}

      {/* Historic Production */}
      {contracts.length > 0 && (
        <HistoricProduction
          startDate={filterStartDate}
          endDate={filterEndDate}
        />
      )}

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
    </Menu>
  );
};

export default ContractsPage;
