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
  Table,
  Badge,
  Select,
  NumberInput,
  Button,
  Alert,
  TextInput,
  Pagination,
} from '@mantine/core';
import {
  IconRefresh,
  IconUsers,
  IconUserCheck,
  IconCoins,
  IconReceipt2,
  IconInfoCircle,
  IconSearch,
  IconFileSpreadsheet,
} from '@tabler/icons-react';
import { getLicensingReport, LicensingReport, PriceTierInfo, UserLicenseDetail } from '../../services/contractService';
import Menu from '../Menu';
import './LicensingPage.css';

const LicensingPage: React.FC = () => {
  const [month, setMonth] = useState<string>((new Date().getMonth() + 1).toString());
  const [year, setYear] = useState<string>(new Date().getFullYear().toString());
  const [minimumDays, setMinimumDays] = useState<number | undefined>(undefined);
  const [report, setReport] = useState<LicensingReport | null>(null);
  
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const pageSize = 15;

  const fetchReport = async (isManual = false) => {
    try {
      if (isManual) setRefreshing(true);
      else setLoading(true);
      
      const data = await getLicensingReport(
        parseInt(year),
        parseInt(month),
        minimumDays
      );
      setReport(data);
      if (minimumDays === undefined) {
        setMinimumDays(data.minimumActiveDays);
      }
    } catch (error) {
      console.error('Failed to fetch licensing report', error);
    } finally {
      setLoading(false);
      setRefreshing(false);
    }
  };

  useEffect(() => {
    fetchReport();
  }, [month, year]); // Auto fetch when month/year changes

  const handleApplyMinimumDays = () => {
    fetchReport();
  };

  const handleExportCSV = () => {
    if (!report) return;
    
    const headers = ['Nome', 'Email', 'Cargo', 'Equipe', 'Dias Ativos no Mês', 'Status Licenciamento'];
    const rows = report.users.map(u => [
      u.name,
      u.email,
      u.role,
      u.teamName,
      `${u.activeDaysInMonth} dias`,
      u.isLicensed ? 'Licenciado' : 'Abaixo do mínimo'
    ]);

    const csvContent = "data:text/csv;charset=utf-8,\uFEFF" 
      + [headers.join(','), ...rows.map(e => e.map(val => `"${val.replace(/"/g, '""')}"`).join(','))].join('\n');
    
    const encodedUri = encodeURI(csvContent);
    const link = document.createElement("a");
    link.setAttribute("href", encodedUri);
    link.setAttribute("download", `licenciamento-${month}-${year}.csv`);
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
  };

  const months = [
    { value: '1', label: 'Janeiro' },
    { value: '2', label: 'Fevereiro' },
    { value: '3', label: 'Março' },
    { value: '4', label: 'Abril' },
    { value: '5', label: 'Maio' },
    { value: '6', label: 'Junho' },
    { value: '7', label: 'Julho' },
    { value: '8', label: 'Agosto' },
    { value: '9', label: 'Setembro' },
    { value: '10', label: 'Outubro' },
    { value: '11', label: 'Novembro' },
    { value: '12', label: 'Dezembro' }
  ];

  const currentYear = new Date().getFullYear();
  const years = Array.from({ length: 5 }, (_, i) => {
    const y = currentYear - 2 + i;
    return { value: y.toString(), label: y.toString() };
  });

  // Filter users based on search
  const filteredUsers = report
    ? report.users.filter(u =>
        u.name.toLowerCase().includes(search.toLowerCase()) ||
        u.email.toLowerCase().includes(search.toLowerCase())
      )
    : [];

  const totalPages = Math.ceil(filteredUsers.length / pageSize);
  const paginatedUsers = filteredUsers.slice((page - 1) * pageSize, page * pageSize);

  return (
    <Menu>
      <div className="licensing-page">
        <div className="licensing-header" style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '2rem' }}>
          <div>
            <Title order={2} size="h2" style={{ color: '#111827' }}>Licenciamento de Usuários</Title>
            <Text size="sm" c="dimmed">Detalhamento mensal de usuários ativos e cálculo automático de licenças.</Text>
          </div>
          <Tooltip label="Atualizar dados">
            <ActionIcon
              variant="light"
              size="lg"
              onClick={() => fetchReport(true)}
              loading={refreshing}
              color="red"
            >
              <IconRefresh size={20} />
            </ActionIcon>
          </Tooltip>
        </div>

        {/* Filters Panel */}
        <Paper p="md" radius="md" withBorder mb="xl" style={{ backgroundColor: '#f9fafb' }}>
          <Group align="flex-end" gap="md">
            <Select
              label="Mês"
              data={months}
              value={month}
              onChange={(val) => { if (val) { setMonth(val); setPage(1); } }}
              style={{ width: 150 }}
            />
            <Select
              label="Ano"
              data={years}
              value={year}
              onChange={(val) => { if (val) { setYear(val); setPage(1); } }}
              style={{ width: 120 }}
            />
            <NumberInput
              label="Mínimo de dias ativos"
              value={minimumDays}
              onChange={(val) => setMinimumDays(val === '' ? undefined : Number(val))}
              min={1}
              max={31}
              style={{ width: 180 }}
            />
            <Button color="indigo" onClick={handleApplyMinimumDays}>
              Calcular
            </Button>
          </Group>
        </Paper>

        {loading ? (
          <Center py="xl">
            <Loader size="lg" />
          </Center>
        ) : report ? (
          <>
            {/* KPI Cards Section */}
            <SimpleGrid cols={{ base: 1, sm: 3 }} spacing="md" mb="xl">
              <Paper p="md" radius="md" withBorder className="kpi-card">
                <Group justify="space-between" mb="xs">
                  <Text size="xs" c="dimmed" fw={700} tt="uppercase">Usuários Licenciados</Text>
                  <IconUserCheck size={24} color="#228be6" />
                </Group>
                <div style={{ display: 'flex', alignItems: 'baseline', gap: '8px' }}>
                  <Text size="xxl" fw={700} style={{ fontSize: '2rem', color: '#1f2937' }}>
                    {report.totalLicensedUsers}
                  </Text>
                  <Text size="sm" c="dimmed">
                    / {report.totalUsersConsidered} totais
                  </Text>
                </div>
                <Text size="xs" c="dimmed" mt={4}>Ativos por pelo menos {report.minimumActiveDays} dias</Text>
              </Paper>

              <Paper p="md" radius="md" withBorder className="kpi-card">
                <Group justify="space-between" mb="xs">
                  <Text size="xs" c="dimmed" fw={700} tt="uppercase">Preço da Licença</Text>
                  <IconCoins size={24} color="#40c057" />
                </Group>
                <Text size="xxl" fw={700} style={{ fontSize: '2rem', color: '#1f2937' }}>
                  <NumberFormatter value={report.pricePerUser} prefix="R$ " decimalScale={2} fixedDecimalScale thousandSeparator="." decimalSeparator="," />
                </Text>
                <Text size="xs" c="dimmed" mt={4}>Baseado na faixa de volume atual</Text>
              </Paper>

              <Paper p="md" radius="md" withBorder className="kpi-card" style={{ borderLeft: '4px solid #4f46e5' }}>
                <Group justify="space-between" mb="xs">
                  <Text size="xs" c="dimmed" fw={700} tt="uppercase">Custo Total do Período</Text>
                  <IconReceipt2 size={24} color="#4f46e5" />
                </Group>
                <Text size="xxl" fw={700} style={{ fontSize: '2rem', color: '#4f46e5' }}>
                  <NumberFormatter value={report.totalCost} prefix="R$ " decimalScale={2} fixedDecimalScale thousandSeparator="." decimalSeparator="," />
                </Text>
                <Text size="xs" c="dimmed" mt={4}>Volume total faturado</Text>
              </Paper>
            </SimpleGrid>

            {/* Pricing Tiers & Info Grid */}
            <SimpleGrid cols={{ base: 1, md: 2 }} spacing="xl" mb="xl">
              <div>
                <Title order={4} mb="sm" style={{ color: '#374151' }}>Tabela de Preços por Volume</Title>
                <Table variant="simple" striped withTableBorder withColumnBorders>
                  <Table.Thead>
                    <Table.Tr>
                      <Table.Th>Quantidade de Usuários</Table.Th>
                      <Table.Th>Valor Unitário</Table.Th>
                      <Table.Th>Status</Table.Th>
                    </Table.Tr>
                  </Table.Thead>
                  <Table.Tbody>
                    {report.priceTiers.map((tier, index) => (
                      <Table.Tr 
                        key={index}
                        className={tier.isCurrentTier ? 'active-tier-row' : ''}
                        style={tier.isCurrentTier ? { backgroundColor: '#e0e7ff', fontWeight: 600 } : {}}
                      >
                        <Table.Td>
                          {tier.to ? `${tier.from} a ${tier.to}` : `Acima de ${tier.from - 1}`}
                        </Table.Td>
                        <Table.Td>
                          <NumberFormatter value={tier.pricePerUser} prefix="R$ " decimalScale={2} fixedDecimalScale thousandSeparator="." decimalSeparator="," />
                        </Table.Td>
                        <Table.Td>
                          {tier.isCurrentTier ? (
                            <Badge color="indigo" variant="filled">Aplicado</Badge>
                          ) : (
                            <Text size="xs" c="dimmed">Inativo</Text>
                          )}
                        </Table.Td>
                      </Table.Tr>
                    ))}
                  </Table.Tbody>
                </Table>
              </div>

              <div style={{ display: 'flex', flexDirection: 'column', justifyContent: 'center' }}>
                <Alert variant="light" color="blue" title="Informações Importantes" icon={<IconInfoCircle />}>
                  <Text size="sm" mb="xs">
                    O modelo de desconto por volume é prática padrão no mercado de SaaS e reflete a diluição
                    dos custos fixos de operação à medida que a base de usuários cresce.
                  </Text>
                  <Text size="sm">
                    <strong>Reajuste anual:</strong> os valores de licença serão reajustados anualmente com base no IPCA
                    (Índice Nacional de Preços ao Consumidor Amplo), índice oficial de inflação apurado pelo
                    IBGE. Isso garante previsibilidade orçamentária para ambas as partes — sem surpresas e sem reajustes abusivos.
                  </Text>
                </Alert>
              </div>
            </SimpleGrid>

            {/* Users Detail Section */}
            <Paper p="md" radius="md" withBorder>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '1.5rem', flexWrap: 'wrap', gap: '1rem' }}>
                <Title order={4} style={{ color: '#374151' }}>Detalhamento dos Usuários</Title>
                <Group>
                  <TextInput
                    placeholder="Buscar por nome ou email..."
                    value={search}
                    onChange={(e) => { setSearch(e.target.value); setPage(1); }}
                    leftSection={<IconSearch size={16} />}
                    style={{ width: 280 }}
                  />
                  <Button 
                    variant="light" 
                    color="indigo" 
                    leftSection={<IconFileSpreadsheet size={16} />}
                    onClick={handleExportCSV}
                  >
                    Exportar Planilha
                  </Button>
                </Group>
              </div>

              {paginatedUsers.length === 0 ? (
                <Center py="xl">
                  <Text c="dimmed">Nenhum usuário correspondente aos critérios de busca foi encontrado.</Text>
                </Center>
              ) : (
                <>
                  <Table variant="simple" striped highlightOnHover>
                    <Table.Thead>
                      <Table.Tr>
                        <Table.Th>Nome</Table.Th>
                        <Table.Th>Email</Table.Th>
                        <Table.Th>Cargo</Table.Th>
                        <Table.Th>Equipe</Table.Th>
                        <Table.Th>Dias Ativos no Mês</Table.Th>
                        <Table.Th>Status</Table.Th>
                      </Table.Tr>
                    </Table.Thead>
                    <Table.Tbody>
                      {paginatedUsers.map((u) => (
                        <Table.Tr key={u.userId}>
                          <Table.Td fw={600} style={{ color: '#1f2937' }}>{u.name}</Table.Td>
                          <Table.Td style={{ color: '#4b5563' }}>{u.email}</Table.Td>
                          <Table.Td>
                            <Badge variant="outline" color={u.role === 'admin' ? 'blue' : u.role === 'superadmin' ? 'red' : 'gray'}>
                              {u.role}
                            </Badge>
                          </Table.Td>
                          <Table.Td style={{ color: '#4b5563' }}>{u.teamName}</Table.Td>
                          <Table.Td fw={600}>{u.activeDaysInMonth} dias</Table.Td>
                          <Table.Td>
                            {u.isLicensed ? (
                              <Badge color="green" variant="light">Licenciado</Badge>
                            ) : (
                              <Badge color="gray" variant="light">Abaixo do mínimo</Badge>
                            )}
                          </Table.Td>
                        </Table.Tr>
                      ))}
                    </Table.Tbody>
                  </Table>

                  {totalPages > 1 && (
                    <Group justify="center" mt="lg">
                      <Pagination total={totalPages} value={page} onChange={setPage} color="indigo" />
                    </Group>
                  )}
                </>
              )}
            </Paper>
          </>
        ) : (
          <Center py="xl">
            <Text c="dimmed">Não foi possível carregar os dados de licenciamento.</Text>
          </Center>
        )}
      </div>
    </Menu>
  );
};

export default LicensingPage;
