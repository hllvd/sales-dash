import React, { useState, useEffect } from 'react';
import {
  Card,
  Title,
  Text,
  Group,
  Button,
  Badge,
  Table,
  Select,
  Loader,
  Center,
  Stack,
  ActionIcon,
  Tooltip
} from '@mantine/core';
import {
  IconPlus,
  IconMessageCircle,
  IconRefresh,
  IconFilter,
  IconEye
} from '@tabler/icons-react';
import { helpdeskApiService, HelpdeskQuestionDto } from '../../services/apiService';
import { HelpdeskModal } from './HelpdeskModal';

export const HelpdeskSection: React.FC = () => {
  const [currentUser, setCurrentUser] = useState<any>(null);
  const [questions, setQuestions] = useState<HelpdeskQuestionDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [statusFilter, setStatusFilter] = useState<string | null>(null);

  const [modalOpen, setModalOpen] = useState(false);
  const [selectedQuestionId, setSelectedQuestionId] = useState<string | null>(null);

  useEffect(() => {
    const userJson = localStorage.getItem('user');
    if (userJson) {
      try {
        const u = JSON.parse(userJson);
        setCurrentUser(u);
      } catch (e) {
        console.error(e);
      }
    }
  }, []);

  useEffect(() => {
    if (currentUser) {
      loadQuestions();
    }
  }, [currentUser, statusFilter]);

  const loadQuestions = async () => {
    setLoading(true);
    try {
      const res = await helpdeskApiService.listQuestions(statusFilter || undefined);
      if (res.success && res.data) {
        setQuestions(res.data);
      }
    } catch (e) {
      console.error(e);
    } finally {
      setLoading(false);
    }
  };

  const role = currentUser?.role?.toLowerCase() || '';
  const canAccess = role === 'admin' || role === 'superadmin';
  const isSuperAdmin = role === 'superadmin';

  if (!canAccess) {
    return null;
  }

  const getStatusBadge = (st: string) => {
    switch (st) {
      case 'Não lido':
        return <Badge color="red" variant="filled">Não lido</Badge>;
      case 'Lida':
        return <Badge color="blue" variant="filled">Lida</Badge>;
      case 'Pendente':
        return <Badge color="yellow" variant="filled">Pendente</Badge>;
      case 'Resolvido':
        return <Badge color="green" variant="filled">Resolvido</Badge>;
      default:
        return <Badge color="gray">{st}</Badge>;
    }
  };

  const handleOpenNew = () => {
    setSelectedQuestionId(null);
    setModalOpen(true);
  };

  const handleOpenQuestion = (id: string) => {
    setSelectedQuestionId(id);
    setModalOpen(true);
  };

  return (
    <Card shadow="sm" padding="lg" radius="md" withBorder mt="xl" style={{ backgroundColor: '#ffffff', borderColor: '#d0d7de' }}>
      <Group justify="space-between" mb="md" wrap="wrap">
        <div>
          <Title order={3} style={{ color: '#1f2328' }}>
            {isSuperAdmin ? 'Gestão de Dúvidas & FAQ (SuperAdmin)' : 'Minhas Dúvidas & FAQ Privado'}
          </Title>
          <Text size="sm" c="dimmed">
            {isSuperAdmin
              ? 'Todas as dúvidas enviadas pelos administradores para resposta e orientação.'
              : 'Faça perguntas sobre o sistema e acompanhe suas respostas diretamente com o SuperAdmin.'}
          </Text>
        </div>

        <Group gap="xs">
          <Select
            placeholder="Filtrar por Status"
            size="xs"
            clearable
            style={{ width: 150 }}
            value={statusFilter}
            onChange={setStatusFilter}
            data={['Não lido', 'Lida', 'Pendente', 'Resolvido']}
          />

          <ActionIcon variant="default" size="md" onClick={loadQuestions} title="Atualizar">
            <IconRefresh size={16} />
          </ActionIcon>

          {role === 'admin' && (
            <Button
              size="xs"
              color="blue"
              leftSection={<IconPlus size={16} />}
              onClick={handleOpenNew}
            >
              Fazer uma Pergunta
            </Button>
          )}
        </Group>
      </Group>

      {loading ? (
        <Center py="xl">
          <Loader size="sm" />
        </Center>
      ) : questions.length === 0 ? (
        <Center py="xl">
          <Stack align="center" gap="xs">
            <IconMessageCircle size={36} color="#94a3b8" />
            <Text size="sm" c="dimmed">
              {statusFilter
                ? 'Nenhuma pergunta encontrada com o filtro selecionado.'
                : 'Nenhuma pergunta registrada no momento.'}
            </Text>
          </Stack>
        </Center>
      ) : (
        <Table highlightOnHover verticalSpacing="sm">
          <Table.Thead>
            <Table.Tr>
              <Table.Th>Status</Table.Th>
              <Table.Th>Título</Table.Th>
              {isSuperAdmin && <Table.Th>Autor</Table.Th>}
              <Table.Th>Data</Table.Th>
              <Table.Th>Respostas</Table.Th>
              <Table.Th style={{ textAlign: 'right' }}>Ação</Table.Th>
            </Table.Tr>
          </Table.Thead>
          <Table.Tbody>
            {questions.map((q) => (
              <Table.Tr key={q.questionId} style={{ cursor: 'pointer' }} onClick={() => handleOpenQuestion(q.questionId)}>
                <Table.Td>{getStatusBadge(q.status)}</Table.Td>
                <Table.Td>
                  <Text fw={600} size="sm">{q.title}</Text>
                  {q.sourcePageUrl && (
                    <Text size="xs" c="dimmed">Origem: {q.sourcePageUrl}</Text>
                  )}
                </Table.Td>
                {isSuperAdmin && (
                  <Table.Td>
                    <Text size="xs">{q.userName}</Text>
                    <Text size="xs" c="dimmed">{q.userEmail}</Text>
                  </Table.Td>
                )}
                <Table.Td>
                  <Text size="xs">{new Date(q.updatedAt).toLocaleDateString('pt-BR')}</Text>
                </Table.Td>
                <Table.Td>
                  <Badge size="xs" variant="light" color="gray">
                    {q.messages?.length || 0}
                  </Badge>
                </Table.Td>
                <Table.Td style={{ textAlign: 'right' }}>
                  <Button
                    size="compact-xs"
                    variant="light"
                    color="blue"
                    leftSection={<IconEye size={12} />}
                    onClick={(e) => {
                      e.stopPropagation();
                      handleOpenQuestion(q.questionId);
                    }}
                  >
                    Ver Conversa
                  </Button>
                </Table.Td>
              </Table.Tr>
            ))}
          </Table.Tbody>
        </Table>
      )}

      <HelpdeskModal
        opened={modalOpen}
        onClose={() => setModalOpen(false)}
        questionId={selectedQuestionId}
        onSaved={loadQuestions}
      />
    </Card>
  );
};
