import React, { useState, useEffect, useCallback } from 'react';
import {
  Title,
  Text,
  Group,
  Stack,
  Button,
  Grid,
  Paper,
  Card,
  Center,
  Loader,
  Badge,
  ActionIcon,
  Tooltip
} from '@mantine/core';
import { 
  IconRefresh, 
  IconArrowLeft, 
  IconLayoutDashboard,
  IconExternalLink,
  IconAlertCircle
} from '@tabler/icons-react';
import { BarChart, PieChart, DonutChart, LineChart, AreaChart } from '@mantine/charts';
import Menu from '../Menu';
import { notifications } from '@mantine/notifications';
import { getReportView, ReportView } from '../../services/reportViewService';
import { getReportFilter, getReportResults, ReportFilter, ReportResultsResponse } from '../../services/reportFilterService';

interface ViewExecutionPageProps {
  viewId: string;
}

// ── COMPACT REPORT CARD COMPONENT ───────────────────────────────────────────

interface ReportCardProps {
  reportFilterId: string;
}

const ReportCard: React.FC<ReportCardProps> = ({ reportFilterId }) => {
  const [filter, setFilter] = useState<ReportFilter | null>(null);
  const [results, setResults] = useState<ReportResultsResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const loadReportData = useCallback(async () => {
    try {
      setLoading(true);
      setError(null);
      const [filterData, resultsData] = await Promise.all([
        getReportFilter(reportFilterId),
        getReportResults(reportFilterId, 1, 10) // Fetch first page of 10 rows for dashboard card
      ]);
      setFilter(filterData);
      setResults(resultsData);
    } catch (err: any) {
      setError(err.message || 'Erro ao carregar dados do relatório.');
    } finally {
      setLoading(false);
    }
  }, [reportFilterId]);

  useEffect(() => {
    loadReportData();
  }, [loadReportData]);

  // Dynamic Chart Helper
  const prepareChartData = () => {
    if (!results || results.rows.length === 0) return [];
    const columns = results.columns;
    
    const groupCol = columns.find(c => c.field === 'team' || c.field === 'email' || c.field === 'classification') 
      || columns.find(c => c.source === 'Users_Contract' || c.source === 'Users_Matricula')
      || columns[0];
    const labelKey = groupCol ? groupCol.label : columns[0]?.label;

    // 2. Identify metric/value key (filter.chartMetric if matched, otherwise totalAmount or first numeric col)
    let valueKey: string | null = null;
    if (filter?.chartMetric) {
      const found = columns.find(c => c.label === filter.chartMetric || c.field === filter.chartMetric);
      if (found) {
        valueKey = found.label;
      }
    }

    if (!valueKey) {
      const numericCol = columns.find(c => c.field === 'totalAmount' || c.field === 'contractCount' || c.field === 'quota' || c.field === 'commission')
        || columns.find(c => {
             const val = results.rows[0][c.label];
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

    return results.rows.map((row, idx) => {
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

  if (loading) {
    return (
      <Card withBorder padding="xl" radius="md" style={{ height: '320px', display: 'flex', justifyContent: 'center', alignItems: 'center', backgroundColor: '#ffffff' }}>
        <Stack align="center" gap="xs">
          <Loader size="sm" color="indigo" />
          <Text size="xs" c="dimmed">Carregando dados...</Text>
        </Stack>
      </Card>
    );
  }

  if (error || !filter || !results) {
    return (
      <Card withBorder padding="md" radius="md" style={{ height: '320px', display: 'flex', justifyContent: 'center', alignItems: 'center', backgroundColor: '#fff5f5', borderLeft: '4px solid #ef4444' }}>
        <Stack align="center" gap="xs" style={{ textAlign: 'center', maxWidth: '80%' }}>
          <IconAlertCircle size={24} color="#ef4444" />
          <Text size="xs" color="red" fw={500}>{error || 'Falha ao recuperar relatório'}</Text>
          <Button size="xxs" variant="subtle" color="red" onClick={loadReportData} leftSection={<IconRefresh size={10} />}>
            Tentar novamente
          </Button>
        </Stack>
      </Card>
    );
  }

  const chartData = prepareChartData();
  const outputType = filter.outputType || 'table';
  const chartType = filter.chartType || 'bar';

  return (
    <Card withBorder padding="md" radius="md" style={{ backgroundColor: '#ffffff', minHeight: '320px', display: 'flex', flexDirection: 'column' }}>
      {/* Header */}
      <Group justify="space-between" mb="sm" wrap="nowrap" style={{ borderBottom: '1px solid #f1f3f5', paddingBottom: '8px' }}>
        <Stack gap={1} style={{ flex: 1 }}>
          <Text fw={600} size="sm" style={{ color: '#1c1c1e' }} truncate="end">
            {filter.name}
          </Text>
          <Text size="xxs" c="dimmed" lineClamp={1}>
            {filter.description || 'Módulo de relatório salvo.'}
          </Text>
        </Stack>
        <Group gap={4}>
          <Tooltip label="Abrir Relatório Completo">
            <ActionIcon variant="subtle" color="indigo" size="sm" onClick={() => window.location.hash = `#/reports/${filter.filterId}/results`}>
              <IconExternalLink size={14} />
            </ActionIcon>
          </Tooltip>
        </Group>
      </Group>

      {/* Body Content */}
      <div style={{ flex: 1, display: 'flex', flexDirection: 'column', gap: '8px' }}>
        {/* Table representation */}
        {(outputType === 'table' || outputType === 'both') && (
          <div style={{ flex: 1, overflow: 'auto', maxHeight: '180px', border: '1px solid #f1f3f5', borderRadius: '4px' }}>
            {results.rows.length === 0 ? (
              <Center style={{ padding: '24px' }}>
                <Text size="xxs" c="dimmed" fs="italic">Nenhum contrato encontrado.</Text>
              </Center>
            ) : (
              <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '0.7rem' }}>
                <thead>
                  <tr style={{ backgroundColor: '#f8f9fa', borderBottom: '1px solid #e9ecef' }}>
                    {results.columns.map((col, idx) => (
                      <th key={idx} style={{ padding: '6px 8px', textAlign: 'left', fontWeight: 600, color: '#495057' }}>
                        {col.label}
                      </th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {results.rows.slice(0, 5).map((row, rowIdx) => (
                    <tr key={rowIdx} style={{ borderBottom: '1px solid #f1f3f5', backgroundColor: rowIdx % 2 === 0 ? '#ffffff' : '#fcfcfc' }}>
                      {results.columns.map((col, colIdx) => (
                        <td key={colIdx} style={{ padding: '6px 8px', color: '#4b5563' }}>
                          {String(row[col.label] ?? '—')}
                        </td>
                      ))}
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </div>
        )}

        {/* Sum totals aggregate badge card */}
        {filter.sumTotal && results.totalSum !== undefined && results.totalSum !== null && (
          <Paper withBorder p="xs" radius="sm" style={{ backgroundColor: '#f0fdf4', borderColor: '#bbf7d0', marginTop: 'auto' }}>
            <Group justify="space-between" align="center" wrap="nowrap">
              <Text size="xxs" c="dimmed" fw={600} tt="uppercase">Produção Total</Text>
              <Text size="xs" fw={700} style={{ color: '#166534' }}>
                {new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format(results.totalSum)}
              </Text>
            </Group>
          </Paper>
        )}

        {/* Chart representation */}
        {(outputType === 'chart' || outputType === 'both') && (
          <div style={{ flex: 1, display: 'flex', justifyContent: 'center', alignItems: 'center', minHeight: '140px', padding: '4px' }}>
            {chartData.length === 0 ? (
              <Text size="xxs" c="dimmed" fs="italic">Dados insuficientes para gerar gráfico.</Text>
            ) : (
              <div style={{ width: '100%', display: 'flex', justifyContent: 'center' }}>
                {chartType === 'bar' && (
                  <BarChart
                    h={130}
                    data={chartData.slice(0, 5)}
                    dataKey="name"
                    series={[{ name: 'value', color: 'indigo.5' }]}
                    valueFormatter={(v) => new Intl.NumberFormat('pt-BR').format(v)}
                    style={{ width: '100%', fontSize: '0.6rem' }}
                  />
                )}
                {chartType === 'line' && (
                  <LineChart
                    h={130}
                    data={chartData.slice(0, 5)}
                    dataKey="name"
                    series={[{ name: 'value', color: 'indigo.5' }]}
                    curveType="monotone"
                    valueFormatter={(v) => new Intl.NumberFormat('pt-BR').format(v)}
                    style={{ width: '100%', fontSize: '0.6rem' }}
                  />
                )}
                {chartType === 'area' && (
                  <AreaChart
                    h={130}
                    data={chartData.slice(0, 5)}
                    dataKey="name"
                    series={[{ name: 'value', color: 'indigo.5' }]}
                    curveType="monotone"
                    valueFormatter={(v) => new Intl.NumberFormat('pt-BR').format(v)}
                    style={{ width: '100%', fontSize: '0.6rem' }}
                  />
                )}
                {chartType === 'pie' && (
                  <PieChart
                    data={chartData.slice(0, 5)}
                    size={90}
                    withTooltip
                  />
                )}
                {chartType === 'donut' && (
                  <DonutChart
                    data={chartData.slice(0, 5)}
                    size={90}
                    thickness={15}
                    withTooltip
                  />
                )}
              </div>
            )}
          </div>
        )}
      </div>
    </Card>
  );
};

// ── VIEWER PAGE ─────────────────────────────────────────────────────────────

const ViewExecutionPage: React.FC<ViewExecutionPageProps> = ({ viewId }) => {
  const [view, setView] = useState<ReportView | null>(null);
  const [loading, setLoading] = useState(true);
  const [refreshKey, setRefreshKey] = useState(0);

  const fetchViewConfig = useCallback(async () => {
    try {
      setLoading(true);
      const data = await getReportView(viewId);
      setView(data);
    } catch (err: any) {
      notifications.show({
        title: 'Erro',
        message: err.message || 'Falha ao carregar dashboard',
        color: 'red'
      });
    } finally {
      setLoading(false);
    }
  }, [viewId]);

  useEffect(() => {
    fetchViewConfig();
  }, [fetchViewConfig]);

  const handleRefreshAll = () => {
    setRefreshKey(prev => prev + 1);
    notifications.show({
      title: 'Atualizando',
      message: 'Todos os módulos do painel estão sendo recalculados.',
      color: 'indigo'
    });
  };

  if (loading) {
    return <Menu><Center style={{ height: '80vh' }}><Loader color="indigo" /></Center></Menu>;
  }

  if (!view) {
    return (
      <Menu>
        <Center style={{ height: '80vh' }}>
          <Stack align="center" gap="md">
            <IconAlertCircle size={48} color="gray" />
            <Text c="dimmed">Dashboard não encontrado ou você não possui autorização.</Text>
            <Button color="indigo" onClick={() => window.location.hash = '#/views'}>Voltar para Dashboards</Button>
          </Stack>
        </Center>
      </Menu>
    );
  }

  return (
    <Menu>
      <div style={{ padding: '20px', maxWidth: '1200px', margin: '0 auto' }}>
        {/* Top Header */}
        <Group justify="space-between" mb="xl" wrap="nowrap">
          <Group gap="md" style={{ flex: 1 }}>
            <ActionIcon variant="subtle" color="gray" size="lg" onClick={() => window.location.hash = '#/views'}>
              <IconArrowLeft size={20} />
            </ActionIcon>
            <Stack gap={1} style={{ flex: 1 }}>
              <Group gap="sm" wrap="nowrap">
                <IconLayoutDashboard size={24} style={{ color: '#4f46e5' }} />
                <Title order={2} style={{ color: '#1c1c1e', fontWeight: 700 }} lineClamp={1}>
                  {view.name}
                </Title>
                <Badge color={view.scope === 'shared' ? 'indigo' : 'gray'} variant="light">
                  {view.scope === 'shared' ? 'Compartilhado' : 'Privado'}
                </Badge>
              </Group>
              {view.description && (
                <Text size="xs" c="dimmed" lineClamp={1}>
                  {view.description}
                </Text>
              )}
            </Stack>
          </Group>

          <Button 
            leftSection={<IconRefresh size={16} />} 
            color="indigo" 
            variant="light" 
            onClick={handleRefreshAll}
            size="sm"
          >
            Atualizar Painel
          </Button>
        </Group>

        {/* GRID VIEW LAYOUT */}
        <Stack gap="xl" key={refreshKey}>
          {view.rows.map((row, rowIndex) => (
            <Grid key={rowIndex} gutter="md">
              {row.columns.map((col, colIndex) => {
                const columnSpan = row.columns.length === 1 ? 12 : row.columns.length === 2 ? 6 : 4;
                return (
                  <Grid.Col key={colIndex} span={{ base: 12, md: columnSpan }}>
                    {col.reportFilterId ? (
                      <ReportCard reportFilterId={col.reportFilterId} />
                    ) : (
                      <Card withBorder padding="md" radius="md" style={{ height: '320px', display: 'flex', justifyContent: 'center', alignItems: 'center', backgroundColor: '#fafafa', borderStyle: 'dashed' }}>
                        <Text size="xs" c="dimmed" fs="italic">Slot de Relatório Vazio</Text>
                      </Card>
                    )}
                  </Grid.Col>
                );
              })}
            </Grid>
          ))}
        </Stack>
      </div>
    </Menu>
  );
};

export default ViewExecutionPage;
