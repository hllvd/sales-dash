import React, { useState, useEffect } from 'react';
import './MyContractsPage.css';
import Menu from './Menu';
import {
  Contract,
  getContracts,
  getContractByNumber,
  assignContract,
} from '../services/contractService';

const MyContractsPage: React.FC = () => {
  const [contracts, setContracts] = useState<Contract[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [showAssignModal, setShowAssignModal] = useState(false);
  
  // Contract assignment state
  const [contractNumber, setContractNumber] = useState('');
  const [retrievedContract, setRetrievedContract] = useState<Contract | null>(null);
  const [assignLoading, setAssignLoading] = useState(false);
  const [assignError, setAssignError] = useState('');

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

      // Load contracts for current user
      const data = await getContracts(userId);
      setContracts(data);
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
    <div className="my-contracts-layout">
      <Menu />
      <div className="my-contracts-content">
        <div className="my-contracts-page">
          <div className="my-contracts-header">
            <h1>Meus Contratos</h1>
            <button className="new-contract-btn" onClick={handleNewClick}>
              + Novo
            </button>
          </div>

          {error && <div className="my-contracts-error">{error}</div>}

          {loading ? (
            <div className="my-contracts-loading">
              <div className="spinner"></div>
              <p>Carregando contratos...</p>
            </div>
          ) : contracts.length === 0 ? (
            <div className="my-contracts-empty">
              <p>Você ainda não possui contratos atribuídos.</p>
              <button className="new-contract-btn" onClick={handleNewClick}>
                Atribuir Primeiro Contrato
              </button>
            </div>
          ) : (
            <div className="my-contracts-table-container">
              <table className="my-contracts-table">
                <thead>
                  <tr>
                    <th>Número</th>
                    <th>Cliente</th>
                    <th>Grupo</th>
                    <th>Valor Total</th>
                    <th>Status</th>
                    <th>Data Início</th>
                    <th>Data Término</th>
                  </tr>
                </thead>
                <tbody>
                  {contracts.map((contract) => (
                    <tr key={contract.id}>
                      <td>{contract.contractNumber}</td>
                      <td>{contract.customerName || '-'}</td>
                      <td>{contract.groupName}</td>
                      <td>{formatCurrency(contract.totalAmount)}</td>
                      <td>
                        <span className={`status-badge ${getStatusBadgeClass(contract.status)}`}>
                          {getStatusLabel(contract.status)}
                        </span>
                      </td>
                      <td>{formatDate(contract.contractStartDate)}</td>
                      <td>{formatDate(contract.contractEndDate)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
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
                      <span className={`status-badge ${getStatusBadgeClass(retrievedContract.status)}`}>
                        {getStatusLabel(retrievedContract.status)}
                      </span>
                    </div>
                    <div className="detail-row">
                      <span className="detail-label">Data Início:</span>
                      <span className="detail-value">{formatDate(retrievedContract.contractStartDate)}</span>
                    </div>
                    <div className="detail-row">
                      <span className="detail-label">Data Término:</span>
                      <span className="detail-value">{formatDate(retrievedContract.contractEndDate)}</span>
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
    </div>
  );
};

export default MyContractsPage;
