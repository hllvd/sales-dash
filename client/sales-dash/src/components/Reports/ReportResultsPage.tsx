import React, { useState, useEffect, useCallback } from 'react';
import { Title, Button, Table, Group, Text, Center, Loader, Paper, Card, Stack } from '@mantine/core';
import { BarChart, PieChart, DonutChart, LineChart, AreaChart } from '@mantine/charts';
import { IconEdit, IconArrowLeft } from '@tabler/icons-react';
import Menu from '../Menu';
import { notifications } from '@mantine/notifications';
import { 
  getReportResults, 
  getReportFilter, 
  ReportFilter, 
  OutputColumn,
  startReportExport,
  getReportExportStatusUrl,
  getReportExportDownloadUrl
} from '../../services/reportFilterService';
import ExportButton from '../../shared/ExportButton';
import ExportProgressIndicator from '../../shared/ExportProgressIndicator';
import '../UsersPage.css'; // Reusing some basic table/pagination CSS

interface ReportResultsPageProps {
  filterId: string;
}

const ReportResultsPage: React.FC<ReportResultsPageProps> = ({ filterId }) => {
  const [report, setReport] = useState<ReportFilter | null>(null);
  const [columns, setColumns] = useState<OutputColumn[]>([]);
  const [rows, setRows] = useState<Record<string, any>[]>([]);
  
  const [loading, setLoading] = useState(true);
  const [page, setPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const [totalSum, setTotalSum] = useState<number | undefined>(undefined);
  const [overallRetention, setOverallRetention] = useState<number | undefined>(undefined);
  const pageSize = 25; // Replicating pagination size

  const [currentUserRole, setCurrentUserRole] = useState<string>('');
  const [currentUserId, setCurrentUserId] = useState<string>('');

  // Export state
  const [exportJobId, setExportJobId] = useState<string | null>(null);
  const [isExporting, setIsExporting] = useState(false);
  const exportToken = localStorage.getItem('token') || '';

  const fetchResults = useCallback(async () => {
    try {
      setLoading(true);
      const [filterData, resultsData] = await Promise.all([
        getReportFilter(filterId),
        getReportResults(filterId, page, pageSize)
      ]);
      
      setReport(filterData);
      setColumns(resultsData.columns);
      setRows(resultsData.rows);
      setTotalPages(resultsData.totalPages);
      setTotalCount(resultsData.totalCount);
      setTotalSum(resultsData.totalSum);
      setOverallRetention(resultsData.overallRetention);
    } catch (err: any) {
      notifications.show({ title: 'Erro', message: err.message || 'Falha ao carregar resultados do relatório', color: 'red' });
    } finally {
      setLoading(false);
    }
  }, [filterId, page, pageSize]);

  useEffect(() => {
    fetchResults();
    const user = JSON.parse(localStorage.getItem('user') || '{}');
    setCurrentUserRole(user.role || '');
    setCurrentUserId(user.id || '');
  }, [fetchResults]);

  const isSuperadmin = currentUserRole === 'superadmin';
  const isOwner = report?.userId === currentUserId;

  const handleExport = async () => {
    setIsExporting(true);
    try {
      const job = await startReportExport(filterId);
      setExportJobId(job.jobId);
    } catch (err: any) {
      notifications.show({ title: 'Erro', message: err.message || 'Falha ao iniciar exportação', color: 'red' });
      setIsExporting(false);
    }
  };

  // Prepare Dynamic Aggregated Chart Data
  const prepareChartData = () => {
    if (!rows || rows.length === 0) return [];
    
    // 1. Identify category/label key (Team, Email, Classification or first string col)
    const groupCol = columns.find(c => c.field === 'team' || c.field === 'email' || c.field === 'classification') 
      || columns.find(c => c.source === 'Users_Contract' || c.source === 'Users_Matricula')
      || columns[0];
      
    const labelKey = groupCol ? groupCol.label : columns[0]?.label;

    // 2. Identify metric/value key (report.chartMetric if matched, otherwise totalAmount or first numeric col)
    let valueKey: string | null = null;
    if (report?.chartMetric) {
      const found = columns.find(c => c.label === report.chartMetric || c.field === report.chartMetric);
      if (found) {
        valueKey = found.label;
      }
    }
    
    if (!valueKey) {
      const numericCol = columns.find(c => c.field === 'totalAmount' || c.field === 'contractCount' || c.field === 'quota' || c.field === 'commission')
        || columns.find(c => {
             const val = rows[0][c.label];
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

    return rows.map((row, idx) => {
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

  const chartData = prepareChartData();
  const outputType = report?.outputType || 'table';
  const chartType = report?.chartType || 'bar';

  return (
    <Menu>
      <div className="users-container" style={{ padding: '20px', maxWidth: '1400px', margin: '0 auto' }}>
        <Group justify="space-between" align="center" mb="xl">
          <Group>
            <Button variant="subtle" onClick={() => window.location.hash = '#/reports'} leftSection={<IconArrowLeft size={16} />}>
              Voltar para Relatórios
            </Button>
            <div>
              <Text size="sm" c="dimmed">Relatórios → {report?.name || 'Carregando...'}</Text>
              <Title order={2}>{report?.name || 'Resultados do Relatório'}</Title>
              <Text size="sm" c="dimmed">
                {totalCount} {totalCount === 1 ? "registro" : "registros"} encontrado(s)
              </Text>
            </div>
          </Group>

          <Group>
            {isSuperadmin && isOwner && (
              <Button 
                variant="light"
                leftSection={<IconEdit size={16} />} 
                onClick={() => window.location.hash = `#/reports/${filterId}/edit`}
              >
                Editar
              </Button>
            )}
            <ExportButton
              onExport={handleExport}
              isExporting={isExporting}
            />
          </Group>
        </Group>

        <ExportProgressIndicator
          jobId={exportJobId}
          pollUrl={getReportExportStatusUrl}
          downloadUrl={getReportExportDownloadUrl}
          token={exportToken}
          onComplete={() => { setIsExporting(false); setExportJobId(null); }}
          onError={(msg) => { notifications.show({ title: 'Erro', message: msg, color: 'red' }); setIsExporting(false); setExportJobId(null); }}
        />

        {loading ? (
          <Center style={{ height: '50vh' }}>
            <Loader />
          </Center>
        ) : rows.length === 0 ? (
          <div className="empty-state" style={{ padding: '40px', textAlign: 'center' }}>
            <Text c="dimmed">Nenhum resultado encontrado para estes filtros.</Text>
          </div>
        ) : (
          <Stack gap="lg">
            
            {/* Part 1: Table (rendered if outputType is table or both) */}
            {(outputType === 'table' || outputType === 'both') && (
              <>
                <div className="table-container" style={{ backgroundColor: 'white', borderRadius: '8px', boxShadow: '0 1px 3px rgba(0,0,0,0.1)', overflow: 'hidden' }}>
                  <Table.ScrollContainer minWidth={800}>
                    <Table striped highlightOnHover>
                      <Table.Thead>
                        <Table.Tr>
                          {columns.map((col) => (
                            <Table.Th key={col.field}>{col.label}</Table.Th>
                          ))}
                        </Table.Tr>
                      </Table.Thead>
                      <Table.Tbody>
                        {rows.map((row, index) => (
                          <Table.Tr key={index}>
                            {columns.map((col) => (
                              <Table.Td key={col.field}>
                                {row[col.label] !== null && row[col.label] !== undefined 
                                  ? String(row[col.label]) 
                                  : '-'}
                              </Table.Td>
                            ))}
                          </Table.Tr>
                        ))}
                      </Table.Tbody>
                    </Table>
                  </Table.ScrollContainer>
                </div>

                {totalPages > 1 && (
                  <div className="pagination" style={{ display: 'flex', justifyContent: 'center', alignItems: 'center', gap: '16px', marginTop: '10px' }}>
                    <Button
                      onClick={() => setPage((p) => Math.max(1, p - 1))}
                      disabled={page === 1}
                      variant="default"
                      size="sm"
                    >
                      ← Anterior
                    </Button>
                    <Text size="sm" className="pagination-info">
                      Página {page} de {totalPages}
                    </Text>
                    <Button
                      onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
                      disabled={page === totalPages}
                      variant="default"
                      size="sm"
                    >
                      Próxima →
                    </Button>
                  </div>
                )}
              </>
            )}

            {/* Part 2: Summary Sum Card (if sumTotal is true) */}
            {report?.sumTotal && totalSum !== undefined && totalSum !== null && (
              <Paper withBorder p="md" radius="md" style={{ backgroundColor: '#f5fdf8', borderLeft: '4px solid #10b981' }}>
                <Group justify="space-between" align="center">
                  <Stack gap={2}>
                    <Text size="xs" c="dimmed" tt="uppercase" fw={700} style={{ letterSpacing: '0.05em' }}>
                      Resumo do Relatório (Summary)
                    </Text>
                    <Title order={3} style={{ color: '#0f766e', fontWeight: 700 }}>
                      {new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format(totalSum)}
                    </Title>
                  </Stack>
                  <Group gap="sm">
                    {overallRetention !== undefined && overallRetention !== null && (
                      <Paper withBorder p="xs" radius="sm" style={{ backgroundColor: '#ffffff', minWidth: '120px' }}>
                        <Text size="xxs" c="dimmed" fw={500} style={{ textAlign: 'center' }}>
                          {report?.summaryRetentionType === 'strict' ? "Retenção Estrita Geral" : "Retenção Geral"}
                        </Text>
                        <Text size="md" fw={700} style={{ textAlign: 'center', color: '#0f766e' }}>
                          {new Intl.NumberFormat('pt-BR', { style: 'percent', minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(overallRetention)}
                        </Text>
                      </Paper>
                    )}
                    <Paper withBorder p="xs" radius="sm" style={{ backgroundColor: '#ffffff', minWidth: '120px' }}>
                      <Text size="xxs" c="dimmed" fw={500} style={{ textAlign: 'center' }}>
                        {report?.groupByEmail ? "Total Geral de Usuários" : report?.groupByTeam ? "Total Geral de Equipes" : report?.groupByClassification ? "Total Geral de Níveis" : "Total Geral de Contratos"}
                      </Text>
                      <Text size="md" fw={700} style={{ textAlign: 'center', color: '#1f2937' }}>
                        {totalCount}
                      </Text>
                    </Paper>
                  </Group>
                </Group>
              </Paper>
            )}

            {/* Part 3: Chart (rendered if outputType is chart or both) */}
            {(outputType === 'chart' || outputType === 'both') && (
              <div>
                <Paper withBorder p="lg" radius="md" style={{ backgroundColor: '#ffffff', minHeight: '380px' }}>
                  <Text size="sm" fw={700} c="dimmed" tt="uppercase" mb="lg" style={{ letterSpacing: '0.05em' }}>
                    Visualização Analítica do Relatório
                  </Text>
                  
                  {chartData.length === 0 ? (
                    <Center style={{ height: '300px' }}>
                      <Text size="xs" c="dimmed" fs="italic">Não há dados suficientes ou colunas numéricas disponíveis para renderizar o gráfico.</Text>
                    </Center>
                  ) : (
                    <Center style={{ width: '100%', minHeight: '320px' }}>
                      <div style={{ width: '100%', maxWidth: '720px', display: 'flex', justifyContent: 'center' }}>
                        {chartType === 'bar' && (
                          <BarChart
                            h={320}
                            data={chartData}
                            dataKey="name"
                            series={[{ name: 'value', color: 'indigo.6' }]}
                            valueFormatter={(value) => new Intl.NumberFormat('pt-BR').format(value)}
                            style={{ width: '100%' }}
                          />
                        )}
                        {chartType === 'line' && (
                          <LineChart
                            h={320}
                            data={chartData}
                            dataKey="name"
                            series={[{ name: 'value', color: 'indigo.6' }]}
                            curveType="monotone"
                            valueFormatter={(value) => new Intl.NumberFormat('pt-BR').format(value)}
                            style={{ width: '100%' }}
                          />
                        )}
                        {chartType === 'area' && (
                          <AreaChart
                            h={320}
                            data={chartData}
                            dataKey="name"
                            series={[{ name: 'value', color: 'indigo.6' }]}
                            curveType="monotone"
                            valueFormatter={(value) => new Intl.NumberFormat('pt-BR').format(value)}
                            style={{ width: '100%' }}
                          />
                        )}
                        {chartType === 'pie' && (
                          <PieChart
                            data={chartData}
                            withTooltip
                            tooltipDataSource="segment"
                            size={240}
                          />
                        )}
                        {chartType === 'donut' && (
                          <DonutChart
                            data={chartData}
                            withTooltip
                            tooltipDataSource="segment"
                            size={240}
                            thickness={25}
                          />
                        )}
                      </div>
                    </Center>
                  )}
                </Paper>
              </div>
            )}
            
          </Stack>
        )}
      </div>
    </Menu>
  );
};

export default ReportResultsPage;
