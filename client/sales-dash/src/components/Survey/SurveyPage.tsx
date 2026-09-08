import React, { useState, useEffect, useMemo } from 'react';
import {
  Title,
  Tabs,
  Card,
  Stack,
  Group,
  TextInput,
  Textarea,
  SegmentedControl,
  Button,
  Table,
  Checkbox,
  Select,
  Badge,
  Text,
  ActionIcon,
  Alert,
  Loader,
  Center,
} from '@mantine/core';
import { notifications } from '@mantine/notifications';
import {
  IconPlus,
  IconTrash,
  IconRefresh,
  IconSend,
  IconAlertCircle,
  IconCheck,
  IconX,
  IconQuestionMark,
} from '@tabler/icons-react';
import Menu from '../Menu';
import { apiService, User } from '../../services/apiService';
import {
  SurveyQuestionType,
  SurveySummaryDto,
  CreateSurveyDto,
} from '../../types/Survey';
import { SurveyResultModal } from './SurveyResultModal';
import './SurveyPage.css';

export const SurveyPage: React.FC = () => {
  const [activeTab, setActiveTab] = useState<string | null>('create');
  const [isSuperAdmin, setIsSuperAdmin] = useState<boolean>(false);

  // Form states
  const [title, setTitle] = useState<string>('');
  const [questionText, setQuestionText] = useState<string>('');
  const [questionType, setQuestionType] = useState<SurveyQuestionType>('yesno');
  const [options, setOptions] = useState<string[]>(['Opção 1', 'Opção 2']);
  const [newOptionText, setNewOptionText] = useState<string>('');

  // Target users & filters
  const [allUsers, setAllUsers] = useState<User[]>([]);
  const [allTeams, setAllTeams] = useState<{ value: string; label: string }[]>([]);
  const [loadingUsers, setLoadingUsers] = useState<boolean>(false);
  const [filterRole, setFilterRole] = useState<string | null>(null);
  const [filterEmail, setFilterEmail] = useState<string>('');
  const [filterName, setFilterName] = useState<string>('');
  const [filterTeam, setFilterTeam] = useState<string | null>(null);
  const [selectedUserIds, setSelectedUserIds] = useState<Set<string>>(new Set());

  // Submitting
  const [submitting, setSubmitting] = useState<boolean>(false);

  // Surveys list
  const [surveys, setSurveys] = useState<SurveySummaryDto[]>([]);
  const [loadingSurveys, setLoadingSurveys] = useState<boolean>(false);
  const [selectedSurveyId, setSelectedSurveyId] = useState<string | null>(null);

  // Check superadmin permissions
  useEffect(() => {
    const userJson = localStorage.getItem('user');
    const token = localStorage.getItem('token');
    let hasAdminAccess = false;

    if (userJson) {
      try {
        const u = JSON.parse(userJson);
        if (u.role === 'superadmin') {
          hasAdminAccess = true;
        }
      } catch {}
    }

    if (token && !hasAdminAccess) {
      try {
        const payload = token.split('.')[1];
        const base64 = payload.replace(/-/g, '+').replace(/_/g, '/');
        const decoded = JSON.parse(decodeURIComponent(escape(atob(base64))));
        const perms = Array.isArray(decoded.perm) ? decoded.perm : [decoded.perm];
        if (perms.includes('system:superadmin')) {
          hasAdminAccess = true;
        }
      } catch {}
    }

    setIsSuperAdmin(hasAdminAccess);
  }, []);

  // Fetch users & teams on mount
  useEffect(() => {
    if (!isSuperAdmin) return;

    const loadInitialData = async () => {
      setLoadingUsers(true);
      try {
        const [usersRes, teamsRes] = await Promise.all([
          apiService.getUsers(1, 1000, undefined, undefined, false, true, 'active'),
          apiService.getTeams(),
        ]);

        if (usersRes.success && usersRes.data) {
          const items = usersRes.data.items || [];
          setAllUsers(items.filter((u) => u.isActive));
        }

        if (teamsRes.success && teamsRes.data) {
          setAllTeams(
            teamsRes.data.map((t) => ({
              value: t.name,
              label: t.name,
            }))
          );
        }
      } catch (err) {
        console.error('Error loading users/teams:', err);
      } finally {
        setLoadingUsers(false);
      }
    };

    loadInitialData();
  }, [isSuperAdmin]);

  // Fetch existing surveys when tab changes to "list"
  const loadSurveys = async () => {
    setLoadingSurveys(true);
    try {
      const res = await apiService.getSurveys();
      if (res.success && res.data) {
        setSurveys(res.data);
      }
    } catch (err: any) {
      notifications.show({
        title: 'Erro',
        message: err.message || 'Falha ao buscar perguntas.',
        color: 'red',
      });
    } finally {
      setLoadingSurveys(false);
    }
  };

  useEffect(() => {
    if (activeTab === 'list' && isSuperAdmin) {
      loadSurveys();
    }
  }, [activeTab, isSuperAdmin]);

  // Filtered users calculation
  const filteredUsers = useMemo(() => {
    return allUsers.filter((u) => {
      if (filterRole && u.role.toLowerCase() !== filterRole.toLowerCase()) {
        return false;
      }
      if (filterEmail && !u.email.toLowerCase().includes(filterEmail.toLowerCase())) {
        return false;
      }
      if (filterName && !u.name.toLowerCase().includes(filterName.toLowerCase())) {
        return false;
      }
      if (filterTeam && u.currentTeamName !== filterTeam) {
        return false;
      }
      return true;
    });
  }, [allUsers, filterRole, filterEmail, filterName, filterTeam]);

  // Selection handlers
  const handleSelectAllFiltered = () => {
    const next = new Set(selectedUserIds);
    filteredUsers.forEach((u) => next.add(u.id));
    setSelectedUserIds(next);
  };

  const handleClearSelection = () => {
    setSelectedUserIds(new Set());
  };

  const toggleUserSelection = (userId: string) => {
    const next = new Set(selectedUserIds);
    if (next.has(userId)) {
      next.delete(userId);
    } else {
      next.add(userId);
    }
    setSelectedUserIds(next);
  };

  // Options handlers for choices
  const handleAddOption = () => {
    const trimmed = newOptionText.trim();
    if (!trimmed) return;
    if (options.includes(trimmed)) {
      notifications.show({
        title: 'Atenção',
        message: 'Esta opção já existe.',
        color: 'orange',
      });
      return;
    }
    setOptions([...options, trimmed]);
    setNewOptionText('');
  };

  const handleRemoveOption = (index: number) => {
    if (options.length <= 2) {
      notifications.show({
        title: 'Atenção',
        message: 'Mínimo de 2 opções necessárias.',
        color: 'orange',
      });
      return;
    }
    setOptions(options.filter((_, i) => i !== index));
  };

  // Submit create survey
  const handleCreateSurvey = async () => {
    if (!title.trim()) {
      notifications.show({
        title: 'Atenção',
        message: 'Informe o título da pergunta.',
        color: 'orange',
      });
      return;
    }

    if (!questionText.trim()) {
      notifications.show({
        title: 'Atenção',
        message: 'Informe o texto da pergunta.',
        color: 'orange',
      });
      return;
    }

    if (selectedUserIds.size === 0) {
      notifications.show({
        title: 'Atenção',
        message: 'Selecione pelo menos um usuário destinatário.',
        color: 'orange',
      });
      return;
    }

    if ((questionType === 'singlechoice' || questionType === 'multichoice') && options.length < 2) {
      notifications.show({
        title: 'Atenção',
        message: 'Perguntas de escolha exigem ao menos duas opções.',
        color: 'orange',
      });
      return;
    }

    const payload: CreateSurveyDto = {
      title: title.trim(),
      questionText: questionText.trim(),
      questionType,
      options: questionType === 'yesno' ? undefined : options,
      targetUserIds: Array.from(selectedUserIds),
    };

    try {
      setSubmitting(true);
      const res = await apiService.createSurvey(payload);
      if (res.success) {
        notifications.show({
          title: 'Sucesso',
          message: 'Pergunta enviada com sucesso para os usuários selecionados!',
          color: 'green',
          icon: <IconCheck size={16} />,
        });

        // Reset form
        setTitle('');
        setQuestionText('');
        setQuestionType('yesno');
        setOptions(['Opção 1', 'Opção 2']);
        setSelectedUserIds(new Set());
        setActiveTab('list');
      } else {
        notifications.show({
          title: 'Erro',
          message: res.message || 'Falha ao criar pergunta.',
          color: 'red',
          icon: <IconX size={16} />,
        });
      }
    } catch (err: any) {
      notifications.show({
        title: 'Erro',
        message: err.message || 'Erro inesperado.',
        color: 'red',
        icon: <IconX size={16} />,
      });
    } finally {
      setSubmitting(false);
    }
  };

  if (!isSuperAdmin) {
    return (
      <Menu>
        <div className="survey-page-container">
          <Alert icon={<IconAlertCircle size={20} />} title="Acesso Restrito" color="red">
            Apenas superadministradores possuem permissão para gerenciar e criar perguntas de pesquisa.
          </Alert>
        </div>
      </Menu>
    );
  }

  return (
    <Menu>
      <div className="survey-page-container">
        <Group justify="space-between" mb="lg">
          <div>
            <Title order={2}>Gerenciamento de Perguntas / QA</Title>
            <Text c="dimmed" size="sm">
              Crie perguntas pontuais para usuários selecionados ou acompanhe os resultados em tempo real.
            </Text>
          </div>
        </Group>

        <Tabs value={activeTab} onChange={setActiveTab}>
          <Tabs.List mb="md">
            <Tabs.Tab value="create" leftSection={<IconPlus size={16} />}>
              Criar Pergunta
            </Tabs.Tab>
            <Tabs.Tab value="list" leftSection={<IconQuestionMark size={16} />}>
              Perguntas Enviadas ({surveys.length})
            </Tabs.Tab>
          </Tabs.List>

          {/* TAB 1: CRIAR PERGUNTA */}
          <Tabs.Panel value="create">
            <Stack gap="lg">
              <Card withBorder padding="md" radius="md">
                <Text fw={600} size="md" mb="sm">
                  1. Detalhes da Pergunta
                </Text>
                <Stack gap="md">
                  <TextInput
                    label="Título da Pergunta"
                    placeholder="Ex: Confirmação de Matrícula Ativa"
                    required
                    value={title}
                    onChange={(e) => setTitle(e.currentTarget.value)}
                  />

                  <Textarea
                    label="Texto / Pergunta"
                    placeholder="Ex: Você já realizou o procedimento de validação das matrículas este mês?"
                    required
                    minRows={3}
                    value={questionText}
                    onChange={(e) => setQuestionText(e.currentTarget.value)}
                  />

                  <div>
                    <Text size="sm" fw={500} mb={4}>
                      Tipo de Pergunta
                    </Text>
                    <SegmentedControl
                      value={questionType}
                      onChange={(val) => setQuestionType(val as SurveyQuestionType)}
                      data={[
                        { label: 'Sim / Não / Não tenho certeza', value: 'yesno' },
                        { label: 'Escolha Única', value: 'singlechoice' },
                        { label: 'Múltipla Escolha', value: 'multichoice' },
                      ]}
                      fullWidth
                    />
                  </div>

                  {(questionType === 'singlechoice' || questionType === 'multichoice') && (
                    <Card withBorder padding="sm" radius="md" bg="#f9fafb">
                      <Text size="sm" fw={500} mb="xs">
                        Opções da Pergunta
                      </Text>
                      <Stack gap="xs" mb="sm">
                        {options.map((opt, idx) => (
                          <Group key={idx} justify="space-between">
                            <Text size="sm">• {opt}</Text>
                            <ActionIcon
                              color="red"
                              variant="subtle"
                              size="sm"
                              onClick={() => handleRemoveOption(idx)}
                              disabled={options.length <= 2}
                            >
                              <IconTrash size={14} />
                            </ActionIcon>
                          </Group>
                        ))}
                      </Stack>
                      <Group>
                        <TextInput
                          placeholder="Adicionar nova opção..."
                          size="xs"
                          style={{ flex: 1 }}
                          value={newOptionText}
                          onChange={(e) => setNewOptionText(e.currentTarget.value)}
                          onKeyDown={(e) => {
                            if (e.key === 'Enter') {
                              e.preventDefault();
                              handleAddOption();
                            }
                          }}
                        />
                        <Button size="xs" variant="light" color="red" onClick={handleAddOption}>
                          Adicionar
                        </Button>
                      </Group>
                    </Card>
                  )}
                </Stack>
              </Card>

              {/* User Selection Card */}
              <Card withBorder padding="md" radius="md">
                <Text fw={600} size="md" mb="xs">
                  2. Selecionar Destinatários
                </Text>

                {/* Filters */}
                <div className="survey-filter-card">
                  <Group grow align="flex-end" mb="xs">
                    <Select
                      label="Filtrar por Papel / Role"
                      placeholder="Todos os papéis"
                      clearable
                      data={[
                        { value: 'user', label: 'Usuário (User)' },
                        { value: 'admin', label: 'Administrador' },
                        { value: 'superadmin', label: 'Superadministrador' },
                      ]}
                      value={filterRole}
                      onChange={setFilterRole}
                    />

                    <Select
                      label="Filtrar por Equipe"
                      placeholder="Todas as equipes"
                      clearable
                      searchable
                      data={allTeams}
                      value={filterTeam}
                      onChange={setFilterTeam}
                    />

                    <TextInput
                      label="Filtrar por Nome"
                      placeholder="Buscar por nome..."
                      value={filterName}
                      onChange={(e) => setFilterName(e.currentTarget.value)}
                    />

                    <TextInput
                      label="Filtrar por Email"
                      placeholder="Buscar por email..."
                      value={filterEmail}
                      onChange={(e) => setFilterEmail(e.currentTarget.value)}
                    />
                  </Group>
                </div>

                {/* Selection Toolbar */}
                <div className="survey-selection-toolbar">
                  <Group gap="xs">
                    <Button size="xs" variant="outline" color="gray" onClick={handleSelectAllFiltered}>
                      Selecionar Todos os Filtrados ({filteredUsers.length})
                    </Button>
                    <Button size="xs" variant="subtle" color="gray" onClick={handleClearSelection}>
                      Desmarcar Todos
                    </Button>
                  </Group>
                  <Badge size="lg" color={selectedUserIds.size > 0 ? 'red' : 'gray'}>
                    {selectedUserIds.size} usuário(s) selecionado(s)
                  </Badge>
                </div>

                {/* Users Table */}
                <div className="survey-user-table-wrapper">
                  {loadingUsers ? (
                    <Center p="xl">
                      <Loader color="red" />
                    </Center>
                  ) : (
                    <Table striped highlightOnHover withColumnBorders>
                      <Table.Thead>
                        <Table.Tr>
                          <Table.Th style={{ width: 40 }}></Table.Th>
                          <Table.Th>Nome</Table.Th>
                          <Table.Th>Email</Table.Th>
                          <Table.Th>Papel</Table.Th>
                          <Table.Th>Equipe</Table.Th>
                        </Table.Tr>
                      </Table.Thead>
                      <Table.Tbody>
                        {filteredUsers.length === 0 ? (
                          <Table.Tr>
                            <Table.Td colSpan={5} ta="center">
                              Nenhum usuário encontrado com os filtros aplicados.
                            </Table.Td>
                          </Table.Tr>
                        ) : (
                          filteredUsers.map((u) => {
                            const isChecked = selectedUserIds.has(u.id);
                            return (
                              <Table.Tr
                                key={u.id}
                                className="survey-user-row"
                                onClick={() => toggleUserSelection(u.id)}
                                style={{ cursor: 'pointer' }}
                              >
                                <Table.Td onClick={(e) => e.stopPropagation()}>
                                  <Checkbox
                                    checked={isChecked}
                                    onChange={() => toggleUserSelection(u.id)}
                                  />
                                </Table.Td>
                                <Table.Td fw={500}>{u.name}</Table.Td>
                                <Table.Td>{u.email}</Table.Td>
                                <Table.Td>
                                  <Badge size="xs" variant="outline">
                                    {u.role}
                                  </Badge>
                                </Table.Td>
                                <Table.Td>{u.currentTeamName || '—'}</Table.Td>
                              </Table.Tr>
                            );
                          })
                        )}
                      </Table.Tbody>
                    </Table>
                  )}
                </div>
              </Card>

              {/* Action Button */}
              <Group justify="flex-end">
                <Button
                  color="red"
                  size="md"
                  leftSection={<IconSend size={18} />}
                  loading={submitting}
                  onClick={handleCreateSurvey}
                  disabled={selectedUserIds.size === 0 || !title.trim() || !questionText.trim()}
                >
                  Distribuir Pergunta ({selectedUserIds.size} destinatários)
                </Button>
              </Group>
            </Stack>
          </Tabs.Panel>

          {/* TAB 2: PERGUNTAS ENVIADAS */}
          <Tabs.Panel value="list">
            <Stack gap="md">
              <Group justify="space-between">
                <Text fw={600} size="md">
                  Histórico de Perguntas Criadas
                </Text>
                <Button
                  leftSection={<IconRefresh size={16} />}
                  variant="subtle"
                  color="gray"
                  size="xs"
                  onClick={loadSurveys}
                  loading={loadingSurveys}
                >
                  Atualizar
                </Button>
              </Group>

              {loadingSurveys ? (
                <Center p="xl">
                  <Loader color="red" />
                </Center>
              ) : surveys.length === 0 ? (
                <Card withBorder padding="xl" ta="center">
                  <Text c="dimmed">Nenhuma pergunta foi criada ainda.</Text>
                </Card>
              ) : (
                <Table striped highlightOnHover withTableBorder withColumnBorders>
                  <Table.Thead>
                    <Table.Tr>
                      <Table.Th>Título</Table.Th>
                      <Table.Th>Tipo</Table.Th>
                      <Table.Th>Data de Criação</Table.Th>
                      <Table.Th ta="center">Atribuídos</Table.Th>
                      <Table.Th ta="center">Respondidos</Table.Th>
                      <Table.Th ta="center">Pendentes</Table.Th>
                      <Table.Th ta="center">Expirados</Table.Th>
                    </Table.Tr>
                  </Table.Thead>
                  <Table.Tbody>
                    {surveys.map((s) => (
                      <Table.Tr
                        key={s.id}
                        className="survey-table-row-clickable"
                        onClick={() => setSelectedSurveyId(s.id)}
                      >
                        <Table.Td fw={600}>{s.title}</Table.Td>
                        <Table.Td>
                          <Badge size="xs" variant="light" color="blue">
                            {s.questionType === 'yesno'
                              ? 'Sim/Não'
                              : s.questionType === 'singlechoice'
                              ? 'Escolha Única'
                              : 'Múltipla Escolha'}
                          </Badge>
                        </Table.Td>
                        <Table.Td>{new Date(s.createdAt).toLocaleString('pt-BR')}</Table.Td>
                        <Table.Td ta="center">
                          <Badge size="sm" color="gray">
                            {s.totalAssigned}
                          </Badge>
                        </Table.Td>
                        <Table.Td ta="center">
                          <Badge size="sm" color="green">
                            {s.totalAnswered}
                          </Badge>
                        </Table.Td>
                        <Table.Td ta="center">
                          <Badge size="sm" color="yellow">
                            {s.totalPending}
                          </Badge>
                        </Table.Td>
                        <Table.Td ta="center">
                          <Badge size="sm" color="gray">
                            {s.totalExpired}
                          </Badge>
                        </Table.Td>
                      </Table.Tr>
                    ))}
                  </Table.Tbody>
                </Table>
              )}
            </Stack>
          </Tabs.Panel>
        </Tabs>

        {/* Detail / Results Modal */}
        <SurveyResultModal
          surveyId={selectedSurveyId}
          onClose={() => {
            setSelectedSurveyId(null);
            if (activeTab === 'list') {
              loadSurveys();
            }
          }}
        />
      </div>
    </Menu>
  );
};
export default SurveyPage;
