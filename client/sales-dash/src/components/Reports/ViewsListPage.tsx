import React, { useState, useEffect, useCallback, useMemo } from 'react';
import { 
  Title, 
  Button, 
  Group, 
  Text, 
  SegmentedControl, 
  TextInput, 
  Card, 
  Badge, 
  ActionIcon,
  Tooltip,
  Stack,
  Center,
  Loader
} from '@mantine/core';
import { 
  IconPlus, 
  IconSearch, 
  IconUsers, 
  IconLock, 
  IconPlayerPlay, 
  IconEdit, 
  IconTrash
} from '@tabler/icons-react';
import Menu from '../Menu';
import { getReportViews, deleteReportView, ReportView } from '../../services/reportViewService';
import StandardModal from '../../shared/StandardModal';
import { notifications } from '@mantine/notifications';

const ViewsListPage: React.FC = () => {
  const [views, setViews] = useState<ReportView[]>([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState('');
  const [scopeFilter, setScopeFilter] = useState('all');
  const [deleteConfirm, setDeleteConfirm] = useState<string | null>(null);
  const [currentUserRole, setCurrentUserRole] = useState<string>('');
  const [currentUserId, setCurrentUserId] = useState<string>('');

  const fetchViews = useCallback(async () => {
    try {
      setLoading(true);
      const data = await getReportViews();
      setViews(data);
    } catch (err: any) {
      notifications.show({
        title: 'Erro',
        message: err.message || 'Falha ao carregar views/dashboards',
        color: 'red'
      });
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchViews();
    const user = JSON.parse(localStorage.getItem('user') || '{}');
    setCurrentUserRole(user.role || '');
    setCurrentUserId(user.id || '');
  }, [fetchViews]);

  const handleDelete = async (id: string) => {
    try {
      await deleteReportView(id);
      notifications.show({ title: 'Sucesso', message: 'Dashboard excluído com sucesso', color: 'green' });
      setDeleteConfirm(null);
      fetchViews();
    } catch (err: any) {
      notifications.show({ title: 'Erro', message: err.message || 'Falha ao excluir dashboard', color: 'red' });
    }
  };

  const isSuperadmin = currentUserRole === 'superadmin';

  const filteredViews = useMemo(() => {
    const searchLower = search.trim().toLowerCase();
    return views.filter(v => {
      const matchesSearch = !searchLower ||
                           v.name.toLowerCase().includes(searchLower) || 
                           (v.description || '').toLowerCase().includes(searchLower);
      if (!matchesSearch) return false;

      if (scopeFilter === 'shared') return v.scope === 'shared';
      if (scopeFilter === 'mine') return v.userId === currentUserId;
      return true;
    });
  }, [views, search, scopeFilter, currentUserId]);

  const sharedViews = filteredViews.filter(v => v.scope === 'shared');
  const myViews = filteredViews.filter(v => v.userId === currentUserId && v.scope === 'private');

  const renderViewCard = (view: ReportView) => {
    const isOwner = view.userId === currentUserId;
    const canEditDelete = isSuperadmin && isOwner;

    // Calculate total report modules compiled inside layout
    let reportCount = 0;
    view.rows.forEach(r => {
      r.columns.forEach(c => {
        if (c.reportFilterId) reportCount++;
      });
    });

    return (
      <Card key={view.viewId} shadow="sm" padding="lg" radius="md" withBorder mb="md" style={{ backgroundColor: '#ffffff' }}>
        <Group justify="space-between" mb="xs">
          <Group>
            {view.scope === 'shared' ? <IconUsers size={20} color="#4f46e5" /> : <IconLock size={20} color="gray" />}
            <Text fw={600} size="lg" style={{ color: '#1c1c1e' }}>{view.name}</Text>
            <Badge color={view.scope === 'shared' ? 'indigo' : 'gray'} variant="light">
              {view.scope === 'shared' ? 'Compartilhado' : 'Privado'}
            </Badge>
          </Group>
          
          <Group gap="xs">
            <Tooltip label="Abrir Dashboard">
              <ActionIcon variant="light" color="indigo" onClick={() => window.location.hash = `#/views/${view.viewId}`}>
                <IconPlayerPlay size={18} />
              </ActionIcon>
            </Tooltip>
            {canEditDelete && (
              <>
                <Tooltip label="Editar Layout">
                  <ActionIcon variant="light" color="blue" onClick={() => window.location.hash = `#/views/${view.viewId}/edit`}>
                    <IconEdit size={18} />
                  </ActionIcon>
                </Tooltip>
                <Tooltip label="Excluir Dashboard">
                  <ActionIcon variant="light" color="red" onClick={() => setDeleteConfirm(view.viewId)}>
                    <IconTrash size={18} />
                  </ActionIcon>
                </Tooltip>
              </>
            )}
          </Group>
        </Group>

        <Text size="sm" c="dimmed" lineClamp={2} mb="md">
          {view.description || 'Nenhuma descrição fornecida.'}
        </Text>

        <Group justify="space-between" mt="md" style={{ borderTop: '1px solid #f1f3f5', paddingTop: '10px' }}>
          <Text size="xs" c="dimmed" fw={500}>
            Criado em {new Date(view.createdAt).toLocaleDateString()} · {view.rows.length} {view.rows.length === 1 ? 'linha' : 'linhas'} de grid · {reportCount} {reportCount === 1 ? 'relatório compilado' : 'relatórios compilados'}
          </Text>
        </Group>
      </Card>
    );
  };

  return (
    <Menu>
      <div style={{ padding: '20px', maxWidth: '1000px', margin: '0 auto' }}>
        <Group justify="space-between" align="flex-start" mb="xl">
          <Stack gap={2}>
            <Title order={2} style={{ color: '#1c1c1e', fontWeight: 700 }}>Dashboards (Views Engine)</Title>
            <Text size="sm" c="dimmed">Compile múltiplos relatórios analíticos em painéis de linhas e colunas customizados.</Text>
          </Stack>
          {isSuperadmin && (
            <Button leftSection={<IconPlus size={16} />} color="indigo" onClick={() => window.location.hash = '#/views/new'}>
              Novo Dashboard
            </Button>
          )}
        </Group>

        <Group mb="xl" style={{ display: 'flex' }}>
          <TextInput
            placeholder="Buscar dashboards..."
            leftSection={<IconSearch size={16} />}
            value={search}
            onChange={(e) => setSearch(e.currentTarget.value)}
            style={{ flex: 1 }}
            size="sm"
          />
          <SegmentedControl
            value={scopeFilter}
            onChange={setScopeFilter}
            data={[
              { label: 'Todos', value: 'all' },
              { label: 'Compartilhados', value: 'shared' },
              { label: 'Meus', value: 'mine' },
            ]}
            size="sm"
          />
        </Group>

        {loading ? (
          <Center mt="xl"><Loader color="indigo" /></Center>
        ) : (
          <Stack gap="xl">
            {(scopeFilter === 'all' || scopeFilter === 'shared') && (
              <section>
                <Title order={4} mb="md" style={{ color: '#4b5563', fontSize: '0.9rem', textTransform: 'uppercase', letterSpacing: '0.05em' }}>Dashboards Compartilhados</Title>
                {sharedViews.length === 0 ? (
                  <Text c="dimmed" fs="italic" size="sm" style={{ paddingLeft: '8px' }}>Nenhum dashboard compartilhado encontrado.</Text>
                ) : (
                  sharedViews.map(renderViewCard)
                )}
              </section>
            )}

            {(scopeFilter === 'all' || scopeFilter === 'mine') && (
              <section>
                <Title order={4} mb="md" style={{ color: '#4b5563', fontSize: '0.9rem', textTransform: 'uppercase', letterSpacing: '0.05em' }}>Meus Dashboards Privados</Title>
                {myViews.length === 0 ? (
                  <Text c="dimmed" fs="italic" size="sm" style={{ paddingLeft: '8px' }}>Nenhum dashboard privado encontrado.</Text>
                ) : (
                  myViews.map(renderViewCard)
                )}
              </section>
            )}
          </Stack>
        )}

        <StandardModal
          isOpen={deleteConfirm !== null}
          onClose={() => setDeleteConfirm(null)}
          title="Confirmar Exclusão"
          size="md"
          footer={
            <Group justify="flex-end" gap="xs">
              <Button variant="subtle" onClick={() => setDeleteConfirm(null)} size="sm">Cancelar</Button>
              <Button color="red" onClick={() => deleteConfirm && handleDelete(deleteConfirm)} size="sm">Excluir</Button>
            </Group>
          }
        >
          <Text size="sm">Tem certeza de que deseja excluir este dashboard compilado? Essa ação é permanente e removerá toda a configuração de linhas e layouts de visualização.</Text>
        </StandardModal>
      </div>
    </Menu>
  );
};

export default ViewsListPage;
