import React, { useState, useEffect, useCallback, useRef } from 'react';
import { Title, Button, Table, ActionIcon, Group, Text, MultiSelect, Checkbox } from '@mantine/core';
import { IconEdit, IconTrash, IconPlus, IconUpload, IconSettings } from '@tabler/icons-react';
import './ContractsPage.css';
import Menu from './Menu';
import ContractForm from './ContractForm';
import BulkImportModal from './BulkImportModal';
import StandardModal from '../shared/StandardModal';
import AggregationSummary from '../shared/AggregationSummary';
import HistoricProduction from '../shared/HistoricProduction';
import Pagination from './Pagination';
import ContractStatusBadge from '../shared/ContractStatusBadge';
import ExportButton from '../shared/ExportButton';
import ExportProgressIndicator from '../shared/ExportProgressIndicator';
import { apiService, Team } from '../services/apiService';
import { useContractsContext } from '../contexts/ContractsContext';
import { toast } from '../utils/toast';
import {
  Contract,
  User,
  ContractAggregation,
  getContracts,
  deleteContract,
  getUsers,
  getGroups,
} from '../services/contractService';

interface VisibleColumns {
  contractNumber: boolean;
  user: boolean;
  matricula: boolean;
  group: boolean;
  customer: boolean;
  totalAmount: boolean;
  status: boolean;
  startDate: boolean;
}

const DEFAULT_COLUMNS: VisibleColumns = {
  contractNumber: true,
  user: true,
  matricula: true,
  group: true,
  customer: true,
  totalAmount: true,
  status: true,
  startDate: true,
};

const ContractsPage: React.FC = () => {
  // Track latest API request to prevent race conditions
  const requestCountRef = useRef(0);

  // Get context for caching
  const { setContracts: setCachedContracts, setUsers: setCachedUsers, setGroups: setCachedGroups } = useContractsContext();
  
  const [contracts, setContracts] = useState<Contract[]>([]);
  const [users, setUsers] = useState<User[]>([]);
  const [teams, setTeams] = useState<Team[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [showForm, setShowForm] = useState(false);
  const [editingContract, setEditingContract] = useState<Contract | null>(null);
  const [deleteConfirm, setDeleteConfirm] = useState<number | null>(null);
  const [showImportModal, setShowImportModal] = useState(false);
  const [aggregation, setAggregation] = useState<ContractAggregation | null>(null);
  const [totalCount, setTotalCount] = useState(0);

  // Export state
  const [exportJobId, setExportJobId] = useState<string | null>(null);
  const [isExporting, setIsExporting] = useState(false);
  const exportToken = localStorage.getItem('token') || '';

  // Filters
  const [isInitializing, setIsInitializing] = useState(true);
  const [filterUserIds, setFilterUserIds] = useState<string[]>([]); // stored as string[] for Mantine MultiSelect
  const [debouncedUserIds, setDebouncedUserIds] = useState<string[]>([]);
  const [filterStartDate, setFilterStartDate] = useState('');
  const [debouncedStartDate, setDebouncedStartDate] = useState('');
  const [filterEndDate, setFilterEndDate] = useState('');
  const [debouncedEndDate, setDebouncedEndDate] = useState('');
  const [filterContractNumber, setFilterContractNumber] = useState('');
  const [debouncedContractNumber, setDebouncedContractNumber] = useState('');
  const [filterShowUnassigned, setFilterShowUnassigned] = useState<string>('all');
  const [debouncedShowUnassigned, setDebouncedShowUnassigned] = useState<string>('all');
  const [filterMatricula, setFilterMatricula] = useState('');
  const [debouncedMatricula, setDebouncedMatricula] = useState('');
  const [filterTeamIds, setFilterTeamIds] = useState<string[]>([]); // stored as string[] for Mantine MultiSelect
  const [debouncedTeamIds, setDebouncedTeamIds] = useState<string[]>([]);

  // Columns visibility state
  const [showColumnsModal, setShowColumnsModal] = useState(false);
  const [visibleColumns, setVisibleColumns] = useState<VisibleColumns>(() => {
    const saved = localStorage.getItem('contracts_visibleColumns');
    if (saved) {
      try {
        return JSON.parse(saved);
      } catch (e) {
        console.error('Failed to parse visible columns state:', e);
      }
    }
    return DEFAULT_COLUMNS;
  });

  const handleColumnToggle = (columnKey: keyof VisibleColumns, value: boolean) => {
    const updated = { ...visibleColumns, [columnKey]: value };
    setVisibleColumns(updated);
    localStorage.setItem('contracts_visibleColumns', JSON.stringify(updated));
  };

  // Pagination state
  const [currentPage, setCurrentPage] = useState(1);
  const [pageSize, setPageSize] = useState(() => {
    const saved = localStorage.getItem('contracts_pageSize');
    return saved ? parseInt(saved) : 100;
  });

  const loadFilters = useCallback(async () => {
    try {
      const [usersData, groupsData, teamsResponse] = await Promise.all([
        getUsers(true),
        getGroups(),
        apiService.getTeams(),
      ]);
      setUsers(usersData);
      setTeams(teamsResponse.data ?? []);
      // Cache the data in context for use by ContractForm
      setCachedUsers(usersData);
      setCachedGroups(groupsData);
    } catch (err: any) {
      console.error('Failed to load filter options:', err);
      toast.error(err.message || 'Falha ao carregar opções de filtro');
    }
  }, [setCachedUsers, setCachedGroups]);

  const loadContracts = useCallback(async () => {
    // Local date validation: block api call if end date is before start date
    if (debouncedStartDate && debouncedEndDate && debouncedEndDate < debouncedStartDate) {
      setLoading(false);
      setContracts([]);
      setAggregation(null);
      setTotalCount(0);
      return;
    }

    setLoading(true);
    setError('');
    const requestId = ++requestCountRef.current;

    try {
      const { contracts: data, aggregation: aggData, totalCount: fetchedTotalCount } = await getContracts(
        undefined, // userId
        undefined, // groupId
        debouncedStartDate || undefined,
        debouncedEndDate || undefined,
        debouncedContractNumber || undefined,
        debouncedShowUnassigned === 'unassigned' ? true : debouncedShowUnassigned === 'assigned' ? false : undefined,
        debouncedMatricula || undefined,
        undefined, // userEmail
        debouncedTeamIds.length > 0 ? debouncedTeamIds.map(id => parseInt(id)) : undefined,
        debouncedUserIds.length > 0 ? debouncedUserIds : undefined,
        currentPage,
        pageSize
      );
      if (requestId !== requestCountRef.current) return;
      setContracts(data);
      setAggregation(aggData || null);
      setTotalCount(fetchedTotalCount);
      // Cache contracts in context
      setCachedContracts(data);
    } catch (err: any) {
      if (requestId !== requestCountRef.current) return;
      const errorMessage = err.message || 'Falha ao carregar contratos';
      setError(errorMessage);
      toast.error(errorMessage);
    } finally {
      if (requestId === requestCountRef.current) {
        setLoading(false);
      }
    }
  }, [debouncedStartDate, debouncedEndDate, debouncedContractNumber, debouncedShowUnassigned, debouncedMatricula, debouncedTeamIds, debouncedUserIds, currentPage, pageSize, setCachedContracts]);

  // Load saved filters from localStorage
  useEffect(() => {
    const savedStartDate = localStorage.getItem('contracts_filterStartDate');
    if (savedStartDate) {
      setFilterStartDate(savedStartDate);
      setDebouncedStartDate(savedStartDate);
    } else {
      const d = new Date();
      d.setMonth(d.getMonth() - 15);
      const defaultDate = d.toISOString().split('T')[0]; // "YYYY-MM-DD"
      setFilterStartDate(defaultDate);
      setDebouncedStartDate(defaultDate);
      localStorage.setItem('contracts_filterStartDate', defaultDate);
    }

    const savedEndDate = localStorage.getItem('contracts_filterEndDate');
    if (savedEndDate) {
      setFilterEndDate(savedEndDate);
      setDebouncedEndDate(savedEndDate);
    } else {
      const defaultEndDate = new Date().toISOString().split('T')[0]; // "YYYY-MM-DD"
      setFilterEndDate(defaultEndDate);
      setDebouncedEndDate(defaultEndDate);
      localStorage.setItem('contracts_filterEndDate', defaultEndDate);
    }

    loadFilters();
    setIsInitializing(false);
  }, [loadFilters]);
  
  // Debounce all filters
  useEffect(() => {
    const timer = setTimeout(() => {
      setDebouncedUserIds(filterUserIds);
      setDebouncedStartDate(filterStartDate);
      setDebouncedEndDate(filterEndDate);
      setDebouncedContractNumber(filterContractNumber);
      setDebouncedShowUnassigned(filterShowUnassigned);
      setDebouncedMatricula(filterMatricula);
      setDebouncedTeamIds(filterTeamIds);
    }, 500); // 500ms debounce for all fields

    return () => clearTimeout(timer);
  }, [filterUserIds, filterStartDate, filterEndDate, filterContractNumber, filterShowUnassigned, filterMatricula, filterTeamIds]);

  useEffect(() => {
    if (isInitializing) return;
    loadContracts();
  }, [isInitializing, loadContracts]);

  // Reset to page 1 when filters change (using debounced values to avoid flickering)
  useEffect(() => {
    setCurrentPage(1);
  }, [debouncedUserIds, debouncedStartDate, debouncedEndDate, debouncedContractNumber, debouncedShowUnassigned, debouncedMatricula, debouncedTeamIds]);

  // Calculate pagination
  const totalPages = Math.ceil(totalCount / pageSize);
  const paginatedContracts = contracts;

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



  return (
    <Menu>
      <div className="contracts-page">
          <div className="contracts-header">
            <Title order={2} size="h2">Gerenciamento de Contratos</Title>
            <div style={{ display: 'flex', gap: '10px', alignItems: 'center' }}>
              <ExportButton
                onExport={async () => {
                  setIsExporting(true);
                  try {
                    const job = await apiService.startContractExport({
                      startDate: debouncedStartDate || undefined,
                      endDate: debouncedEndDate || undefined,
                      contractNumber: debouncedContractNumber || undefined,
                      showUnassigned: debouncedShowUnassigned === 'unassigned' ? true : debouncedShowUnassigned === 'assigned' ? false : undefined,
                      matricula: debouncedMatricula || undefined,
                      teamIds: debouncedTeamIds.length > 0 ? debouncedTeamIds.map(id => parseInt(id)) : undefined,
                      userIds: debouncedUserIds.length > 0 ? debouncedUserIds : undefined,
                    });
                    setExportJobId(job.jobId);
                  } catch (e: any) {
                    toast.error(e.message || 'Falha ao iniciar exportação');
                    setIsExporting(false);
                  }
                }}
                isExporting={isExporting}
              />
              <Button onClick={() => setShowColumnsModal(true)} variant="default" leftSection={<IconSettings size={16} />}>
                Colunas
              </Button>
              <Button onClick={() => setShowImportModal(true)} leftSection={<IconUpload size={16} />}>
                Importar
              </Button>
              <Button onClick={handleCreateClick} leftSection={<IconPlus size={16} />}>
                Criar
              </Button>
            </div>
          </div>

          <ExportProgressIndicator
            jobId={exportJobId}
            pollUrl={apiService.contractExportPollUrl.bind(apiService)}
            downloadUrl={apiService.contractExportDownloadUrl.bind(apiService)}
            token={exportToken}
            onComplete={() => { setIsExporting(false); setExportJobId(null); }}
            onError={(msg) => { toast.error(msg); setIsExporting(false); setExportJobId(null); }}
          />

          {error && <div className="contracts-error">{error}</div>}

          <div className="contracts-filters">
        <div className="filter-group">
          <label htmlFor="filterUsers">Usuários</label>
          <MultiSelect
            id="filterUsers"
            placeholder={users.length === 0 ? 'Nenhum usuário disponível' : 'Selecionar usuários...'}
            value={filterUserIds}
            onChange={setFilterUserIds}
            data={users.map(u => ({ value: u.id, label: u.email ? `${u.name} (${u.email})` : u.name }))}
            clearable
            searchable
            styles={{ input: { minHeight: '36px' } }}
          />
        </div>

        <div className="filter-group">
          <label htmlFor="filterShowUnassigned">Vínculo de Usuário</label>
          <select
            id="filterShowUnassigned"
            value={filterShowUnassigned}
            onChange={(e) => setFilterShowUnassigned(e.target.value)}
          >
            <option value="all">Todos</option>
            <option value="assigned">Vinculados</option>
            <option value="unassigned">Não Vinculados</option>
          </select>
        </div>

        <div className="filter-group">
          <label htmlFor="filterTeam">Time</label>
          <MultiSelect
            id="filterTeam"
            placeholder={teams.length === 0 ? 'Nenhum time disponível' : 'Selecionar times...'}
            value={filterTeamIds}
            onChange={setFilterTeamIds}
            data={teams.map(t => ({ value: String(t.id), label: t.name }))}
            clearable
            searchable
            styles={{ input: { minHeight: '36px' } }}
          />
        </div>

        <div className="filter-group">
          <label htmlFor="filterMatricula">Matrícula</label>
          <input
            type="text"
            id="filterMatricula"
            value={filterMatricula}
            onChange={(e) => setFilterMatricula(e.target.value)}
            placeholder="Filtrar por matrícula..."
          />
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
            onChange={(e) => {
              const value = e.target.value;
              setFilterEndDate(value);
              if (value) {
                localStorage.setItem('contracts_filterEndDate', value);
              } else {
                localStorage.removeItem('contracts_filterEndDate');
              }
            }}
          />
          {filterStartDate && filterEndDate && filterEndDate < filterStartDate && (
            <span className="filter-error-msg" style={{ color: '#ef4444', fontSize: '12px', marginTop: '4px' }}>
              Data fim deve ser maior ou igual à data início
            </span>
          )}
        </div>


        {(filterUserIds.length > 0 || filterStartDate || filterEndDate || filterContractNumber || filterMatricula || filterShowUnassigned !== 'all' || filterTeamIds.length > 0) && (
          <button
            className="clear-filters-btn"
            onClick={() => {
              setFilterUserIds([]);
              setDebouncedUserIds([]);
              setFilterStartDate('');
              setDebouncedStartDate('');
              setFilterEndDate('');
              setDebouncedEndDate('');
              setFilterContractNumber('');
              setDebouncedContractNumber('');
              setFilterMatricula('');
              setDebouncedMatricula('');
              setFilterShowUnassigned('all');
              setDebouncedShowUnassigned('all');
              setFilterTeamIds([]);
              setDebouncedTeamIds([]);
              localStorage.removeItem('contracts_filterStartDate');
              localStorage.removeItem('contracts_filterEndDate');
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
            totalItems={totalCount}
            onPageChange={setCurrentPage}
            onPageSizeChange={handlePageSizeChange}
            showBottomControls={false}
          />

          <div className="contracts-table-container">
            <Table striped highlightOnHover>
            <Table.Thead>
              <Table.Tr>
                {visibleColumns.contractNumber && <Table.Th>Número do Contrato</Table.Th>}
                {visibleColumns.user && <Table.Th>Usuário</Table.Th>}
                {visibleColumns.matricula && <Table.Th>Matrícula</Table.Th>}
                {visibleColumns.group && <Table.Th>Grupo</Table.Th>}
                {visibleColumns.customer && <Table.Th>Cliente</Table.Th>}
                {visibleColumns.totalAmount && <Table.Th>Valor Total</Table.Th>}
                {visibleColumns.status && <Table.Th>Status</Table.Th>}
                {visibleColumns.startDate && <Table.Th>Data Início</Table.Th>}
                <Table.Th>Ações</Table.Th>
              </Table.Tr>
            </Table.Thead>
            <Table.Tbody>
              {paginatedContracts.map((contract) => (
                <Table.Tr key={contract.id}>
                  {visibleColumns.contractNumber && <Table.Td>{contract.contractNumber}</Table.Td>}
                  {visibleColumns.user && <Table.Td>{contract.userName}</Table.Td>}
                  {visibleColumns.matricula && <Table.Td>{contract.matriculaNumber || '-'}</Table.Td>}
                  {visibleColumns.group && <Table.Td>{contract.groupName}</Table.Td>}
                  {visibleColumns.customer && <Table.Td>{contract.customerName || '-'}</Table.Td>}
                  {visibleColumns.totalAmount && <Table.Td>{formatCurrency(contract.totalAmount)}</Table.Td>}
                  {visibleColumns.status && (
                    <Table.Td>
                      <ContractStatusBadge status={contract.status} />
                    </Table.Td>
                  )}
                  {visibleColumns.startDate && <Table.Td>{formatDate(contract.contractStartDate)}</Table.Td>}
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
          totalItems={totalCount}
          onPageChange={setCurrentPage}
          onPageSizeChange={handlePageSizeChange}
          showTopControls={false}
        />
      </>
      )}

      {/* Aggregation Summary */}
      {aggregation && totalCount > 0 && (
        <AggregationSummary
          total={aggregation?.total || 0}
          totalCancel={aggregation?.totalCancel || 0}
          totalActive={aggregation?.totalActive || 0}
          totalLate={aggregation?.totalLate || 0}
          retention={aggregation?.retention || 0}
          strictRetention={aggregation?.strictRetention || 0}
        />
      )}

      {/* Historic Production */}
      {totalCount > 0 && (
        <HistoricProduction
          startDate={filterStartDate}
          endDate={debouncedEndDate || undefined}
          showUnassigned={filterShowUnassigned === 'unassigned' ? true : filterShowUnassigned === 'assigned' ? false : undefined}
        />
      )}

      {/* Select Columns Modal */}
      <StandardModal
        isOpen={showColumnsModal}
        onClose={() => setShowColumnsModal(false)}
        title="Selecionar Colunas"
        size="md"
        footer={
          <>
            <Button variant="default" onClick={() => {
              setVisibleColumns(DEFAULT_COLUMNS);
              localStorage.setItem('contracts_visibleColumns', JSON.stringify(DEFAULT_COLUMNS));
            }}>
              Restaurar Padrão
            </Button>
            <Button onClick={() => setShowColumnsModal(false)}>
              Concluir
            </Button>
          </>
        }
      >
        <div style={{ display: 'flex', flexDirection: 'column', gap: '12px' }}>
          <Checkbox
            label="Número do Contrato"
            checked={visibleColumns.contractNumber}
            onChange={(e) => handleColumnToggle('contractNumber', e.currentTarget.checked)}
          />
          <Checkbox
            label="Usuário"
            checked={visibleColumns.user}
            onChange={(e) => handleColumnToggle('user', e.currentTarget.checked)}
          />
          <Checkbox
            label="Matrícula"
            checked={visibleColumns.matricula}
            onChange={(e) => handleColumnToggle('matricula', e.currentTarget.checked)}
          />
          <Checkbox
            label="Grupo"
            checked={visibleColumns.group}
            onChange={(e) => handleColumnToggle('group', e.currentTarget.checked)}
          />
          <Checkbox
            label="Cliente"
            checked={visibleColumns.customer}
            onChange={(e) => handleColumnToggle('customer', e.currentTarget.checked)}
          />
          <Checkbox
            label="Valor Total"
            checked={visibleColumns.totalAmount}
            onChange={(e) => handleColumnToggle('totalAmount', e.currentTarget.checked)}
          />
          <Checkbox
            label="Status"
            checked={visibleColumns.status}
            onChange={(e) => handleColumnToggle('status', e.currentTarget.checked)}
          />
          <Checkbox
            label="Data Início"
            checked={visibleColumns.startDate}
            onChange={(e) => handleColumnToggle('startDate', e.currentTarget.checked)}
          />
        </div>
      </StandardModal>

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
          templateId={3}
          title="Importar Contratos em Lote"
        />
      )}

      <StandardModal
        isOpen={deleteConfirm !== null}
        onClose={() => setDeleteConfirm(null)}
        title="Confirmar Exclusão"
        size="md"
        footer={
          <>
            <Button variant="default" onClick={() => setDeleteConfirm(null)}>
              Cancelar
            </Button>
            <Button
              color="red"
              onClick={handleDeleteConfirm}
            >
              Excluir
            </Button>
          </>
        }
      >
        <div style={{ padding: '10px 0' }}>
          <Text size="sm">Tem certeza que deseja excluir este contrato?</Text>
        </div>
      </StandardModal>
      </div>
    </Menu>
  );
};

export default ContractsPage;
