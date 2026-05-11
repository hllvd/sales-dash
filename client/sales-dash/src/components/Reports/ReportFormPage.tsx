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
import { IconArrowUp, IconArrowDown, IconTrash, IconDeviceFloppy } from '@tabler/icons-react';
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

  // Columns
  const [outputColumns, setOutputColumns] = useState<OutputColumn[]>([]);
  
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
        
        setOutputColumns(report.outputColumns || []);
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

  // Handle adding a column
  const handleAddColumn = (sourceField: string | null) => {
    if (!sourceField) return;
    const [source, field] = sourceField.split('|');
    if (outputColumns.find(c => c.source === source && c.field === field)) return; // prevent duplicates
    
    const newCol: OutputColumn = {
      source,
      field,
      label: field, // default label
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

  const handleSave = async () => {
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
      };

      const payload = {
        name,
        description,
        scope,
        filterConfig,
        outputColumns
      };

      if (isEditMode) {
        await updateReportFilter(filterId, payload);
        notifications.show({ title: 'Sucesso', message: 'Relatório atualizado', color: 'green' });
      } else {
        await createReportFilter(payload);
        notifications.show({ title: 'Sucesso', message: 'Relatório criado', color: 'green' });
      }
      
      window.location.hash = '#/reports';
    } catch (err: any) {
      notifications.show({ title: 'Erro', message: err.message || 'Falha ao salvar relatório', color: 'red' });
    } finally {
      setSaving(false);
    }
  };

  // Grouped options for Column Select
  const columnSelectData = availableColumns.map(source => ({
    group: source.source,
    items: source.fields.map(f => ({ value: `${source.source}|${f}`, label: f }))
  }));

  if (loading) {
    return <Menu><Center style={{ height: '80vh' }}><Loader /></Center></Menu>;
  }

  return (
    <Menu>
      <div style={{ padding: '20px', maxWidth: '1000px', margin: '0 auto' }}>
        <Group justify="space-between" mb="xl">
          <Title order={2}>{isEditMode ? 'Editar Relatório' : 'Novo Relatório'}</Title>
          <Button variant="default" onClick={() => window.location.hash = '#/reports'}>Cancelar</Button>
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
            </Grid>
          </Paper>

          {/* Section 3: Output Columns */}
          <Paper shadow="sm" p="lg" radius="md" withBorder>
            <Title order={4} mb="md">Colunas de Saída</Title>
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

          {/* Footer actions */}
          <Group justify="flex-end" mt="xl">
            <Button variant="default" onClick={() => window.location.hash = '#/reports'}>Cancelar</Button>
            <Button 
              leftSection={<IconDeviceFloppy size={16} />} 
              onClick={handleSave} 
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
