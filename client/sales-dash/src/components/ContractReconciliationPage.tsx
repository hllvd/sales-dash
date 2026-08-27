import React, { useState, useEffect, useMemo } from 'react';
import {
  Text,
  Button,
  Select,
  TextInput,
  FileInput,
  Badge,
  Table,
  Tabs,
  Alert,
  Loader,
} from '@mantine/core';
import {
  IconTools,
  IconFileSpreadsheet,
  IconAlertTriangle,
  IconAlertCircle,
  IconDownload,
  IconSearch,
  IconUserX,
  IconScale,
  IconCheck,
  IconCalendarTime,
  IconUserExclamation,
  IconTags,
} from '@tabler/icons-react';
import {
  apiService,
  ContractReconciliationResult,
  Team,
} from '../services/apiService';
import Menu from './Menu';
import './ContractReconciliationPage.css';

const formatCurrency = (val: number) => {
  return new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format(val || 0);
};

const formatDate = (dateStr?: string) => {
  if (!dateStr) return '-';
  try {
    const d = new Date(dateStr);
    return isNaN(d.getTime()) ? dateStr : d.toLocaleDateString('pt-BR');
  } catch {
    return dateStr;
  }
};

const ContractReconciliationPage: React.FC = () => {
  // Filters
  const todayStr = new Date().toISOString().split('T')[0];
  const firstDayStr = new Date(new Date().getFullYear(), new Date().getMonth(), 1).toISOString().split('T')[0];

  const [startDate, setStartDate] = useState(firstDayStr);
  const [endDate, setEndDate] = useState(todayStr);
  const [selectedTeamId, setSelectedTeamId] = useState<string | null>(null);
  const [selectedUserId, setSelectedUserId] = useState<string | null>(null);
  const [file, setFile] = useState<File | null>(null);

  // Teams & Users list for dropdowns
  const [teams, setTeams] = useState<Team[]>([]);
  const [loadingTeams, setLoadingTeams] = useState(false);
  const [users, setUsers] = useState<Array<{ value: string; label: string }>>([]);
  const [loadingUsers, setLoadingUsers] = useState(false);

  // Execution state
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [result, setResult] = useState<ContractReconciliationResult | null>(null);

  // Active tab & search filter
  const [activeTab, setActiveTab] = useState<string | null>('missing-in-system');
  const [searchQuery, setSearchQuery] = useState('');

  // Fetch teams and users on mount
  useEffect(() => {
    const fetchData = async () => {
      setLoadingTeams(true);
      setLoadingUsers(true);

      try {
        const [teamsRes, usersRes] = await Promise.allSettled([
          apiService.getTeams(),
          apiService.getUsers(1, 500, undefined, undefined, false, true, 'active'),
        ]);

        if (teamsRes.status === 'fulfilled' && teamsRes.value.success && teamsRes.value.data) {
          setTeams(teamsRes.value.data);
        }

        if (usersRes.status === 'fulfilled' && usersRes.value.success && usersRes.value.data?.items) {
          const userOptions = usersRes.value.data.items.map((u) => ({
            value: u.id,
            label: `${u.name} (${u.email})`,
          }));
          setUsers(userOptions);
        }
      } catch (err) {
        console.error('Erro ao carregar equipes e usuários:', err);
      } finally {
        setLoadingTeams(false);
        setLoadingUsers(false);
      }
    };

    fetchData();
  }, []);

  // Filtered users when a team is selected
  const selectedTeam = useMemo(
    () => teams.find((t) => t.id.toString() === selectedTeamId),
    [teams, selectedTeamId]
  );

  const teamFilteredUsers = useMemo(() => {
    if (!selectedTeamId || !selectedTeam) {
      return users;
    }
    const activeMemberIds = new Set(
      (selectedTeam.members || [])
        .filter((m) => m.isActive)
        .map((m) => m.userId.toLowerCase())
    );
    return users.filter((u) => activeMemberIds.has(u.value.toLowerCase()));
  }, [users, selectedTeamId, selectedTeam]);

  const handleTeamChange = (val: string | null) => {
    setSelectedTeamId(val || null);
    setSelectedUserId(null);
  };

  const handleRunReconciliation = async (e: React.FormEvent) => {
    e.preventDefault();

    if (!file) {
      setError('Por favor, selecione um arquivo .xlsx ou .csv.');
      return;
    }

    if (!startDate || !endDate) {
      setError('Por favor, preencha o período inicial e final.');
      return;
    }

    setLoading(true);
    setError(null);

    try {
      const res = await apiService.reconcileContracts(
        file,
        startDate,
        endDate,
        selectedUserId || undefined,
        selectedTeamId ? parseInt(selectedTeamId, 10) : undefined
      );
      setResult(res);
      setActiveTab('missing-in-system');
    } catch (err: any) {
      setError(err?.message || 'Ocorreu um erro ao processar o arquivo de reconciliação.');
    } finally {
      setLoading(false);
    }
  };

  // CSV Export for active tab
  const handleExportCSV = () => {
    if (!result) return;

    let headers: string[] = [];
    let rows: string[][] = [];
    let filename = `reconciliacao_${activeTab}_${todayStr}.csv`;

    if (activeTab === 'missing-in-system') {
      headers = ['Número do Contrato', 'Valor (XLSX)', 'Usuário', 'Data'];
      rows = filteredMissingInSystem.map((item) => [
        `"${item.contractNumber}"`,
        item.totalAmount.toFixed(2),
        `"${item.systemUserName || item.userIdentifier || ''}"`,
        `"${formatDate(item.date)}"`,
      ]);
    } else if (activeTab === 'missing-in-import') {
      headers = ['Número do Contrato', 'Valor (Sistema)', 'Usuário no Sistema', 'Data de Venda'];
      rows = filteredMissingInImport.map((item) => [
        `"${item.contractNumber}"`,
        item.totalAmount.toFixed(2),
        `"${item.systemUserName || item.userIdentifier || ''}"`,
        `"${formatDate(item.date)}"`,
      ]);
    } else if (activeTab === 'amount-mismatches') {
      headers = ['Número do Contrato', 'Valor Sistema', 'Valor XLSX', 'Diferença', 'Usuário', 'Data de Venda'];
      rows = filteredAmountMismatches.map((item) => [
        `"${item.contractNumber}"`,
        item.systemAmount.toFixed(2),
        item.xlsxAmount.toFixed(2),
        item.difference.toFixed(2),
        `"${item.systemUserName || item.userIdentifier || ''}"`,
        `"${formatDate(item.saleStartDate)}"`,
      ]);
    } else if (activeTab === 'date-mismatches') {
      headers = ['Número do Contrato', 'Data no Sistema', 'Data no XLSX', 'Valor Total', 'Usuário no Sistema'];
      rows = filteredDateMismatches.map((item) => [
        `"${item.contractNumber}"`,
        `"${formatDate(item.systemDate)}"`,
        `"${formatDate(item.xlsxDate)}"`,
        item.totalAmount.toFixed(2),
        `"${item.systemUserName || ''}"`,
      ]);
    } else if (activeTab === 'seller-mismatches') {
      headers = ['Número do Contrato', 'Vendedor no Sistema', 'Vendedor no XLSX', 'Valor Total', 'Data de Venda'];
      rows = filteredSellerMismatches.map((item) => [
        `"${item.contractNumber}"`,
        `"${item.systemUserName || ''}"`,
        `"${item.xlsxUserIdentifier || ''}"`,
        item.totalAmount.toFixed(2),
        `"${formatDate(item.saleStartDate)}"`,
      ]);
    } else if (activeTab === 'status-mismatches') {
      headers = ['Número do Contrato', 'Status no Sistema', 'Status no XLSX', 'Valor Total', 'Usuário no Sistema', 'Data de Venda'];
      rows = filteredStatusMismatches.map((item) => [
        `"${item.contractNumber}"`,
        `"${item.systemStatus || ''}"`,
        `"${item.xlsxStatus || ''}"`,
        item.totalAmount.toFixed(2),
        `"${item.systemUserName || ''}"`,
        `"${formatDate(item.saleStartDate)}"`,
      ]);
    } else if (activeTab === 'unassigned-users') {
      headers = ['Número do Contrato', 'Valor (XLSX)', 'Identificador de Usuário (XLSX)', 'Data'];
      rows = filteredUnassigned.map((item) => [
        `"${item.contractNumber}"`,
        item.totalAmount.toFixed(2),
        `"${item.userIdentifier || ''}"`,
        `"${formatDate(item.date)}"`,
      ]);
    }

    const csvContent = '\uFEFF' + [headers.join(';'), ...rows.map((r) => r.join(';'))].join('\n');
    const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.setAttribute('href', url);
    link.setAttribute('download', filename);
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
  };

  // Filtered lists by searchQuery
  const filterItem = (contractNum: string, userVal?: string) => {
    if (!searchQuery.trim()) return true;
    const q = searchQuery.toLowerCase().trim();
    return (
      contractNum.toLowerCase().includes(q) ||
      (userVal && userVal.toLowerCase().includes(q))
    );
  };

  const filteredMissingInSystem =
    result?.missingInSystem.filter((i) => filterItem(i.contractNumber, i.systemUserName || i.userIdentifier)) || [];

  const filteredMissingInImport =
    result?.missingInImport.filter((i) => filterItem(i.contractNumber, i.systemUserName || i.userIdentifier)) || [];

  const filteredAmountMismatches =
    result?.amountMismatches.filter((i) => filterItem(i.contractNumber, i.systemUserName || i.userIdentifier)) || [];

  const filteredDateMismatches =
    result?.dateMismatches?.filter((i) => filterItem(i.contractNumber, i.systemUserName)) || [];

  const filteredSellerMismatches =
    result?.sellerMismatches?.filter((i) => filterItem(i.contractNumber, `${i.systemUserName || ''} ${i.xlsxUserIdentifier || ''}`)) || [];

  const filteredStatusMismatches =
    result?.statusMismatches?.filter((i) =>
      filterItem(i.contractNumber, `${i.systemUserName || ''} ${i.systemStatus || ''} ${i.xlsxStatus || ''}`)
    ) || [];

  const filteredUnassigned =
    result?.unassignedUserContracts.filter((i) => filterItem(i.contractNumber, i.userIdentifier)) || [];

  return (
    <Menu>
      <div className="reconciliation-container">
        {/* Page Title Header */}
        <div className="reconciliation-header">
          <h1 className="reconciliation-title">
            <IconScale size={28} color="#3b82f6" />
            Reconciliação de Contratos
          </h1>
          <p className="reconciliation-subtitle">
            Ferramenta do Administrador para cruzamento de planilhas de clientes (XLSX) com os contratos cadastrados no sistema.
          </p>
        </div>

        {/* Filter Card & File Upload */}
        <div className="reconciliation-filter-card">
          <form onSubmit={handleRunReconciliation}>
            <div className="filter-grid">
              <TextInput
                label="Data Inicial (Venda)"
                type="date"
                value={startDate}
                onChange={(e) => setStartDate(e.target.value)}
                required
              />

              <TextInput
                label="Data Final (Venda)"
                type="date"
                value={endDate}
                onChange={(e) => setEndDate(e.target.value)}
                required
              />

              <Select
                label="Equipe (Opcional)"
                placeholder="Todas as Equipes"
                data={[{ value: '', label: 'Todas as Equipes' }, ...teams.map((t) => ({ value: t.id.toString(), label: t.name }))]}
                value={selectedTeamId || ''}
                onChange={handleTeamChange}
                searchable
                clearable
                disabled={loadingTeams}
              />

              <Select
                label="Usuário Específico (Opcional)"
                placeholder={selectedTeam ? `Usuários da equipe ${selectedTeam.name}` : 'Todos os Usuários'}
                data={[{ value: '', label: 'Todos os Usuários' }, ...teamFilteredUsers]}
                value={selectedUserId || ''}
                onChange={(val) => setSelectedUserId(val || null)}
                searchable
                clearable
                disabled={loadingUsers}
              />

              <FileInput
                label="Planilha XLSX do Cliente"
                placeholder="Selecione o arquivo (.xlsx)"
                leftSection={<IconFileSpreadsheet size={18} />}
                accept=".xlsx,.csv"
                value={file}
                onChange={setFile}
                required
              />

              <Button
                type="submit"
                leftSection={loading ? <Loader size="xs" color="white" /> : <IconTools size={18} />}
                loading={loading}
                disabled={!file || loading}
                color="blue"
              >
                Executar Reconciliação
              </Button>
            </div>
          </form>
        </div>

        {/* Error Alert */}
        {error && (
          <Alert icon={<IconAlertTriangle size={18} />} title="Atenção" color="red" mb="lg">
            {error}
          </Alert>
        )}

        {/* Results Section */}
        {result && (
          <>
            {/* Target User / Team Info Notice */}
            {(result.targetUserName || selectedTeam) && (
              <Alert icon={<IconCheck size={18} />} color="blue" mb="lg">
                Reconciliação executada{selectedTeam ? <> para a equipe <strong>{selectedTeam.name}</strong></> : ''}{result.targetUserName ? <> filtrado pelo usuário <strong>{result.targetUserName}</strong></> : ''} no período de{' '}
                {formatDate(result.startDate)} a {formatDate(result.endDate)}.
              </Alert>
            )}

            {/* 6 KPI Summary Cards */}
            <div className="kpi-grid">
              {/* Card 1: Missing in System */}
              <div
                className={`kpi-card red ${activeTab === 'missing-in-system' ? 'active' : ''}`}
                onClick={() => setActiveTab('missing-in-system')}
              >
                <div className="kpi-header">
                  <span className="kpi-label">Ausentes no Sistema</span>
                  <div className="kpi-icon-wrapper">
                    <IconAlertCircle size={20} />
                  </div>
                </div>
                <div className="kpi-count">{result.missingInSystemSummary.count}</div>
                <div className="kpi-amount">
                  Total XLSX: {formatCurrency(result.missingInSystemSummary.totalAmount)}
                </div>
              </div>

              {/* Card 2: Missing in Import */}
              <div
                className={`kpi-card amber ${activeTab === 'missing-in-import' ? 'active' : ''}`}
                onClick={() => setActiveTab('missing-in-import')}
              >
                <div className="kpi-header">
                  <span className="kpi-label">
                    {selectedTeam ? `Ausentes no XLSX (${selectedTeam.name})` : 'Ausentes no XLSX'}
                  </span>
                  <div className="kpi-icon-wrapper">
                    <IconFileSpreadsheet size={20} />
                  </div>
                </div>
                <div className="kpi-count">{result.missingInImportSummary.count}</div>
                <div className="kpi-amount">
                  Total Sistema: {formatCurrency(result.missingInImportSummary.totalAmount)}
                </div>
              </div>

              {/* Card 3: Amount Mismatches */}
              <div
                className={`kpi-card purple ${activeTab === 'amount-mismatches' ? 'active' : ''}`}
                onClick={() => setActiveTab('amount-mismatches')}
              >
                <div className="kpi-header">
                  <span className="kpi-label">Divergência de Valor</span>
                  <div className="kpi-icon-wrapper">
                    <IconScale size={20} />
                  </div>
                </div>
                <div className="kpi-count">{result.amountMismatchSummary.count}</div>
                <div className="kpi-amount">
                  Diferença: {formatCurrency(result.amountMismatchSummary.totalAmount)}
                </div>
              </div>

              {/* Card 4: Date Mismatches */}
              <div
                className={`kpi-card cyan ${activeTab === 'date-mismatches' ? 'active' : ''}`}
                onClick={() => setActiveTab('date-mismatches')}
              >
                <div className="kpi-header">
                  <span className="kpi-label">Divergência de Data</span>
                  <div className="kpi-icon-wrapper">
                    <IconCalendarTime size={20} />
                  </div>
                </div>
                <div className="kpi-count">{result.dateMismatchSummary?.count || 0}</div>
                <div className="kpi-amount">
                  Total Sistema: {formatCurrency(result.dateMismatchSummary?.totalAmount || 0)}
                </div>
              </div>

              {/* Card 5: Seller Mismatches */}
              <div
                className={`kpi-card indigo ${activeTab === 'seller-mismatches' ? 'active' : ''}`}
                onClick={() => setActiveTab('seller-mismatches')}
              >
                <div className="kpi-header">
                  <span className="kpi-label">Divergência de Vendedor</span>
                  <div className="kpi-icon-wrapper">
                    <IconUserExclamation size={20} />
                  </div>
                </div>
                <div className="kpi-count">{result.sellerMismatchSummary?.count || 0}</div>
                <div className="kpi-amount">
                  Total Sistema: {formatCurrency(result.sellerMismatchSummary?.totalAmount || 0)}
                </div>
              </div>

              {/* Card 6: Status Mismatches */}
              <div
                className={`kpi-card teal ${activeTab === 'status-mismatches' ? 'active' : ''}`}
                onClick={() => setActiveTab('status-mismatches')}
              >
                <div className="kpi-header">
                  <span className="kpi-label">Divergência de Status</span>
                  <div className="kpi-icon-wrapper">
                    <IconTags size={20} />
                  </div>
                </div>
                <div className="kpi-count">{result.statusMismatchSummary?.count || 0}</div>
                <div className="kpi-amount">
                  Total Sistema: {formatCurrency(result.statusMismatchSummary?.totalAmount || 0)}
                </div>
              </div>

              {/* Card 7: Unassigned Users */}
              <div
                className={`kpi-card gray ${activeTab === 'unassigned-users' ? 'active' : ''}`}
                onClick={() => setActiveTab('unassigned-users')}
              >
                <div className="kpi-header">
                  <span className="kpi-label">Sem Usuário Atribuído</span>
                  <div className="kpi-icon-wrapper">
                    <IconUserX size={20} />
                  </div>
                </div>
                <div className="kpi-count">{result.unassignedUserSummary.count}</div>
                <div className="kpi-amount">
                  Total XLSX: {formatCurrency(result.unassignedUserSummary.totalAmount)}
                </div>
              </div>
            </div>

            {/* Interactive Detailed Table Card */}
            <div className="results-card">
              <Tabs value={activeTab} onChange={setActiveTab}>
                <Tabs.List mb="md">
                  <Tabs.Tab
                    value="missing-in-system"
                    leftSection={<IconAlertCircle size={16} />}
                    rightSection={
                      <Badge size="xs" color="red" variant="filled">
                        {result.missingInSystemSummary.count}
                      </Badge>
                    }
                  >
                    Importados no XLSX que não estão no sistema
                  </Tabs.Tab>

                  <Tabs.Tab
                    value="missing-in-import"
                    leftSection={<IconFileSpreadsheet size={16} />}
                    rightSection={
                      <Badge size="xs" color="yellow" variant="filled">
                        {result.missingInImportSummary.count}
                      </Badge>
                    }
                  >
                    {selectedTeam
                      ? `Contratos da Equipe "${selectedTeam.name}" ausentes no XLSX`
                      : 'No Sistema que não vieram no XLSX'}
                  </Tabs.Tab>

                  <Tabs.Tab
                    value="amount-mismatches"
                    leftSection={<IconScale size={16} />}
                    rightSection={
                      <Badge size="xs" color="violet" variant="filled">
                        {result.amountMismatchSummary.count}
                      </Badge>
                    }
                  >
                    Divergência de Valor Total
                  </Tabs.Tab>

                  <Tabs.Tab
                    value="date-mismatches"
                    leftSection={<IconCalendarTime size={16} />}
                    rightSection={
                      <Badge size="xs" color="cyan" variant="filled">
                        {result.dateMismatchSummary?.count || 0}
                      </Badge>
                    }
                  >
                    Divergência de Data
                  </Tabs.Tab>

                  <Tabs.Tab
                    value="seller-mismatches"
                    leftSection={<IconUserExclamation size={16} />}
                    rightSection={
                      <Badge size="xs" color="indigo" variant="filled">
                        {result.sellerMismatchSummary?.count || 0}
                      </Badge>
                    }
                  >
                    Divergência de Vendedor
                  </Tabs.Tab>

                  <Tabs.Tab
                    value="status-mismatches"
                    leftSection={<IconTags size={16} />}
                    rightSection={
                      <Badge size="xs" color="teal" variant="filled">
                        {result.statusMismatchSummary?.count || 0}
                      </Badge>
                    }
                  >
                    Divergência de Status
                  </Tabs.Tab>

                  <Tabs.Tab
                    value="unassigned-users"
                    leftSection={<IconUserX size={16} />}
                    rightSection={
                      <Badge size="xs" color="gray" variant="filled">
                        {result.unassignedUserSummary.count}
                      </Badge>
                    }
                  >
                    Importados sem Usuário
                  </Tabs.Tab>
                </Tabs.List>

                {/* Table Toolbar */}
                <div className="table-toolbar">
                  <TextInput
                    placeholder="Buscar por contrato ou usuário..."
                    leftSection={<IconSearch size={16} />}
                    value={searchQuery}
                    onChange={(e) => setSearchQuery(e.target.value)}
                    style={{ minWidth: '280px' }}
                  />

                  <Button
                    variant="light"
                    color="blue"
                    leftSection={<IconDownload size={16} />}
                    onClick={handleExportCSV}
                  >
                    Exportar Relatório CSV
                  </Button>
                </div>

                {/* Tab 1: Missing in System */}
                <Tabs.Panel value="missing-in-system">
                  {filteredMissingInSystem.length === 0 ? (
                    <div className="empty-state">
                      <IconCheck className="empty-icon" color="green" />
                      <Text fw={600}>Nenhum contrato ausente no sistema!</Text>
                      <Text size="sm">Todos os contratos da planilha importada para este usuário/equipe existem no sistema.</Text>
                    </div>
                  ) : (
                    <div className="table-responsive">
                      <Table striped highlightOnHover>
                        <Table.Thead>
                          <Table.Tr>
                            <Table.Th>Número do Contrato</Table.Th>
                            <Table.Th>Valor na Planilha (XLSX)</Table.Th>
                            <Table.Th>Usuário</Table.Th>
                            <Table.Th>Data do Registro</Table.Th>
                            <Table.Th>Status</Table.Th>
                          </Table.Tr>
                        </Table.Thead>
                        <Table.Tbody>
                          {filteredMissingInSystem.map((item, index) => (
                            <Table.Tr key={index}>
                              <Table.Td>
                                <Text fw={600}>{item.contractNumber}</Text>
                              </Table.Td>
                              <Table.Td>{formatCurrency(item.totalAmount)}</Table.Td>
                              <Table.Td>{item.systemUserName || item.userIdentifier || '-'}</Table.Td>
                              <Table.Td>{formatDate(item.date)}</Table.Td>
                              <Table.Td>
                                <Badge color="red" variant="light">
                                  Não Encontrado no Sistema
                                </Badge>
                              </Table.Td>
                            </Table.Tr>
                          ))}
                        </Table.Tbody>
                      </Table>
                    </div>
                  )}
                </Tabs.Panel>

                {/* Tab 2: Missing in Import */}
                <Tabs.Panel value="missing-in-import">
                  {filteredMissingInImport.length === 0 ? (
                    <div className="empty-state">
                      <IconCheck className="empty-icon" color="green" />
                      <Text fw={600}>Nenhum contrato ausente na planilha!</Text>
                      <Text size="sm">Todos os contratos cadastrados no sistema dentro do período constam no arquivo enviado.</Text>
                    </div>
                  ) : (
                    <div className="table-responsive">
                      <Table striped highlightOnHover>
                        <Table.Thead>
                          <Table.Tr>
                            <Table.Th>Número do Contrato</Table.Th>
                            <Table.Th>Valor no Sistema</Table.Th>
                            <Table.Th>Usuário no Sistema</Table.Th>
                            <Table.Th>Data de Venda</Table.Th>
                            <Table.Th>Status</Table.Th>
                          </Table.Tr>
                        </Table.Thead>
                        <Table.Tbody>
                          {filteredMissingInImport.map((item, index) => (
                            <Table.Tr key={index}>
                              <Table.Td>
                                <Text fw={600}>{item.contractNumber}</Text>
                              </Table.Td>
                              <Table.Td>{formatCurrency(item.totalAmount)}</Table.Td>
                              <Table.Td>{item.systemUserName || item.userIdentifier || '-'}</Table.Td>
                              <Table.Td>{formatDate(item.date)}</Table.Td>
                              <Table.Td>
                                <Badge color="yellow" variant="light">
                                  Ausente na Planilha XLSX
                                </Badge>
                              </Table.Td>
                            </Table.Tr>
                          ))}
                        </Table.Tbody>
                      </Table>
                    </div>
                  )}
                </Tabs.Panel>

                {/* Tab 3: Amount Mismatches */}
                <Tabs.Panel value="amount-mismatches">
                  {filteredAmountMismatches.length === 0 ? (
                    <div className="empty-state">
                      <IconCheck className="empty-icon" color="green" />
                      <Text fw={600}>Nenhuma divergência de valor!</Text>
                      <Text size="sm">Os valores de todos os contratos correspondentes conferem exatamente.</Text>
                    </div>
                  ) : (
                    <div className="table-responsive">
                      <Table striped highlightOnHover>
                        <Table.Thead>
                          <Table.Tr>
                            <Table.Th>Número do Contrato</Table.Th>
                            <Table.Th>Valor no Sistema</Table.Th>
                            <Table.Th>Valor no XLSX</Table.Th>
                            <Table.Th>Diferença</Table.Th>
                            <Table.Th>Usuário</Table.Th>
                            <Table.Th>Data de Venda</Table.Th>
                          </Table.Tr>
                        </Table.Thead>
                        <Table.Tbody>
                          {filteredAmountMismatches.map((item, index) => (
                            <Table.Tr key={index}>
                              <Table.Td>
                                <Text fw={600}>{item.contractNumber}</Text>
                              </Table.Td>
                              <Table.Td>{formatCurrency(item.systemAmount)}</Table.Td>
                              <Table.Td>{formatCurrency(item.xlsxAmount)}</Table.Td>
                              <Table.Td>
                                <Text fw={700} color="red">
                                  {formatCurrency(item.difference)}
                                </Text>
                              </Table.Td>
                              <Table.Td>{item.systemUserName || item.userIdentifier || '-'}</Table.Td>
                              <Table.Td>{formatDate(item.saleStartDate)}</Table.Td>
                            </Table.Tr>
                          ))}
                        </Table.Tbody>
                      </Table>
                    </div>
                  )}
                </Tabs.Panel>

                {/* Tab 4: Date Mismatches */}
                <Tabs.Panel value="date-mismatches">
                  {filteredDateMismatches.length === 0 ? (
                    <div className="empty-state">
                      <IconCheck className="empty-icon" color="green" />
                      <Text fw={600}>Nenhuma divergência de data!</Text>
                      <Text size="sm">As datas de venda de todos os contratos conferem entre a planilha e o sistema.</Text>
                    </div>
                  ) : (
                    <div className="table-responsive">
                      <Table striped highlightOnHover>
                        <Table.Thead>
                          <Table.Tr>
                            <Table.Th>Número do Contrato</Table.Th>
                            <Table.Th>Data no Sistema</Table.Th>
                            <Table.Th>Data no XLSX</Table.Th>
                            <Table.Th>Valor Total</Table.Th>
                            <Table.Th>Usuário no Sistema</Table.Th>
                            <Table.Th>Status</Table.Th>
                          </Table.Tr>
                        </Table.Thead>
                        <Table.Tbody>
                          {filteredDateMismatches.map((item, index) => (
                            <Table.Tr key={index}>
                              <Table.Td>
                                <Text fw={600}>{item.contractNumber}</Text>
                              </Table.Td>
                              <Table.Td>{formatDate(item.systemDate)}</Table.Td>
                              <Table.Td>{formatDate(item.xlsxDate)}</Table.Td>
                              <Table.Td>{formatCurrency(item.totalAmount)}</Table.Td>
                              <Table.Td>{item.systemUserName || '-'}</Table.Td>
                              <Table.Td>
                                <Badge color="cyan" variant="light">
                                  Data Divergente
                                </Badge>
                              </Table.Td>
                            </Table.Tr>
                          ))}
                        </Table.Tbody>
                      </Table>
                    </div>
                  )}
                </Tabs.Panel>

                {/* Tab 5: Seller Mismatches */}
                <Tabs.Panel value="seller-mismatches">
                  {filteredSellerMismatches.length === 0 ? (
                    <div className="empty-state">
                      <IconCheck className="empty-icon" color="green" />
                      <Text fw={600}>Nenhuma divergência de vendedor!</Text>
                      <Text size="sm">Os vendedores de todos os contratos correspondentes coincidem entre o sistema e o XLSX.</Text>
                    </div>
                  ) : (
                    <div className="table-responsive">
                      <Table striped highlightOnHover>
                        <Table.Thead>
                          <Table.Tr>
                            <Table.Th>Número do Contrato</Table.Th>
                            <Table.Th>Vendedor no Sistema</Table.Th>
                            <Table.Th>Vendedor no XLSX</Table.Th>
                            <Table.Th>Valor Total</Table.Th>
                            <Table.Th>Data de Venda</Table.Th>
                            <Table.Th>Status</Table.Th>
                          </Table.Tr>
                        </Table.Thead>
                        <Table.Tbody>
                          {filteredSellerMismatches.map((item, index) => (
                            <Table.Tr key={index}>
                              <Table.Td>
                                <Text fw={600}>{item.contractNumber}</Text>
                              </Table.Td>
                              <Table.Td>{item.systemUserName || '-'}</Table.Td>
                              <Table.Td>{item.xlsxUserIdentifier || '-'}</Table.Td>
                              <Table.Td>{formatCurrency(item.totalAmount)}</Table.Td>
                              <Table.Td>{formatDate(item.saleStartDate)}</Table.Td>
                              <Table.Td>
                                <Badge color="indigo" variant="light">
                                  Vendedor Divergente
                                </Badge>
                              </Table.Td>
                            </Table.Tr>
                          ))}
                        </Table.Tbody>
                      </Table>
                    </div>
                  )}
                </Tabs.Panel>

                {/* Tab: Status Mismatches */}
                <Tabs.Panel value="status-mismatches">
                  {filteredStatusMismatches.length === 0 ? (
                    <div className="empty-state">
                      <IconCheck className="empty-icon" color="green" />
                      <Text fw={600}>Nenhuma divergência de status!</Text>
                      <Text size="sm">Os status de todos os contratos correspondentes coincidem entre o sistema e o XLSX.</Text>
                    </div>
                  ) : (
                    <div className="table-responsive">
                      <Table striped highlightOnHover>
                        <Table.Thead>
                          <Table.Tr>
                            <Table.Th>Número do Contrato</Table.Th>
                            <Table.Th>Status no Sistema</Table.Th>
                            <Table.Th>Status no XLSX</Table.Th>
                            <Table.Th>Valor Total</Table.Th>
                            <Table.Th>Usuário no Sistema</Table.Th>
                            <Table.Th>Data de Venda</Table.Th>
                            <Table.Th>Status</Table.Th>
                          </Table.Tr>
                        </Table.Thead>
                        <Table.Tbody>
                          {filteredStatusMismatches.map((item, index) => (
                            <Table.Tr key={index}>
                              <Table.Td>
                                <Text fw={600}>{item.contractNumber}</Text>
                              </Table.Td>
                              <Table.Td>
                                <Badge color="blue" variant="light">{item.systemStatus || 'Não Definido'}</Badge>
                              </Table.Td>
                              <Table.Td>
                                <Badge color="orange" variant="light">{item.xlsxStatus || 'Não Informado'}</Badge>
                              </Table.Td>
                              <Table.Td>{formatCurrency(item.totalAmount)}</Table.Td>
                              <Table.Td>{item.systemUserName || '-'}</Table.Td>
                              <Table.Td>{formatDate(item.saleStartDate)}</Table.Td>
                              <Table.Td>
                                <Badge color="teal" variant="light">
                                  Status Divergente
                                </Badge>
                              </Table.Td>
                            </Table.Tr>
                          ))}
                        </Table.Tbody>
                      </Table>
                    </div>
                  )}
                </Tabs.Panel>

                {/* Tab 7: Unassigned Users */}
                <Tabs.Panel value="unassigned-users">
                  {filteredUnassigned.length === 0 ? (
                    <div className="empty-state">
                      <IconCheck className="empty-icon" color="green" />
                      <Text fw={600}>Nenhum contrato sem usuário!</Text>
                      <Text size="sm">Todos os contratos importados possuem usuário reconhecido no sistema.</Text>
                    </div>
                  ) : (
                    <div className="table-responsive">
                      <Table striped highlightOnHover>
                        <Table.Thead>
                          <Table.Tr>
                            <Table.Th>Número do Contrato</Table.Th>
                            <Table.Th>Valor no XLSX</Table.Th>
                            <Table.Th>Identificador no Arquivo</Table.Th>
                            <Table.Th>Data</Table.Th>
                            <Table.Th>Observação</Table.Th>
                          </Table.Tr>
                        </Table.Thead>
                        <Table.Tbody>
                          {filteredUnassigned.map((item, index) => (
                            <Table.Tr key={index}>
                              <Table.Td>
                                <Text fw={600}>{item.contractNumber}</Text>
                              </Table.Td>
                              <Table.Td>{formatCurrency(item.totalAmount)}</Table.Td>
                              <Table.Td>
                                <Badge color="gray">{item.userIdentifier || 'Não Informado'}</Badge>
                              </Table.Td>
                              <Table.Td>{formatDate(item.date)}</Table.Td>
                              <Table.Td>
                                <Text size="sm" color="dimmed">
                                  Usuário não localizado no banco de dados
                                </Text>
                              </Table.Td>
                            </Table.Tr>
                          ))}
                        </Table.Tbody>
                      </Table>
                    </div>
                  )}
                </Tabs.Panel>
              </Tabs>
            </div>
          </>
        )}
      </div>
    </Menu>
  );
};

export default ContractReconciliationPage;
