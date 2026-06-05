import React, { useState, useEffect, useCallback } from 'react';
import {
  Title,
  TextInput,
  Textarea,
  Button,
  Group,
  Stack,
  Switch,
  MultiSelect,
  Text,
  Badge,
  ActionIcon,
  Select,
  Grid,
  Paper,
  Card,
  Center,
  Loader,
  SegmentedControl,
  Collapse,
  Modal,
  ScrollArea,
  Checkbox
} from '@mantine/core';
import { DatePickerInput } from '@mantine/dates';
import { BarChart, PieChart, DonutChart, LineChart, AreaChart } from '@mantine/charts';
import { 
  IconArrowUp, 
  IconArrowDown, 
  IconTrash, 
  IconDeviceFloppy, 
  IconSearch, 
  IconCopy,
  IconGripVertical,
  IconChevronDown,
  IconChevronUp,
  IconRefresh,
  IconPlus,
  IconFilter
} from '@tabler/icons-react';
import Menu from '../Menu';
import { notifications } from '@mantine/notifications';
import { 
  getReportFilter, 
  createReportFilter, 
  updateReportFilter, 
  getAvailableColumns,
  getReportResults,
  ReportFilter,
  OutputColumn,
  FilterConfig,
  SourceColumns,
  ReportResultsResponse
} from '../../services/reportFilterService';
import { apiService, User } from '../../services/apiService';
import { getGroups } from '../../services/contractService';

interface ReportFormPageProps {
  filterId?: string; // If provided, we are in edit mode
}

interface ColumnMetadata {
  title: string;
  description: string;
}

const COLUMN_METADATA_DICT: Record<string, ColumnMetadata> = {
  'Contracts|contractNumber': {
    title: 'Número do Contrato',
    description: 'Código identificador único gerado no momento da venda do contrato.'
  },
  'Contracts|totalAmount': {
    title: 'Valor Total',
    description: 'O montante financeiro total negociado e assinado no contrato comercial.'
  },
  'Contracts|saleStartDate': {
    title: 'Data de Venda',
    description: 'Data oficial em que a venda foi sacramentada e o período de vigência se iniciou.'
  },
  'Contracts|isActive': {
    title: 'Contrato Ativo',
    description: 'Status binário que indica se o contrato está vigente ou se foi cancelado/defaultado.'
  },
  'Contracts|contractType': {
    title: 'Tipo de Contrato',
    description: 'O modelo de contrato comercial ou categoria operacional atribuída.'
  },
  'Contracts|quota': {
    title: 'Parcelas / Quotas',
    description: 'A quantidade de parcelas periódicas acordadas para o pagamento da venda.'
  },
  'Contracts|customerName': {
    title: 'Nome do Cliente',
    description: 'Nome completo ou razão social do comprador do contrato comercial.'
  },
  'Contracts|tempMatricula': {
    title: 'Matrícula Temporária',
    description: 'Código de matrícula temporário utilizado durante a importação antes da normalização.'
  },
  'Contracts|matriculaNumber': {
    title: 'Número de Matrícula',
    description: 'Número consolidado e normalizado da matrícula no banco de dados.'
  },
  'Contracts|createdAt': {
    title: 'Criado Em',
    description: 'Data e hora da inserção original deste registro de contrato no sistema.'
  },
  'Contracts|updatedAt': {
    title: 'Atualizado Em',
    description: 'Data e hora do último salvamento ou modificação dos campos deste contrato.'
  },
  'Users_Contract|name': {
    title: 'Nome do Vendedor',
    description: 'Nome completo do vendedor titular da venda no momento da assinatura.'
  },
  'Users_Contract|email': {
    title: 'E-mail do Vendedor',
    description: 'Endereço de e-mail institucional de cadastro do vendedor do contrato.'
  },
  'Users_Contract|team': {
    title: 'Equipe do Vendedor',
    description: 'Nome da equipe ativa do vendedor associada à venda na linha temporal.'
  },
  'Users_Contract|teamOwner': {
    title: 'Chefe da Equipe',
    description: 'Nome do líder, gerente ou supervisor imediato responsável pela equipe.'
  },
  'Users_Contract|classification': {
    title: 'Nível de Classificação',
    description: 'Nível de classificação ativo (ex: Nível I, Nível II) do vendedor.'
  },
  'Users_Matricula|name': {
    title: 'Nome do Titular da Matrícula',
    description: 'Nome do usuário que possui a titularidade oficial da matrícula vinculada.'
  },
  'Users_Matricula|email': {
    title: 'E-mail do Titular da Matrícula',
    description: 'E-mail cadastrado para o titular oficial da matrícula do contrato.'
  },
  'Status|name': {
    title: 'Status do Contrato',
    description: 'Nome descritivo da situação atual do contrato (ex: Ativo, Atraso 1, Transferido).'
  },
  'PV|id': {
    title: 'ID do Ponto de Venda',
    description: 'Código de identificação física do ponto de venda no sistema.'
  },
  'PV|name': {
    title: 'Nome do Ponto de Venda',
    description: 'Nome fantasia ou designação comercial do ponto de venda.'
  },
  'Group|name': {
    title: 'Nome do Grupo',
    description: 'Nome identificador do grupo ou agrupamento organizacional.'
  },
  'Group|description': {
    title: 'Descrição do Grupo',
    description: 'Breve texto descritivo sobre o propósito ou setor do grupo comercial.'
  },
  'Group|commission': {
    title: 'Comissão do Grupo',
    description: 'Taxa ou fator percentual de comissão definido para o grupo.'
  },
  'Computed|contractCount': {
    title: 'Quantidade de Contratos',
    description: 'Contagem total de contratos agregados computada para o período configurado.'
  },
  'Computed|retention': {
    title: 'Taxa de Retenção',
    description: 'Fração percentual computada de contratos ativos em relação ao portfolio total.'
  },
  'Computed|strictRetention': {
    title: 'Taxa de Retenção Estrita',
    description: 'Taxa de retenção considerando contratos adimplentes (excluindo qualquer atraso).'
  }
};

const toISOStringSafe = (val: any): string | undefined => {
  if (!val) return undefined;
  const d = val instanceof Date ? val : new Date(val);
  return !isNaN(d.getTime()) ? d.toISOString() : undefined;
};

const ReportFormPage: React.FC<ReportFormPageProps> = ({ filterId }) => {
  const [localFilterId, setLocalFilterId] = useState<string | undefined>(filterId);
  const isEditMode = !!localFilterId;

  // Metadata
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [scope, setScope] = useState<'private' | 'shared'>('private');
  
  // Visibility Restrictions (Allowed Teams and Roles)
  const [allowedTeamIds, setAllowedTeamIds] = useState<string[]>([]);
  const [allowedRoles, setAllowedRoles] = useState<string[]>([]);

  // Date Range Configuration
  const [dateRangePreset, setDateRangePreset] = useState<'30d' | '12m' | '15m' | 'custom'>('12m');
  const [dateMode, setDateMode] = useState<'absolute' | 'relative'>('relative');
  const [dateRange, setDateRange] = useState<[Date | null, Date | null]>([null, null]);
  const [relativeStartDate, setRelativeStartDate] = useState('-12M');
  const [relativeEndDate, setRelativeEndDate] = useState('now');

  // Advanced Filters Collapse
  const [advancedOpen, setAdvancedOpen] = useState(false);
  const [matriculas, setMatriculas] = useState<string[]>([]);
  const [currentUserAsParent, setCurrentUserAsParent] = useState(false);
  const [statusOperator, setStatusOperator] = useState<'or' | 'and'>('or');

  // Standard Filters
  const [emails, setEmails] = useState<string[]>([]);
  const [groups, setGroups] = useState<string[]>([]); 
  const [teams, setTeams] = useState<string[]>([]);
  const [pvs, setPvs] = useState<string[]>([]);
  const [statuses, setStatuses] = useState<string[]>([]);

  // Grouping
  const [groupingType, setGroupingType] = useState<'none' | 'team' | 'email' | 'classification'>('none');
  const [groupByEmail, setGroupByEmail] = useState(false);
  const [groupByTeam, setGroupByTeam] = useState(false);
  const [groupByClassification, setGroupByClassification] = useState(false);
  const [hideUnassignedTeams, setHideUnassignedTeams] = useState(false);

  // Column Summing & Output Type
  const [sumTotal, setSumTotal] = useState(false);
  const [summaryRetentionType, setSummaryRetentionType] = useState<'standard' | 'strict'>('standard');
  const [chartMetric, setChartMetric] = useState<string>('');
  const [outputType, setOutputType] = useState<string>('table');
  const [chartType, setChartType] = useState<string>('bar');

  // Column Selection Modal
  const [columnModalOpen, setColumnModalOpen] = useState(false);
  const [columnSearch, setColumnSearch] = useState('');
  const [tempSelectedColumns, setTempSelectedColumns] = useState<string[]>([]);

  // Columns & Sorting
  const [outputColumns, setOutputColumns] = useState<OutputColumn[]>([]);
  const [orderByField, setOrderByField] = useState<string | null>(null);
  const [orderByDirection, setOrderByDirection] = useState<'asc' | 'desc'>('asc');
  
  // Options
  const [availableColumns, setAvailableColumns] = useState<SourceColumns[]>([]);
  const [userOptions, setUserOptions] = useState<{value: string, label: string}[]>([]);
  const [groupOptions, setGroupOptions] = useState<{value: string, label: string}[]>([]);
  const [teamOptions, setTeamOptions] = useState<{value: string, label: string}[]>([]);
  const [pvOptions, setPvOptions] = useState<{value: string, label: string}[]>([]);
  
  // Preview
  const [previewData, setPreviewData] = useState<ReportResultsResponse | null>(null);
  const [previewLoading, setPreviewLoading] = useState(false);
  const [previewError, setPreviewError] = useState<string | null>(null);

  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);

  // Load Form Data
  const loadData = useCallback(async () => {
    try {
      setLoading(true);
      // Fetch available columns
      const colsRes = await getAvailableColumns();
      setAvailableColumns(colsRes.sources);

      // Fetch users, groups, PVs, teams for multiselects
      const [usersRes, groupsData, pvsRes, teamsRes] = await Promise.all([
        apiService.getUsers(1, 1000),
        getGroups(),
        apiService.getPVs(),
        apiService.getTeams()
      ]);
      
      setUserOptions((usersRes.data?.items || []).map((u: User) => ({ value: u.email, label: u.name })));
      setGroupOptions((groupsData || []).map((g: any) => ({ value: g.id.toString(), label: g.name })));
      setTeamOptions((teamsRes.data || []).map((t: any) => ({ value: t.id.toString(), label: t.name })));
      setPvOptions((pvsRes.data || []).map((p: any) => ({ value: p.id.toString(), label: p.name })));

      // If edit mode, fetch the report filter
      if (localFilterId) {
        const report = await getReportFilter(localFilterId);
        setName(report.name);
        setDescription(report.description || '');
        setScope(report.scope);
        setAllowedTeamIds((report.allowedTeamIds || []).map(String));
        setAllowedRoles(report.allowedRoles || []);
        
        const fc = report.filterConfig;
        setMatriculas(fc.matriculas || []);
        setDateRange([
          fc.startDate ? new Date(fc.startDate) : null,
          fc.endDate ? new Date(fc.endDate) : null
        ]);
        setRelativeStartDate(fc.relativeStartDate || '');
        setRelativeEndDate(fc.relativeEndDate || '');
        if (fc.relativeStartDate || fc.relativeEndDate) {
          setDateMode('relative');
        } else {
          setDateMode('absolute');
        }

        // Determine Preset Date Range
        let preset: '30d' | '12m' | '15m' | 'custom' = 'custom';
        if (fc.relativeStartDate === '-30d' && fc.relativeEndDate === 'now') {
          preset = '30d';
        } else if ((fc.relativeStartDate === '-12M' || fc.relativeStartDate === '-1y') && fc.relativeEndDate === 'now') {
          preset = '12m';
        } else if (fc.relativeStartDate === '-15M' && fc.relativeEndDate === 'now') {
          preset = '15m';
        } else if (!fc.startDate && !fc.endDate && !fc.relativeStartDate && !fc.relativeEndDate) {
          preset = 'custom';
        }
        setDateRangePreset(preset);

        setCurrentUserAsParent(fc.currentUserAsParent || false);
        setEmails(fc.emails || []);
        setGroups((fc.groups || []).map(g => g.toString()));
        setTeams((fc.teams || []).map(t => t.toString()));
        setPvs((fc.pvs || []).map(p => p.toString()));
        setStatuses(fc.statuses || []);
        setStatusOperator(fc.statusOperator || 'or');
        
        setOutputColumns(report.outputColumns || []);
        setGroupByEmail(report.groupByEmail || false);
        setGroupByTeam(report.groupByTeam || false);
        setGroupByClassification(report.groupByClassification || false);
        setHideUnassignedTeams(report.hideUnassignedTeams || false);
        setOrderByField(report.orderByField || null);
        setOrderByDirection(report.orderByDirection || 'asc');

        // Restore Sum and Chart types
        setSumTotal(report.sumTotal || false);
        setSummaryRetentionType(report.summaryRetentionType || 'standard');
        setChartMetric(report.chartMetric || '');
        setOutputType(report.outputType || 'table');
        setChartType(report.chartType || 'bar');

        // Parse Grouping type
        if (report.groupByEmail) {
          setGroupingType('email');
        } else if (report.groupByTeam) {
          setGroupingType('team');
        } else if (report.groupByClassification) {
          setGroupingType('classification');
        } else {
          setGroupingType('none');
        }

        // Auto-run preview for existing reports on load
        try {
          setPreviewLoading(true);
          const results = await getReportResults(localFilterId, 1, 10);
          setPreviewData(results);
        } catch (err: any) {
          setPreviewError(err.message || 'Falha ao carregar prévia inicial');
        } finally {
          setPreviewLoading(false);
        }
      }
    } catch (err: any) {
      notifications.show({ title: 'Erro', message: err.message || 'Falha ao carregar dados do formulário', color: 'red' });
    } finally {
      setLoading(false);
    }
  }, [localFilterId]);

  useEffect(() => {
    loadData();
  }, [loadData]);

  // Date range preset handler
  const handleDatePresetChange = (value: '30d' | '12m' | '15m' | 'custom') => {
    setDateRangePreset(value);
    if (value === '30d') {
      setDateMode('relative');
      setRelativeStartDate('-30d');
      setRelativeEndDate('now');
    } else if (value === '12m') {
      setDateMode('relative');
      setRelativeStartDate('-12M');
      setRelativeEndDate('now');
    } else if (value === '15m') {
      setDateMode('relative');
      setRelativeStartDate('-15M');
      setRelativeEndDate('now');
    } else if (value === 'custom') {
      // Retain previous relative options or absolute date range defaults
    }
  };

  // Grouping type changes
  const handleGroupingTypeChange = (value: 'none' | 'team' | 'email' | 'classification') => {
    setGroupingType(value);
    if (value === 'none') {
      setGroupByEmail(false);
      setGroupByTeam(false);
      setGroupByClassification(false);
      setHideUnassignedTeams(false);
    } else if (value === 'team') {
      setGroupByEmail(false);
      setGroupByTeam(true);
      setGroupByClassification(false);
    } else if (value === 'email') {
      setGroupByEmail(true);
      setGroupByTeam(false);
      setGroupByClassification(false);
      setHideUnassignedTeams(false);
    } else if (value === 'classification') {
      setGroupByEmail(false);
      setGroupByTeam(false);
      setGroupByClassification(true);
      setHideUnassignedTeams(false);
    }
  };

  const getFieldLabel = (source: string, field: string) => {
    if (source === 'Users_Contract') {
      if (field === 'team') return 'Equipe';
      if (field === 'teamOwner') return 'Chefe da equipe';
      if (field === 'name') return 'Nome (Vendedor)';
      if (field === 'email') return 'E-mail (Vendedor)';
      if (field === 'classification') return 'Nível de Classificação';
    }
    if (source === 'Users_Matricula') {
      if (field === 'name') return 'Nome (Matrícula)';
      if (field === 'email') return 'E-mail (Matrícula)';
    }
    return field;
  };

  // Open Column selection modal in multi-selection mode
  const handleOpenColumnModal = () => {
    // pre-populate temp selection with currently active columns
    const activeKeys = outputColumns.map(c => `${c.source}|${c.field}`);
    setTempSelectedColumns(activeKeys);
    setColumnSearch('');
    setColumnModalOpen(true);
  };

  // Handle saving columns chosen inside modal
  const handleSaveModalColumns = () => {
    const newColumns: OutputColumn[] = [];
    
    // We map over temp selection and preserve orders or recreate orders logically
    tempSelectedColumns.forEach((key, idx) => {
      const [source, field] = key.split('|');
      const existing = outputColumns.find(c => c.source === source && c.field === field);
      newColumns.push({
        source,
        field,
        label: existing ? existing.label : getFieldLabel(source, field),
        order: idx + 1,
        format: existing ? existing.format : undefined
      });
    });

    setOutputColumns(newColumns);
    setColumnModalOpen(false);
    notifications.show({ title: 'Colunas Atualizadas', message: `${newColumns.length} colunas configuradas no relatório.`, color: 'green' });
  };

  const handleToggleColumnTemp = (key: string) => {
    if (tempSelectedColumns.includes(key)) {
      setTempSelectedColumns(tempSelectedColumns.filter(k => k !== key));
    } else {
      setTempSelectedColumns([...tempSelectedColumns, key]);
    }
  };

  const handleRemoveColumn = (index: number) => {
    const newCols = [...outputColumns];
    newCols.splice(index, 1);
    // re-adjust order
    newCols.forEach((c, i) => c.order = i + 1);
    setOutputColumns(newCols);
  };

  const handleMoveColumn = (index: number, direction: 'up' | 'down') => {
    if (direction === 'up' && index === 0) return;
    if (direction === 'down' && index === outputColumns.length - 1) return;

    const newCols = [...outputColumns];
    const swapIndex = direction === 'up' ? index - 1 : index + 1;
    const temp = newCols[index];
    newCols[index] = newCols[swapIndex];
    newCols[swapIndex] = temp;
    
    // re-adjust order
    newCols.forEach((c, i) => c.order = i + 1);
    setOutputColumns(newCols);
  };

  const handleLabelChange = (index: number, newLabel: string) => {
    const newCols = [...outputColumns];
    newCols[index].label = newLabel;
    setOutputColumns(newCols);
  };

  const handleFormatChange = (index: number, newFormat: string | null) => {
    const newCols = [...outputColumns];
    newCols[index].format = newFormat || undefined;
    setOutputColumns(newCols);
  };

  // Safe build of standard report payload
  const buildPayload = () => {
    const filterConfig: FilterConfig = {
      matriculas: matriculas.length > 0 ? matriculas : undefined,
      startDate: dateMode === 'absolute' ? toISOStringSafe(dateRange[0]) : undefined,
      endDate: dateMode === 'absolute' ? toISOStringSafe(dateRange[1]) : undefined,
      relativeStartDate: dateMode === 'relative' && relativeStartDate ? relativeStartDate.trim() : undefined,
      relativeEndDate: dateMode === 'relative' && relativeEndDate ? relativeEndDate.trim() : undefined,
      currentUserAsParent: currentUserAsParent || undefined,
      emails: emails.length > 0 ? emails : undefined,
      groups: groups.length > 0 ? groups.map(Number) : undefined,
      teams: teams.length > 0 ? teams.map(Number) : undefined,
      pvs: pvs.length > 0 ? pvs.map(Number) : undefined,
      statuses: statuses.length > 0 ? statuses : undefined,
      statusOperator: statuses.length > 1 ? statusOperator : undefined,
    };

    return {
      name,
      description,
      scope,
      allowedTeamIds: scope === 'shared' ? allowedTeamIds.map(Number) : [],
      allowedRoles: scope === 'shared' ? allowedRoles : [],
      sumTotal,
      summaryRetentionType: sumTotal ? summaryRetentionType : undefined,
      chartMetric: chartMetric || undefined,
      outputType,
      chartType,
      filterConfig,
      outputColumns,
      groupByEmail,
      groupByTeam,
      groupByClassification,
      hideUnassignedTeams,
      orderByField: orderByField || undefined,
      orderByDirection: orderByField ? orderByDirection : undefined
    };
  };

  const handleSave = async (isPreview: boolean = false) => {
    try {
      setSaving(true);
      const payload = buildPayload();

      let savedFilter: ReportFilter;
      if (localFilterId) {
        savedFilter = await updateReportFilter(localFilterId, payload);
        notifications.show({ title: 'Sucesso', message: 'Relatório atualizado', color: 'green' });
      } else {
        savedFilter = await createReportFilter(payload);
        notifications.show({ title: 'Sucesso', message: 'Relatório criado', color: 'green' });
        setLocalFilterId(savedFilter.filterId);
      }
      
      if (isPreview) {
        window.location.hash = `#/reports/${savedFilter.filterId}/results`;
      } else {
        window.location.hash = '#/reports';
      }
    } catch (err: any) {
      notifications.show({ title: 'Erro', message: err.message || 'Falha ao salvar relatório', color: 'red' });
    } finally {
      setSaving(false);
    }
  };

  const handleClone = async () => {
    try {
      setSaving(true);
      const payload = {
        ...buildPayload(),
        name: `Cópia de ${name}`
      };

      const savedFilter = await createReportFilter(payload);
      notifications.show({ title: 'Sucesso', message: 'Relatório clonado', color: 'green' });
      setLocalFilterId(savedFilter.filterId);
      window.location.hash = `#/reports/${savedFilter.filterId}/edit`;
    } catch (err: any) {
      notifications.show({ title: 'Erro', message: err.message || 'Falha ao clonar relatório', color: 'red' });
    } finally {
      setSaving(false);
    }
  };

  // In-place live preview generation
  const handleRunPreview = async () => {
    if (!name.trim()) {
      notifications.show({ title: 'Aviso', message: 'Insira o nome do relatório antes de carregar a prévia.', color: 'orange' });
      return;
    }
    if (outputColumns.length === 0) {
      notifications.show({ title: 'Aviso', message: 'Adicione pelo menos uma coluna de saída para ver a prévia.', color: 'orange' });
      return;
    }

    try {
      setPreviewLoading(true);
      setPreviewError(null);
      const payload = buildPayload();
      
      let activeId = localFilterId;
      if (activeId) {
        await updateReportFilter(activeId, payload);
      } else {
        const savedFilter = await createReportFilter(payload);
        activeId = savedFilter.filterId;
        setLocalFilterId(activeId);
        window.history.replaceState(null, '', `#/reports/${activeId}/edit`);
      }

      const results = await getReportResults(activeId, 1, 10);
      setPreviewData(results);
      notifications.show({ title: 'Sucesso', message: 'Prévia atualizada', color: 'green' });
    } catch (err: any) {
      setPreviewError(err.message || 'Erro ao processar prévia do relatório.');
      notifications.show({ title: 'Erro', message: err.message || 'Falha ao gerar prévia', color: 'red' });
    } finally {
      setPreviewLoading(false);
    }
  };

  // Prepare Dynamic Aggregated Chart Data
  const prepareChartData = () => {
    if (!previewData || previewData.rows.length === 0) return [];
    
    const columns = previewData.columns;
    
    // 1. Identify category/label key (Team, Email, Classification or first string col)
    const groupCol = columns.find(c => c.field === 'team' || c.field === 'email' || c.field === 'classification') 
      || columns.find(c => c.source === 'Users_Contract' || c.source === 'Users_Matricula')
      || columns[0];
      
    const labelKey = groupCol ? groupCol.label : columns[0]?.label;

    // 2. Identify metric/value key (chartMetric if matched, otherwise totalAmount or first numeric col)
    let valueKey: string | null = null;
    if (chartMetric) {
      const found = columns.find(c => c.label === chartMetric || c.field === chartMetric);
      if (found) {
        valueKey = found.label;
      }
    }
    
    if (!valueKey) {
      const numericCol = columns.find(c => c.field === 'totalAmount' || c.field === 'contractCount' || c.field === 'quota' || c.field === 'commission')
        || columns.find(c => {
             const val = previewData.rows[0][c.label];
             return typeof val === 'number' || (typeof val === 'string' && !isNaN(parseFloat(val.replace(/[^0-9.-]+/g, ''))));
           })
        || columns[1]
        || columns[0];

      valueKey = numericCol ? numericCol.label : null;
    }

    const activeLabelKey = labelKey;
    const activeValueKey = valueKey;

    if (!activeLabelKey || !activeValueKey) return [];

    const colors = [
      '#6366f1', '#10b981', '#f59e0b', '#ef4444', '#228be6',
      '#845ef7', '#be4bdb', '#f06595', '#ff922b', '#51cf66'
    ];

    return previewData.rows.map((row, idx) => {
      const rawVal = row[activeValueKey];
      let valNum = 0;
      if (typeof rawVal === 'number') {
        valNum = rawVal;
      } else if (typeof rawVal === 'string') {
        const clean = rawVal.replace(/[R$\s.%]/g, '').replace(',', '.');
        valNum = parseFloat(clean) || 0;
      }

      return {
        name: String(row[activeLabelKey] || `Item ${idx + 1}`),
        value: valNum,
        color: colors[idx % colors.length]
      };
    });
  };

  // Compile list of all fields to display inside the searchable modal
  const getModalFields = () => {
    const list: { key: string; source: string; field: string; title: string; description: string }[] = [];
    availableColumns.forEach(src => {
      src.fields.forEach(f => {
        const key = `${src.source}|${f}`;
        const meta = COLUMN_METADATA_DICT[key] || { title: f, description: `Coluna técnica vinda da tabela de ${src.source}.` };
        list.push({
          key,
          source: src.source,
          field: f,
          title: meta.title,
          description: meta.description
        });
      });
    });

    if (!columnSearch) return list;
    const cleanSearch = columnSearch.toLowerCase();
    return list.filter(item => 
      item.title.toLowerCase().includes(cleanSearch) || 
      item.description.toLowerCase().includes(cleanSearch) || 
      item.field.toLowerCase().includes(cleanSearch) ||
      item.source.toLowerCase().includes(cleanSearch)
    );
  };

  const modalFieldsList = getModalFields();
  const chartData = prepareChartData();

  if (loading) {
    return <Menu><Center style={{ height: '80vh' }}><Loader /></Center></Menu>;
  }

  return (
    <Menu>
      <div style={{ padding: '20px', maxWidth: '1000px', margin: '0 auto' }}>
        <Group justify="space-between" mb="xl">
          <Stack gap={2}>
            <Title order={2} style={{ color: '#1c1c1e', fontWeight: 700 }}>
              {isEditMode ? 'Editar Relatório' : 'Novo Relatório'}
            </Title>
            <Text size="xs" c="dimmed">Configure filtros, acessos restritos, agregados e gráficos para gerar seu relatório gerencial.</Text>
          </Stack>
          <Group>
            {isEditMode && (
              <Button 
                variant="outline"
                color="gray"
                leftSection={<IconCopy size={16} />}
                onClick={handleClone}
                loading={saving}
              >
                Clonar este Relatório
              </Button>
            )}
            <Button variant="default" onClick={() => window.location.hash = '#/reports'}>Cancelar</Button>
          </Group>
        </Group>

        <Stack gap="lg">
          
          {/* Section 1: Basic details */}
          <Paper shadow="sm" p="lg" radius="md" withBorder style={{ backgroundColor: '#ffffff' }}>
            <Title order={4} mb="md" style={{ color: '#1c1c1e' }}>Detalhes do Relatório</Title>
            <Stack gap="sm">
              <TextInput
                label="Nome do Relatório"
                placeholder="Ex: Contratos de Alto Valor 2026"
                required
                value={name}
                onChange={(e) => setName(e.currentTarget.value)}
                maxLength={100}
              />
              <Textarea
                label="Descrição"
                placeholder="Descrição opcional deste relatório"
                value={description}
                onChange={(e) => setDescription(e.currentTarget.value)}
                maxLength={500}
                rows={2}
              />
              <Switch
                label="Relatório Compartilhado (Visível para outros usuários)"
                checked={scope === 'shared'}
                onChange={(event) => setScope(event.currentTarget.checked ? 'shared' : 'private')}
                mt="xs"
              />

              {scope === 'shared' && (
                <Paper withBorder p="sm" mt="xs" style={{ backgroundColor: '#f8f9fa' }}>
                  <Stack gap="xs">
                    <Text size="xs" fw={700} c="indigo">Restrições de Visibilidade (Quem pode ver)</Text>
                    <Text size="xxs" c="dimmed">Se nenhum grupo ou perfil for selecionado, todos os usuários autenticados poderão ver este relatório.</Text>
                    
                    <Grid gutter="xs">
                      <Grid.Col span={{ base: 12, sm: 6 }}>
                        <MultiSelect
                          label="Equipes Permitidas"
                          placeholder="Escolha as equipes autorizadas"
                          data={teamOptions}
                          value={allowedTeamIds}
                          onChange={setAllowedTeamIds}
                          searchable
                          size="xs"
                        />
                      </Grid.Col>
                      <Grid.Col span={{ base: 12, sm: 6 }}>
                        <MultiSelect
                          label="Perfis (Roles) Permitidos"
                          placeholder="Escolha os cargos autorizados"
                          data={[
                            { value: 'superadmin', label: 'Superadmin' },
                            { value: 'admin', label: 'Admin' },
                            { value: 'user', label: 'Vendedor (User)' }
                          ]}
                          value={allowedRoles}
                          onChange={setAllowedRoles}
                          searchable
                          size="xs"
                        />
                      </Grid.Col>
                    </Grid>
                  </Stack>
                </Paper>
              )}
            </Stack>
          </Paper>

          {/* Section 2: Filters */}
          <Paper shadow="sm" p="lg" radius="md" withBorder style={{ backgroundColor: '#ffffff' }}>
            <Title order={4} mb="xs" style={{ color: '#1c1c1e' }}>Filtros de Dados</Title>
            <Text size="xs" c="dimmed" mb="md">Filtre quais contratos serão incluídos na extração.</Text>
            
            <Stack gap="md">
              {/* Date Ranges Presets */}
              <div>
                <Text size="sm" fw={500} mb="xs">Intervalo de Datas</Text>
                <SegmentedControl
                  value={dateRangePreset}
                  onChange={(val: any) => handleDatePresetChange(val)}
                  data={[
                    { label: 'Últimos 30 dias', value: '30d' },
                    { label: 'Últimos 12 meses', value: '12m' },
                    { label: 'Últimos 15 meses', value: '15m' },
                    { label: 'Personalizado', value: 'custom' },
                  ]}
                  fullWidth
                  mb="xs"
                  size="sm"
                />

                {dateRangePreset === 'custom' && (
                  <Paper withBorder p="sm" radius="sm" mt="xs" style={{ backgroundColor: '#f9fafb' }}>
                    <Stack gap="sm">
                      <SegmentedControl
                        value={dateMode}
                        onChange={(val: any) => setDateMode(val)}
                        data={[
                          { label: 'Intervalo de Datas Absoluto', value: 'absolute' },
                          { label: 'Datas Relativas', value: 'relative' },
                        ]}
                        size="xs"
                        fullWidth
                      />

                      {dateMode === 'absolute' ? (
                        <DatePickerInput
                          type="range"
                          label="Intervalo de Datas Absoluto"
                          placeholder="Escolha o intervalo de datas"
                          value={dateRange}
                          onChange={(val: any) => setDateRange(val)}
                          clearable
                          size="sm"
                        />
                      ) : (
                        <Grid gutter="xs">
                          <Grid.Col span={6}>
                            <Select
                              label="Início Relativo"
                              placeholder="ex: 1 mês atrás"
                              value={relativeStartDate}
                              onChange={(val) => setRelativeStartDate(val || '')}
                              data={[
                                { value: '', label: 'Nenhum' },
                                { value: 'now', label: 'Agora' },
                                { value: 'thisMonth', label: 'Início deste mês' },
                                { value: '-7d', label: '7 dias atrás' },
                                { value: '-1M', label: '1 mês atrás' },
                                { value: '-3M', label: '3 meses atrás' },
                                { value: '-6M', label: '6 meses atrás' },
                                { value: '-7M', label: '7 meses atrás' },
                                { value: '-1y', label: '1 ano atrás' },
                                { value: '-15M', label: '15 meses atrás' },
                              ]}
                              clearable
                              searchable
                              allowDeselect
                              size="xs"
                            />
                          </Grid.Col>
                          <Grid.Col span={6}>
                            <Select
                              label="Término Relativo"
                              placeholder="ex: Agora"
                              value={relativeEndDate}
                              onChange={(val) => setRelativeEndDate(val || '')}
                              data={[
                                { value: '', label: 'Nenhum' },
                                { value: 'now', label: 'Agora' },
                                { value: 'thisMonth', label: 'Início deste mês' },
                                { value: '-7d', label: '7 dias atrás' },
                                { value: '-1M', label: '1 mês atrás' },
                                { value: '-3M', label: '3 meses atrás' },
                                { value: '-6M', label: '6 meses atrás' },
                                { value: '-7M', label: '7 meses atrás' },
                                { value: '-1y', label: '1 ano atrás' },
                              ]}
                              clearable
                              searchable
                              allowDeselect
                              size="xs"
                            />
                          </Grid.Col>
                        </Grid>
                      )}
                    </Stack>
                  </Paper>
                )}
              </div>

              {/* Standard MultiSelects */}
              <Grid gutter="md">
                <Grid.Col span={{ base: 12, md: 6 }}>
                  <MultiSelect
                    label="E-mails"
                    placeholder="Filtrar por e-mails de vendedores"
                    data={userOptions}
                    value={emails}
                    onChange={setEmails}
                    searchable
                    size="sm"
                  />
                </Grid.Col>
                <Grid.Col span={{ base: 12, md: 6 }}>
                  <MultiSelect
                    label="Grupos"
                    placeholder="Filtrar por grupos"
                    data={groupOptions}
                    value={groups}
                    onChange={setGroups}
                    searchable
                    size="sm"
                  />
                </Grid.Col>
                <Grid.Col span={{ base: 12, md: 6 }}>
                  <MultiSelect
                    label="Equipes"
                    placeholder="Filtrar por equipes"
                    data={teamOptions}
                    value={teams}
                    onChange={setTeams}
                    searchable
                    size="sm"
                  />
                </Grid.Col>
                <Grid.Col span={{ base: 12, md: 6 }}>
                  <MultiSelect
                    label="Pontos de Venda"
                    placeholder="Filtrar por pontos de venda"
                    data={pvOptions}
                    value={pvs}
                    onChange={setPvs}
                    searchable
                    size="sm"
                  />
                </Grid.Col>
                <Grid.Col span={{ base: 12, md: 6 }}>
                  <MultiSelect
                    label="Status do Contrato"
                    placeholder="Filtrar por status"
                    data={[
                      { value: 'Active', label: 'Ativo' },
                      { value: 'Late1', label: 'Atraso 1' },
                      { value: 'Late2', label: 'Atraso 2' },
                      { value: 'Late3', label: 'Atraso 3' },
                      { value: 'Defaulted', label: 'Desistente/Excluído' },
                      { value: 'Transferred', label: 'Transferido' }
                    ]}
                    value={statuses}
                    onChange={setStatuses}
                    searchable
                    size="sm"
                  />
                </Grid.Col>
              </Grid>

              {/* Advanced Collapse */}
              <div>
                <Button 
                  variant="subtle" 
                  color="gray" 
                  onClick={() => setAdvancedOpen(!advancedOpen)}
                  rightSection={advancedOpen ? <IconChevronUp size={16} /> : <IconChevronDown size={16} />}
                  size="xs"
                  p={0}
                >
                  Configurações Avançadas de Filtro
                </Button>
                
                <Collapse in={advancedOpen}>
                  <Paper withBorder p="sm" mt="xs" style={{ backgroundColor: '#fafafa' }}>
                    <Stack gap="sm">
                      <Switch
                        label="Usuário Atual como Pai (Hierarquia)"
                        description="Filtra para incluir apenas usuários hierarquicamente sob o usuário logado."
                        checked={currentUserAsParent}
                        onChange={(e) => setCurrentUserAsParent(e.currentTarget.checked)}
                      />
                      <TextInput
                        label="Matrículas"
                        description="Lista de matrículas separadas por vírgula"
                        placeholder="12345, 67890"
                        value={matriculas.join(', ')}
                        onChange={(e) => setMatriculas(e.currentTarget.value.split(',').map(s => s.trim()).filter(Boolean))}
                        size="sm"
                      />
                      {statuses.length > 1 && (
                        <Stack gap={2}>
                          <Text size="xs" fw={500}>Operador lógico dos Status</Text>
                          <SegmentedControl
                            size="xs"
                            value={statusOperator}
                            onChange={(val: any) => setStatusOperator(val)}
                            data={[
                              { label: 'OU (Qualquer um)', value: 'or' },
                              { label: 'E (Todos)', value: 'and' },
                            ]}
                          />
                        </Stack>
                      )}
                    </Stack>
                  </Paper>
                </Collapse>
              </div>
            </Stack>
          </Paper>

          {/* Section 3: Grouping */}
          <Paper shadow="sm" p="lg" radius="md" withBorder style={{ backgroundColor: '#ffffff' }}>
            <Title order={4} mb="xs" style={{ color: '#1c1c1e' }}>Agrupamento de Resultados</Title>
            <Text size="xs" c="dimmed" mb="md">Agregue os valores monetários totais por uma propriedade central.</Text>
            
            <Stack gap="sm">
              <SegmentedControl
                value={groupingType}
                onChange={(val: any) => handleGroupingTypeChange(val)}
                data={[
                  { label: 'Nenhum', value: 'none' },
                  { label: 'Por Equipe', value: 'team' },
                  { label: 'Por E-mail', value: 'email' },
                  { label: 'Por Nível', value: 'classification' },
                ]}
                fullWidth
                size="sm"
              />

              {groupingType === 'team' && (
                <Switch
                  label="Ocultar contratos sem equipe (Sem equipe)"
                  checked={hideUnassignedTeams}
                  onChange={(e) => setHideUnassignedTeams(e.currentTarget.checked)}
                  mt="xs"
                />
              )}
            </Stack>
          </Paper>

          {/* Section 4: Output Columns */}
          <Paper shadow="sm" p="lg" radius="md" withBorder style={{ backgroundColor: '#ffffff' }}>
            <Title order={4} mb="xs" style={{ color: '#1c1c1e' }}>Colunas de Saída</Title>
            <Text size="xs" c="dimmed" mb="md">Escolha, renomeie e ordene as colunas visíveis no relatório.</Text>
            
            <Stack gap="md">
              <Group justify="space-between">
                <Button
                  color="indigo"
                  variant="light"
                  onClick={handleOpenColumnModal}
                  leftSection={<IconPlus size={16} />}
                  size="sm"
                >
                  Adicionar Colunas de Saída
                </Button>

                <Stack gap="xs">
                  <Switch
                    label="Somar total produzido dos contratos e retenção"
                    checked={sumTotal}
                    onChange={(e) => setSumTotal(e.currentTarget.checked)}
                    size="sm"
                  />
                  {sumTotal && (
                    <Select
                      label="Tipo de Retenção do Sumário"
                      size="xs"
                      w={300}
                      value={summaryRetentionType}
                      onChange={(val) => setSummaryRetentionType((val as 'standard' | 'strict') || 'standard')}
                      data={[
                        { value: 'standard', label: 'Retenção Padrão (Sem Inadimplência)' },
                        { value: 'strict', label: 'Retenção Estrita (Sem Inadimplência ou Atrasos)' }
                      ]}
                    />
                  )}
                </Stack>
              </Group>

              {/* Column Selection Modal */}
              <Modal
                opened={columnModalOpen}
                onClose={() => setColumnModalOpen(false)}
                title={<Title order={3} style={{ color: '#1c1c1e', fontWeight: 700 }}>Selecione as Colunas do Relatório</Title>}
                size="lg"
                centered
              >
                <Stack gap="md">
                  <TextInput
                    placeholder="Pesquise por nome, descrição ou fonte do campo..."
                    value={columnSearch}
                    onChange={(e) => setColumnSearch(e.currentTarget.value)}
                    leftSection={<IconFilter size={16} />}
                    size="sm"
                  />

                  <ScrollArea h={340} offsetScrollbars>
                    <Stack gap="xs" pr="sm">
                      {modalFieldsList.length === 0 ? (
                        <Text c="dimmed" fs="italic" size="xs" style={{ textAlign: 'center', padding: '16px 0' }}>
                          Nenhum campo encontrado correspondente à pesquisa.
                        </Text>
                      ) : (
                        modalFieldsList.map(item => {
                          const isSelected = tempSelectedColumns.includes(item.key);
                          return (
                            <Card 
                              key={item.key} 
                              withBorder 
                              p="sm" 
                              radius="xs" 
                              onClick={() => handleToggleColumnTemp(item.key)}
                              style={{ 
                                cursor: 'pointer', 
                                borderLeft: isSelected ? '4px solid #6366f1' : '1px solid #e9ecef',
                                backgroundColor: isSelected ? '#f5f3ff' : '#ffffff',
                                transition: 'all 0.15s ease'
                              }}
                            >
                              <Group justify="space-between" wrap="nowrap">
                                <Stack gap={2} style={{ flex: 1 }}>
                                  <Group gap="xs" wrap="nowrap">
                                    <Text fw={600} size="sm" style={{ color: isSelected ? '#4f46e5' : '#1c1c1e' }}>
                                      {item.title}
                                    </Text>
                                    <Badge size="xxs" color="indigo" variant="light">{item.source}</Badge>
                                  </Group>
                                  <Text size="xxs" c="dimmed" style={{ lineHeight: 1.4 }}>
                                    {item.description}
                                  </Text>
                                </Stack>
                                <Checkbox 
                                  checked={isSelected} 
                                  readOnly 
                                  tabIndex={-1} 
                                  styles={{ input: { cursor: 'pointer' } }}
                                />
                              </Group>
                            </Card>
                          );
                        })
                      )}
                    </Stack>
                  </ScrollArea>

                  <Group justify="flex-end" gap="xs" mt="md" style={{ borderTop: '1px solid #e9ecef', paddingTop: '12px' }}>
                    <Button variant="default" onClick={() => setColumnModalOpen(false)} size="sm">
                      Cancelar
                    </Button>
                    <Button color="indigo" onClick={handleSaveModalColumns} size="sm">
                      Confirmar Seleção ({tempSelectedColumns.length})
                    </Button>
                  </Group>
                </Stack>
              </Modal>

              {outputColumns.length === 0 ? (
                <Text c="dimmed" fs="italic" size="xs" style={{ textAlign: 'center', padding: '16px 0' }}>
                  Nenhuma coluna de saída configurada. Clique em "Adicionar Colunas de Saída" para selecionar os campos.
                </Text>
              ) : (
                <Stack gap="xs">
                  {outputColumns.map((col, index) => (
                    <Card key={`${col.source}-${col.field}`} withBorder padding="xs" radius="sm" style={{ backgroundColor: '#ffffff' }}>
                      <Grid align="center" gutter="xs">
                        {/* Grip & Reorder arrows */}
                        <Grid.Col span={2}>
                          <Group gap="xs" wrap="nowrap" justify="center">
                            <IconGripVertical size={16} style={{ color: '#dbe1e6' }} />
                            <Stack gap={2}>
                              <ActionIcon variant="subtle" size="xs" onClick={() => handleMoveColumn(index, 'up')} disabled={index === 0}>
                                <IconArrowUp size={12} />
                              </ActionIcon>
                               <ActionIcon variant="subtle" size="xs" onClick={() => handleMoveColumn(index, 'down')} disabled={index === outputColumns.length - 1}>
                                <IconArrowDown size={12} />
                              </ActionIcon>
                            </Stack>
                          </Group>
                        </Grid.Col>

                        {/* Column Info */}
                        <Grid.Col span={{ base: 5, sm: 4 }}>
                          <Stack gap={2}>
                            <Text fw={600} size="xs" style={{ color: '#1c1c1e' }} truncate="end">
                              {(COLUMN_METADATA_DICT[`${col.source}|${col.field}`]?.title) || col.label || col.field}
                            </Text>
                            <Group gap={4}>
                              <Badge size="xxs" color="indigo" variant="light">{col.source}</Badge>
                              <Text size="xxs" c="dimmed" truncate="end">{col.field}</Text>
                            </Group>
                          </Stack>
                        </Grid.Col>

                        {/* Formatting & Editing in-place */}
                        <Grid.Col span={{ base: 5, sm: 6 }}>
                          <Group gap={4} justify="flex-end" wrap="nowrap">
                            <TextInput
                              placeholder="Rótulo"
                              value={col.label}
                              onChange={(e) => handleLabelChange(index, e.currentTarget.value)}
                              size="xs"
                              w={120}
                            />
                            <Select
                              placeholder="Formato"
                              value={col.format || ''}
                              onChange={(val) => handleFormatChange(index, val)}
                              size="xs"
                              w={100}
                              data={[
                                { value: '', label: 'Padrão' },
                                { value: 'br', label: 'BRL (BR)' },
                                { value: 'percentage', label: 'Porcentagem' }
                              ]}
                            />
                            <ActionIcon color="red" variant="subtle" size="sm" onClick={() => handleRemoveColumn(index)}>
                              <IconTrash size={14} />
                            </ActionIcon>
                          </Group>
                        </Grid.Col>
                      </Grid>
                    </Card>
                  ))}
                </Stack>
              )}
            </Stack>
          </Paper>

          {/* Section 5: Ordering */}
          <Paper shadow="sm" p="lg" radius="md" withBorder style={{ backgroundColor: '#ffffff' }}>
            <Title order={4} mb="xs" style={{ color: '#1c1c1e' }}>Ordenação dos Resultados</Title>
            <Grid gutter="md">
              <Grid.Col span={{ base: 12, sm: 6 }}>
                <Select
                  label="Ordenar por Coluna"
                  placeholder="Selecione uma coluna"
                  data={[
                    { value: '', label: 'Sem ordenação' },
                    ...(groupByEmail ? [{ value: 'Email', label: 'Email' }] : []),
                    ...(groupByTeam ? [{ value: 'Equipe', label: 'Equipe' }] : []),
                    ...(groupByClassification ? [{ value: 'Classificação', label: 'Classificação' }] : []),
                    ...Array.from(new Set(outputColumns.map(c => c.label).filter(l => l && l.trim() !== ""))).map(label => ({ value: label, label }))
                  ]}
                  value={orderByField}
                  onChange={setOrderByField}
                  clearable
                  size="sm"
                />
              </Grid.Col>
              {orderByField && (
                <Grid.Col span={{ base: 12, sm: 6 }}>
                  <Text size="sm" fw={500} mb={3}>Direção</Text>
                  <SegmentedControl
                    fullWidth
                    value={orderByDirection}
                    onChange={(val: any) => setOrderByDirection(val)}
                    data={[
                      { label: 'Crescente (A-Z)', value: 'asc' },
                      { label: 'Decrescente (Z-A)', value: 'desc' },
                    ]}
                    size="sm"
                  />
                </Grid.Col>
              )}
            </Grid>
          </Paper>

          {/* Section 6: Output / Display Type */}
          <Paper shadow="sm" p="lg" radius="md" withBorder style={{ backgroundColor: '#ffffff' }}>
            <Title order={4} mb="xs" style={{ color: '#1c1c1e' }}>Formato de Exibição dos Resultados</Title>
            <Text size="xs" c="dimmed" mb="md">Configure o formato visual de saída do seu relatório (Tabela, Gráfico ou Ambos).</Text>
            
            <Grid gutter="md" align="flex-end">
              <Grid.Col span={{ base: 12, sm: 4 }}>
                <Text size="sm" fw={500} mb="xs">Tipo de Exibição</Text>
                <SegmentedControl
                  value={outputType}
                  onChange={setOutputType}
                  data={[
                    { label: 'Tabela Apenas', value: 'table' },
                    { label: 'Gráfico Apenas', value: 'chart' },
                    { label: 'Tabela e Gráfico', value: 'both' }
                  ]}
                  fullWidth
                  size="sm"
                />
              </Grid.Col>

              {outputType !== 'table' && (
                <>
                  <Grid.Col span={{ base: 12, sm: 4 }}>
                    <Select
                      label="Tipo de Gráfico"
                      placeholder="Selecione o formato do gráfico"
                      value={chartType}
                      onChange={(val) => setChartType(val || 'bar')}
                      data={[
                        { value: 'bar', label: 'Gráfico de Barras' },
                        { value: 'pie', label: 'Gráfico de Pizza' },
                        { value: 'donut', label: 'Gráfico de Rosca' },
                        { value: 'line', label: 'Gráfico de Linhas' },
                        { value: 'area', label: 'Gráfico de Área' }
                      ]}
                      size="sm"
                    />
                  </Grid.Col>
                  <Grid.Col span={{ base: 12, sm: 4 }}>
                    <Select
                      label="Métrica do Gráfico"
                      placeholder="Métrica padrão (Automática)"
                      value={chartMetric || ''}
                      onChange={(val) => setChartMetric(val || '')}
                      data={[
                        { value: '', label: 'Automática (Primeira coluna numérica)' },
                        ...outputColumns.map(col => ({
                          value: col.label || col.field,
                          label: col.label || col.field
                        }))
                      ]}
                      size="sm"
                      clearable
                    />
                  </Grid.Col>
                </>
              )}
            </Grid>
          </Paper>

          {/* Section 7: Results Preview */}
          <Paper shadow="sm" p="lg" radius="md" withBorder style={{ backgroundColor: '#ffffff' }}>
            <Group justify="space-between" mb="md" wrap="nowrap">
              <Stack gap={2}>
                <Title order={4} style={{ color: '#1c1c1e', fontWeight: 600 }}>Prévia do Relatório</Title>
                <Text size="xs" c="dimmed">Visualização dos dados de prévia carregados dinamicamente.</Text>
              </Stack>
              <Button 
                variant="light" 
                color="blue" 
                leftSection={<IconRefresh size={16} />}
                onClick={handleRunPreview}
                loading={previewLoading}
                disabled={!name || outputColumns.length === 0}
                size="sm"
              >
                Atualizar Prévia
              </Button>
            </Group>

            <Stack gap="md">
              {previewLoading ? (
                <Center style={{ height: '260px' }}>
                  <Stack align="center" gap="xs">
                    <Loader size="md" color="indigo" />
                    <Text size="sm" c="dimmed" fw={500}>Buscando dados no servidor...</Text>
                  </Stack>
                </Center>
              ) : previewError ? (
                <Center style={{ height: '260px', padding: '24px' }}>
                  <Text size="sm" color="red" style={{ textAlign: 'center', fontWeight: 500 }}>{previewError}</Text>
                </Center>
              ) : !previewData ? (
                <Center style={{ height: '260px', padding: '24px' }}>
                  <Stack align="center" gap="md" style={{ textAlign: 'center', maxWidth: '400px' }}>
                    <Text size="sm" c="dimmed" style={{ lineHeight: 1.5 }}>
                      Defina o nome do relatório, selecione colunas e clique no botão acima para rodar a prévia em tempo real com dados reais.
                    </Text>
                    <Button 
                      variant="outline" 
                      color="indigo"
                      onClick={handleRunPreview} 
                      disabled={!name || outputColumns.length === 0}
                      size="sm"
                    >
                      Carregar Dados de Prévia
                    </Button>
                  </Stack>
                </Center>
              ) : (
                <>
                  {/* Part 1: Table (if outputType is table or both) */}
                  {(outputType === 'table' || outputType === 'both') && (
                    <div>
                      <Text size="xs" fw={700} c="dimmed" tt="uppercase" mb="xs">Tabela de Resultados (Amostra de 10 linhas)</Text>
                      <div 
                        style={{ 
                          overflowY: 'auto', 
                          overflowX: 'auto', 
                          border: '1px solid #e9ecef', 
                          borderRadius: '6px', 
                          backgroundColor: '#f8f9fa', 
                          padding: '8px' 
                        }}
                      >
                        {previewData.rows.length === 0 ? (
                          <Text size="sm" c="dimmed" style={{ textAlign: 'center', padding: '16px' }}>
                            Nenhum registro encontrado correspondente aos filtros de dados ativos.
                          </Text>
                        ) : (
                          <div style={{ display: 'inline-block', minWidth: '100%' }}>
                            <table className="preview-results-table" style={{ width: '100%', borderCollapse: 'collapse', fontSize: '0.85rem' }}>
                              <thead>
                                <tr style={{ borderBottom: '2px solid #e5e7eb', backgroundColor: '#f1f3f5' }}>
                                  {previewData.columns.map((col) => (
                                    <th 
                                      key={`${col.source}-${col.field}`} 
                                      style={{ 
                                        padding: '10px 12px', 
                                        textAlign: 'left', 
                                        fontWeight: 600, 
                                        color: '#374151',
                                        fontSize: '0.75rem',
                                        textTransform: 'uppercase',
                                        letterSpacing: '0.05em',
                                        whiteSpace: 'nowrap'
                                      }}
                                    >
                                      {col.label || col.field}
                                    </th>
                                  ))}
                                </tr>
                              </thead>
                              <tbody>
                                {previewData.rows.map((row, idx) => (
                                  <tr 
                                    key={idx} 
                                    style={{ 
                                      borderBottom: '1px solid #f3f4f6', 
                                      backgroundColor: idx % 2 === 0 ? '#ffffff' : '#f9fafb',
                                      transition: 'background-color 0.15s ease' 
                                    }}
                                  >
                                    {previewData.columns.map((col) => (
                                      <td 
                                        key={`${col.source}-${col.field}`} 
                                        style={{ 
                                          padding: '10px 12px', 
                                          color: '#4b5563', 
                                          fontSize: '0.85rem',
                                          whiteSpace: 'nowrap' 
                                        }}
                                      >
                                        {String(row[col.label] ?? '—')}
                                      </td>
                                    ))}
                                  </tr>
                                ))}
                              </tbody>
                            </table>
                          </div>
                        )}
                      </div>
                    </div>
                  )}

                  {/* Part 2: Summary Sum Card (if sumTotal is true) */}
                  {sumTotal && previewData.totalSum !== undefined && previewData.totalSum !== null && (
                    <Paper withBorder p="md" radius="md" style={{ backgroundColor: '#f5fdf8', borderLeft: '4px solid #10b981' }}>
                      <Group justify="space-between" align="center">
                        <Stack gap={2}>
                          <Text size="xs" c="dimmed" tt="uppercase" fw={700} style={{ letterSpacing: '0.05em' }}>
                            Resumo do Relatório (Summary)
                          </Text>
                          <Title order={3} style={{ color: '#0f766e', fontWeight: 700 }}>
                            {new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format(previewData.totalSum)}
                          </Title>
                        </Stack>
                        <Group gap="sm">
                          {previewData.overallRetention !== undefined && previewData.overallRetention !== null && (
                            <Paper withBorder p="xs" radius="sm" style={{ backgroundColor: '#ffffff', minWidth: '120px' }}>
                              <Text size="xxs" c="dimmed" fw={500} style={{ textAlign: 'center' }}>
                                {summaryRetentionType === 'strict' ? "Retenção Estrita Geral" : "Retenção Geral"}
                              </Text>
                              <Text size="md" fw={700} style={{ textAlign: 'center', color: '#0f766e' }}>
                                {new Intl.NumberFormat('pt-BR', { style: 'percent', minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(previewData.overallRetention)}
                              </Text>
                            </Paper>
                          )}
                          <Paper withBorder p="xs" radius="sm" style={{ backgroundColor: '#ffffff', minWidth: '120px' }}>
                            <Text size="xxs" c="dimmed" fw={500} style={{ textAlign: 'center' }}>
                              {groupByEmail ? "Total Geral de Usuários" : groupByTeam ? "Total Geral de Equipes" : groupByClassification ? "Total Geral de Níveis" : "Total Geral de Contratos"}
                            </Text>
                            <Text size="md" fw={700} style={{ textAlign: 'center', color: '#1f2937' }}>
                              {previewData.totalCount}
                            </Text>
                          </Paper>
                        </Group>
                      </Group>
                    </Paper>
                  )}

                  {/* Part 3: Chart (if outputType is chart or both) */}
                  {(outputType === 'chart' || outputType === 'both') && (
                    <div>
                      <Text size="xs" fw={700} c="dimmed" tt="uppercase" mb="sm">
                        Gráfico Analítico ({chartType === 'bar' ? 'Barras' : chartType === 'pie' ? 'Pizza' : chartType === 'donut' ? 'Rosca' : chartType === 'line' ? 'Linhas' : 'Área'})
                      </Text>
                      
                      <Paper withBorder p="md" radius="md" style={{ backgroundColor: '#ffffff', minHeight: '340px' }}>
                        {chartData.length === 0 ? (
                          <Center style={{ height: '300px' }}>
                            <Text size="xs" c="dimmed" fs="italic">Não há dados suficientes ou colunas numéricas no relatório para projetar o gráfico.</Text>
                          </Center>
                        ) : (
                          <Center style={{ height: '100%', minHeight: '300px', width: '100%' }}>
                            <div style={{ width: '100%', maxWidth: '640px', display: 'flex', justifyContent: 'center' }}>
                              {chartType === 'bar' && (
                                <BarChart
                                  h={300}
                                  data={chartData}
                                  dataKey="name"
                                  series={[{ name: 'value', color: 'indigo.6' }]}
                                  valueFormatter={(value) => new Intl.NumberFormat('pt-BR').format(value)}
                                  style={{ width: '100%' }}
                                />
                              )}
                              {chartType === 'line' && (
                                <LineChart
                                  h={300}
                                  data={chartData}
                                  dataKey="name"
                                  series={[{ name: 'value', color: 'indigo.6' }]}
                                  curveType="monotone"
                                  valueFormatter={(value) => new Intl.NumberFormat('pt-BR').format(value)}
                                  style={{ width: '100%' }}
                                />
                              )}
                              {chartType === 'area' && (
                                <AreaChart
                                  h={300}
                                  data={chartData}
                                  dataKey="name"
                                  series={[{ name: 'value', color: 'indigo.6' }]}
                                  curveType="monotone"
                                  valueFormatter={(value) => new Intl.NumberFormat('pt-BR').format(value)}
                                  style={{ width: '100%' }}
                                />
                              )}
                              {chartType === 'pie' && (
                                <PieChart
                                  data={chartData}
                                  withTooltip
                                  tooltipDataSource="segment"
                                  size={220}
                                />
                              )}
                              {chartType === 'donut' && (
                                <DonutChart
                                  data={chartData}
                                  withTooltip
                                  tooltipDataSource="segment"
                                  size={220}
                                  thickness={25}
                                />
                              )}
                            </div>
                          </Center>
                        )}
                      </Paper>
                    </div>
                  )}
                </>
              )}
            </Stack>
          </Paper>

          {/* Action Buttons Footer */}
          <Group justify="flex-end" mt="md" gap="sm">
            <Button variant="default" onClick={() => window.location.hash = '#/reports'} size="sm">
              Cancelar
            </Button>
            <Button 
              variant="outline"
              color="indigo"
              leftSection={<IconSearch size={16} />}
              onClick={() => handleSave(true)}
              loading={saving}
              disabled={!name || outputColumns.length === 0}
              size="sm"
            >
              Salvar e Visualizar
            </Button>
            <Button 
              color="indigo"
              leftSection={<IconDeviceFloppy size={16} />} 
              onClick={() => handleSave(false)} 
              loading={saving}
              disabled={!name || outputColumns.length === 0}
              size="sm"
            >
              Salvar Relatório
            </Button>
          </Group>

        </Stack>
      </div>
    </Menu>
  );
};

export default ReportFormPage;
