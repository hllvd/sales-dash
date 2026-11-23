import React, { useState, useEffect, useCallback, useRef } from 'react';
import { Title, Table, ActionIcon, Group, Text, MultiSelect, Tooltip } from '@mantine/core';
import { IconRefresh } from '@tabler/icons-react';
import './MonitoringContractsPage.css';
import Menu from '../Menu';
import Pagination from '../Pagination';
import ContractStatusBadge from '../../shared/ContractStatusBadge';
import { Team } from '../../services/apiService';
import { useReferenceData } from '../../contexts/ReferenceDataContext';
import { toast } from '../../utils/toast';
import { normalizeName } from '../../utils/normalization';
import {
  Contract,
  User as ContractUser,
  ContractUpdateSummary,
  getContractUpdateSummary,
  getMonitoringContracts,
} from '../../services/contractService';
import { getSegmentLabel } from '../../types/Segment';

const MULTI_SELECT_STYLES = {
  input: { minHeight: '36px' },
  pill: { maxWidth: '100%' },
  pillLabel: { overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' },
};

interface FilterOptionConfig {
  value: string;
  label: string;
  summaryKey: keyof ContractUpdateSummary;
  color: string;
}

const UPDATE_FILTER_OPTIONS: FilterOptionConfig[] = [
  { value: 'all', label: 'Todos', summaryKey: 'total', color: '#6366f1' },
  { value: 'moreThan1Hour', label: '> 1 hora', summaryKey: 'moreThan1Hour', color: '#10b981' },
  { value: 'moreThan3Hours', label: '> 3 horas', summaryKey: 'moreThan3Hours', color: '#3b82f6' },
  { value: 'moreThan6Hours', label: '> 6 horas', summaryKey: 'moreThan6Hours', color: '#f59e0b' },
  { value: 'moreThan1Day', label: '> 1 dia', summaryKey: 'moreThan1Day', color: '#f97316' },
  { value: 'moreThan3Days', label: '> 3 dias', summaryKey: 'moreThan3Days', color: '#ea580c' },
  { value: 'moreThan1Week', label: '> 1 semana', summaryKey: 'moreThan1Week', color: '#ef4444' },
];

const MonitoringContractsPage: React.FC = () => {
  const requestCountRef = useRef(0);
  const { fetchTeams, fetchContractFilterUsers } = useReferenceData();

  const [contracts, setContracts] = useState<Contract[]>([]);
  const [users, setUsers] = useState<ContractUser[]>([]);
  const [teams, setTeams] = useState<Team[]>([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [summary, setSummary] = useState<ContractUpdateSummary>({
    moreThan1Hour: 0,
    moreThan3Hours: 0,
    moreThan6Hours: 0,
    moreThan1Day: 0,
    moreThan3Days: 0,
    moreThan1Week: 0,
    total: 0,
  });

  // Filters
  const [updateFilter, setUpdateFilter] = useState<string>('all');
  const [filterStartDate, setFilterStartDate] = useState('');
  const [filterEndDate, setFilterEndDate] = useState('');
  const [filterUserIds, setFilterUserIds] = useState<string[]>([]);
  const [filterTeamIds, setFilterTeamIds] = useState<string[]>([]);
  const [filterMatriculas, setFilterMatriculas] = useState<string[]>([]);
  const [matriculaSearch, setMatriculaSearch] = useState('');

  // Pagination
  const [currentPage, setCurrentPage] = useState(1);
  const [pageSize, setPageSize] = useState(100);
  const [totalCount, setTotalCount] = useState(0);

  // Load Reference Data
  useEffect(() => {
    fetchTeams().then(teamsData => {
      setTeams(teamsData);
    }).catch(err => {
      console.error('Falha ao carregar equipes:', err);
    });

    fetchContractFilterUsers().then(usersData => {
      setUsers(usersData);
    }).catch(err => {
      console.error('Falha ao carregar usuários:', err);
    });
  }, [fetchTeams, fetchContractFilterUsers]);

  // Load Summary Counts
  const loadSummary = useCallback(async () => {
    try {
      const summaryData = await getContractUpdateSummary({
        teamIds: filterTeamIds.length > 0 ? filterTeamIds.map(id => parseInt(id, 10)) : undefined,
        userIds: filterUserIds.length > 0 ? filterUserIds : undefined,
        matriculas: filterMatriculas.length > 0 ? filterMatriculas : undefined,
        startDate: filterStartDate || undefined,
        endDate: filterEndDate || undefined,
      });
      setSummary(summaryData);
    } catch (err: any) {
      console.error('Falha ao carregar resumo de atualização:', err);
    }
  }, [filterTeamIds, filterUserIds, filterMatriculas, filterStartDate, filterEndDate]);

  // Load Contracts List
  const loadContracts = useCallback(async () => {
    setLoading(true);
    const requestId = ++requestCountRef.current;

    try {
      const result = await getMonitoringContracts({
        updateFilter: updateFilter !== 'all' ? updateFilter : undefined,
        teamIds: filterTeamIds.length > 0 ? filterTeamIds.map(id => parseInt(id, 10)) : undefined,
        userIds: filterUserIds.length > 0 ? filterUserIds : undefined,
        matriculas: filterMatriculas.length > 0 ? filterMatriculas : undefined,
        startDate: filterStartDate || undefined,
        endDate: filterEndDate || undefined,
        page: currentPage,
        pageSize,
      });

      if (requestId !== requestCountRef.current) return;
      setContracts(result.contracts);
      setTotalCount(result.totalCount);
    } catch (err: any) {
      if (requestId !== requestCountRef.current) return;
      toast.error(err.message || 'Falha ao carregar contratos');
      setContracts([]);
      setTotalCount(0);
    } finally {
      if (requestId === requestCountRef.current) {
        setLoading(false);
        setRefreshing(false);
      }
    }
  }, [updateFilter, filterTeamIds, filterUserIds, filterMatriculas, filterStartDate, filterEndDate, currentPage, pageSize]);

  // Trigger loading when filters change
  useEffect(() => {
    loadSummary();
  }, [loadSummary]);

  useEffect(() => {
    loadContracts();
  }, [loadContracts]);

  const handleRefresh = async () => {
    setRefreshing(true);
    await Promise.all([loadSummary(), loadContracts()]);
  };

  const handleClearFilters = () => {
    setUpdateFilter('all');
    setFilterStartDate('');
    setFilterEndDate('');
    setFilterTeamIds([]);
    setFilterUserIds([]);
    setFilterMatriculas([]);
    setMatriculaSearch('');
    setCurrentPage(1);
  };

  const handleCardClick = (filterValue: string) => {
    setUpdateFilter(filterValue);
    setCurrentPage(1);
  };

  const handlePageSizeChange = (newSize: number) => {
    setPageSize(newSize);
    setCurrentPage(1);
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
    return isNaN(date.getTime()) ? '-' : date.toLocaleDateString('pt-BR');
  };

  const renderLastUpdated = (dateString: string | null) => {
    if (!dateString) return '-';
    const date = new Date(dateString);
    if (isNaN(date.getTime())) return '-';

    const now = new Date();
    const diffMs = now.getTime() - date.getTime();
    const diffDays = Math.floor(diffMs / (1000 * 60 * 60 * 24));

    let dotColor = '#10b981'; // green (< 24h)
    if (diffDays >= 7) {
      dotColor = '#ef4444'; // red (> 7 days)
    } else if (diffDays >= 3) {
      dotColor = '#f97316'; // orange (3-7 days)
    } else if (diffDays >= 1) {
      dotColor = '#3b82f6'; // blue (1-3 days)
    }

    let relativeText = 'há instantes';
    if (diffDays > 365) {
      const years = Math.floor(diffDays / 365);
      relativeText = years === 1 ? 'há 1 ano' : `há ${years} anos`;
    } else if (diffDays >= 30) {
      const months = Math.floor(diffDays / 30);
      relativeText = months === 1 ? 'há 1 mês' : `há ${months} meses`;
    } else if (diffDays >= 7) {
      const weeks = Math.floor(diffDays / 7);
      relativeText = weeks === 1 ? 'há 1 semana' : `há ${weeks} semanas`;
    } else if (diffDays >= 1) {
      relativeText = diffDays === 1 ? 'há 1 dia' : `há ${diffDays} dias`;
    } else {
      const diffHours = Math.floor(diffMs / (1000 * 60 * 60));
      if (diffHours >= 1) {
        relativeText = diffHours === 1 ? 'há 1 hora' : `há ${diffHours} horas`;
      } else {
        const diffMins = Math.floor(diffMs / (1000 * 60));
        if (diffMins >= 1) {
          relativeText = diffMins === 1 ? 'há 1 min' : `há ${diffMins} min`;
        }
      }
    }

    const fullDateStr = date.toLocaleString('pt-BR');

    return (
      <span title={fullDateStr} style={{ display: 'inline-flex', alignItems: 'center', gap: '6px' }}>
        <span
          style={{
            display: 'inline-block',
            width: '8px',
            height: '8px',
            borderRadius: '50%',
            backgroundColor: dotColor,
            flexShrink: 0,
          }}
        />
        <span>{relativeText}</span>
      </span>
    );
  };

  const totalPages = Math.ceil(totalCount / pageSize) || 1;
  const isFiltered = updateFilter !== 'all' || filterTeamIds.length > 0 || filterUserIds.length > 0 || filterMatriculas.length > 0 || Boolean(filterStartDate) || Boolean(filterEndDate);

  return (
    <Menu>
      <div className="monitoring-contracts-page">
        <div className="monitoring-contracts-header">
          <div>
            <Title order={2}>Monitoramento de Contratos</Title>
            <Text className="monitoring-contracts-subtitle">
              Acompanhe a recência de atualização dos contratos para validar a cobertura do scrape
            </Text>
          </div>
          <Group className="monitoring-header-actions">
            <Tooltip label="Atualizar dados">
              <ActionIcon
                variant="light"
                size="lg"
                onClick={handleRefresh}
                loading={refreshing}
                color="indigo"
              >
                <IconRefresh size={20} />
              </ActionIcon>
            </Tooltip>
          </Group>
        </div>

        {/* Quick Filter Summary Cards Grid */}
        <div className="update-cards-grid">
          {UPDATE_FILTER_OPTIONS.map(opt => {
            const count = summary[opt.summaryKey] ?? 0;
            const isActive = updateFilter === opt.value;
            return (
              <div
                key={opt.value}
                className={`update-filter-card ${isActive ? 'active' : ''}`}
                onClick={() => handleCardClick(opt.value)}
                data-testid={`card-filter-${opt.value}`}
              >
                <div className="update-filter-card-header">
                  <span className="update-filter-card-label">{opt.label}</span>
                  <span
                    className="update-filter-card-indicator"
                    style={{ backgroundColor: opt.color }}
                  />
                </div>
                <div className="update-filter-card-count">
                  {count.toLocaleString('pt-BR')}
                </div>
              </div>
            );
          })}
        </div>

        {/* Filters Bar */}
        <div className="monitoring-filters">
          <div className="filter-group">
            <label htmlFor="selectUpdateFilter">Última Atualização</label>
            <select
              id="selectUpdateFilter"
              value={updateFilter}
              onChange={(e) => {
                setUpdateFilter(e.target.value);
                setCurrentPage(1);
              }}
            >
              {UPDATE_FILTER_OPTIONS.map(opt => (
                <option key={opt.value} value={opt.value}>
                  {opt.label} ({summary[opt.summaryKey]?.toLocaleString('pt-BR') ?? 0})
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
              onChange={(e) => {
                setFilterStartDate(e.target.value);
                setCurrentPage(1);
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
                setFilterEndDate(e.target.value);
                setCurrentPage(1);
              }}
            />
          </div>

          <div className="filter-group">
            <label htmlFor="filterTeam">Equipe</label>
            <MultiSelect
              id="filterTeam"
              placeholder={teams.length === 0 ? 'Nenhuma equipe disponível' : 'Selecionar equipes...'}
              value={filterTeamIds}
              onChange={(vals) => {
                setFilterTeamIds(vals);
                setCurrentPage(1);
              }}
              data={teams.map(t => ({ value: String(t.id), label: t.name }))}
              clearable
              searchable
              styles={MULTI_SELECT_STYLES}
            />
          </div>

          <div className="filter-group">
            <label htmlFor="filterMatricula">Matrícula</label>
            <MultiSelect
              id="filterMatricula"
              placeholder="Digitar matrícula e pressionar Enter..."
              value={filterMatriculas}
              onChange={(vals) => {
                setFilterMatriculas(vals);
                setCurrentPage(1);
              }}
              searchValue={matriculaSearch}
              onSearchChange={setMatriculaSearch}
              data={filterMatriculas}
              onKeyDown={(e) => {
                if (e.key === 'Enter' && matriculaSearch.trim()) {
                  e.preventDefault();
                  const val = matriculaSearch.trim();
                  if (!filterMatriculas.includes(val)) {
                    setFilterMatriculas([...filterMatriculas, val]);
                    setCurrentPage(1);
                  }
                  setMatriculaSearch('');
                }
              }}
              clearable
              searchable
              styles={MULTI_SELECT_STYLES}
            />
          </div>

          <div className="filter-group">
            <label htmlFor="filterUser">Usuário</label>
            <MultiSelect
              id="filterUser"
              placeholder={users.length === 0 ? 'Carregando usuários...' : 'Selecionar usuários...'}
              value={filterUserIds}
              onChange={(vals) => {
                setFilterUserIds(vals);
                setCurrentPage(1);
              }}
              data={users.map(u => ({
                value: u.id,
                label: normalizeName(u.name) || u.email,
              }))}
              clearable
              searchable
              styles={MULTI_SELECT_STYLES}
            />
          </div>

          {isFiltered && (
            <button
              className="clear-filters-btn"
              onClick={handleClearFilters}
              type="button"
            >
              Limpar Filtros
            </button>
          )}
        </div>

        {/* Content Area */}
        {loading ? (
          <div className="monitoring-contracts-loading">
            <div className="spinner" />
            <p>Carregando contratos...</p>
          </div>
        ) : contracts.length === 0 ? (
          <div className="monitoring-contracts-empty">
            <p>Nenhum contrato encontrado para os filtros selecionados.</p>
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

            <div className="monitoring-contracts-table-container">
              <Table striped highlightOnHover>
                <Table.Thead>
                  <Table.Tr>
                    <Table.Th>Número do Contrato</Table.Th>
                    <Table.Th>Usuário</Table.Th>
                    <Table.Th>Equipe</Table.Th>
                    <Table.Th>Matrícula</Table.Th>
                    <Table.Th>Grupo</Table.Th>
                    <Table.Th>Segmento</Table.Th>
                    <Table.Th>Cota</Table.Th>
                    <Table.Th>Cliente</Table.Th>
                    <Table.Th>Valor Total</Table.Th>
                    <Table.Th>Status</Table.Th>
                    <Table.Th>Data Início</Table.Th>
                    <Table.Th>Data da Última Atualização</Table.Th>
                    <Table.Th>Aguardando Pagamento</Table.Th>
                  </Table.Tr>
                </Table.Thead>
                <Table.Tbody>
                  {contracts.map(contract => (
                    <Table.Tr key={contract.id}>
                      <Table.Td>{contract.contractNumber}</Table.Td>
                      <Table.Td>{normalizeName(contract.userName) || '-'}</Table.Td>
                      <Table.Td>{contract.teamName || '-'}</Table.Td>
                      <Table.Td>{contract.matriculaNumber || '-'}</Table.Td>
                      <Table.Td>{contract.groupName || '-'}</Table.Td>
                      <Table.Td>{contract.segmentName || getSegmentLabel(contract.segment) || '-'}</Table.Td>
                      <Table.Td>{contract.quota || '-'}</Table.Td>
                      <Table.Td>{normalizeName(contract.customerName) || '-'}</Table.Td>
                      <Table.Td>{formatCurrency(contract.totalAmount)}</Table.Td>
                      <Table.Td>
                        <ContractStatusBadge
                          status={contract.status}
                          rawStatus={contract.rawStatus}
                          isRemappedToAwaitingPayment={contract.isRemappedToAwaitingPayment}
                        />
                      </Table.Td>
                      <Table.Td>{formatDate(contract.contractStartDate)}</Table.Td>
                      <Table.Td>{renderLastUpdated(contract.updatedAt)}</Table.Td>
                      <Table.Td>{contract.isAwaitingPayment ? 'Sim' : 'Não'}</Table.Td>
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
      </div>
    </Menu>
  );
};

export default MonitoringContractsPage;
