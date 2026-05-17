import React, { useState, useEffect } from 'react';
import { 
  Title, 
  Table, 
  Badge, 
  Text, 
  Group, 
  TextInput, 
  Paper, 
  ActionIcon, 
  Tooltip,
  Loader,
  Center,
  SimpleGrid,
  NumberFormatter
} from '@mantine/core';
import { IconSearch, IconRefresh, IconActivity, IconAlertCircle, IconCheck, IconFileAnalytics, IconAlertTriangle, IconCircleX } from '@tabler/icons-react';
import { getMatriculaHealth, MatriculaHealth } from '../../services/contractService';
import dayjs from 'dayjs';
import relativeTime from 'dayjs/plugin/relativeTime';
import 'dayjs/locale/pt-br';
import Menu from '../Menu';
import './MatriculaHealthPage.css';

dayjs.extend(relativeTime);
dayjs.locale('pt-br');

const MatriculaHealthPage: React.FC = () => {
  const [data, setData] = useState<MatriculaHealth[]>([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState('');
  const [refreshing, setRefreshing] = useState(false);

  const fetchData = async (isManual = false) => {
    try {
      if (isManual) setRefreshing(true);
      else setLoading(true);
      
      const healthData = await getMatriculaHealth();
      setData(healthData);
    } catch (error) {
      console.error('Failed to fetch health data', error);
    } finally {
      setLoading(false);
      setRefreshing(false);
    }
  };

  useEffect(() => {
    fetchData();
  }, []);

  const filteredData = data.filter(item => 
    item.matricula.toLowerCase().includes(search.toLowerCase())
  );

  const getStatusBadge = (status: string) => {
    switch (status) {
      case 'Healthy':
        return <Badge color="green" leftSection={<IconCheck size={12} />}>Atualizado</Badge>;
      case 'Warning':
        return <Badge color="yellow" leftSection={<IconAlertCircle size={12} />}>Atenção</Badge>;
      case 'OutOfDate':
        return <Badge color="orange" leftSection={<IconAlertCircle size={12} />}>Atrasado</Badge>;
      case 'Danger':
        return <Badge color="red" leftSection={<IconAlertCircle size={12} />}>Perigo</Badge>;
      default:
        return <Badge color="gray">{status}</Badge>;
    }
  };

  const stats = {
    total: data.length,
    healthy: data.filter(d => d.status === 'Healthy').length,
    warning: data.filter(d => d.status === 'Warning').length,
    outOfDate: data.filter(d => d.status === 'OutOfDate').length,
    danger: data.filter(d => d.status === 'Danger').length,
    totalContracts: data.reduce((acc, curr) => acc + curr.contractCount, 0)
  };

  const rows = filteredData.map((item) => (
    <Table.Tr key={item.matricula}>
      <Table.Td>
        <Text fw={600}>{item.matricula}</Text>
      </Table.Td>
      <Table.Td>
        <Text fw={600} size="sm">
          <NumberFormatter value={item.contractCount} thousandSeparator="." decimalSeparator="," />
        </Text>
      </Table.Td>
      <Table.Td>
        <Text size="sm">{dayjs(item.lastUpdate).format('DD/MM/YYYY HH:mm')}</Text>
        <Text size="xs" c="dimmed">{dayjs(item.lastUpdate).fromNow()}</Text>
      </Table.Td>
      <Table.Td>{getStatusBadge(item.status)}</Table.Td>
    </Table.Tr>
  ));

  return (
    <Menu>
      <div className="monitoring-page">
        <div className="monitoring-header">
          <div>
            <Title order={2} size="h2">Saúde das Matrículas</Title>
          </div>
          <Group className="monitoring-header-actions">
            <Tooltip label="Atualizar dados">
              <ActionIcon 
                variant="light" 
                size="lg" 
                onClick={() => fetchData(true)} 
                loading={refreshing}
                color="red"
              >
                <IconRefresh size={20} />
              </ActionIcon>
            </Tooltip>
          </Group>
        </div>

        <SimpleGrid cols={{ base: 1, sm: 2, md: 4 }} spacing="md" mb="xl">
          <Paper p="md" className="stat-card">
            <Group justify="space-between" mb="xs">
              <Text size="xs" c="dimmed" fw={700} tt="uppercase">Total de Matrículas</Text>
              <IconActivity size={20} color="#228be6" />
            </Group>
            <Text size="xl" fw={700} c="dark.7">{stats.total}</Text>
            <Text size="xs" c="dimmed" mt={7}>Base de dados completa</Text>
          </Paper>

          <Paper p="md" className="stat-card">
            <Group justify="space-between" mb="xs">
              <Text size="xs" c="dimmed" fw={700} tt="uppercase">Contratos Totais</Text>
              <IconFileAnalytics size={20} color="#15aabf" />
            </Group>
            <Text size="xl" fw={700} c="dark.7">
              <NumberFormatter value={stats.totalContracts} thousandSeparator="." decimalSeparator="," />
            </Text>
            <Text size="xs" c="dimmed" mt={7}>Volume total monitorado</Text>
          </Paper>

          <Paper p="md" className="stat-card">
            <Group justify="space-between" mb="xs">
              <Text size="xs" c="dimmed" fw={700} tt="uppercase">Atenção (&gt;24h)</Text>
              <IconAlertTriangle size={20} color="#fab005" />
            </Group>
            <Text size="xl" fw={700} c="yellow.8">{stats.warning}</Text>
            <Text size="xs" c="dimmed" mt={7}>Necessário verificar</Text>
          </Paper>

          <Paper p="md" className="stat-card">
            <Group justify="space-between" mb="xs">
              <Text size="xs" c="dimmed" fw={700} tt="uppercase">Perigo (&gt;72h)</Text>
              <IconCircleX size={20} color="#fa5252" />
            </Group>
            <Text size="xl" fw={700} c="red.8">{stats.outOfDate + stats.danger}</Text>
            <Text size="xs" c="dimmed" mt={7}>Altamente desatualizado</Text>
          </Paper>
        </SimpleGrid>

        <div className="search-container">
          <TextInput
            placeholder="Pesquisar matrícula..."
            leftSection={<IconSearch size={16} />}
            value={search}
            onChange={(event) => setSearch(event.currentTarget.value)}
            style={{ maxWidth: 400 }}
          />
        </div>

        {loading ? (
          <Center py="xl">
            <Loader size="lg" />
          </Center>
        ) : (
          <div className="monitoring-table-container">
            <Table.ScrollContainer minWidth={600}>
              <Table striped highlightOnHover>
                <Table.Thead>
                  <Table.Tr>
                    <Table.Th>Matrícula</Table.Th>
                    <Table.Th>Qtd. Contratos</Table.Th>
                    <Table.Th>Última Atualização</Table.Th>
                    <Table.Th>Status</Table.Th>
                  </Table.Tr>
                </Table.Thead>
                <Table.Tbody>
                  {rows.length > 0 ? rows : (
                    <Table.Tr>
                      <Table.Td colSpan={4}>
                        <Text c="dimmed" ta="center" py="xl">Nenhuma matrícula encontrada</Text>
                      </Table.Td>
                    </Table.Tr>
                  )}
                </Table.Tbody>
              </Table>
            </Table.ScrollContainer>
          </div>
        )}
      </div>
    </Menu>
  );
};

export default MatriculaHealthPage;
