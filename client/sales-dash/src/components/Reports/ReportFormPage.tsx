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
  SegmentedControl
} from '@mantine/core';
import { DatePickerInput } from '@mantine/dates';
import { IconArrowUp, IconArrowDown, IconTrash, IconDeviceFloppy, IconSearch, IconCopy } from '@tabler/icons-react';
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
  SourceColumns
} from '../../services/reportFilterService';
import { apiService, User } from '../../services/apiService';
import { getGroups } from '../../services/contractService';

interface ReportFormPageProps {
  filterId?: string; // If provided, we are in edit mode
}

const ReportFormPage: React.FC<ReportFormPageProps> = ({ filterId }) => {
  const isEditMode = !!filterId;

  // Metadata
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [scope, setScope] = useState<'private' | 'shared'>('private');

  // Filters
  const [matriculas, setMatriculas] = useState<string[]>([]);
  const [dateMode, setDateMode] = useState<'absolute' | 'relative'>('absolute');
  const [dateRange, setDateRange] = useState<[Date | null, Date | null]>([null, null]);
  const [relativeStartDate, setRelativeStartDate] = useState('');
  const [relativeEndDate, setRelativeEndDate] = useState('');
  const [currentUserAsParent, setCurrentUserAsParent] = useState(false);
  const [emails, setEmails] = useState<string[]>([]);
  const [groups, setGroups] = useState<string[]>([]); // Using string for MultiSelect
  const [pvs, setPvs] = useState<string[]>([]);
  const [statuses, setStatuses] = useState<string[]>([]);
  const [statusOperator, setStatusOperator] = useState<'or' | 'and'>('or');

  // Columns
  const [outputColumns, setOutputColumns] = useState<OutputColumn[]>([]);
  const [groupByEmail, setGroupByEmail] = useState(false);
  const [groupByTeam, setGroupByTeam] = useState(false);
  const [groupByClassification, setGroupByClassification] = useState(false);
  const [hideUnassignedTeams, setHideUnassignedTeams] = useState(false);
  const [orderByField, setOrderByField] = useState<string | null>(null);
  const [orderByDirection, setOrderByDirection] = useState<'asc' | 'desc'>('asc');
  
  // Options
  const [availableColumns, setAvailableColumns] = useState<SourceColumns[]>([]);
  const [userOptions, setUserOptions] = useState<{value: string, label: string}[]>([]);
  const [groupOptions, setGroupOptions] = useState<{value: string, label: string}[]>([]);
  const [pvOptions, setPvOptions] = useState<{value: string, label: string}[]>([]);
  
  // Preview
  const [previewCount, setPreviewCount] = useState<number | null>(null);
  const [previewLoading, setPreviewLoading] = useState(false);

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
      if (isEditMode) {
        const report = await getReportFilter(filterId);
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
      }
    } catch (err: any) {
      notifications.show({ title: 'Erro', message: err.message || 'Falha ao carregar dados do formulário', color: 'red' });
    } finally {
      setLoading(false);
    }
  }, [filterId, isEditMode]);

  useEffect(() => {
    loadData();
  }, [loadData]);

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

  const handleSave = async (isPreview: boolean = false) => {
    try {
      setSaving(true);
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

      const payload = {
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

      let savedFilter: ReportFilter;
      if (isEditMode) {
        savedFilter = await updateReportFilter(filterId, payload);
        notifications.show({ title: 'Sucesso', message: 'Relatório atualizado', color: 'green' });
      } else {
        savedFilter = await createReportFilter(payload);
        notifications.show({ title: 'Sucesso', message: 'Relatório criado', color: 'green' });
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

      const payload = {
        name: `Cópia de ${name}`,
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

      const savedFilter = await createReportFilter(payload);
      notifications.show({ title: 'Sucesso', message: 'Relatório clonado', color: 'green' });
      window.location.hash = `#/reports/${savedFilter.filterId}/edit`;
    } catch (err: any) {
      notifications.show({ title: 'Erro', message: err.message || 'Falha ao clonar relatório', color: 'red' });
    } finally {
      setSaving(false);
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
          <Title order={2}>{isEditMode ? 'Editar Relatório' : 'Novo Relatório'}</Title>
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

        <Stack gap="xl">
          {/* Section 1: Metadata */}
          <Paper shadow="sm" p="lg" radius="md" withBorder>
            <Title order={4} mb="md">Detalhes do Relatório</Title>
            <Grid>
              <Grid.Col span={12}>
                <TextInput
                  label="Nome do Relatório"
                  placeholder="Ex: Contratos de Alto Valor 2026"
                  required
                  value={name}
                  onChange={(e) => setName(e.currentTarget.value)}
                  maxLength={100}
                />
              </Grid.Col>
              <Grid.Col span={12}>
                <Textarea
                  label="Descrição"
                  placeholder="Descrição opcional deste relatório"
                  value={description}
                  onChange={(e) => setDescription(e.currentTarget.value)}
                  maxLength={500}
                />
              </Grid.Col>
              <Grid.Col span={12}>
                <Switch
                  label="Relatório Compartilhado (Visível para todos)"
                  checked={scope === 'shared'}
                  onChange={(event) => setScope(event.currentTarget.checked ? 'shared' : 'private')}
                />
              </Grid.Col>
            </Grid>
          </Paper>

          {/* Section 2: Filters */}
          <Paper shadow="sm" p="lg" radius="md" withBorder>
            <Group justify="space-between" mb="md">
              <Title order={4}>Filtros</Title>
            </Group>
            
            <Grid>
              <Grid.Col span={12}>
                <SegmentedControl
                  value={dateMode}
                  onChange={(val: any) => setDateMode(val)}
                  data={[
                    { label: 'Intervalo de Datas Absoluto', value: 'absolute' },
                    { label: 'Datas Relativas', value: 'relative' },
                  ]}
                  mb="xs"
                />
              </Grid.Col>

              {dateMode === 'absolute' ? (
                <Grid.Col span={{ base: 12, md: 6 }}>
                  <DatePickerInput
                    type="range"
                    label="Intervalo de Datas Absoluto"
                    placeholder="Escolha o intervalo de datas"
                    value={dateRange}
                    onChange={(val: any) => setDateRange(val)}
                    clearable
                  />
                </Grid.Col>
              ) : (
                <>
                  <Grid.Col span={{ base: 12, md: 6 }}>
                    <Select
                      label="Data de Início Relativa"
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
                    />
                  </Grid.Col>
                  <Grid.Col span={{ base: 12, md: 6 }}>
                    <Select
                      label="Data de Término Relativa"
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
                    />
                  </Grid.Col>
                </>
              )}

              <Grid.Col span={{ base: 12, md: 6 }}>
                <Switch
                  label="Usuário Atual como Pai (Hierarquia)"
                  description="Filtrar para incluir apenas usuários sob o usuário logado atualmente."
                  checked={currentUserAsParent}
                  onChange={(e) => setCurrentUserAsParent(e.currentTarget.checked)}
                  mt="md"
                />
              </Grid.Col>
              
              <Grid.Col span={{ base: 12, md: 6 }}>
                <TextInput
                  label="Matrículas"
                  description="Lista de matrículas separadas por vírgula"
                  placeholder="12345, 67890"
                  value={matriculas.join(', ')}
                  onChange={(e) => setMatriculas(e.currentTarget.value.split(',').map(s => s.trim()).filter(Boolean))}
                />
              </Grid.Col>
              <Grid.Col span={{ base: 12, md: 6 }}>
                <MultiSelect
                  label="E-mails"
                  placeholder="Selecionar usuários"
                  data={userOptions}
                  value={emails}
                  onChange={setEmails}
                  searchable
                />
              </Grid.Col>
              <Grid.Col span={{ base: 12, md: 6 }}>
                <MultiSelect
                  label="Grupos"
                  placeholder="Selecionar grupos"
                  data={groupOptions}
                  value={groups}
                  onChange={setGroups}
                  searchable
                />
              </Grid.Col>
              <Grid.Col span={{ base: 12, md: 6 }}>
                <MultiSelect
                  label="Pontos de Venda"
                  placeholder="Selecionar Pontos de Venda"
                  data={pvOptions}
                  value={pvs}
                  onChange={setPvs}
                  searchable
                />
              </Grid.Col>
              <Grid.Col span={{ base: 12, md: 6 }}>
                <MultiSelect
                  label="Status do Contrato"
                  placeholder="Selecionar estados"
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
                />
                {statuses.length > 1 && (
                  <Group gap="xs" mt="xs">
                    <Text size="xs" c="dimmed">Operador lógico:</Text>
                    <SegmentedControl
                      size="xs"
                      value={statusOperator}
                      onChange={(val: any) => setStatusOperator(val)}
                      data={[
                        { label: 'OU (Qualquer um)', value: 'or' },
                        { label: 'E (Todos)', value: 'and' },
                      ]}
                    />
                  </Group>
                )}
              </Grid.Col>
            </Grid>
          </Paper>

          {/* Section 3: Output Columns */}
          <Paper shadow="sm" p="lg" radius="md" withBorder>
            <Group justify="space-between" mb="md">
              <Title order={4}>Colunas de Saída</Title>
              <Group gap="md">
                <Switch
                  label="Agrupar por E-mail e somar total de produção por usuário"
                  checked={groupByEmail}
                  onChange={(e) => {
                    setGroupByEmail(e.currentTarget.checked);
                    if (e.currentTarget.checked) {
                      setGroupByTeam(false);
                      setGroupByClassification(false);
                      setHideUnassignedTeams(false);
                    }
                  }}
                />
                <Switch
                  label="Agrupar por Equipe"
                  checked={groupByTeam}
                  onChange={(e) => {
                    setGroupByTeam(e.currentTarget.checked);
                    if (e.currentTarget.checked) {
                      setGroupByEmail(false);
                      setGroupByClassification(false);
                    } else {
                      setHideUnassignedTeams(false);
                    }
                  }}
                />
                <Switch
                  label="Agrupar por Nível de Classificação"
                  checked={groupByClassification}
                  onChange={(e) => {
                    setGroupByClassification(e.currentTarget.checked);
                    if (e.currentTarget.checked) {
                      setGroupByEmail(false);
                      setGroupByTeam(false);
                      setHideUnassignedTeams(false);
                    }
                  }}
                />
                {groupByTeam && (
                  <Switch
                    label="Ocultar contratos sem equipe (Sem equipe)"
                    checked={hideUnassignedTeams}
                    onChange={(e) => setHideUnassignedTeams(e.currentTarget.checked)}
                  />
                )}
              </Group>
            </Group>
            <Group mb="md" align="flex-end">
              <Select
                label="Adicionar Coluna"
                placeholder="Selecionar um campo"
                data={columnSelectData}
                searchable
                onChange={handleAddColumn}
                value={null}
                style={{ flex: 1 }}
              />
            </Group>

            <Stack gap="sm">
              {outputColumns.length === 0 ? (
                <Text c="dimmed" fs="italic">Nenhuma coluna adicionada. O relatório ficará vazio.</Text>
              ) : (
                outputColumns.map((col, index) => (
                  <Card key={`${col.source}-${col.field}`} withBorder padding="sm">
                    <Group justify="space-between" align="center">
                      <Group>
                        <ActionIcon variant="light" size="sm" onClick={() => handleMoveColumn(index, 'up')} disabled={index === 0}>
                          <IconArrowUp size={14} />
                        </ActionIcon>
                        <ActionIcon variant="light" size="sm" onClick={() => handleMoveColumn(index, 'down')} disabled={index === outputColumns.length - 1}>
                          <IconArrowDown size={14} />
                        </ActionIcon>
                        <Badge color="gray">{col.source}</Badge>
                        <Text fw={500}>{col.field}</Text>
                      </Group>
                      
                      <Group>
                        <TextInput
                          placeholder="Rótulo da Coluna"
                          value={col.label}
                          onChange={(e) => handleLabelChange(index, e.currentTarget.value)}
                          size="sm"
                          w={200}
                        />
                        <Select
                          placeholder="Formato"
                          value={col.format || ''}
                          onChange={(val) => handleFormatChange(index, val)}
                          size="sm"
                          w={140}
                          data={[
                            { value: '', label: 'Padrão (ISO)' },
                            { value: 'br', label: 'Brasileiro (BR)' },
                            { value: 'percentage', label: 'Porcentagem (%)' }
                          ]}
                        />
                        <ActionIcon color="red" variant="subtle" onClick={() => handleRemoveColumn(index)}>
                          <IconTrash size={16} />
                        </ActionIcon>
                      </Group>
                    </Group>
                  </Card>
                ))
              )}
            </Stack>
          </Paper>
          
          {/* Section 4: Ordering */}
          <Paper shadow="sm" p="lg" radius="md" withBorder>
            <Title order={4} mb="md">Ordenação</Title>
            <Grid gutter="md">
              <Grid.Col span={{ base: 12, md: 6 }}>
                <Select
                  label="Ordenar por Coluna"
                  placeholder="Selecione uma coluna para ordenar"
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
                />
              </Grid.Col>
              {orderByField && (
                <Grid.Col span={{ base: 12, md: 6 }}>
                  <Text size="sm" fw={500} mb={3}>Direção</Text>
                  <SegmentedControl
                    fullWidth
                    value={orderByDirection}
                    onChange={(val: any) => setOrderByDirection(val)}
                    data={[
                      { label: 'Crescente (A-Z / 1-9)', value: 'asc' },
                      { label: 'Decrescente (Z-A / 9-1)', value: 'desc' },
                    ]}
                  />
                </Grid.Col>
              )}
            </Grid>
          </Paper>

          {/* Footer actions */}
          <Group justify="flex-end" mt="xl">
            <Button variant="default" onClick={() => window.location.hash = '#/reports'}>Cancelar</Button>
            <Button 
              variant="light"
              color="blue"
              leftSection={<IconSearch size={16} />}
              onClick={() => handleSave(true)}
              loading={saving}
              disabled={!name || outputColumns.length === 0}
            >
              Salvar e Visualizar
            </Button>
            <Button 
              leftSection={<IconDeviceFloppy size={16} />} 
              onClick={() => handleSave(false)} 
              loading={saving}
              disabled={!name || outputColumns.length === 0}
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
