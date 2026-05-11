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
import { getReportFilters, deleteReportFilter, ReportFilter } from '../../services/reportFilterService';
import StandardModal from '../../shared/StandardModal';
import { notifications } from '@mantine/notifications';

const ReportListPage: React.FC = () => {
  const [reports, setReports] = useState<ReportFilter[]>([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState('');
  const [scopeFilter, setScopeFilter] = useState('all');
  const [deleteConfirm, setDeleteConfirm] = useState<string | null>(null);
  const [currentUserRole, setCurrentUserRole] = useState<string>('');
  const [currentUserId, setCurrentUserId] = useState<string>('');

  const fetchReports = useCallback(async () => {
    try {
      setLoading(true);
      const data = await getReportFilters();
      setReports(data);
    } catch (err: any) {
      notifications.show({
        title: 'Error',
        message: err.message || 'Failed to load reports',
        color: 'red'
      });
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchReports();
    const user = JSON.parse(localStorage.getItem('user') || '{}');
    setCurrentUserRole(user.role || '');
    setCurrentUserId(user.id || '');
  }, [fetchReports]);

  const handleDelete = async (id: string) => {
    try {
      await deleteReportFilter(id);
      notifications.show({ title: 'Success', message: 'Report deleted', color: 'green' });
      setDeleteConfirm(null);
      fetchReports();
    } catch (err: any) {
      notifications.show({ title: 'Error', message: err.message || 'Failed to delete report', color: 'red' });
    }
  };

  const isSuperadmin = currentUserRole === 'superadmin';

  const filteredReports = useMemo(() => {
    return reports.filter(r => {
      const matchesSearch = r.name.toLowerCase().includes(search.toLowerCase()) || 
                           (r.description || '').toLowerCase().includes(search.toLowerCase());
      if (!matchesSearch) return false;

      if (scopeFilter === 'shared') return r.scope === 'shared';
      if (scopeFilter === 'mine') return r.userId === currentUserId;
      return true;
    });
  }, [reports, search, scopeFilter, currentUserId]);

  const sharedReports = filteredReports.filter(r => r.scope === 'shared');
  const myReports = filteredReports.filter(r => r.userId === currentUserId && r.scope === 'private');

  const renderReportCard = (report: ReportFilter) => {
    const isOwner = report.userId === currentUserId;
    const canEditDelete = isSuperadmin && isOwner;

    return (
      <Card key={report.filterId} shadow="sm" padding="lg" radius="md" withBorder mb="md">
        <Group justify="space-between" mb="xs">
          <Group>
            {report.scope === 'shared' ? <IconUsers size={20} color="gray" /> : <IconLock size={20} color="gray" />}
            <Text fw={500} size="lg">{report.name}</Text>
            <Badge color={report.scope === 'shared' ? 'blue' : 'gray'}>
              {report.scope === 'shared' ? 'Shared' : 'Private'}
            </Badge>
          </Group>
          
          <Group gap="xs">
            <Tooltip label="Run Report">
              <ActionIcon variant="light" color="green" onClick={() => window.location.hash = `#/reports/${report.filterId}/results`}>
                <IconPlayerPlay size={18} />
              </ActionIcon>
            </Tooltip>
            {canEditDelete && (
              <>
                <Tooltip label="Edit Report">
                  <ActionIcon variant="light" color="blue" onClick={() => window.location.hash = `#/reports/${report.filterId}/edit`}>
                    <IconEdit size={18} />
                  </ActionIcon>
                </Tooltip>
                <Tooltip label="Delete Report">
                  <ActionIcon variant="light" color="red" onClick={() => setDeleteConfirm(report.filterId)}>
                    <IconTrash size={18} />
                  </ActionIcon>
                </Tooltip>
              </>
            )}
          </Group>
        </Group>

        <Text size="sm" c="dimmed" lineClamp={1} mb="md">
          {report.description || 'No description provided.'}
        </Text>

        <Group justify="space-between" mt="md">
          <Text size="xs" c="dimmed">
            Created by Author · {new Date(report.createdAt).toLocaleDateString()} · {report.outputColumns.length} columns
          </Text>
        </Group>
      </Card>
    );
  };

  return (
    <Menu>
      <div style={{ padding: '20px', maxWidth: '1200px', margin: '0 auto' }}>
        <Group justify="space-between" align="flex-start" mb="xl">
          <div>
            <Title order={2} size="h2">Reports</Title>
            <Text c="dimmed">Saved filter configurations for contracts</Text>
          </div>
          {isSuperadmin && (
            <Button leftSection={<IconPlus size={16} />} onClick={() => window.location.hash = '#/reports/new'}>
              New Report
            </Button>
          )}
        </Group>

        <Group mb="xl" style={{ display: 'flex' }}>
          <TextInput
            placeholder="Search reports..."
            leftSection={<IconSearch size={16} />}
            value={search}
            onChange={(e) => setSearch(e.currentTarget.value)}
            style={{ flex: 1 }}
          />
          <SegmentedControl
            value={scopeFilter}
            onChange={setScopeFilter}
            data={[
              { label: 'All', value: 'all' },
              { label: 'Shared', value: 'shared' },
              { label: 'Mine', value: 'mine' },
            ]}
          />
        </Group>

        {loading ? (
          <Center mt="xl"><Loader /></Center>
        ) : (
          <Stack gap="xl">
            {(scopeFilter === 'all' || scopeFilter === 'shared') && (
              <section>
                <Title order={4} mb="md">Shared Reports</Title>
                {sharedReports.length === 0 ? (
                  <Text c="dimmed" fs="italic">No shared reports found.</Text>
                ) : (
                  sharedReports.map(renderReportCard)
                )}
              </section>
            )}

            {(scopeFilter === 'all' || scopeFilter === 'mine') && (
              <section>
                <Title order={4} mb="md">My Private Reports</Title>
                {myReports.length === 0 ? (
                  <Text c="dimmed" fs="italic">No private reports found.</Text>
                ) : (
                  myReports.map(renderReportCard)
                )}
              </section>
            )}
          </Stack>
        )}

        <StandardModal
          isOpen={deleteConfirm !== null}
          onClose={() => setDeleteConfirm(null)}
          title="Confirm Deletion"
          size="md"
          footer={
            <>
              <Button variant="subtle" onClick={() => setDeleteConfirm(null)}>Cancel</Button>
              <Button color="red" onClick={() => deleteConfirm && handleDelete(deleteConfirm)}>Delete</Button>
            </>
          }
        >
          <Text>Are you sure you want to delete this report? This action cannot be undone.</Text>
        </StandardModal>
      </div>
    </Menu>
  );
};

export default ReportListPage;
