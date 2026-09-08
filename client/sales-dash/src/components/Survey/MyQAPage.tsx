import React, { useState, useEffect } from 'react';
import {
  Title,
  Text,
  Stack,
  Group,
  Card,
  Badge,
  Button,
  Loader,
  Center,
  Tabs,
} from '@mantine/core';
import { notifications } from '@mantine/notifications';
import {
  IconQuestionMark,
  IconCheck,
  IconClock,
  IconRefresh,
  IconCircleX,
} from '@tabler/icons-react';
import Menu from '../Menu';
import { apiService } from '../../services/apiService';
import { UserSurveyHistoryDto, SurveyAssignmentDto } from '../../types/Survey';
import { SurveyModal } from './SurveyModal';
import './MyQAPage.css';

export const MyQAPage: React.FC = () => {
  const [history, setHistory] = useState<UserSurveyHistoryDto[]>([]);
  const [loading, setLoading] = useState<boolean>(true);
  const [activeTab, setActiveTab] = useState<string | null>('all');
  const [selectedForAnswering, setSelectedForAnswering] = useState<SurveyAssignmentDto | null>(null);

  const loadHistory = async () => {
    setLoading(true);
    try {
      const res = await apiService.getMySurveyHistory();
      if (res.success && res.data) {
        setHistory(res.data);
      } else {
        notifications.show({
          title: 'Erro',
          message: res.message || 'Falha ao buscar histórico de perguntas.',
          color: 'red',
        });
      }
    } catch (err: any) {
      notifications.show({
        title: 'Erro',
        message: err.message || 'Erro ao carregar histórico.',
        color: 'red',
      });
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadHistory();

    const handleUpdate = () => {
      loadHistory();
    };

    window.addEventListener('survey:updated', handleUpdate);
    return () => {
      window.removeEventListener('survey:updated', handleUpdate);
    };
  }, []);

  const pendingList = history.filter((h) => h.status === 'pending');
  const answeredList = history.filter((h) => h.status === 'answered');
  const expiredList = history.filter((h) => h.status === 'expired');

  const filteredHistory = () => {
    if (activeTab === 'pending') return pendingList;
    if (activeTab === 'answered') return answeredList;
    if (activeTab === 'expired') return expiredList;
    return history;
  };

  const handleOpenAnswerModal = async (item: UserSurveyHistoryDto) => {
    try {
      const pendingRes = await apiService.getPendingSurveys();
      if (pendingRes.success && pendingRes.data) {
        const target = pendingRes.data.find((p) => p.assignmentId === item.assignmentId);
        if (target) {
          setSelectedForAnswering(target);
          return;
        }
      }
    } catch (err) {
      console.error(err);
    }

    // Fallback assignment dto
    setSelectedForAnswering({
      assignmentId: item.assignmentId,
      surveyId: item.surveyId,
      title: item.title,
      questionText: item.questionText,
      questionType: item.questionType,
      sentAt: item.sentAt,
      expiresAt: item.expiresAt,
    });
  };

  const renderStatusBadge = (status: string) => {
    switch (status) {
      case 'answered':
        return (
          <Badge color="green" leftSection={<IconCheck size={12} />}>
            Respondida
          </Badge>
        );
      case 'pending':
        return (
          <Badge color="yellow" leftSection={<IconClock size={12} />}>
            Pendente
          </Badge>
        );
      case 'expired':
        return (
          <Badge color="gray" leftSection={<IconCircleX size={12} />}>
            Expirada
          </Badge>
        );
      default:
        return <Badge color="gray">{status}</Badge>;
    }
  };

  return (
    <Menu>
      <div className="my-qa-container">
        <Group justify="space-between" mb="lg">
          <div>
            <Title order={2}>Meu Histórico de Perguntas / QA</Title>
            <Text c="dimmed" size="sm">
              Consulte todas as perguntas direcionadas a você, responda às pendentes e veja suas respostas anteriores.
            </Text>
          </div>
          <Button
            leftSection={<IconRefresh size={16} />}
            variant="subtle"
            color="gray"
            size="sm"
            onClick={loadHistory}
            loading={loading}
          >
            Atualizar
          </Button>
        </Group>

        <Tabs value={activeTab} onChange={setActiveTab} mb="lg">
          <Tabs.List>
            <Tabs.Tab value="all">Todas ({history.length})</Tabs.Tab>
            <Tabs.Tab value="pending" color="yellow">
              Pendentes ({pendingList.length})
            </Tabs.Tab>
            <Tabs.Tab value="answered" color="green">
              Respondidas ({answeredList.length})
            </Tabs.Tab>
            <Tabs.Tab value="expired" color="gray">
              Expiradas ({expiredList.length})
            </Tabs.Tab>
          </Tabs.List>
        </Tabs>

        {loading ? (
          <Center p="xl">
            <Loader color="red" />
          </Center>
        ) : filteredHistory().length === 0 ? (
          <Card withBorder padding="xl" ta="center">
            <Text c="dimmed">Nenhuma pergunta encontrada nesta categoria.</Text>
          </Card>
        ) : (
          <Stack gap="md">
            {filteredHistory().map((item) => {
              let parsedAnswer = item.answer;
              if (item.answer && item.answer.startsWith('[') && item.answer.endsWith(']')) {
                try {
                  const arr = JSON.parse(item.answer);
                  if (Array.isArray(arr)) {
                    parsedAnswer = arr.join(', ');
                  }
                } catch {}
              }

              const cardClass =
                item.status === 'answered'
                  ? 'qa-card-answered'
                  : item.status === 'pending'
                  ? 'qa-card-pending'
                  : 'qa-card-expired';

              return (
                <Card
                  key={item.assignmentId}
                  withBorder
                  padding="md"
                  radius="md"
                  className={cardClass}
                >
                  <Group justify="space-between" align="flex-start" mb="xs">
                    <div>
                      <Text fw={700} size="md">
                        {item.title}
                      </Text>
                      <Text size="xs" c="dimmed">
                        Enviada em: {new Date(item.sentAt).toLocaleString('pt-BR')} | Expira em:{' '}
                        {new Date(item.expiresAt).toLocaleString('pt-BR')}
                      </Text>
                    </div>
                    {renderStatusBadge(item.status)}
                  </Group>

                  <Text size="sm" mb="sm" style={{ whiteSpace: 'pre-wrap' }}>
                    {item.questionText}
                  </Text>

                  {item.status === 'answered' && (
                    <Card withBorder padding="xs" radius="sm" bg="#f0fdf4" mt="xs">
                      <Group justify="space-between">
                        <div>
                          <Text size="xs" fw={600} c="green.8">
                            Sua resposta:
                          </Text>
                          <Text size="sm" fw={700} c="green.9">
                            {parsedAnswer}
                          </Text>
                        </div>
                        {item.answeredAt && (
                          <Text size="xs" c="dimmed">
                            Respondida em: {new Date(item.answeredAt).toLocaleString('pt-BR')}
                          </Text>
                        )}
                      </Group>
                    </Card>
                  )}

                  {item.status === 'pending' && (
                    <Group justify="flex-end" mt="xs">
                      <Button
                        color="red"
                        size="xs"
                        leftSection={<IconQuestionMark size={14} />}
                        onClick={() => handleOpenAnswerModal(item)}
                      >
                        Responder agora
                      </Button>
                    </Group>
                  )}
                </Card>
              );
            })}
          </Stack>
        )}

        {/* Modal explicitly opened for answering from history */}
        <SurveyModal
          explicitAssignment={selectedForAnswering}
          onClose={() => {
            setSelectedForAnswering(null);
            loadHistory();
          }}
        />
      </div>
    </Menu>
  );
};
export default MyQAPage;
