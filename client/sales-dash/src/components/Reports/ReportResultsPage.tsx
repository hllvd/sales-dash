import React, { useState, useEffect, useCallback } from 'react';
import { Title, Button, Table, Group, Text, Center, Loader } from '@mantine/core';
import { IconEdit, IconArrowLeft } from '@tabler/icons-react';
import Menu from '../Menu';
import { notifications } from '@mantine/notifications';
import { 
  getReportResults, 
  getReportFilter, 
  ReportFilter, 
  OutputColumn 
} from '../../services/reportFilterService';
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
  const pageSize = 25; // Replicating pagination size

  const [currentUserRole, setCurrentUserRole] = useState<string>('');
  const [currentUserId, setCurrentUserId] = useState<string>('');

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

          {isSuperadmin && isOwner && (
            <Button 
              leftSection={<IconEdit size={16} />} 
              onClick={() => window.location.hash = `#/reports/${filterId}/edit`}
            >
              Editar Relatório
            </Button>
          )}
        </Group>

        {loading ? (
          <Center style={{ height: '50vh' }}>
            <Loader />
          </Center>
        ) : rows.length === 0 ? (
          <div className="empty-state" style={{ padding: '40px', textAlign: 'center' }}>
            <Text c="dimmed">Nenhum resultado encontrado para estes filtros.</Text>
          </div>
        ) : (
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
                            {row[col.field] !== null && row[col.field] !== undefined 
                              ? String(row[col.field]) 
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
              <div className="pagination" style={{ display: 'flex', justifyContent: 'center', alignItems: 'center', gap: '16px', marginTop: '20px' }}>
                <Button
                  onClick={() => setPage((p) => Math.max(1, p - 1))}
                  disabled={page === 1}
                  variant="default"
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
                >
                  Próxima →
                </Button>
              </div>
            )}
          </>
        )}
      </div>
    </Menu>
  );
};

export default ReportResultsPage;
