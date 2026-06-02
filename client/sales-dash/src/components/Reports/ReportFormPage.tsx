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
  Collapse
} from '@mantine/core';
import { DatePickerInput } from '@mantine/dates';
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
  IconRefresh
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

const ReportFormPage: React.FC<ReportFormPageProps> = ({ filterId }) => {
  const [localFilterId, setLocalFilterId] = useState<string | undefined>(filterId);
  const isEditMode = !!localFilterId;

  // Metadata
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [scope, setScope] = useState<'private' | 'shared'>('private');

  // Date Range Configuration
  const [dateRangePreset, setDateRangePreset] = useState<'30d' | '90d' | '12m' | 'custom'>('12m');
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
  const [pvs, setPvs] = useState<string[]>([]);
  const [statuses, setStatuses] = useState<string[]>([]);

  // Grouping
  const [groupingType, setGroupingType] = useState<'none' | 'team' | 'email' | 'classification'>('none');
  const [groupByEmail, setGroupByEmail] = useState(false);
  const [groupByTeam, setGroupByTeam] = useState(false);
  const [groupByClassification, setGroupByClassification] = useState(false);
  const [hideUnassignedTeams, setHideUnassignedTeams] = useState(false);

  // Columns & Sorting
  const [outputColumns, setOutputColumns] = useState<OutputColumn[]>([]);
  const [orderByField, setOrderByField] = useState<string | null>(null);
  const [orderByDirection, setOrderByDirection] = useState<'asc' | 'desc'>('asc');
  
  // Options
  const [availableColumns, setAvailableColumns] = useState<SourceColumns[]>([]);
  const [userOptions, setUserOptions] = useState<{value: string, label: string}[]>([]);
  const [groupOptions, setGroupOptions] = useState<{value: string, label: string}[]>([]);
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

      // Fetch users, groups, PVs for multiselects
      const [usersRes, groupsData, pvsRes] = await Promise.all([
        apiService.getUsers(1, 1000),
        getGroups(),
        apiService.getPVs()
      ]);
      
      setUserOptions((usersRes.data?.items || []).map((u: User) => ({ value: u.email, label: u.name })));
      setGroupOptions((groupsData || []).map((g: any) => ({ value: g.id.toString(), label: g.name })));
      setPvOptions((pvsRes.data || []).map((p: any) => ({ value: p.id.toString(), label: p.name })));

      // If edit mode, fetch the report filter
      if (localFilterId) {
        const report = await getReportFilter(localFilterId);
        setName(report.name);
        setDescription(report.description || '');
        setScope(report.scope);
        
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
        let preset: '30d' | '90d' | '12m' | 'custom' = 'custom';
        if (fc.relativeStartDate === '-30d' && fc.relativeEndDate === 'now') {
          preset = '30d';
        } else if (fc.relativeStartDate === '-90d' && fc.relativeEndDate === 'now') {
          preset = '90d';
        } else if ((fc.relativeStartDate === '-12M' || fc.relativeStartDate === '-1y') && fc.relativeEndDate === 'now') {
          preset = '12m';
        } else if (!fc.startDate && !fc.endDate && !fc.relativeStartDate && !fc.relativeEndDate) {
          preset = 'custom';
        }
        setDateRangePreset(preset);

        setCurrentUserAsParent(fc.currentUserAsParent || false);
        setEmails(fc.emails || []);
        setGroups((fc.groups || []).map(g => g.toString()));
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
  const handleDatePresetChange = (value: '30d' | '90d' | '12m' | 'custom') => {
    setDateRangePreset(value);
    if (value === '30d') {
      setDateMode('relative');
      setRelativeStartDate('-30d');
      setRelativeEndDate('now');
    } else if (value === '90d') {
      setDateMode('relative');
      setRelativeStartDate('-90d');
      setRelativeEndDate('now');
    } else if (value === '12m') {
      setDateMode('relative');
      setRelativeStartDate('-12M');
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

  // Handle adding a column
  const handleAddColumn = (sourceField: string | null) => {
    if (!sourceField) return;
    const [source, field] = sourceField.split('|');
    if (outputColumns.find(c => c.source === source && c.field === field)) return; // prevent duplicates
    
    const newCol: OutputColumn = {
      source,
      field,
      label: getFieldLabel(source, field),
      order: outputColumns.length + 1
    };
    setOutputColumns([...outputColumns, newCol]);
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
      startDate: dateMode === 'absolute' && dateRange[0] ? dateRange[0].toISOString() : undefined,
      endDate: dateMode === 'absolute' && dateRange[1] ? dateRange[1].toISOString() : undefined,
      relativeStartDate: dateMode === 'relative' && relativeStartDate ? relativeStartDate.trim() : undefined,
      relativeEndDate: dateMode === 'relative' && relativeEndDate ? relativeEndDate.trim() : undefined,
      currentUserAsParent: currentUserAsParent || undefined,
      emails: emails.length > 0 ? emails : undefined,
      groups: groups.length > 0 ? groups.map(Number) : undefined,
      pvs: pvs.length > 0 ? pvs.map(Number) : undefined,
      statuses: statuses.length > 0 ? statuses : undefined,
      statusOperator: statuses.length > 1 ? statusOperator : undefined,
    };

    return {
      name,
      description,
      scope,
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

  // Grouped options for Column Select
  const columnSelectData = availableColumns.map(source => ({
    group: source.source === 'Users_Contract' ? 'Vendedor (Contrato)' : (source.source === 'Users_Matricula' ? 'Titular da Matrícula' : source.source),
    items: source.fields.map(f => ({ value: `${source.source}|${f}`, label: getFieldLabel(source.source, f) }))
  }));

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
            <Text size="xs" c="dimmed">Configure filtros, agrupamentos e colunas abaixo para gerar seu relatório gerencial.</Text>
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
                label="Relatório Compartilhado (Visível para todos)"
                checked={scope === 'shared'}
                onChange={(event) => setScope(event.currentTarget.checked ? 'shared' : 'private')}
                mt="xs"
              />
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
                    { label: 'Últimos 90 dias', value: '90d' },
                    { label: 'Últimos 12 meses', value: '12m' },
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
                    label="Equipes (Grupos)"
                    placeholder="Filtrar por equipes"
                    data={groupOptions}
                    value={groups}
                    onChange={setGroups}
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
                  px="md"
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
              <Select
                label="Adicionar Campo"
                placeholder="Selecione um campo para adicionar..."
                data={columnSelectData}
                searchable
                onChange={handleAddColumn}
                value={null}
                size="sm"
                leftSection={<IconSearch size={16} />}
              />

              {outputColumns.length === 0 ? (
                <Text c="dimmed" fs="italic" size="xs" style={{ textAlign: 'center', padding: '16px 0' }}>
                  Nenhuma coluna de saída adicionada. Adicione colunas acima.
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
                              {col.label || col.field}
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

          {/* Section 6: Results Preview */}
          <Paper shadow="sm" p="lg" radius="md" withBorder style={{ backgroundColor: '#ffffff' }}>
            <Group justify="space-between" mb="md" wrap="nowrap">
              <Stack gap={2}>
                <Title order={4} style={{ color: '#1c1c1e', fontWeight: 600 }}>Prévia do Relatório</Title>
                <Text size="xs" c="dimmed">Visualização em tempo real das 10 primeiras linhas de acordo com as regras ativas.</Text>
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

            {/* Table Wrapper with horizontal scrolling */}
            <div 
              style={{ 
                overflowY: 'auto', 
                overflowX: 'auto', 
                minHeight: '260px', 
                border: '1px solid #e9ecef', 
                borderRadius: '6px', 
                backgroundColor: '#f8f9fa', 
                padding: '12px' 
              }}
            >
              {previewLoading ? (
                <Center style={{ height: '200px' }}>
                  <Stack align="center" gap="xs">
                    <Loader size="md" color="indigo" />
                    <Text size="sm" c="dimmed" fw={500}>Buscando dados no servidor...</Text>
                  </Stack>
                </Center>
              ) : previewError ? (
                <Center style={{ height: '200px', padding: '24px' }}>
                  <Text size="sm" color="red" style={{ textAlign: 'center', fontWeight: 500 }}>{previewError}</Text>
                </Center>
              ) : !previewData ? (
                <Center style={{ height: '200px', padding: '24px' }}>
                  <Stack align="center" gap="md" style={{ textAlign: 'center', maxWidth: '400px' }}>
                    <Text size="sm" c="dimmed" style={{ lineHeight: 1.5 }}>
                      Defina o nome do relatório, adicione colunas e clique no botão acima para rodar a prévia em tempo real com dados reais.
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
              ) : previewData.rows.length === 0 ? (
                <Center style={{ height: '200px' }}>
                  <Text size="sm" c="dimmed" fw={500}>Nenhum registro encontrado correspondente aos filtros de dados ativos.</Text>
                </Center>
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
