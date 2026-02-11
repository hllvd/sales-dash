import React, { useState, useEffect } from 'react';
import { Title, Button, Table, Badge, TextInput, Select } from '@mantine/core';
import './MyContractsPage.css';
import Menu from './Menu';
import StandardModal from '../shared/StandardModal';
import FormField from './FormField';
import AggregationSummary from '../shared/AggregationSummary';
import HistoricProduction from '../shared/HistoricProduction';
import ContractStatusBadge from '../shared/ContractStatusBadge';
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
          
          // Auto-select ONLY if there is exactly one
          if (activeMatriculas.length === 1) {
            setSelectedMatricula(activeMatriculas[0].matriculaNumber);
          } else {
            // Keep empty if multiple exist, forcing manual selection
            setSelectedMatricula('');
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

    if (userMatriculas.length > 1 && !selectedMatricula) {
      setAssignError('Por favor, selecione uma matrícula');
      return;
    }

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
                        <ContractStatusBadge status={contract.status} />
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
      <StandardModal
        isOpen={showAssignModal}
        onClose={() => setShowAssignModal(false)}
        title="Atribuir Contrato"
        size="md"
        footer={
          !retrievedContract ? (
            <>
              <button
                className="btn-cancel"
                onClick={() => setShowAssignModal(false)}
                disabled={assignLoading}
              >
                Cancelar
              </button>
              <button
                className="btn-submit"
                onClick={handleRetrieveContract}
                disabled={!contractNumber.trim() || assignLoading}
              >
                {assignLoading ? 'Buscando...' : 'Buscar Contrato'}
              </button>
            </>
          ) : (
            <>
              <button
                className="btn-cancel"
                onClick={() => setRetrievedContract(null)}
                disabled={assignLoading}
              >
                Voltar
              </button>
              <button
                className="btn-submit"
                onClick={handleConfirmAssignment}
                disabled={assignLoading || (userMatriculas.length > 1 && !selectedMatricula)}
              >
                {assignLoading ? 'Atribuindo...' : 'Confirmar Atribuição'}
              </button>
            </>
          )
        }
      >
        {assignError && <div style={{ color: '#fa5252', marginBottom: '1rem', fontSize: '14px' }}>{assignError}</div>}

        {!retrievedContract ? (
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
        ) : (
          <>
            <div style={{ marginBottom: '1.5rem' }}>
              <h3 style={{ color: '#111827', marginBottom: '1rem', fontSize: '16px', fontWeight: 600 }}>Detalhes do Contrato</h3>
              <div style={{ display: 'flex', flexDirection: 'column', gap: '0.8rem' }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', borderBottom: '1px solid #f3f4f6', paddingBottom: '0.6rem' }}>
                  <span style={{ color: '#6b7280', fontSize: '13px' }}>Número:</span>
                  <span style={{ color: '#111827', fontSize: '14px', fontWeight: 500 }}>{retrievedContract.contractNumber}</span>
                </div>
                <div style={{ display: 'flex', justifyContent: 'space-between', borderBottom: '1px solid #f3f4f6', paddingBottom: '0.6rem' }}>
                  <span style={{ color: '#6b7280', fontSize: '13px' }}>Cliente:</span>
                  <span style={{ color: '#111827', fontSize: '14px', fontWeight: 500 }}>{retrievedContract.customerName || '-'}</span>
                </div>
                <div style={{ display: 'flex', justifyContent: 'space-between', borderBottom: '1px solid #f3f4f6', paddingBottom: '0.6rem' }}>
                  <span style={{ color: '#6b7280', fontSize: '13px' }}>Grupo:</span>
                  <span style={{ color: '#111827', fontSize: '14px', fontWeight: 500 }}>{retrievedContract.groupName}</span>
                </div>
                <div style={{ display: 'flex', justifyContent: 'space-between', borderBottom: '1px solid #f3f4f6', paddingBottom: '0.6rem' }}>
                  <span style={{ color: '#6b7280', fontSize: '13px' }}>Valor Total:</span>
                  <span style={{ color: '#111827', fontSize: '14px', fontWeight: 500 }}>{formatCurrency(retrievedContract.totalAmount)}</span>
                </div>
                 <div style={{ display: 'flex', justifyContent: 'space-between', borderBottom: '1px solid #f3f4f6', paddingBottom: '0.6rem' }}>
                  <span style={{ color: '#6b7280', fontSize: '13px' }}>Status:</span>
                  <ContractStatusBadge status={retrievedContract.status} />
                </div>
                <div style={{ display: 'flex', justifyContent: 'space-between' }}>
                  <span style={{ color: '#6b7280', fontSize: '13px' }}>Data Início:</span>
                  <span style={{ color: '#111827', fontSize: '14px', fontWeight: 500 }}>{formatDate(retrievedContract.contractStartDate)}</span>
                </div>
              </div>
            </div>

            {/* Matricula Selection */}
            {userMatriculas.length > 0 && (
              <FormField 
                label={`Matrícula ${userMatriculas.length > 1 ? '(Selecione)' : ''}`}
                description={
                  userMatriculas.length === 1 ? 'Matrícula será atribuída automaticamente' :
                  'Selecione a matrícula para este contrato'
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
                    placeholder="Selecione uma matrícula..."
                    value={selectedMatricula}
                    onChange={(value) => setSelectedMatricula(value || '')}
                    comboboxProps={{ zIndex: 2000 }}
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
          </>
        )}
      </StandardModal>

    </Menu>
  );
};

export default MyContractsPage;
