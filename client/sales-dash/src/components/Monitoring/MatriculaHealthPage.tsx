import React, { useState, useEffect } from 'react';
import { 
  Title, 
  Text, 
  Group, 
  Paper, 
  ActionIcon, 
  Tooltip,
  Loader,
  Center,
  SimpleGrid,
  NumberFormatter,
  Tabs
} from '@mantine/core';
import { 
  IconRefresh, 
  IconActivity, 
  IconFileAnalytics, 
  IconAlertTriangle, 
  IconCircleX,
  IconUsers,
  IconUserCheck
} from '@tabler/icons-react';
import { 
  getMatriculaHealth, 
  getEquipesHealth, 
  getAdminImportStats,
  MatriculaHealth, 
  TeamMatriculaHealth, 
  AdminImportStats 
} from '../../services/contractService';
import dayjs from 'dayjs';
import relativeTime from 'dayjs/plugin/relativeTime';
import 'dayjs/locale/pt-br';
import Menu from '../Menu';
import MatriculasTab from './MatriculasTab';
import EquipesTab from './EquipesTab';
import AdminsTab from './AdminsTab';
import './MatriculaHealthPage.css';

dayjs.extend(relativeTime);
dayjs.locale('pt-br');

const MatriculaHealthPage: React.FC = () => {
  const [matriculasData, setMatriculasData] = useState<MatriculaHealth[]>([]);
  const [equipesData, setEquipesData] = useState<TeamMatriculaHealth[]>([]);
  const [adminsData, setAdminsData] = useState<AdminImportStats[]>([]);
  
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [activeTab, setActiveTab] = useState<string | null>('matriculas');

  const fetchData = async (isManual = false) => {
    try {
      if (isManual) setRefreshing(true);
      else setLoading(true);
      
      const [healthData, teamsData, adminStats] = await Promise.all([
        getMatriculaHealth(),
        getEquipesHealth(),
        getAdminImportStats()
      ]);

      setMatriculasData(healthData);
      setEquipesData(teamsData);
      setAdminsData(adminStats);
    } catch (error) {
      console.error('Failed to fetch monitoring data', error);
    } finally {
      setLoading(false);
      setRefreshing(false);
    }
  };

  useEffect(() => {
    fetchData();
  }, []);

  const stats = {
    total: matriculasData.length,
    healthy: matriculasData.filter(d => d.status === 'Healthy').length,
    warning: matriculasData.filter(d => d.status === 'Warning').length,
    outOfDate: matriculasData.filter(d => d.status === 'OutOfDate').length,
    danger: matriculasData.filter(d => d.status === 'Danger').length,
    totalContracts: matriculasData.reduce((acc, curr) => acc + curr.contractCount, 0)
  };

  return (
    <Menu>
      <div className="monitoring-page">
        <div className="monitoring-header">
          <div>
            <Title order={2} size="h2">Saúde das Matrículas</Title>
          </div>
          <Group className="monitoring-header-actions">
            <Tooltip label="Atualizar todos os dados">
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
              <Text size="xs" c="dimmed" fw={700} tt="uppercase">Requer Atenção (&gt;36h)</Text>
              <IconAlertTriangle size={20} color="#fab005" />
            </Group>
            <Text size="xl" fw={700} c="yellow.8">{stats.warning}</Text>
            <Text size="xs" c="dimmed" mt={7}>Necessário verificar</Text>
          </Paper>

          <Paper p="md" className="stat-card">
            <Group justify="space-between" mb="xs">
              <Text size="xs" c="dimmed" fw={700} tt="uppercase">Muito Importante (&gt;72h)</Text>
              <IconCircleX size={20} color="#fa5252" />
            </Group>
            <Text size="xl" fw={700} c="red.8">{stats.outOfDate + stats.danger}</Text>
            <Text size="xs" c="dimmed" mt={7}>Altamente desatualizado</Text>
          </Paper>
        </SimpleGrid>

        {loading ? (
          <Center py="xl">
            <Loader size="lg" />
          </Center>
        ) : (
          <Tabs value={activeTab} onChange={setActiveTab} variant="outline" defaultValue="matriculas">
            <Tabs.List style={{ marginBottom: '1.5rem' }}>
              <Tabs.Tab value="matriculas" leftSection={<IconActivity size={16} />}>
                Matrículas
              </Tabs.Tab>
              <Tabs.Tab value="equipes" leftSection={<IconUsers size={16} />}>
                Equipes
              </Tabs.Tab>
              <Tabs.Tab value="admins" leftSection={<IconUserCheck size={16} />}>
                Admins
              </Tabs.Tab>
            </Tabs.List>

            <Tabs.Panel value="matriculas">
              <MatriculasTab data={matriculasData} />
            </Tabs.Panel>

            <Tabs.Panel value="equipes">
              <EquipesTab data={equipesData} />
            </Tabs.Panel>

            <Tabs.Panel value="admins">
              <AdminsTab data={adminsData} />
            </Tabs.Panel>
          </Tabs>
        )}
      </div>
    </Menu>
  );
};

export default MatriculaHealthPage;
