import React, { useState, useEffect } from 'react';
import { Title, Button, Table, Badge, TextInput, Select } from '@mantine/core';
import './MyContractsPage.css';
import Menu from './Menu';
import StyledModal from './StyledModal';
import FormField from './FormField';
import AggregationSummary from '../shared/AggregationSummary';
import HistoricProduction from '../shared/HistoricProduction';
import {
  Contract,
  ContractAggregation,
  ContractStatus,
  getContracts,
  getUserContracts,
  getContractByNumber,
  assignContract,
} from '../services/contractService';
import { apiService, UserMatricula } from '../services/apiService';

const MyContractsPage: React.FC = () => {
  const [contracts, setContracts] = useState<Contract[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [aggregation, setAggregation] = useState<ContractAggregation | null>(null);
  const [showAssignModal, setShowAssignModal] = useState(false);
  
  // Date filter state
  const [startDate, setStartDate] = useState<string>('');
  const [endDate, setEndDate] = useState<string>('');
  
  // Contract assignment state
  const [contractNumber, setContractNumber] = useState('');
  const [retrievedContract, setRetrievedContract] = useState<Contract | null>(null);
  const [assignLoading, setAssignLoading] = useState(false);
  const [assignError, setAssignError] = useState('');
  const [userMatriculas, setUserMatriculas] = useState<UserMatricula[]>([]);
  const [selectedMatricula, setSelectedMatricula] = useState<string>('');

  // Load saved date filters from localStorage
  useEffect(() => {
    const savedStart = localStorage.getItem('myContracts_startDate');
    const savedEnd = localStorage.getItem('myContracts_endDate');
    if (savedStart) setStartDate(savedStart);
    if (savedEnd) setEndDate(savedEnd);
  }, []);

  useEffect(() => {
    loadMyContracts();
  }, []);

  const loadMyContracts = async () => {
    setLoading(true);
    setError('');

    try {
      // Get current user ID from localStorage
      const user = JSON.parse(localStorage.getItem('user') || '{}');
      const userId = user.id;

      if (!userId) {
        setError('Usuário não autenticado');
        return;
      }

      // Load contracts for current user with date filters
      const { contracts: data, aggregation: aggData } = await getUserContracts(
        userId,
        startDate || undefined,
        endDate || undefined
      );
      setContracts(data);
      setAggregation(aggData || null);
    } catch (err: any) {
      setError(err.message || 'Falha ao carregar contratos');
    } finally {
      setLoading(false);
    }
  };

  const handleNewClick = async () => {
    setContractNumber('');
    setRetrievedContract(null);
    setAssignError('');
    setSelectedMatricula('');
    
    // Fetch user's active matriculas
    try {
      const user = JSON.parse(localStorage.getItem('user') || '{}');
      const userId = user.id;
      if (userId) {
        const response = await apiService.getUserMatriculas(userId);
        if (response.success && response.data) {
          // Filter active matriculas
          const now = new Date();
          const activeMatriculas = response.data.filter(m => 
            m.isActive && (!m.endDate || new Date(m.endDate) > now)
          );
          // Sort by most recent (startDate descending)
          activeMatriculas.sort((a, b) => 
            new Date(b.startDate).getTime() - new Date(a.startDate).getTime()
          );
          setUserMatriculas(activeMatriculas);
          // Auto-select if only one
          if (activeMatriculas.length === 1) {
            setSelectedMatricula(activeMatriculas[0].matriculaNumber);
          } else if (activeMatriculas.length > 1) {
            // Default to most recent
            setSelectedMatricula(activeMatriculas[0].matriculaNumber);
          }
        }
      }
    } catch (err) {
      console.error('Failed to fetch matriculas:', err);
    }
    
    setShowAssignModal(true);
  };

  const handleRetrieveContract = async () => {
    if (!contractNumber.trim()) {
      setAssignError('Por favor, insira um número de contrato');
      return;
    }

    setAssignLoading(true);
    setAssignError('');

    try {
      const contract = await getContractByNumber(contractNumber);
      setRetrievedContract(contract);
    } catch (err: any) {
      setAssignError(err.message || 'Contrato não encontrado');
      setRetrievedContract(null);
    } finally {
      setAssignLoading(false);
    }
  };

  const handleConfirmAssignment = async () => {
    if (!contractNumber.trim()) return;

    setAssignLoading(true);
    setAssignError('');

    try {
      // Pass selected matricula if available
      await assignContract(contractNumber, selectedMatricula || undefined);
      setShowAssignModal(false);
      setContractNumber('');
      setRetrievedContract(null);
      setSelectedMatricula('');
      loadMyContracts(); // Refresh the list
    } catch (err: any) {
      setAssignError(err.message || 'Falha ao atribuir contrato');
    } finally {
      setAssignLoading(false);
    }
  };

  const handleStartDateChange = (value: string) => {
    setStartDate(value);
    if (value) {
      localStorage.setItem('myContracts_startDate', value);
    } else {
      localStorage.removeItem('myContracts_startDate');
    }
  };

  const handleEndDateChange = (value: string) => {
    setEndDate(value);
    if (value) {
      localStorage.setItem('myContracts_endDate', value);
    } else {
      localStorage.removeItem('myContracts_endDate');
    }
  };

  const handleApplyFilters = () => {
    loadMyContracts();
  };

  const handleClearFilters = () => {
    setStartDate('');
    setEndDate('');
    localStorage.removeItem('myContracts_startDate');
    localStorage.removeItem('myContracts_endDate');
    // Reload without filters
    setTimeout(() => loadMyContracts(), 0);
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
    <Menu>
      <div className="my-contracts-page">
          <div className="my-contracts-header">
            <Title order={2} size="h2" className="page-title-break">Meus Contratos</Title>
            <Button onClick={handleNewClick} leftSection="+">
              Novo
            </Button>
          </div>

          {error && <div className="my-contracts-error">{error}</div>}

          {/* Date Filters */}
          {!loading && contracts.length > 0 && (
            <div className="date-filters">
              <div className="filter-group">
                <label htmlFor="startDate">Data Início:</label>
                <input
                  id="startDate"
                  type="date"
                  value={startDate}
                  onChange={(e) => handleStartDateChange(e.target.value)}
                />
              </div>
              <div className="filter-group">
                <label htmlFor="endDate">Data Fim:</label>
                <input
                  id="endDate"
                  type="date"
                  value={endDate}
                  onChange={(e) => handleEndDateChange(e.target.value)}
                />
              </div>
              <div className="filter-actions">
                <Button onClick={handleApplyFilters} size="sm">
                  Aplicar Filtros
                </Button>
                {(startDate || endDate) && (
                  <Button onClick={handleClearFilters} variant="subtle" size="sm">
                    Limpar Filtros
                  </Button>
                )}
              </div>
            </div>
          )}

          {loading ? (
            <div className="my-contracts-loading">
              <div className="spinner"></div>
              <p>Carregando contratos...</p>
            </div>
          ) : contracts.length === 0 ? (
            <div className="my-contracts-empty">
              <p>Você ainda não possui contratos atribuídos.</p>
              <Button onClick={handleNewClick}>
                Atribuir Primeiro Contrato
              </Button>
            </div>
          ) : contracts.length === 0 ? (
            <div className="my-contracts-empty">
              <p>Nenhum contrato encontrado para o período selecionado.</p>
              <Button onClick={handleClearFilters}>
                Limpar Filtros
              </Button>
            </div>
          ) : (
            <div className="my-contracts-table-container">
              <Table striped highlightOnHover>
                <Table.Thead>
                  <Table.Tr>
                    <Table.Th>Número do Contrato</Table.Th>
                    <Table.Th>Cliente</Table.Th>
                    <Table.Th>Matrícula</Table.Th>
                    <Table.Th>Grupo</Table.Th>
                    <Table.Th>Valor Total</Table.Th>
                    <Table.Th>Status</Table.Th>
                    <Table.Th>Data Início</Table.Th>
                  </Table.Tr>
                </Table.Thead>
                <Table.Tbody>
                  {contracts.map((contract) => (
                    <Table.Tr key={contract.id}>
                      <Table.Td>{contract.contractNumber}</Table.Td>
                      <Table.Td>{contract.customerName || '-'}</Table.Td>
                      <Table.Td>{contract.matriculaNumber || '-'}</Table.Td>
                      <Table.Td>{contract.groupName}</Table.Td>
                      <Table.Td>{formatCurrency(contract.totalAmount)}</Table.Td>
                      <Table.Td>
                        <Badge 
                          color={
                            contract.status === ContractStatus.Active ? 'green' :
                            contract.status === ContractStatus.Defaulted ? 'red' :
                            contract.status.startsWith('Late') ? 'orange' : 'gray'
                          }
                        >
                          {getStatusLabel(contract.status)}
                        </Badge>
                      </Table.Td>
                      <Table.Td>{formatDate(contract.contractStartDate)}</Table.Td>
                    </Table.Tr>
                  ))}
                </Table.Tbody>
              </Table>
            </div>
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
            startDate={startDate}
            endDate={endDate}
            userId={JSON.parse(localStorage.getItem('user') || '{}').id}
          />
        )}
        </div>

        


      {/* Assignment Modal */}
      {showAssignModal && (
        <StyledModal
          opened={true}
          onClose={() => setShowAssignModal(false)}
          title="Atribuir Contrato"
          size="md"
        >
          {assignError && <div style={{ color: 'red', marginBottom: '1rem' }}>{assignError}</div>}

          {!retrievedContract ? (
            <>
              <FormField label="Número do Contrato" required>
                <TextInput
                  required
                  value={contractNumber}
                  onChange={(e) => setContractNumber(e.target.value)}
                  placeholder="Digite o número do contrato"
                  onKeyPress={(e) => {
                    if (e.key === 'Enter') {
                      handleRetrieveContract();
                    }
                  }}
                />
              </FormField>

              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '8px', marginTop: '1.5rem' }}>
                <Button
                  variant="default"
                  onClick={() => setShowAssignModal(false)}
                  disabled={assignLoading}
                >
                  Cancelar
                </Button>
                <Button
                  onClick={handleRetrieveContract}
                  disabled={!contractNumber.trim() || assignLoading}
                  loading={assignLoading}
                >
                  Buscar Contrato
                </Button>
              </div>
            </>
          ) : (
            <>
              <div style={{ marginBottom: '1.5rem' }}>
                <h3 style={{ color: 'white', marginBottom: '1rem' }}>Detalhes do Contrato</h3>
                <div style={{ display: 'flex', flexDirection: 'column', gap: '0.5rem' }}>
                  <div style={{ display: 'flex', justifyContent: 'space-between' }}>
                    <span style={{ color: '#a0a0a0' }}>Número:</span>
                    <span style={{ color: 'white' }}>{retrievedContract.contractNumber}</span>
                  </div>
                  <div style={{ display: 'flex', justifyContent: 'space-between' }}>
                    <span style={{ color: '#a0a0a0' }}>Cliente:</span>
                    <span style={{ color: 'white' }}>{retrievedContract.customerName || '-'}</span>
                  </div>
                  <div style={{ display: 'flex', justifyContent: 'space-between' }}>
                    <span style={{ color: '#a0a0a0' }}>Grupo:</span>
                    <span style={{ color: 'white' }}>{retrievedContract.groupName}</span>
                  </div>
                  <div style={{ display: 'flex', justifyContent: 'space-between' }}>
                    <span style={{ color: '#a0a0a0' }}>Valor Total:</span>
                    <span style={{ color: 'white' }}>{formatCurrency(retrievedContract.totalAmount)}</span>
                  </div>
                  <div style={{ display: 'flex', justifyContent: 'space-between' }}>
                    <span style={{ color: '#a0a0a0' }}>Status:</span>
                    <Badge 
                      color={
                        retrievedContract.status === ContractStatus.Active ? 'green' :
                        retrievedContract.status === ContractStatus.Defaulted ? 'red' :
                        retrievedContract.status.startsWith('Late') ? 'orange' : 'gray'
                      }
                    >
                      {getStatusLabel(retrievedContract.status)}
                    </Badge>
                  </div>
                  <div style={{ display: 'flex', justifyContent: 'space-between' }}>
                    <span style={{ color: '#a0a0a0' }}>Data Início:</span>
                    <span style={{ color: 'white' }}>{formatDate(retrievedContract.contractStartDate)}</span>
                  </div>
                </div>
              </div>

              {/* Matricula Selection */}
              {userMatriculas.length > 0 && (
                <FormField 
                  label={`Matrícula ${userMatriculas.length > 1 ? '(Selecione)' : ''}`}
                  description={
                    userMatriculas.length === 0 ? 'Você não possui matrículas ativas' :
                    userMatriculas.length === 1 ? 'Matrícula será atribuída automaticamente' :
                    'Selecione a matrícula para este contrato (padrão: mais recente)'
                  }
                >
                  {userMatriculas.length === 1 ? (
                    <TextInput
                      value={`${userMatriculas[0].matriculaNumber} (${new Date(userMatriculas[0].startDate).toLocaleDateString('pt-BR')})`}
                      readOnly
                      disabled
                    />
                  ) : (
                    <Select
                      value={selectedMatricula}
                      onChange={(value) => setSelectedMatricula(value || '')}
                      data={userMatriculas.map((m) => ({
                        value: m.matriculaNumber,
                        label: `${m.matriculaNumber} - ${new Date(m.startDate).toLocaleDateString('pt-BR')}${
                          m.endDate ? ` até ${new Date(m.endDate).toLocaleDateString('pt-BR')}` : ''
                        }${m.isOwner ? ' (Proprietário)' : ''}`
                      }))}
                    />
                  )}
                </FormField>
              )}

              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '8px', marginTop: '1.5rem' }}>
                <Button
                  variant="default"
                  onClick={() => setRetrievedContract(null)}
                  disabled={assignLoading}
                >
                  Voltar
                </Button>
                <Button
                  onClick={handleConfirmAssignment}
                  disabled={assignLoading}
                  loading={assignLoading}
                >
                  Confirmar Atribuição
                </Button>
              </div>
            </>
          )}
        </StyledModal>
      )}

    </Menu>
  );
};

export default MyContractsPage;
