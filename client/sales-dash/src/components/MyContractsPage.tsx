import React, { useState, useEffect, useCallback } from 'react';
import { Title, Button, Table, TextInput, Select, Alert, Badge } from '@mantine/core';
import { notifications } from '@mantine/notifications';
import { normalizeNumber } from '../utils/normalization';
import './MyContractsPage.css';
import Menu from './Menu';
import StandardModal from '../shared/StandardModal';
import InfoHelper from '../shared/InfoHelper';
import FormField from './FormField';
import AggregationSummary from '../shared/AggregationSummary';
import HistoricProduction from '../shared/HistoricProduction';
import ContractStatusBadge from '../shared/ContractStatusBadge';
import ExportButton from '../shared/ExportButton';
import ExportProgressIndicator from '../shared/ExportProgressIndicator';
import {
  Contract,
  ContractAggregation,
  getUserContracts,
  getContractByNumber,
  assignContract,
  registerPendingClaim,
  getMyPendingClaims,
  getPendingClaimsByMatricula,
  deletePendingClaim,
  PendingClaimResponse
} from '../services/contractService';
import { apiService, UserMatricula } from '../services/apiService';

const MyContractsPage: React.FC = () => {
  const [contracts, setContracts] = useState<Contract[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [aggregation, setAggregation] = useState<ContractAggregation | null>(null);
  const [showAssignModal, setShowAssignModal] = useState(false);
  const [showHelperModal, setShowHelperModal] = useState(false);

  // Export state
  const [exportJobId, setExportJobId] = useState<string | null>(null);
  const [isExporting, setIsExporting] = useState(false);
  const exportToken = localStorage.getItem('token') || '';

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

  const [pendingClaims, setPendingClaims] = useState<PendingClaimResponse[]>([]);
  const [matriculaPendingClaims, setMatriculaPendingClaims] = useState<PendingClaimResponse[]>([]);
  const [contractNotYetImported, setContractNotYetImported] = useState(false);

  const loadMyContracts = useCallback(async () => {
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
  }, [startDate, endDate]);

  // Load saved date filters from localStorage
  useEffect(() => {
    const savedStart = localStorage.getItem('myContracts_startDate');
    const savedEnd = localStorage.getItem('myContracts_endDate');
    if (savedStart) setStartDate(savedStart);
    if (savedEnd) setEndDate(savedEnd);
  }, []);

  const loadPendingClaims = useCallback(async () => {
    try {
      const myClaims = await getMyPendingClaims();
      setPendingClaims(myClaims);

      const userStr = localStorage.getItem('user');
      if (userStr) {
        const user = JSON.parse(userStr);
        if (user.isMatriculaOwner && user.activeMatriculas) {
          const allMatriculaClaims = [];
          for (const m of user.activeMatriculas) {
            if (m.isOwner) {
              const claims = await getPendingClaimsByMatricula(m.matriculaId);
              allMatriculaClaims.push(...claims);
            }
          }
          setMatriculaPendingClaims(allMatriculaClaims);
        }
      }
    } catch (err) {
      console.error("Failed to load pending claims", err);
    }
  }, []);

  useEffect(() => {
    loadMyContracts();
    loadPendingClaims();
  }, [loadMyContracts, loadPendingClaims]);

  const handleNewClick = async () => {
    setContractNumber('');
    setRetrievedContract(null);
    setAssignError('');
    setSelectedMatricula('');

    // Read matriculas from the login response already stored in localStorage.
    // Using the API (getUserMatriculas) is not viable here because regular users
    // lack the "matriculas:read" permission — it would silently return empty.
    try {
      const user = JSON.parse(localStorage.getItem('user') || '{}');
      const rawMatriculas: any[] = user.activeMatriculas || [];
      const now = new Date();
      const activeMatriculas = rawMatriculas
        .filter(m => m.isActive !== false && (!m.endDate || new Date(m.endDate) > now))
        .sort((a, b) => new Date(b.startDate).getTime() - new Date(a.startDate).getTime());

      setUserMatriculas(activeMatriculas as any);

      if (activeMatriculas.length === 1) {
        setSelectedMatricula(activeMatriculas[0].matriculaNumber);
      } else {
        setSelectedMatricula('');
      }
      setShowAssignModal(true);
    } catch (err) {
      console.error("Failed to load user matriculas", err);
      setShowAssignModal(true);
    }
  };

  const handleCancelClaim = async (claimId: number) => {
    if (!window.confirm('Tem certeza que deseja cancelar esta solicitação?')) return;

    try {
      await deletePendingClaim(claimId);
      notifications.show({
        title: 'Sucesso',
        message: 'Solicitação cancelada com sucesso.',
        color: 'green',
      });
      loadPendingClaims();
    } catch (err: any) {
      notifications.show({
        title: 'Erro',
        message: err.message || 'Falha ao cancelar solicitação.',
        color: 'red',
      });
    }
  };

  const handleRetrieveContract = async () => {
    const normalizedContractNumber = normalizeNumber(contractNumber);
    if (!normalizedContractNumber) {
      setAssignError('Por favor, insira um número de contrato');
      return;
    }

    setAssignLoading(true);
    setAssignError('');

    try {
      const contract = await getContractByNumber(normalizedContractNumber);
      setRetrievedContract(contract);
      setContractNotYetImported(false);

      // Auto-select matricula if it matches one of the user's matriculas
      if (contract.matriculaNumber) {
        const matchingMatricula = userMatriculas.find(m => m.matriculaNumber === contract.matriculaNumber);
        if (matchingMatricula) {
          setSelectedMatricula(matchingMatricula.matriculaNumber);
        }
      }
    } catch (err: any) {
      if (err.notFoundYet) {
        setAssignError('');
        setContractNotYetImported(true);
        if (err.alreadyClaimed) {
          setAssignError(err.message);
        }
      } else {
        setAssignError(err.message || 'Contrato não encontrado');
        setContractNotYetImported(false);
      }
      setRetrievedContract(null);
    } finally {
      setAssignLoading(false);
    }
  };

  const handleConfirmAssignment = async () => {
    if (!contractNumber.trim()) return;

    if (contractNotYetImported && userMatriculas.length > 1 && !selectedMatricula) {
      setAssignError('Por favor, selecione uma matrícula');
      return;
    }

    setAssignLoading(true);
    setAssignError('');
    const normalizedContractNumber = normalizeNumber(contractNumber);

    try {
      // Resolve the ID of the selected matricula
      const selectedMatriculaObj = userMatriculas.find(m => m.matriculaNumber === selectedMatricula);

      if (contractNotYetImported) {
        if (!selectedMatriculaObj) {
          setAssignError('Matrícula é obrigatória para registrar interesse.');
          setAssignLoading(false);
          return;
        }
        await registerPendingClaim(normalizedContractNumber, selectedMatriculaObj.id);
        loadPendingClaims();
      } else {
        await assignContract(
          normalizedContractNumber,
          selectedMatricula || undefined,
          selectedMatriculaObj?.id
        );
      }

      setShowAssignModal(false);
      setContractNumber('');
      setRetrievedContract(null);
      setContractNotYetImported(false);
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



  const helperContent = (
    <div className="info-helper-card">
      <div className="info-helper-card-icon">
        <svg width="20" height="20" viewBox="0 0 24 24" fill="white" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
          <polygon points="5 3 19 12 5 21 5 3"></polygon>
        </svg>
      </div>
      <div className="info-helper-card-body">
        <h4>Tutorial em vídeo disponível</h4>
        <p>Aprenda como solicitar ou atribuir um contrato à sua matrícula passo a passo.</p>
        <button className="info-helper-btn" onClick={() => setShowHelperModal(true)}>
          Assistir tutorial
          <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" style={{ marginLeft: "4px" }}>
            <line x1="7" y1="17" x2="17" y2="7"></line>
            <polyline points="7 7 17 7 17 17"></polyline>
          </svg>
        </button>
      </div>
    </div>
  );

  return (
    <Menu>
      <div className="my-contracts-page">
        <div className="my-contracts-header">
          <Title order={2} size="h2">Meus Contratos</Title>
          <div className="header-actions">
            <InfoHelper label="Como Atribuir?">
              {helperContent}
            </InfoHelper>
            <ExportButton
              onExport={async () => {
                setIsExporting(true);
                try {
                  const job = await apiService.startMyContractExport({
                    startDate: startDate || undefined,
                    endDate: endDate || undefined,
                  });
                  setExportJobId(job.jobId);
                } catch (e: any) {
                  setError(e.message || 'Falha ao iniciar exportação');
                  setIsExporting(false);
                }
              }}
              isExporting={isExporting}
            />
            <Button onClick={handleNewClick} leftSection="+">
              Novo
            </Button>
          </div>
        </div>

        <ExportProgressIndicator
          jobId={exportJobId}
          pollUrl={apiService.myContractExportPollUrl.bind(apiService)}
          downloadUrl={apiService.myContractExportDownloadUrl.bind(apiService)}
          token={exportToken}
          onComplete={() => { setIsExporting(false); setExportJobId(null); }}
          onError={(msg) => { setError(msg); setIsExporting(false); setExportJobId(null); }}
        />

        {error && <div className="my-contracts-error">{error}</div>}

        {/* Date Filters */}
        {(
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
            {startDate || endDate ? (
              <>
                <p>Nenhum contrato encontrado para o período selecionado.</p>
                <Button onClick={handleClearFilters}>
                  Limpar Filtros
                </Button>
              </>
            ) : (
              <>
                <p>Você ainda não possui contratos atribuídos.</p>
                <Button onClick={handleNewClick}>
                  Atribuir Primeiro Contrato
                </Button>
              </>
            )}
          </div>
        ) : (
          <div className="my-contracts-table-container">
            <Table.ScrollContainer minWidth={800}>
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
            </Table.ScrollContainer>
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

        {/* Pending Claims Section */}
        {pendingClaims.length > 0 && (
          <div style={{ marginTop: '2rem' }}>
            <Title order={3} size="h3" style={{ marginBottom: '1rem', display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
              📋 Contratos Solicitados <Badge color="orange" variant="light">Aguardando importação</Badge>
            </Title>
            <Table.ScrollContainer minWidth={800}>
              <Table striped highlightOnHover>
                <Table.Thead>
                  <Table.Tr>
                    <Table.Th>Nº Contrato</Table.Th>
                    <Table.Th>Matrícula</Table.Th>
                    <Table.Th>Solicitado em</Table.Th>
                    <Table.Th style={{ width: '100px' }}>Ações</Table.Th>
                  </Table.Tr>
                </Table.Thead>
                <Table.Tbody>
                  {pendingClaims.map(claim => (
                    <Table.Tr key={claim.id}>
                      <Table.Td>{claim.contractNumber}</Table.Td>
                      <Table.Td>{claim.matriculaNumber}</Table.Td>
                      <Table.Td>{new Date(claim.claimedAt).toLocaleDateString('pt-BR')}</Table.Td>
                      <Table.Td>
                        <Button
                          variant="subtle"
                          color="red"
                          size="xs"
                          onClick={() => handleCancelClaim(claim.id)}
                        >
                          Cancelar
                        </Button>
                      </Table.Td>
                    </Table.Tr>
                  ))}
                </Table.Tbody>
              </Table>
            </Table.ScrollContainer>
          </div>
        )}

        {/* Matricula Owner Alert Section */}
        {matriculaPendingClaims.length > 0 && (
          <div style={{ marginTop: '2rem' }}>
            <Alert color="orange" title="Atenção Proprietário" mb="md">
              Existem usuários que solicitaram a atribuição de contratos vinculados às suas matrículas, mas os contratos ainda não foram encontrados no sistema. Por favor, importe o arquivo o quanto antes para atribuí-los automaticamente.
            </Alert>
            <Table.ScrollContainer minWidth={800}>
              <Table striped highlightOnHover>
                <Table.Thead>
                  <Table.Tr>
                    <Table.Th>Nº Contrato</Table.Th>
                    <Table.Th>Usuário Solicitante</Table.Th>
                    <Table.Th>Email do Usuário</Table.Th>
                    <Table.Th>Sua Matrícula</Table.Th>
                    <Table.Th>Solicitado em</Table.Th>
                  </Table.Tr>
                </Table.Thead>
                <Table.Tbody>
                  {matriculaPendingClaims.map(claim => (
                    <Table.Tr key={claim.id}>
                      <Table.Td>{claim.contractNumber}</Table.Td>
                      <Table.Td>{claim.userName}</Table.Td>
                      <Table.Td>{claim.userEmail}</Table.Td>
                      <Table.Td>{claim.matriculaNumber}</Table.Td>
                      <Table.Td>{new Date(claim.claimedAt).toLocaleDateString('pt-BR')}</Table.Td>
                    </Table.Tr>
                  ))}
                </Table.Tbody>
              </Table>
            </Table.ScrollContainer>
          </div>
        )}
      </div>

      {/* Assignment Modal */}
      <StandardModal
        isOpen={showAssignModal}
        onClose={() => {
          setShowAssignModal(false);
          setContractNotYetImported(false);
          setContractNumber('');
        }}
        title="Atribuir Contrato"
        size="md"
        footer={
          !retrievedContract && !contractNotYetImported ? (
            <>
              <button
                className="btn-cancel"
                onClick={() => {
                  setShowAssignModal(false);
                  setContractNotYetImported(false);
                  setContractNumber('');
                }}
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
                onClick={() => {
                  setRetrievedContract(null);
                  setContractNotYetImported(false);
                }}
                disabled={assignLoading}
              >
                Voltar
              </button>
              <button
                className="btn-submit"
                onClick={handleConfirmAssignment}
                disabled={assignLoading || (userMatriculas.length > 1 && !selectedMatricula) || (contractNotYetImported && !!assignError)}
              >
                {assignLoading ? 'Processando...' : (contractNotYetImported ? 'Registrar Interesse' : 'Confirmar Atribuição')}
              </button>
            </>
          )
        }
      >
        {assignError && <div style={{ color: '#fa5252', marginBottom: '1rem', fontSize: '14px' }}>{assignError}</div>}

        {!retrievedContract && !contractNotYetImported ? (
          <FormField label="Número do Contrato" required labelColor="#333">
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
            {retrievedContract && (
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
            )}
          </>
        )}

        <>
          {contractNotYetImported && !assignError && (
            <Alert color="blue" title="Contrato não encontrado" mb="md">
              Este contrato ainda não foi importado para o sistema. Você pode registrar seu interesse e ele será atribuído automaticamente à sua matrícula quando for importado.
            </Alert>
          )}
          {/* Matricula Selection */}
          {(contractNotYetImported || retrievedContract) && !assignError && userMatriculas.length >= 1 && (
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
                    label: `${m.matriculaNumber} - ${new Date(m.startDate).toLocaleDateString('pt-BR')}${m.endDate ? ` até ${new Date(m.endDate).toLocaleDateString('pt-BR')}` : ''
                      }${m.isOwner ? ' (Proprietário)' : ''}`
                  }))}
                />
              )}
            </FormField>
          )}
        </>
      </StandardModal>

      {/* Helper Video Modal */}
      <StandardModal
        isOpen={showHelperModal}
        onClose={() => setShowHelperModal(false)}
        title="Como Solicitar ou Atribuir um Contrato"
        size="lg"
      >
        <div style={{ position: 'relative', paddingBottom: '56.25%', height: 0, overflow: 'hidden', borderRadius: '8px' }}>
          <iframe
            src="https://www.youtube.com/embed/bKLTbMfP6Po"
            style={{ position: 'absolute', top: 0, left: 0, width: '100%', height: '100%' }}
            frameBorder="0"
            allow="accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture"
            allowFullScreen
            title="Tutorial de Atribuição de Contratos"
          ></iframe>
        </div>
      </StandardModal>

    </Menu>
  );
};

export default MyContractsPage;
