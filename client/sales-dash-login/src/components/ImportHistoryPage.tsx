import React, { useState, useEffect } from 'react';
import {
  Title,
  Paper,
  Table,
  Badge,
  Text,
  Group,
  ActionIcon,
  Tooltip,
  LoadingOverlay,
  Button,
  Modal,
  Alert,
} from '@mantine/core';
import { IconArrowBackUp, IconAlertCircle, IconCheck } from '@tabler/icons-react';
import StandardModal from '../shared/StandardModal';
import { apiService, ImportSession } from '../services/apiService';
import { notifications } from '@mantine/notifications';
import Menu from './Menu';
import './ImportHistoryPage.css';

const ImportHistoryPage: React.FC = () => {
  const [history, setHistory] = useState<ImportSession[]>([]);
  const [loading, setLoading] = useState(true);
  const [undoingSessionId, setUndoingSessionId] = useState<number | null>(null);
  const [confirmUndoId, setConfirmUndoId] = useState<number | null>(null);

  const fetchHistory = async () => {
    try {
      setLoading(true);
      const response = await apiService.getImportHistory();
      if (response.success) {
        setHistory(response.data || []);
      }
    } catch (error) {
      console.error('Failed to fetch import history:', error);
      notifications.show({
        title: 'Erro',
        message: 'Falha ao carregar histórico de importação',
        color: 'red',
      });
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchHistory();
  }, []);

  const handleUndo = async (id: number) => {
    try {
      setUndoingSessionId(id);
      const response = await apiService.undoImport(id);
      if (response.success) {
        notifications.show({
          title: 'Sucesso',
          message: 'Importação desfeita com sucesso. Os registros criados foram removidos.',
          color: 'green',
          icon: <IconCheck size={16} />,
        });
        fetchHistory();
      }
    } catch (error: any) {
      notifications.show({
        title: 'Erro',
        message: error.message || 'Falha ao desfazer importação',
        color: 'red',
        icon: <IconAlertCircle size={16} />,
      });
    } finally {
      setUndoingSessionId(null);
      setConfirmUndoId(null);
    }
  };

  const getStatusBadge = (status: string) => {
    switch (status) {
      case 'completed':
        return <Badge color="green">Concluído</Badge>;
      case 'completed_with_errors':
        return <Badge color="yellow">Concluído com Erros</Badge>;
      case 'undone':
        return <Badge color="gray">Desfeito</Badge>;
      default:
        return <Badge color="blue">{status}</Badge>;
    }
  };

  const rows = history.map((session) => (
    <Table.Tr key={session.id}>
      <Table.Td>{new Date(session.createdAt).toLocaleString('pt-BR')}</Table.Td>
      <Table.Td>
        <Text fw={500}>{session.fileName}</Text>
        <Text size="xs" c="dimmed">{session.templateName || 'Sem Template'}</Text>
      </Table.Td>
      <Table.Td>{session.uploadedBy?.name || 'Sistema'}</Table.Td>
      <Table.Td>
        <Group gap="xs">
          <Text size="sm">Total: {session.totalRows}</Text>
          <Text size="sm" c="green">Ok: {session.processedRows}</Text>
          <Text size="sm" c="red">Falhas: {session.failedRows}</Text>
        </Group>
      </Table.Td>
      <Table.Td>{getStatusBadge(session.status)}</Table.Td>
      <Table.Td>
        {session.status !== 'undone' && (
          <Tooltip label="Desfazer Importação (Deletar registros criados)">
            <ActionIcon
              color="red"
              variant="light"
              onClick={() => setConfirmUndoId(session.id)}
              loading={undoingSessionId === session.id}
            >
              <IconArrowBackUp size={20} />
            </ActionIcon>
          </Tooltip>
        )}
      </Table.Td>
    </Table.Tr>
  ));

  return (
    <Menu>
      <div className="import-history-page">
        <LoadingOverlay visible={loading} />
        
        <div className="import-history-header">
          <div>
            <Title order={2} size="h2" className="page-title-break">Histórico de Importação</Title>
            <p className="import-history-subtitle">
              Gerencie as importações realizadas e desfaça alterações se necessário.
            </p>
          </div>
        </div>

        {history.length === 0 && !loading ? (
          <Paper p="xl" withBorder radius="md">
            <Text ta="center" c="dimmed">Nenhuma importação encontrada no histórico.</Text>
          </Paper>
        ) : (
          <div className="import-history-table-container">
            <Table striped highlightOnHover verticalSpacing="sm">
              <Table.Thead>
                <Table.Tr>
                  <Table.Th>Data</Table.Th>
                  <Table.Th>Arquivo / Template</Table.Th>
                  <Table.Th>Importado por</Table.Th>
                  <Table.Th>Estatísticas</Table.Th>
                  <Table.Th>Status</Table.Th>
                  <Table.Th>Ações</Table.Th>
                </Table.Tr>
              </Table.Thead>
              <Table.Tbody>{rows}</Table.Tbody>
            </Table>
          </div>
        )}

        <StandardModal
          isOpen={confirmUndoId !== null}
          onClose={() => setConfirmUndoId(null)}
          title="Confirmar Desfazer"
          size="md"
          footer={
            <>
              <button className="btn-cancel" onClick={() => setConfirmUndoId(null)}>
                Cancelar
              </button>
              <button
                className="btn-submit"
                onClick={() => confirmUndoId && handleUndo(confirmUndoId)}
                disabled={undoingSessionId !== null}
              >
                {undoingSessionId !== null ? "Processando..." : "Confirmar e Desfazer"}
              </button>
            </>
          }
        >
          <div style={{ padding: '10px 0' }}>
            <Alert icon={<IconAlertCircle size={16} />} color="red" title="Atenção" mb="md">
              Desfazer uma importação irá deletar permanentemente todos os registros (Contratos, Usuários, PVs, etc.) que foram criados nesta sessão.
            </Alert>
            <Text size="sm" mb="xl">
              Você tem certeza que deseja desfazer esta importação? Esta ação não pode ser revertida.
            </Text>
          </div>
        </StandardModal>
      </div>
    </Menu>
  );
};

export default ImportHistoryPage;
