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
  Switch,
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
  IconUsers,
} from '@tabler/icons-react';
import {
  apiService,
  ContractReconciliationResult,
  Team,
  UserComparisonItem,
} from '../services/apiService';
import { notifications } from '@mantine/notifications';
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
  const [allowPartialNameMatch, setAllowPartialNameMatch] = useState(false);
  const [detectingDates, setDetectingDates] = useState(false);
  const [detectedFormatInfo, setDetectedFormatInfo] = useState<string | null>(null);

  const handleFileChange = async (selectedFile: File | null) => {
    setFile(selectedFile);
    setDetectedFormatInfo(null);
    if (!selectedFile) return;

    try {
      setDetectingDates(true);
      const res = await apiService.detectReconciliationDateRange(selectedFile);
      if (res.startDate && res.endDate) {
        setStartDate(res.startDate);
        setEndDate(res.endDate);
        const formatLabel = res.detectedFormat || 'dd/MM/yyyy';
        setDetectedFormatInfo(`Padrão detectado: ${formatLabel}`);
        notifications.show({
          title: 'Período preenchido automaticamente',
          message: `Datas ajustadas para ${formatDate(res.startDate)} até ${formatDate(res.endDate)} (Padrão: ${formatLabel}).`,
          color: 'teal',
          icon: <IconCheck size={18} />,
        });
      }
    } catch (err: any) {
      console.warn('Não foi possível autodetectar período das datas:', err);
    } finally {
      setDetectingDates(false);
    }
  };

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
  const [exportingXlsx, setExportingXlsx] = useState(false);

  // Fetch teams and users on mount
  useEffect(() => {
    const fetchData = async () => {
      setLoadingTeams(true);
      setLoadingUsers(true);

      try {
        const [teamsRes, usersRes] = await Promise.allSettled([
          apiService.getTeams(),
          apiService.getUsers(1, 1000, undefined, undefined, false, true, 'active'),
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

  const selectedUser = useMemo(
    () => users.find((u) => u.value === selectedUserId),
    [users, selectedUserId]
  );

  const teamFilteredUsers = useMemo(() => {
    if (!selectedTeamId || !selectedTeam) {
      return users;
    }
    return (selectedTeam.members || [])
      .filter((m) => m.isActive)
      .map((m) => ({
        value: m.userId,
        label: `${m.userName} (${m.userEmail})`,
      }))
      .sort((a, b) => a.label.localeCompare(b.label));
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
        selectedTeamId ? parseInt(selectedTeamId, 10) : undefined,
        allowPartialNameMatch
      );
      setResult(res);
      setActiveTab('missing-in-system');
    } catch (err: any) {
      setError(err?.message || 'Ocorreu um erro ao processar o arquivo de reconciliação.');
    } finally {
      setLoading(false);
    }
  };

  const sanitizeFilename = (name: string) => {
    return name.replace(/[/\\?%*:|"<>]/g, '-').trim();
  };

  const getExportScope = () => {
    if (selectedTeam?.name) {
      return sanitizeFilename(selectedTeam.name);
    }
    if (selectedUser?.label) {
      const cleanName = selectedUser.label.replace(/\s*\([^)]*\)\s*$/, '').trim();
      return sanitizeFilename(cleanName);
    }
    return 'Geral';
  };

  // XLSX Export for active tab with problem-descriptive naming
  const handleExportXlsx = async () => {
    if (!result) return;

    const scope = getExportScope();
    let title = '';
    let filename = '';
    let headers: string[] = [];
    let rows: string[][] = [];

    if (activeTab === 'missing-in-system') {
      title = 'Não cadastrados no Sistema';
      filename = `Contratos na planilha que não existem no sistema - ${scope}.xlsx`;
      headers = ['Número do Contrato', 'Valor (XLSX)', 'Usuário', 'Data'];
      rows = filteredMissingInSystem.map((item) => [
        item.contractNumber,
        item.totalAmount.toFixed(2),
        item.systemUserName || item.userIdentifier || '',
        formatDate(item.date),
      ]);
    } else if (activeTab === 'missing-in-import') {
      title = 'Ausentes no XLSX';
      filename = `Contratos no sistema que não existem na planilha ou consultor diferente - ${scope}.xlsx`;
      headers = ['Número do Contrato', 'Valor (Sistema)', 'Usuário no Sistema', 'Data de Venda'];
      rows = filteredMissingInImport.map((item) => [
        item.contractNumber,
        item.totalAmount.toFixed(2),
        item.systemUserName || item.userIdentifier || '',
        formatDate(item.date),
      ]);
    } else if (activeTab === 'amount-mismatches') {
      title = 'Divergência de Valor';
      filename = `Divergência de valor entre planilha e sistema - ${scope}.xlsx`;
      headers = ['Número do Contrato', 'Valor Sistema', 'Valor XLSX', 'Diferença', 'Usuário', 'Data de Venda'];
      rows = filteredAmountMismatches.map((item) => [
        item.contractNumber,
        item.systemAmount.toFixed(2),
        item.xlsxAmount.toFixed(2),
        item.difference.toFixed(2),
        item.systemUserName || item.userIdentifier || '',
        formatDate(item.saleStartDate),
      ]);
    } else if (activeTab === 'date-mismatches') {
      title = 'Divergência de Data';
      filename = `Divergência de data da venda entre planilha e sistema - ${scope}.xlsx`;
      headers = ['Número do Contrato', 'Data no Sistema', 'Data no XLSX', 'Valor Total', 'Usuário no Sistema'];
      rows = filteredDateMismatches.map((item) => [
        item.contractNumber,
        formatDate(item.systemDate),
        formatDate(item.xlsxDate),
        item.totalAmount.toFixed(2),
        item.systemUserName || '',
      ]);
    } else if (activeTab === 'seller-mismatches') {
      title = 'Divergência de Consultor';
      filename = `Divergência de consultor entre planilha e sistema - ${scope}.xlsx`;
      headers = ['Número do Contrato', 'Vendedor no Sistema', 'Vendedor no XLSX', 'Valor Total', 'Data de Venda'];
      rows = filteredSellerMismatches.map((item) => [
        item.contractNumber,
        item.systemUserName || '',
        item.xlsxUserIdentifier || '',
        item.totalAmount.toFixed(2),
        formatDate(item.saleStartDate),
      ]);
    } else if (activeTab === 'status-mismatches') {
      title = 'Divergência de Status';
      filename = `Divergência de status entre planilha e sistema - ${scope}.xlsx`;
      headers = ['Número do Contrato', 'Status no Sistema', 'Status no XLSX', 'Valor Total', 'Usuário no Sistema', 'Data de Venda'];
      rows = filteredStatusMismatches.map((item) => [
        item.contractNumber,
        item.systemStatus || '',
        item.xlsxStatus || '',
        item.totalAmount.toFixed(2),
        item.systemUserName || '',
        formatDate(item.saleStartDate),
      ]);
    } else if (activeTab === 'unassigned-users') {
      title = 'Sem Usuário Atribuído';
      filename = `Contratos na planilha sem consultor identificado no sistema - ${scope}.xlsx`;
      headers = ['Número do Contrato', 'Valor (XLSX)', 'Identificador de Usuário (XLSX)', 'Data'];
      rows = filteredUnassigned.map((item) => [
        item.contractNumber,
        item.totalAmount.toFixed(2),
        item.userIdentifier || '',
        formatDate(item.date),
      ]);
    } else if (activeTab === 'user-comparison') {
      title = 'Comparação por Consultor';
      filename = `Comparação financeira e contratos por consultor - ${scope}.xlsx`;
      headers = ['Nome do Usuário', 'Total Planilha', 'Total Sistema', 'Qtd Contratos'];
      rows = filteredUserComparisons.map((item) => [
        item.userName,
        item.xlsxTotal.toFixed(2),
        item.systemTotal.toFixed(2),
        item.contractDiff > 0 ? `+${item.contractDiff}` : `${item.contractDiff}`,
      ]);
    }

    try {
      setExportingXlsx(true);
      const blob = await apiService.exportReconciliationXlsx({
        title,
        headers,
        rows,
      });

      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.setAttribute('href', url);
      link.setAttribute('download', filename);
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);
      URL.revokeObjectURL(url);
    } catch (err: any) {
      notifications.show({
        title: 'Erro na exportação',
        message: err.message || 'Falha ao gerar arquivo XLSX.',
        color: 'red',
      });
    } finally {
      setExportingXlsx(false);
    }
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

  const userComparisonData = useMemo<UserComparisonItem[]>(() => {
    if (!result) return [];
    if (result.userComparisons && result.userComparisons.length > 0) {
      return result.userComparisons;
    }

    // Helper to normalize names
    const normalizeName = (str?: string) => {
      if (!str) return '';
      return str
        .trim()
        .normalize('NFD')
        .replace(/[\u0300-\u036f]/g, '')
        .toLowerCase()
        .replace(/\s+/g, ' ');
    };

    // Fallback if backend does not supply userComparisons directly
    const userMap = new Map<string, { displayName: string; xlsxTotal: number; systemTotal: number; xlsxCount: number; systemCount: number }>();
    const getAcc = (name: string) => {
      const key = normalizeName(name);
      let acc = userMap.get(key);
      if (!acc) {
        acc = { displayName: name, xlsxTotal: 0, systemTotal: 0, xlsxCount: 0, systemCount: 0 };
        userMap.set(key, acc);
      }
      return acc;
    };

    result.missingInSystem.forEach((item) => {
      const u = item.systemUserName || item.userIdentifier || 'Sem Usuário Atribuído';
      const acc = getAcc(u);
      acc.xlsxTotal += item.totalAmount;
      acc.xlsxCount += 1;
    });

    result.missingInImport.forEach((item) => {
      const u = item.systemUserName || item.userIdentifier || 'Sem Usuário Atribuído';
      const acc = getAcc(u);
      acc.systemTotal += item.totalAmount;
      acc.systemCount += 1;
    });

    result.amountMismatches.forEach((item) => {
      const u = item.systemUserName || item.userIdentifier || 'Sem Usuário Atribuído';
      const acc = getAcc(u);
      acc.xlsxTotal += item.xlsxAmount;
      acc.systemTotal += item.systemAmount;
      acc.xlsxCount += 1;
      acc.systemCount += 1;
    });

    result.unassignedUserContracts.forEach((item) => {
      const u = item.userIdentifier ? `Não atribuído (${item.userIdentifier})` : 'Sem Usuário Atribuído';
      const acc = getAcc(u);
      acc.xlsxTotal += item.totalAmount;
      acc.xlsxCount += 1;
    });

    return Array.from(userMap.values())
      .map((data) => ({
        userName: data.displayName,
        xlsxTotal: data.xlsxTotal,
        systemTotal: data.systemTotal,
        xlsxCount: data.xlsxCount,
        systemCount: data.systemCount,
        contractDiff: data.xlsxCount - data.systemCount,
      }))
      .sort((a, b) => b.xlsxTotal - a.xlsxTotal || b.systemTotal - a.systemTotal);
  }, [result]);

  const filteredUserComparisons = useMemo(() => {
    if (!searchQuery.trim()) return userComparisonData;
    const q = searchQuery.toLowerCase().trim();
    return userComparisonData.filter((item) => item.userName.toLowerCase().includes(q));
  }, [userComparisonData, searchQuery]);

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

              <div style={{ display: 'flex', flexDirection: 'column' }}>
                <FileInput
                  label="Planilha XLSX do Cliente"
                  placeholder="Selecione o arquivo (.xlsx)"
                  leftSection={<IconFileSpreadsheet size={18} />}
                  accept=".xlsx,.csv"
                  value={file}
                  onChange={handleFileChange}
                  required
                />
                {detectingDates && (
                  <Text size="xs" c="dimmed" mt={4}>
                    Analisando datas do arquivo...
                  </Text>
                )}
                {detectedFormatInfo && (
                  <Text size="xs" c="teal" mt={4}>
                    ✓ {detectedFormatInfo}
                  </Text>
                )}
              </div>

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

            <div style={{ marginTop: '16px', display: 'flex', alignItems: 'center', gap: '12px' }}>
              <Switch
                label="Permitir correspondência parcial de nomes de consultor/vendedor (se único)"
                checked={allowPartialNameMatch}
                onChange={(e) => setAllowPartialNameMatch(e.currentTarget.checked)}
                color="blue"
              />
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
                  <span className="kpi-label">No XLSX (Fora do Sistema)</span>
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
                    {selectedTeam ? `No Sistema (Fora do XLSX - ${selectedTeam.name})` : 'No Sistema (Fora do XLSX)'}
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

              {/* Card 8: User Comparison */}
              <div
                className={`kpi-card blue ${activeTab === 'user-comparison' ? 'active' : ''}`}
                onClick={() => setActiveTab('user-comparison')}
              >
                <div className="kpi-header">
                  <span className="kpi-label">Comparação por Usuário</span>
                  <div className="kpi-icon-wrapper">
                    <IconUsers size={20} />
                  </div>
                </div>
                <div className="kpi-count">{userComparisonData.length}</div>
                <div className="kpi-amount">
                  {userComparisonData.filter(u => u.contractDiff !== 0).length > 0
                    ? `${userComparisonData.filter(u => u.contractDiff !== 0).length} com divergência`
                    : 'Todos equalizados'}
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
                    No XLSX (Não cadastrados no Sistema)
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
                      ? `No Sistema (Equipe "${selectedTeam.name}", ausentes no XLSX)`
                      : 'No Sistema (Ausentes no XLSX)'}
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

                  <Tabs.Tab
                    value="user-comparison"
                    leftSection={<IconUsers size={16} />}
                    rightSection={
                      <Badge size="xs" color="blue" variant="filled">
                        {userComparisonData.length}
                      </Badge>
                    }
                  >
                    Comparação por Usuário
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
                    leftSection={exportingXlsx ? <Loader size="xs" color="blue" /> : <IconDownload size={16} />}
                    onClick={handleExportXlsx}
                    disabled={exportingXlsx}
                  >
                    {exportingXlsx ? 'Gerando XLSX...' : 'Exportar Planilha (XLSX)'}
                  </Button>
                </div>

                {/* Tab 1: Missing in System */}
                <Tabs.Panel value="missing-in-system">
                  {filteredMissingInSystem.length === 0 ? (
                    <div className="empty-state">
                      <IconCheck className="empty-icon" color="green" />
                      <Text fw={600}>Nenhum contrato da planilha pendente de cadastro no sistema!</Text>
                      <Text size="sm">Todos os contratos da planilha importada para este usuário/equipe já existem no sistema.</Text>
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
                                  Não Cadastrado no Sistema
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
                      <Text fw={600}>Nenhum contrato do sistema ausente na planilha!</Text>
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
                                  Não Consta na Planilha XLSX
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

                {/* Tab 8: User Comparison */}
                <Tabs.Panel value="user-comparison">
                  {filteredUserComparisons.length === 0 ? (
                    <div className="empty-state">
                      <IconCheck className="empty-icon" color="green" />
                      <Text fw={600}>Nenhum usuário encontrado para os filtros selecionados!</Text>
                      <Text size="sm">Não há dados de produção para comparar entre a planilha e o sistema.</Text>
                    </div>
                  ) : (
                    <div className="table-responsive">
                      <Table striped highlightOnHover>
                        <Table.Thead>
                          <Table.Tr>
                            <Table.Th>Nome do Usuário</Table.Th>
                            <Table.Th>Total Planilha</Table.Th>
                            <Table.Th>Total Sistema</Table.Th>
                            <Table.Th>Qtd Contratos</Table.Th>
                          </Table.Tr>
                        </Table.Thead>
                        <Table.Tbody>
                          {filteredUserComparisons.map((item, index) => (
                            <Table.Tr key={index}>
                              <Table.Td>
                                <Text fw={600}>{item.userName}</Text>
                              </Table.Td>
                              <Table.Td>{formatCurrency(item.xlsxTotal)}</Table.Td>
                              <Table.Td>{formatCurrency(item.systemTotal)}</Table.Td>
                              <Table.Td>
                                {item.contractDiff > 0 ? (
                                  <Badge color="green" variant="light">
                                    +{item.contractDiff} ({item.xlsxCount} xlsx / {item.systemCount} sistema)
                                  </Badge>
                                ) : item.contractDiff < 0 ? (
                                  <Badge color="red" variant="light">
                                    {item.contractDiff} ({item.xlsxCount} xlsx / {item.systemCount} sistema)
                                  </Badge>
                                ) : (
                                  <Badge color="gray" variant="light">
                                    0 ({item.xlsxCount} contratos)
                                  </Badge>
                                )}
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
