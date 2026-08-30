import React, { useState, useEffect, useCallback, useMemo, useRef } from 'react';
import {
  Title, Text, Group, Badge, Button, TextInput,
  Loader, Stack, Divider, Modal, Alert, SegmentedControl, Card
} from '@mantine/core';
import { notifications } from '@mantine/notifications';
import {
  IconSearch, IconCalendar, IconUsers, IconRefresh,
  IconArrowRight, IconAlertTriangle, IconCheck, IconClock, IconBuildingCommunity
} from '@tabler/icons-react';
import Menu from './Menu';
import {
  apiService, TeamCalendarUser, UserTeamHistoryEntry,
  CalendarContractPreviewResponse, AdjustTeamBoundaryRequest
} from '../services/apiService';
import { normalizeName } from '../utils/normalization';
import './TeamCalendarPage.css';

const TEAM_COLORS = [
  '#3b82f6', // blue
  '#10b981', // green
  '#f59e0b', // amber
  '#8b5cf6', // purple
  '#ec4899', // pink
  '#06b6d4', // cyan
  '#f97316', // orange
  '#6366f1', // indigo
  '#14b8a6', // teal
  '#84cc16', // lime
];

function getTeamColor(teamId: number): string {
  return TEAM_COLORS[Math.abs(teamId) % TEAM_COLORS.length];
}

function formatDateBR(dateStr?: string | null): string {
  if (!dateStr) return 'Atual';
  try {
    const d = new Date(dateStr);
    if (isNaN(d.getTime())) return dateStr;
    return d.toLocaleDateString('pt-BR', { timeZone: 'UTC' });
  } catch {
    return dateStr;
  }
}

function formatDateISO(date: Date): string {
  return date.toISOString().split('T')[0];
}

function parseUTCDate(dateStr: string): Date {
  const [year, month, day] = dateStr.split('T')[0].split('-').map(Number);
  return new Date(Date.UTC(year, month - 1, day, 12, 0, 0));
}

function getDurationInDays(startStr: string, endStr?: string | null): number {
  const start = parseUTCDate(startStr).getTime();
  const end = endStr ? parseUTCDate(endStr).getTime() : new Date().getTime();
  return Math.max(1, Math.round((end - start) / (1000 * 60 * 60 * 24)));
}

function formatDuration(days: number): string {
  if (days < 30) return `${days} dia${days !== 1 ? 's' : ''}`;
  const months = Math.floor(days / 30);
  const remDays = days % 30;
  if (months < 12) {
    return `${months} m${months > 1 ? 'eses' : 'ês'}${remDays > 0 ? ` e ${remDays}d` : ''}`;
  }
  const years = Math.floor(days / 365);
  const remMonths = Math.floor((days % 365) / 30);
  return `${years} ano${years > 1 ? 's' : ''}${remMonths > 0 ? ` e ${remMonths}m` : ''}`;
}

const TeamCalendarPage: React.FC = () => {
  const [users, setUsers] = useState<TeamCalendarUser[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [search, setSearch] = useState('');
  const [levelFilter, setLevelFilter] = useState('all');
  const [selectedUserId, setSelectedUserId] = useState<string | null>(null);

  // Drag and drop state
  const timelineTrackRef = useRef<HTMLDivElement | null>(null);
  const [draggingBoundary, setDraggingBoundary] = useState<{
    olderIndex: number;
    newerIndex: number;
    initialDate: Date;
    currentDate: Date;
    minDate: Date;
    maxDate: Date;
  } | null>(null);

  // Confirmation Modal state
  const [modalOpen, setModalOpen] = useState(false);
  const [transitionUser, setTransitionUser] = useState<TeamCalendarUser | null>(null);
  const [olderTeam, setOlderTeam] = useState<UserTeamHistoryEntry | null>(null);
  const [newerTeam, setNewerTeam] = useState<UserTeamHistoryEntry | null>(null);
  const [boundaryDateInput, setBoundaryDateInput] = useState('');
  const [previewLoading, setPreviewLoading] = useState(false);
  const [previewData, setPreviewData] = useState<CalendarContractPreviewResponse | null>(null);
  const [savingTransition, setSavingTransition] = useState(false);

  // Fetch calendar users
  const fetchCalendar = useCallback(async () => {
    setLoading(true);
    setError('');
    try {
      const res = await apiService.getTeamCalendar();
      if (res.success && res.data) {
        setUsers(res.data);
        if (res.data.length > 0 && !selectedUserId) {
          setSelectedUserId(res.data[0].userId);
        }
      }
    } catch (err: any) {
      setError(err.message || 'Falha ao carregar calendário de equipes');
    } finally {
      setLoading(false);
    }
  }, [selectedUserId]);

  useEffect(() => {
    fetchCalendar();
  }, [fetchCalendar]);

  // Filtered users
  const filteredUsers = useMemo(() => {
    return users.filter(u => {
      const matchesSearch =
        u.userName.toLowerCase().includes(search.toLowerCase()) ||
        u.userEmail.toLowerCase().includes(search.toLowerCase()) ||
        (u.currentTeamName && u.currentTeamName.toLowerCase().includes(search.toLowerCase()));

      const matchesLevel =
        levelFilter === 'all' ||
        (levelFilter === '1' && u.hierarchyLevel === 1) ||
        (levelFilter === '2' && u.hierarchyLevel === 2) ||
        (levelFilter === '3' && u.hierarchyLevel === 3);

      return matchesSearch && matchesLevel;
    });
  }, [users, search, levelFilter]);

  // Selected user
  const selectedUser = useMemo(() => {
    return users.find(u => u.userId === selectedUserId) || null;
  }, [users, selectedUserId]);

  // Fetch contract preview for modal
  const fetchPreview = useCallback(async (userId: string, dateStr: string) => {
    setPreviewLoading(true);
    try {
      const res = await apiService.getContractPreview(userId, dateStr);
      if (res.success && res.data) {
        setPreviewData(res.data);
      }
    } catch (err: any) {
      console.error('Failed to load contract preview', err);
      notifications.show({
        title: 'Aviso',
        message: 'Não foi possível carregar o preview dos contratos.',
        color: 'yellow',
      });
    } finally {
      setPreviewLoading(false);
    }
  }, []);

  // Open boundary adjustment modal
  const openAdjustmentModal = useCallback((
    user: TeamCalendarUser,
    older: UserTeamHistoryEntry | null,
    newer: UserTeamHistoryEntry | null,
    targetDate: Date
  ) => {
    setTransitionUser(user);
    setOlderTeam(older);
    setNewerTeam(newer);
    const isoDate = formatDateISO(targetDate);
    setBoundaryDateInput(isoDate);
    setModalOpen(true);
    fetchPreview(user.userId, isoDate);
  }, [fetchPreview]);

  // Handle Date Input change in modal
  const handleBoundaryDateChange = (newDateStr: string) => {
    setBoundaryDateInput(newDateStr);
    if (transitionUser && newDateStr) {
      fetchPreview(transitionUser.userId, newDateStr);
    }
  };

  // Save boundary transition
  const handleSaveTransition = async () => {
    if (!transitionUser || !boundaryDateInput) return;

    // Validate 1-week rule (7 days)
    const boundaryTime = parseUTCDate(boundaryDateInput).getTime();

    if (olderTeam) {
      const olderStart = parseUTCDate(olderTeam.startDate).getTime();
      const olderDays = (boundaryTime - olderStart) / (1000 * 60 * 60 * 24);
      if (olderDays < 7) {
        notifications.show({
          title: 'Data Inválida',
          message: 'O período da equipe anterior deve ter duração mínima de 1 semana (7 dias).',
          color: 'red',
        });
        return;
      }
    }

    if (newerTeam && newerTeam.endDate) {
      const newerEnd = parseUTCDate(newerTeam.endDate).getTime();
      const newerDays = (newerEnd - boundaryTime) / (1000 * 60 * 60 * 24);
      if (newerDays < 7) {
        notifications.show({
          title: 'Data Inválida',
          message: 'O período da nova equipe deve ter duração mínima de 1 semana (7 dias).',
          color: 'red',
        });
        return;
      }
    }

    setSavingTransition(true);
    try {
      const req: AdjustTeamBoundaryRequest = {
        userId: transitionUser.userId,
        olderTeamId: olderTeam ? olderTeam.teamId : undefined,
        newerTeamId: newerTeam ? newerTeam.teamId : undefined,
        boundaryDate: `${boundaryDateInput}T12:00:00Z`,
      };

      const res = await apiService.adjustTeamBoundary(req);
      if (res.success && res.data) {
        notifications.show({
          title: 'Sucesso',
          message: 'Datas da equipe atualizadas com sucesso!',
          color: 'green',
        });
        setModalOpen(false);
        // Refresh local data
        setUsers(prev =>
          prev.map(u => (u.userId === transitionUser.userId ? res.data! : u))
        );
      }
    } catch (err: any) {
      notifications.show({
        title: 'Erro ao salvar',
        message: err.message || 'Falha ao atualizar datas de equipe.',
        color: 'red',
      });
    } finally {
      setSavingTransition(false);
    }
  };

  // Timeline scale calculations
  const timelineScale = useMemo(() => {
    if (!selectedUser || selectedUser.teamHistory.length === 0) return null;

    const startTimes = selectedUser.teamHistory.map(h => parseUTCDate(h.startDate).getTime());
    const endTimes = selectedUser.teamHistory.map(h =>
      h.endDate ? parseUTCDate(h.endDate).getTime() : new Date().getTime()
    );

    const minTime = Math.min(...startTimes);
    const maxTime = Math.max(...endTimes, new Date().getTime());
    const totalDuration = maxTime - minTime || 1;

    return { minTime, maxTime, totalDuration };
  }, [selectedUser]);

  // Handle Drag Move & Release on Timeline
  const handleBoundaryMouseDown = (
    e: React.MouseEvent,
    olderIndex: number,
    newerIndex: number
  ) => {
    e.preventDefault();
    if (!selectedUser || !timelineScale) return;

    const older = selectedUser.teamHistory[olderIndex];
    const newer = selectedUser.teamHistory[newerIndex];

    const olderStart = parseUTCDate(older.startDate);
    const newerEnd = newer.endDate ? parseUTCDate(newer.endDate) : new Date();

    // Min date: older.startDate + 7 days
    const minDate = new Date(olderStart.getTime() + 7 * 24 * 60 * 60 * 1000);
    // Max date: newer.endDate - 7 days (or today)
    const maxDate = newer.endDate
      ? new Date(newerEnd.getTime() - 7 * 24 * 60 * 60 * 1000)
      : new Date();

    const initialDate = parseUTCDate(older.endDate || newer.startDate);

    setDraggingBoundary({
      olderIndex,
      newerIndex,
      initialDate,
      currentDate: initialDate,
      minDate,
      maxDate,
    });
  };

  useEffect(() => {
    if (!draggingBoundary) return;

    const handleMouseMove = (e: MouseEvent) => {
      if (!timelineTrackRef.current || !timelineScale) return;
      const rect = timelineTrackRef.current.getBoundingClientRect();
      const ratio = Math.max(0, Math.min(1, (e.clientX - rect.left) / rect.width));
      const targetTime = timelineScale.minTime + ratio * timelineScale.totalDuration;

      let targetDate = new Date(targetTime);
      if (targetDate < draggingBoundary.minDate) targetDate = draggingBoundary.minDate;
      if (targetDate > draggingBoundary.maxDate) targetDate = draggingBoundary.maxDate;

      setDraggingBoundary(prev => (prev ? { ...prev, currentDate: targetDate } : null));
    };

    const handleMouseUp = () => {
      if (draggingBoundary && selectedUser) {
        const older = selectedUser.teamHistory[draggingBoundary.olderIndex];
        const newer = selectedUser.teamHistory[draggingBoundary.newerIndex];
        const finalDate = draggingBoundary.currentDate;
        setDraggingBoundary(null);
        openAdjustmentModal(selectedUser, older, newer, finalDate);
      } else {
        setDraggingBoundary(null);
      }
    };

    window.addEventListener('mousemove', handleMouseMove);
    window.addEventListener('mouseup', handleMouseUp);
    return () => {
      window.removeEventListener('mousemove', handleMouseMove);
      window.removeEventListener('mouseup', handleMouseUp);
    };
  }, [draggingBoundary, timelineScale, selectedUser, openAdjustmentModal]);

  return (
    <Menu>
      <div className="team-calendar-page">
        <div className="team-calendar-header">
          <div>
            <Title order={2} c="#111827" fw={700}>
              Calendário de Equipes
            </Title>
            <Text size="sm" c="#6b7280">
              Linha do tempo e histórico de equipes dos usuários por nível hierárquico
            </Text>
          </div>
          <Button
            leftSection={<IconRefresh size={16} />}
            variant="outline"
            color="gray"
            onClick={fetchCalendar}
            loading={loading}
          >
            Atualizar
          </Button>
        </div>

        {error && (
          <Alert icon={<IconAlertTriangle size={16} />} title="Erro" color="red" mb="md">
            {error}
          </Alert>
        )}

        <div className="team-calendar-layout">
          {/* ── Left Pane: User List ────────────────────────────────────────── */}
          <div className="team-calendar-users-pane">
            <TextInput
              placeholder="Buscar por nome, email ou equipe..."
              leftSection={<IconSearch size={16} />}
              value={search}
              onChange={e => setSearch(e.target.value)}
              className="team-calendar-users-search"
            />

            <SegmentedControl
              fullWidth
              size="xs"
              value={levelFilter}
              onChange={setLevelFilter}
              data={[
                { label: 'Todos', value: 'all' },
                { label: 'Nível 1', value: '1' },
                { label: 'Nível 2', value: '2' },
                { label: 'Nível 3', value: '3' },
              ]}
              className="team-calendar-level-filters"
            />

            {loading ? (
              <Stack align="center" py="xl">
                <Loader size="md" />
                <Text size="sm" c="dimmed">
                  Carregando usuários...
                </Text>
              </Stack>
            ) : filteredUsers.length === 0 ? (
              <Text size="sm" c="dimmed" ta="center" py="xl">
                Nenhum usuário encontrado.
              </Text>
            ) : (
              <div className="team-calendar-users-list">
                {[1, 2, 3].map(level => {
                  const levelUsers = filteredUsers.filter(u => u.hierarchyLevel === level);
                  if (levelUsers.length === 0) return null;

                  return (
                    <div key={level} className="team-calendar-level-group">
                      <div className="team-calendar-level-header">
                        <span>Nível {level}</span>
                        <Badge size="xs" variant="light" color="blue">
                          {levelUsers.length}
                        </Badge>
                      </div>

                      {levelUsers.map(user => {
                        const isSelected = user.userId === selectedUserId;
                        return (
                          <div
                            key={user.userId}
                            className={`team-calendar-user-card ${isSelected ? 'active' : ''}`}
                            onClick={() => setSelectedUserId(user.userId)}
                          >
                            <div className="team-calendar-user-card__top">
                              <span className="team-calendar-user-card__name">
                                {normalizeName(user.userName)}
                              </span>
                              <Badge size="xs" color="gray" variant="outline">
                                Nível {user.hierarchyLevel}
                              </Badge>
                            </div>
                            <span className="team-calendar-user-card__email">
                              {user.userEmail}
                            </span>
                            <div className="team-calendar-user-card__meta">
                              {user.currentTeamName ? (
                                <Badge size="xs" color="blue" variant="filled">
                                  {user.currentTeamName}
                                </Badge>
                              ) : (
                                <Badge size="xs" color="gray" variant="light">
                                  Sem equipe
                                </Badge>
                              )}
                              {user.teamHistory.length > 1 && (
                                <Text size="xs" c="dimmed">
                                  {user.teamHistory.length} equipes
                                </Text>
                              )}
                            </div>
                          </div>
                        );
                      })}
                    </div>
                  );
                })}
              </div>
            )}
          </div>

          {/* ── Right Pane: Timeline & Details ──────────────────────────────── */}
          <div className="team-calendar-details-pane">
            {!selectedUser ? (
              <div className="team-calendar-empty-state">
                <IconUsers size={48} stroke={1.5} />
                <Text size="lg" fw={500}>
                  Selecione um usuário
                </Text>
                <Text size="sm">
                  Escolha um membro na lista à esquerda para visualizar sua linha do tempo de equipes.
                </Text>
              </div>
            ) : (
              <div>
                {/* User Detail Header */}
                <div className="team-calendar-user-detail-header">
                  <div>
                    <Group gap="xs" mb={4}>
                      <Title order={3} c="#111827">
                        {normalizeName(selectedUser.userName)}
                      </Title>
                      <Badge color="blue" size="sm">
                        Nível {selectedUser.hierarchyLevel}
                      </Badge>
                    </Group>
                    <Text size="sm" c="#6b7280">
                      {selectedUser.userEmail}
                    </Text>
                    {selectedUser.parentUserName && (
                      <Text size="xs" c="#9ca3af" mt={2}>
                        Superior: {normalizeName(selectedUser.parentUserName)}
                      </Text>
                    )}
                  </div>
                  <Group gap="xs">
                    {selectedUser.currentTeamName ? (
                      <Badge size="lg" color="green" variant="light" leftSection={<IconBuildingCommunity size={14} />}>
                        Equipe Atual: {selectedUser.currentTeamName}
                      </Badge>
                    ) : (
                      <Badge size="lg" color="gray" variant="light">
                        Sem equipe ativa
                      </Badge>
                    )}
                  </Group>
                </div>

                {/* Timeline Bar Section */}
                {selectedUser.teamHistory.length === 0 ? (
                  <div className="team-calendar-empty-state" style={{ height: 260 }}>
                    <IconCalendar size={40} stroke={1.5} />
                    <Text size="md" fw={500}>
                      Nenhum histórico de equipe registrado
                    </Text>
                    <Text size="sm">
                      Este usuário ainda não foi associado a nenhuma equipe no sistema.
                    </Text>
                  </div>
                ) : timelineScale ? (
                  <div className="team-calendar-timeline-container">
                    <Group justify="space-between" mb="xs">
                      <Group gap="xs">
                        <IconClock size={16} color="#6b7280" />
                        <Text size="sm" fw={600} c="#374151">
                          Linha do Tempo de Equipes
                        </Text>
                      </Group>
                      <Text size="xs" c="#6b7280">
                        {selectedUser.teamHistory.length > 1
                          ? 'Arraste a borda entre equipes para ajustar as datas de início e fim'
                          : 'Período único na equipe'}
                      </Text>
                    </Group>

                    {/* Timeline Track */}
                    <div ref={timelineTrackRef} className="team-calendar-timeline-track">
                      {selectedUser.teamHistory.map((item, idx) => {
                        const start = parseUTCDate(item.startDate).getTime();
                        const end = item.endDate
                          ? parseUTCDate(item.endDate).getTime()
                          : new Date().getTime();

                        const leftPercent =
                          ((start - timelineScale.minTime) / timelineScale.totalDuration) * 100;
                        const widthPercent =
                          Math.max(3, ((end - start) / timelineScale.totalDuration) * 100);

                        const color = getTeamColor(item.teamId);
                        const days = getDurationInDays(item.startDate, item.endDate);

                        return (
                          <div
                            key={item.userTeamId || idx}
                            className="team-calendar-timeline-block"
                            style={{
                              position: 'absolute',
                              left: `${leftPercent}%`,
                              width: `${widthPercent}%`,
                              backgroundColor: color,
                            }}
                            title={`${item.teamName} (${formatDateBR(item.startDate)} - ${formatDateBR(item.endDate)})`}
                          >
                            <span className="team-calendar-timeline-block__title">
                              {item.teamName}
                            </span>
                            <span className="team-calendar-timeline-block__dates">
                              {formatDuration(days)}
                            </span>
                          </div>
                        );
                      })}

                      {/* Drag Handles between adjacent teams */}
                      {selectedUser.teamHistory.map((item, idx) => {
                        if (idx === selectedUser.teamHistory.length - 1) return null;
                        const nextItem = selectedUser.teamHistory[idx + 1];
                        const boundaryDate = item.endDate
                          ? parseUTCDate(item.endDate)
                          : parseUTCDate(nextItem.startDate);

                        const isCurrentlyDragging =
                          draggingBoundary?.olderIndex === idx &&
                          draggingBoundary?.newerIndex === idx + 1;

                        const displayDate = isCurrentlyDragging
                          ? draggingBoundary.currentDate
                          : boundaryDate;

                        const posPercent =
                          ((displayDate.getTime() - timelineScale.minTime) /
                            timelineScale.totalDuration) *
                          100;

                        return (
                          <div
                            key={`handle-${idx}`}
                            className={`team-calendar-boundary-handle ${
                              isCurrentlyDragging ? 'dragging' : ''
                            }`}
                            style={{ left: `${posPercent}%` }}
                            onMouseDown={e => handleBoundaryMouseDown(e, idx, idx + 1)}
                            onClick={() =>
                              openAdjustmentModal(selectedUser, item, nextItem, boundaryDate)
                            }
                            title="Clique ou arraste para ajustar a data de transição"
                          >
                            <div className="team-calendar-boundary-handle__line" />
                            {isCurrentlyDragging && (
                              <div className="team-calendar-boundary-handle__pill">
                                {formatDateBR(formatDateISO(displayDate))}
                              </div>
                            )}
                          </div>
                        );
                      })}
                    </div>

                    <Group justify="space-between" mt="xs">
                      <Text size="xs" c="#6b7280">
                        Início: {formatDateBR(selectedUser.teamHistory[0].startDate)}
                      </Text>
                      <Text size="xs" c="#6b7280">
                        Fim: {formatDateBR(selectedUser.teamHistory[selectedUser.teamHistory.length - 1].endDate)}
                      </Text>
                    </Group>
                  </div>
                ) : null}

                {/* Periods List */}
                {selectedUser.teamHistory.length > 0 && (
                  <div>
                    <Text size="sm" fw={600} c="#374151" mb="xs">
                      Detalhamento dos Períodos
                    </Text>
                    <div className="team-calendar-periods-list">
                      {selectedUser.teamHistory.map((item, idx) => {
                        const color = getTeamColor(item.teamId);
                        const days = getDurationInDays(item.startDate, item.endDate);

                        return (
                          <div
                            key={item.userTeamId || idx}
                            className="team-calendar-period-card"
                            style={{ borderLeftColor: color }}
                          >
                            <div>
                              <Group gap="xs" mb={4}>
                                <Text fw={600} size="sm" c="#111827">
                                  {item.teamName}
                                </Text>
                                {item.isActive ? (
                                  <Badge size="xs" color="green" variant="filled">
                                    Ativo
                                  </Badge>
                                ) : (
                                  <Badge size="xs" color="gray" variant="light">
                                    Encerrado
                                  </Badge>
                                )}
                              </Group>
                              <Text size="xs" c="#6b7280">
                                {formatDateBR(item.startDate)} até {formatDateBR(item.endDate)} ({formatDuration(days)})
                              </Text>
                            </div>

                            {idx < selectedUser.teamHistory.length - 1 && (
                              <Button
                                size="xs"
                                variant="light"
                                color="blue"
                                onClick={() => {
                                  const next = selectedUser.teamHistory[idx + 1];
                                  const targetDate = item.endDate
                                    ? parseUTCDate(item.endDate)
                                    : parseUTCDate(next.startDate);
                                  openAdjustmentModal(selectedUser, item, next, targetDate);
                                }}
                              >
                                Ajustar Transição
                              </Button>
                            )}
                          </div>
                        );
                      })}
                    </div>
                  </div>
                )}
              </div>
            )}
          </div>
        </div>

        {/* ── Adjustment & Contract Preview Modal ──────────────────────────── */}
        <Modal
          opened={modalOpen}
          onClose={() => !savingTransition && setModalOpen(false)}
          title={
            <Group gap="xs">
              <IconCalendar size={20} color="#3b82f6" />
              <Text fw={700} size="md">
                Ajustar Data de Transição entre Equipes
              </Text>
            </Group>
          }
          size="xl"
          centered
        >
          <div className="team-calendar-preview-modal">
            {transitionUser && (
              <Card withBorder padding="sm" radius="md" bg="#f9fafb">
                <Group justify="space-between">
                  <div>
                    <Text size="xs" c="dimmed">
                      Usuário
                    </Text>
                    <Text fw={600} size="sm">
                      {normalizeName(transitionUser.userName)} ({transitionUser.userEmail})
                    </Text>
                  </div>
                  <Group gap="xs">
                    <Badge color="blue" size="md">
                      {olderTeam?.teamName || 'Equipe Anterior'}
                    </Badge>
                    <IconArrowRight size={16} color="#6b7280" />
                    <Badge color="green" size="md">
                      {newerTeam?.teamName || 'Nova Equipe'}
                    </Badge>
                  </Group>
                </Group>
              </Card>
            )}

            <div>
              <TextInput
                label="Nova Data de Transição (Início da Nova Equipe / Fim da Equipe Anterior)"
                type="date"
                value={boundaryDateInput}
                onChange={e => handleBoundaryDateChange(e.target.value)}
                description="Mínimo de 1 semana (7 dias) de intervalo para cada período de equipe."
                required
              />
            </div>

            <Divider />

            {/* Contract Preview Columns */}
            <div>
              <Text size="sm" fw={600} c="#374151" mb="xs">
                Preview dos Contratos Afetados pela Data de Corte
              </Text>
              <Text size="xs" c="#6b7280" mb="md">
                Veja abaixo os últimos contratos que ficam na equipe anterior e os primeiros contratos da nova equipe.
              </Text>

              {previewLoading ? (
                <Stack align="center" py="xl">
                  <Loader size="sm" />
                  <Text size="xs" c="dimmed">
                    Carregando preview de contratos...
                  </Text>
                </Stack>
              ) : (
                <div className="team-calendar-preview-columns">
                  {/* Older Team Contracts */}
                  <div className="team-calendar-preview-col">
                    <div className="team-calendar-preview-col__header">
                      <Text size="xs" fw={700} c="#1e40af">
                        Últimos 5 Contratos — {olderTeam?.teamName || 'Equipe Anterior'}
                      </Text>
                      <Badge size="xs" color="blue" variant="light">
                        Antes de {formatDateBR(boundaryDateInput)}
                      </Badge>
                    </div>

                    {!previewData?.olderTeamContracts || previewData.olderTeamContracts.length === 0 ? (
                      <div className="team-calendar-preview-empty">
                        Nenhum contrato antes desta data.
                      </div>
                    ) : (
                      <table className="team-calendar-preview-table">
                        <thead>
                          <tr>
                            <th>Contrato</th>
                            <th>Data</th>
                            <th>Cliente</th>
                            <th>Matrícula</th>
                          </tr>
                        </thead>
                        <tbody>
                          {previewData.olderTeamContracts.map(c => (
                            <tr key={c.contractId}>
                              <td style={{ fontWeight: 600 }}>{c.contractNumber}</td>
                              <td>{formatDateBR(c.saleStartDate)}</td>
                              <td>{c.customerName || '—'}</td>
                              <td>{c.matriculaNumber || '—'}</td>
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    )}
                  </div>

                  {/* Newer Team Contracts */}
                  <div className="team-calendar-preview-col">
                    <div className="team-calendar-preview-col__header">
                      <Text size="xs" fw={700} c="#065f46">
                        Primeiros 5 Contratos — {newerTeam?.teamName || 'Nova Equipe'}
                      </Text>
                      <Badge size="xs" color="green" variant="light">
                        A partir de {formatDateBR(boundaryDateInput)}
                      </Badge>
                    </div>

                    {!previewData?.newerTeamContracts || previewData.newerTeamContracts.length === 0 ? (
                      <div className="team-calendar-preview-empty">
                        Nenhum contrato a partir desta data.
                      </div>
                    ) : (
                      <table className="team-calendar-preview-table">
                        <thead>
                          <tr>
                            <th>Contrato</th>
                            <th>Data</th>
                            <th>Cliente</th>
                            <th>Matrícula</th>
                          </tr>
                        </thead>
                        <tbody>
                          {previewData.newerTeamContracts.map(c => (
                            <tr key={c.contractId}>
                              <td style={{ fontWeight: 600 }}>{c.contractNumber}</td>
                              <td>{formatDateBR(c.saleStartDate)}</td>
                              <td>{c.customerName || '—'}</td>
                              <td>{c.matriculaNumber || '—'}</td>
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    )}
                  </div>
                </div>
              )}
            </div>

            <Group justify="flex-end" gap="sm" mt="md">
              <Button
                variant="default"
                onClick={() => setModalOpen(false)}
                disabled={savingTransition}
              >
                Cancelar
              </Button>
              <Button
                color="blue"
                onClick={handleSaveTransition}
                loading={savingTransition}
                leftSection={<IconCheck size={16} />}
              >
                Confirmar Alteração
              </Button>
            </Group>
          </div>
        </Modal>
      </div>
    </Menu>
  );
};

export default TeamCalendarPage;
