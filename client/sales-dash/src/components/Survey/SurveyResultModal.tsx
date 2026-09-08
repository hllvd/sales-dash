import React, { useState, useEffect } from 'react';
import {
  Modal,
  Stack,
  Group,
  Text,
  Title,
  Badge,
  Table,
  Button,
  Card,
  Progress,
  Divider,
  Loader,
  Center,
} from '@mantine/core';
import { notifications } from '@mantine/notifications';
import { IconRefresh, IconCheck, IconX } from '@tabler/icons-react';
import { SurveyResultDto } from '../../types/Survey';
import { apiService } from '../../services/apiService';

interface SurveyResultModalProps {
  surveyId: string | null;
  onClose: () => void;
}

export const SurveyResultModal: React.FC<SurveyResultModalProps> = ({ surveyId, onClose }) => {
  const [loading, setLoading] = useState<boolean>(false);
  const [resending, setResending] = useState<boolean>(false);
  const [results, setResults] = useState<SurveyResultDto | null>(null);

  const fetchResults = async (id: string) => {
    try {
      setLoading(true);
      const res = await apiService.getSurveyResults(id);
      if (res.success && res.data) {
        setResults(res.data);
      } else {
        notifications.show({
          title: 'Erro',
          message: res.message || 'Falha ao carregar resultados da pergunta.',
          color: 'red',
        });
      }
    } catch (err: any) {
      notifications.show({
        title: 'Erro',
        message: err.message || 'Erro ao buscar resultados.',
        color: 'red',
      });
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    if (surveyId) {
      fetchResults(surveyId);
    } else {
      setResults(null);
    }
  }, [surveyId]);

  if (!surveyId) return null;

  const handleResend = async () => {
    if (!results) return;
    const nonAnsweredCount = results.summary.totalPending + results.summary.totalExpired;
    if (nonAnsweredCount === 0) {
      notifications.show({
        title: 'Aviso',
        message: 'Todos os usuários já responderam a esta pergunta.',
        color: 'blue',
      });
      return;
    }

    const confirmed = window.confirm(
      `Deseja reenviar esta pergunta para ${nonAnsweredCount} usuário(s) que ainda não responderam? O prazo de 2 dias será reiniciado.`
    );
    if (!confirmed) return;

    try {
      setResending(true);
      const res = await apiService.resendSurvey(surveyId, {});
      if (res.success) {
        notifications.show({
          title: 'Sucesso',
          message: 'Pergunta reenviada com sucesso para os usuários não respondidos!',
          color: 'green',
          icon: <IconCheck size={16} />,
        });
        await fetchResults(surveyId);
      } else {
        notifications.show({
          title: 'Erro',
          message: res.message || 'Falha ao reenviar pergunta.',
          color: 'red',
          icon: <IconX size={16} />,
        });
      }
    } catch (err: any) {
      notifications.show({
        title: 'Erro',
        message: err.message || 'Erro ao processar reenvio.',
        color: 'red',
        icon: <IconX size={16} />,
      });
    } finally {
      setResending(false);
    }
  };

  const renderStatusBadge = (status: string) => {
    switch (status) {
      case 'answered':
        return <Badge color="green">Respondido</Badge>;
      case 'pending':
        return <Badge color="yellow">Pendente</Badge>;
      case 'expired':
        return <Badge color="gray">Expirado</Badge>;
      default:
        return <Badge color="gray">{status}</Badge>;
    }
  };

  return (
    <Modal
      opened={!!surveyId}
      onClose={onClose}
      title={
        <Group justify="space-between" style={{ width: '100%', paddingRight: '1rem' }}>
          <Title order={2} style={{ color: '#1c1c1e', fontWeight: 700, fontSize: '1.4rem' }}>
            {results?.summary.title || 'Detalhes da Pergunta'}
          </Title>
          {results?.summary.questionType && (
            <Badge color="blue" variant="light">
              {results.summary.questionType === 'yesno'
                ? 'Sim / Não'
                : results.summary.questionType === 'singlechoice'
                ? 'Escolha Única'
                : 'Múltipla Escolha'}
            </Badge>
          )}
        </Group>
      }
      size="xl"
      centered
      styles={{
        header: {
          borderBottom: '1px solid #f3f4f6',
          paddingBottom: '12px',
          marginBottom: '16px',
        },
        title: {
          width: '100%',
          color: '#1c1c1e',
        }
      }}
    >
      {loading ? (
        <Center p="xl">
          <Loader color="red" />
        </Center>
      ) : results ? (
        <Stack gap="lg">
          <Card withBorder padding="md" radius="md">
            <Text fw={600} size="sm" c="dimmed" mb={4}>
              Pergunta:
            </Text>
            <Text size="md" style={{ whiteSpace: 'pre-wrap' }}>
              {results.summary.questionText}
            </Text>
          </Card>

          {/* Stats Bar */}
          <Group grow>
            <Card withBorder padding="sm" radius="md" ta="center">
              <Text size="xs" c="dimmed">
                Total Atribuídos
              </Text>
              <Text fw={700} size="xl">
                {results.summary.totalAssigned}
              </Text>
            </Card>
            <Card withBorder padding="sm" radius="md" ta="center">
              <Text size="xs" c="dimmed">
                Respondidos
              </Text>
              <Text fw={700} size="xl" c="green">
                {results.summary.totalAnswered}
              </Text>
            </Card>
            <Card withBorder padding="sm" radius="md" ta="center">
              <Text size="xs" c="dimmed">
                Pendentes
              </Text>
              <Text fw={700} size="xl" c="yellow">
                {results.summary.totalPending}
              </Text>
            </Card>
            <Card withBorder padding="sm" radius="md" ta="center">
              <Text size="xs" c="dimmed">
                Expirados
              </Text>
              <Text fw={700} size="xl" c="gray">
                {results.summary.totalExpired}
              </Text>
            </Card>
          </Group>

          {/* Aggregate Results */}
          <div>
            <Text fw={600} size="md" mb="xs">
              Resumo Agregado de Respostas
            </Text>
            <Stack gap="xs">
              {Object.entries(results.aggregateCounts).map(([label, count]) => {
                const total = results.summary.totalAnswered || 1;
                const percentage = Math.round((count / total) * 100);
                return (
                  <div key={label}>
                    <Group justify="space-between" mb={2}>
                      <Text size="sm" fw={500}>
                        {label}
                      </Text>
                      <Text size="xs" c="dimmed">
                        {count} voto(s) ({results.summary.totalAnswered > 0 ? percentage : 0}%)
                      </Text>
                    </Group>
                    <Progress
                      value={results.summary.totalAnswered > 0 ? percentage : 0}
                      color="red"
                      size="md"
                      radius="xl"
                    />
                  </div>
                );
              })}
            </Stack>
          </div>

          <Divider />

          {/* Individual Responses Table */}
          <div>
            <Group justify="space-between" mb="xs">
              <Text fw={600} size="md">
                Respostas Individuais ({results.responses.length})
              </Text>
              <Button
                leftSection={<IconRefresh size={16} />}
                variant="light"
                color="red"
                size="xs"
                loading={resending}
                onClick={handleResend}
                disabled={results.summary.totalPending + results.summary.totalExpired === 0}
              >
                Reenviar para não respondidos ({results.summary.totalPending + results.summary.totalExpired})
              </Button>
            </Group>

            <Table.ScrollContainer minWidth={600}>
              <Table striped highlightOnHover withTableBorder withColumnBorders>
                <Table.Thead>
                  <Table.Tr>
                    <Table.Th>Usuário</Table.Th>
                    <Table.Th>Email</Table.Th>
                    <Table.Th>Status</Table.Th>
                    <Table.Th>Resposta</Table.Th>
                    <Table.Th>Data da Resposta</Table.Th>
                  </Table.Tr>
                </Table.Thead>
                <Table.Tbody>
                  {results.responses.length === 0 ? (
                    <Table.Tr>
                      <Table.Td colSpan={5} ta="center">
                        Nenhum usuário atribuído.
                      </Table.Td>
                    </Table.Tr>
                  ) : (
                    results.responses.map((resp) => {
                      let formattedAnswer = resp.answer || '—';
                      if (resp.answer && resp.answer.startsWith('[') && resp.answer.endsWith(']')) {
                        try {
                          const parsed = JSON.parse(resp.answer);
                          if (Array.isArray(parsed)) {
                            formattedAnswer = parsed.join(', ');
                          }
                        } catch {
                          // keep raw
                        }
                      }

                      return (
                        <Table.Tr key={resp.assignmentId}>
                          <Table.Td fw={500}>{resp.userName}</Table.Td>
                          <Table.Td>{resp.userEmail}</Table.Td>
                          <Table.Td>{renderStatusBadge(resp.status)}</Table.Td>
                          <Table.Td>{formattedAnswer}</Table.Td>
                          <Table.Td>
                            {resp.answeredAt
                              ? new Date(resp.answeredAt).toLocaleString('pt-BR')
                              : '—'}
                          </Table.Td>
                        </Table.Tr>
                      );
                    })
                  )}
                </Table.Tbody>
              </Table>
            </Table.ScrollContainer>
          </div>
        </Stack>
      ) : (
        <Center p="xl">
          <Text c="dimmed">Nenhum detalhe disponível.</Text>
        </Center>
      )}
    </Modal>
  );
};
