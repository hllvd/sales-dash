import React, { useState, useEffect } from 'react';
import { Title, Button, Table, Badge } from '@mantine/core';
import './MyContractsPage.css';
import Menu from './Menu';
import AggregationSummary from '../shared/AggregationSummary';
import {
  Contract,
  ContractAggregation,
  getContracts,
  getUserContracts,
  getContractByNumber,
  assignContract,
} from '../services/contractService';

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

  const handleNewClick = () => {
    setContractNumber('');
    setRetrievedContract(null);
    setAssignError('');
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
      await assignContract(contractNumber);
      setShowAssignModal(false);
      setContractNumber('');
      setRetrievedContract(null);
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
                    <Table.Th>Número</Table.Th>
                    <Table.Th>Cliente</Table.Th>
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
                      <Table.Td>{contract.groupName}</Table.Td>
                      <Table.Td>{formatCurrency(contract.totalAmount)}</Table.Td>
                      <Table.Td>
                        <Badge 
                          color={
                            contract.status === 'Active' ? 'green' :
                            contract.status === 'Defaulted' ? 'red' :
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
        </div>

        


      {/* Assignment Modal */}
      {showAssignModal && (
        <div className="assign-modal-overlay" onClick={() => setShowAssignModal(false)}>
          <div className="assign-modal-content" onClick={(e) => e.stopPropagation()}>
            <div className="assign-modal-header">
              <h2>Atribuir Contrato</h2>
              <button className="close-button" onClick={() => setShowAssignModal(false)}>
                ×
              </button>
            </div>

            <div className="assign-modal-body">
              {assignError && <div className="error-message">{assignError}</div>}

              {!retrievedContract ? (
                <>
                  <div className="form-group">
                    <label htmlFor="contractNumber">Número do Contrato</label>
                    <input
                      id="contractNumber"
                      type="text"
                      value={contractNumber}
                      onChange={(e) => setContractNumber(e.target.value)}
                      placeholder="Digite o número do contrato"
                      onKeyPress={(e) => {
                        if (e.key === 'Enter') {
                          handleRetrieveContract();
                        }
                      }}
                    />
                  </div>

                  <div className="form-actions">
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
                  </div>
                </>
              ) : (
                <>
                  <div className="contract-details">
                    <h3>Detalhes do Contrato</h3>
                    <div className="detail-row">
                      <span className="detail-label">Número:</span>
                      <span className="detail-value">{retrievedContract.contractNumber}</span>
                    </div>
                    <div className="detail-row">
                      <span className="detail-label">Cliente:</span>
                      <span className="detail-value">{retrievedContract.customerName || '-'}</span>
                    </div>
                    <div className="detail-row">
                      <span className="detail-label">Grupo:</span>
                      <span className="detail-value">{retrievedContract.groupName}</span>
                    </div>
                    <div className="detail-row">
                      <span className="detail-label">Valor Total:</span>
                      <span className="detail-value">{formatCurrency(retrievedContract.totalAmount)}</span>
                    </div>
                    <div className="detail-row">
                      <span className="detail-label">Status:</span>
                      <Badge 
                        color={
                          retrievedContract.status === 'Active' ? 'green' :
                          retrievedContract.status === 'Defaulted' ? 'red' :
                          retrievedContract.status.startsWith('Late') ? 'orange' : 'gray'
                        }
                      >
                        {getStatusLabel(retrievedContract.status)}
                      </Badge>
                    </div>
                    <div className="detail-row">
                      <span className="detail-label">Data Início:</span>
                      <span className="detail-value">{formatDate(retrievedContract.contractStartDate)}</span>
                    </div>
                  </div>

                  <div className="form-actions">
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
                      disabled={assignLoading}
                    >
                      {assignLoading ? 'Atribuindo...' : 'Confirmar Atribuição'}
                    </button>
                  </div>
                </>
              )}
            </div>
          </div>
        </div>
      )}

    </Menu>
  );
};

export default MyContractsPage;
