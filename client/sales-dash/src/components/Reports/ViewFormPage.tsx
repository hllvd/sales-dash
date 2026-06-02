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
  ActionIcon,
  Select,
  Grid,
  Paper,
  Card,
  Center,
  Loader
} from '@mantine/core';
import { 
  IconArrowUp, 
  IconArrowDown, 
  IconTrash, 
  IconDeviceFloppy, 
  IconPlus,
  IconGripVertical
} from '@tabler/icons-react';
import Menu from '../Menu';
import { notifications } from '@mantine/notifications';
import { getReportFilters, ReportFilter } from '../../services/reportFilterService';
import { 
  getReportView, 
  createReportView, 
  updateReportView, 
  ViewRow, 
  ViewColumn 
} from '../../services/reportViewService';
import { apiService } from '../../services/apiService';

interface ViewFormPageProps {
  viewId?: string; // Provided in edit mode
}

const ViewFormPage: React.FC<ViewFormPageProps> = ({ viewId }) => {
  const [localViewId, setLocalViewId] = useState<string | undefined>(viewId);
  const isEditMode = !!localViewId;

  // Metadata
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [scope, setScope] = useState<'private' | 'shared'>('private');
  
  // Visibility Restrictions (Allowed Teams and Roles)
  const [allowedTeamIds, setAllowedTeamIds] = useState<string[]>([]);
  const [allowedRoles, setAllowedRoles] = useState<string[]>([]);

  // Layout Builder State
  const [rows, setRows] = useState<ViewRow[]>([]);

  // Selection Options
  const [reportOptions, setReportOptions] = useState<{ value: string; label: string }[]>([]);
  const [teamOptions, setTeamOptions] = useState<{ value: string; label: string }[]>([]);

  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);

  // Load Form Data
  const loadData = useCallback(async () => {
    try {
      setLoading(true);

      // Fetch saved reports and active teams
      const [reportsData, teamsRes] = await Promise.all([
        getReportFilters(),
        apiService.getTeams()
      ]);

      setReportOptions((reportsData || []).map((r: ReportFilter) => ({
        value: r.filterId,
        label: `${r.name} (${r.scope === 'shared' ? 'Compartilhado' : 'Privado'})`
      })));

      setTeamOptions((teamsRes.data || []).map((t: any) => ({
        value: t.id.toString(),
        label: t.name
      })));

      // If in edit mode, fetch the report view config
      if (localViewId) {
        const view = await getReportView(localViewId);
        setName(view.name);
        setDescription(view.description || '');
        setScope(view.scope);
        setAllowedTeamIds((view.allowedTeamIds || []).map(String));
        setAllowedRoles(view.allowedRoles || []);
        setRows(view.rows || []);
      }
    } catch (err: any) {
      notifications.show({ 
        title: 'Erro', 
        message: err.message || 'Falha ao carregar dados do editor', 
        color: 'red' 
      });
    } finally {
      setLoading(false);
    }
  }, [localViewId]);

  useEffect(() => {
    loadData();
  }, [loadData]);

  // Layout Builder Functions
  const handleAddRow = (columnCount: 1 | 2 | 3) => {
    const newColumns: ViewColumn[] = Array.from({ length: columnCount }, () => ({
      reportFilterId: undefined
    }));
    
    setRows([...rows, { columns: newColumns }]);
    notifications.show({ 
      title: 'Linha Adicionada', 
      message: `Nova linha com ${columnCount} coluna(s) adicionada no fim do grid.`, 
      color: 'indigo' 
    });
  };

  const handleRemoveRow = (index: number) => {
    const newRows = [...rows];
    newRows.splice(index, 1);
    setRows(newRows);
  };

  const handleMoveRow = (index: number, direction: 'up' | 'down') => {
    if (direction === 'up' && index === 0) return;
    if (direction === 'down' && index === rows.length - 1) return;

    const newRows = [...rows];
    const swapIndex = direction === 'up' ? index - 1 : index + 1;
    const temp = newRows[index];
    newRows[index] = newRows[swapIndex];
    newRows[swapIndex] = temp;
    setRows(newRows);
  };

  const handleColumnReportChange = (rowIndex: number, colIndex: number, reportId: string | null) => {
    const newRows = [...rows];
    newRows[rowIndex].columns[colIndex].reportFilterId = reportId || undefined;
    setRows(newRows);
  };

  const buildPayload = () => {
    return {
      name,
      description,
      scope,
      rows,
      allowedTeamIds: scope === 'shared' ? allowedTeamIds.map(Number) : [],
      allowedRoles: scope === 'shared' ? allowedRoles : []
    };
  };

  const handleSave = async () => {
    if (!name.trim()) {
      notifications.show({ title: 'Aviso', message: 'Insira o nome do dashboard.', color: 'orange' });
      return;
    }
    if (rows.length === 0) {
      notifications.show({ title: 'Aviso', message: 'Adicione pelo menos uma linha de layout.', color: 'orange' });
      return;
    }

    try {
      setSaving(true);
      const payload = buildPayload();

      if (localViewId) {
        await updateReportView(localViewId, payload);
        notifications.show({ title: 'Sucesso', message: 'Dashboard atualizado com sucesso', color: 'green' });
      } else {
        const savedView = await createReportView(payload);
        notifications.show({ title: 'Sucesso', message: 'Dashboard criado com sucesso', color: 'green' });
        setLocalViewId(savedView.viewId);
      }
      window.location.hash = '#/views';
    } catch (err: any) {
      notifications.show({ title: 'Erro', message: err.message || 'Falha ao salvar dashboard', color: 'red' });
    } finally {
      setSaving(false);
    }
  };

  if (loading) {
    return <Menu><Center style={{ height: '80vh' }}><Loader color="indigo" /></Center></Menu>;
  }

  return (
    <Menu>
      <div style={{ padding: '20px', maxWidth: '1000px', margin: '0 auto' }}>
        <Group justify="space-between" mb="xl">
          <Stack gap={2}>
            <Title order={2} style={{ color: '#1c1c1e', fontWeight: 700 }}>
              {isEditMode ? 'Editar Dashboard' : 'Novo Dashboard'}
            </Title>
            <Text size="xs" c="dimmed">Organize relatórios em linhas e colunas para compor o painel visual da sua equipe.</Text>
          </Stack>
          <Button variant="default" onClick={() => window.location.hash = '#/views'}>Cancelar</Button>
        </Group>

        <Stack gap="lg">
          {/* Section 1: Basic details */}
          <Paper shadow="sm" p="lg" radius="md" withBorder style={{ backgroundColor: '#ffffff' }}>
            <Title order={4} mb="md" style={{ color: '#1c1c1e' }}>Detalhes do Dashboard</Title>
            <Stack gap="sm">
              <TextInput
                label="Nome do Dashboard"
                placeholder="Ex: Painel Geral de Vendas e Comissão"
                required
                value={name}
                onChange={(e) => setName(e.currentTarget.value)}
                maxLength={100}
              />
              <Textarea
                label="Descrição"
                placeholder="Uma breve descrição sobre a finalidade ou audiência deste painel"
                value={description}
                onChange={(e) => setDescription(e.currentTarget.value)}
                maxLength={500}
                rows={2}
              />
              <Switch
                label="Dashboard Compartilhado (Visível para outros usuários)"
                checked={scope === 'shared'}
                onChange={(event) => setScope(event.currentTarget.checked ? 'shared' : 'private')}
                mt="xs"
              />

              {scope === 'shared' && (
                <Paper withBorder p="sm" mt="xs" style={{ backgroundColor: '#f8f9fa' }}>
                  <Stack gap="xs">
                    <Text size="xs" fw={700} c="indigo">Restrições de Visibilidade (Quem pode ver)</Text>
                    <Text size="xxs" c="dimmed">Se nenhum time ou papel for selecionado, todos os usuários autenticados poderão ver este dashboard.</Text>
                    
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

          {/* Section 2: Layout Builder */}
          <Paper shadow="sm" p="lg" radius="md" withBorder style={{ backgroundColor: '#ffffff' }}>
            <Group justify="space-between" mb="md">
              <Stack gap={2}>
                <Title order={4} style={{ color: '#1c1c1e' }}>Layout do Grid</Title>
                <Text size="xs" c="dimmed">Monte a grade visual adicionando linhas com diferentes números de colunas.</Text>
              </Stack>
              <Group gap="xs">
                <Button 
                  size="xs" 
                  color="indigo" 
                  variant="light" 
                  leftSection={<IconPlus size={14} />} 
                  onClick={() => handleAddRow(1)}
                >
                  + Linha (1 Coluna)
                </Button>
                <Button 
                  size="xs" 
                  color="indigo" 
                  variant="light" 
                  leftSection={<IconPlus size={14} />} 
                  onClick={() => handleAddRow(2)}
                >
                  + Linha (2 Colunas)
                </Button>
                <Button 
                  size="xs" 
                  color="indigo" 
                  variant="light" 
                  leftSection={<IconPlus size={14} />} 
                  onClick={() => handleAddRow(3)}
                >
                  + Linha (3 Colunas)
                </Button>
              </Group>
            </Group>

            {rows.length === 0 ? (
              <Center style={{ height: '160px', border: '1px dashed #dbe1e6', borderRadius: '8px' }}>
                <Text size="sm" c="dimmed" fs="italic">Nenhuma linha adicionada. Escolha um dos botões acima para iniciar a estrutura do layout.</Text>
              </Center>
            ) : (
              <Stack gap="md">
                {rows.map((row, rowIndex) => (
                  <Card key={rowIndex} withBorder padding="sm" radius="md" style={{ backgroundColor: '#fafafa' }}>
                    <Group justify="space-between" mb="sm" style={{ borderBottom: '1px solid #e9ecef', paddingBottom: '6px' }}>
                      <Group gap="xs">
                        <IconGripVertical size={16} style={{ color: '#dbe1e6', cursor: 'grab' }} />
                        <Text fw={600} size="xs" style={{ color: '#495057' }}>
                          Linha {rowIndex + 1} ({row.columns.length} Colunas)
                        </Text>
                      </Group>
                      <Group gap="xs">
                        <ActionIcon variant="subtle" size="xs" onClick={() => handleMoveRow(rowIndex, 'up')} disabled={rowIndex === 0}>
                          <IconArrowUp size={12} />
                        </ActionIcon>
                        <ActionIcon variant="subtle" size="xs" onClick={() => handleMoveRow(rowIndex, 'down')} disabled={rowIndex === rows.length - 1}>
                          <IconArrowDown size={12} />
                        </ActionIcon>
                        <ActionIcon variant="subtle" color="red" size="xs" onClick={() => handleRemoveRow(rowIndex)}>
                          <IconTrash size={14} />
                        </ActionIcon>
                      </Group>
                    </Group>

                    <Grid gutter="md">
                      {row.columns.map((col, colIndex) => {
                        const span = row.columns.length === 1 ? 12 : row.columns.length === 2 ? 6 : 4;
                        return (
                          <Grid.Col key={colIndex} span={{ base: 12, md: span }}>
                            <Paper withBorder p="xs" radius="xs" style={{ backgroundColor: '#ffffff' }}>
                              <Select
                                label={`Coluna ${colIndex + 1}`}
                                placeholder="Escolha um relatório..."
                                data={reportOptions}
                                value={col.reportFilterId || null}
                                onChange={(val) => handleColumnReportChange(rowIndex, colIndex, val)}
                                searchable
                                clearable
                                size="xs"
                                styles={{ label: { fontSize: '0.7rem', fontWeight: 600, color: '#495057' } }}
                              />
                            </Paper>
                          </Grid.Col>
                        );
                      })}
                    </Grid>
                  </Card>
                ))}
              </Stack>
            )}
          </Paper>

          {/* Action Buttons Footer */}
          <Group justify="flex-end" mt="md" gap="sm">
            <Button variant="default" onClick={() => window.location.hash = '#/views'} size="sm">
              Cancelar
            </Button>
            <Button 
              color="indigo"
              leftSection={<IconDeviceFloppy size={16} />} 
              onClick={handleSave} 
              loading={saving}
              disabled={!name || rows.length === 0}
              size="sm"
            >
              Salvar Dashboard
            </Button>
          </Group>
        </Stack>
      </div>
    </Menu>
  );
};

export default ViewFormPage;
