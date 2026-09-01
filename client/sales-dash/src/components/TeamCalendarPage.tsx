import React, { useState, useEffect, useCallback, useMemo, useRef } from 'react';
import {
  Title, Text, Group, Badge, Button, TextInput, Select,
  Loader, Stack, Divider, Modal, Alert, SegmentedControl, Card, Stepper, Checkbox
} from '@mantine/core';
import { notifications } from '@mantine/notifications';
import {
  IconSearch, IconCalendar, IconUsers, IconRefresh,
  IconArrowRight, IconAlertTriangle, IconCheck, IconClock,
  IconBuildingCommunity, IconUserPlus, IconArrowsRightLeft, IconInfoCircle,
  IconEdit
} from '@tabler/icons-react';
import Menu from './Menu';
import {
  apiService, TeamCalendarUser, UserTeamHistoryEntry, AvailableTeamItem,
  CalendarContractPreviewResponse, AdjustTeamBoundaryRequest, AssignUserTeamRequest
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

  // Available teams for assignment
  const [availableTeams, setAvailableTeams] = useState<AvailableTeamItem[]>([]);
  const [loadingAvailableTeams, setLoadingAvailableTeams] = useState(false);

  // Wizard Modal state
  const [wizardOpen, setWizardOpen] = useState(false);
  const [wizardStep, setWizardStep] = useState(0);
  const [wizardSelectedTeamId, setWizardSelectedTeamId] = useState<string | null>(null);
  const [wizardStartDate, setWizardStartDate] = useState(formatDateISO(new Date()));
  const [wizardPreviewLoading, setWizardPreviewLoading] = useState(false);
  const [wizardPreviewData, setWizardPreviewData] = useState<CalendarContractPreviewResponse | null>(null);
  const [wizardSaving, setWizardSaving] = useState(false);
  const [wizardUpdateParentUser, setWizardUpdateParentUser] = useState(true);

  // Direct Team Period Edit Modal state (opened when clicking a team block)
  const [editPeriodModalOpen, setEditPeriodModalOpen] = useState(false);
  const [editingPeriodMembership, setEditingPeriodMembership] = useState<UserTeamHistoryEntry | null>(null);
  const [editPeriodStartDate, setEditPeriodStartDate] = useState('');
  const [editPeriodEndDate, setEditPeriodEndDate] = useState('');
  const [editPeriodIsActive, setEditPeriodIsActive] = useState(false);
  const [savingPeriodEdit, setSavingPeriodEdit] = useState(false);

  // Drag and drop timeline state
  const timelineTrackRef = useRef<HTMLDivElement | null>(null);
  const [draggingBoundary, setDraggingBoundary] = useState<{
    olderIndex: number;
    newerIndex: number;
    initialDate: Date;
    currentDate: Date;
    minDate: Date;
    maxDate: Date;
  } | null>(null);

  // Quick Adjustment Modal state (boundary transition)
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

  // Fetch available teams
  const fetchAvailableTeams = useCallback(async () => {
    setLoadingAvailableTeams(true);
    try {
      const res = await apiService.getAvailableTeamsForAssignment();
      if (res.success && res.data) {
        setAvailableTeams(res.data);
      }
    } catch (err) {
      console.error('Failed to load available teams', err);
    } finally {
      setLoadingAvailableTeams(false);
    }
  }, []);

  useEffect(() => {
    fetchCalendar();
    fetchAvailableTeams();
  }, [fetchCalendar, fetchAvailableTeams]);

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

  // Active membership for selected user
  const activeMembership = useMemo(() => {
    if (!selectedUser) return null;
    return selectedUser.teamHistory.find(h => h.isActive) || null;
  }, [selectedUser]);

  // Open Wizard
  const openAssignTeamWizard = () => {
    if (!selectedUser) return;
    setWizardStep(0);
    setWizardSelectedTeamId(null);

    // If first team, check if user has earliest contract date to default to 1 day before
    if (selectedUser.teamHistory.length === 0 && selectedUser.earliestContractDate) {
      const contractDate = parseUTCDate(selectedUser.earliestContractDate);
      const oneDayBefore = new Date(contractDate.getTime() - 24 * 60 * 60 * 1000);
      setWizardStartDate(formatDateISO(oneDayBefore));
    } else {
      setWizardStartDate(formatDateISO(new Date()));
    }

    setWizardPreviewData(null);
    setWizardUpdateParentUser(true);
    setWizardOpen(true);
  };

  // Open Direct Period Edit Modal (when clicking a team on the timeline)
  const openEditPeriodModal = (membership: UserTeamHistoryEntry) => {
    if (!selectedUser) return;
    setEditingPeriodMembership(membership);
    setEditPeriodStartDate(formatDateISO(parseUTCDate(membership.startDate)));
    setEditPeriodEndDate(membership.endDate ? formatDateISO(parseUTCDate(membership.endDate)) : '');
    setEditPeriodIsActive(membership.isActive);
    setEditPeriodModalOpen(true);
  };

  // Save Direct Period Edit
  const handleSavePeriodEdit = async () => {
    if (!selectedUser || !editingPeriodMembership || !editPeriodStartDate) return;

    if (!editPeriodIsActive && !editPeriodEndDate) {
      notifications.show({
        title: 'Data de Término Obrigatória',
        message: 'Para períodos não ativos, informe a data de término.',
        color: 'red',
      });
      return;
    }

    if (!editPeriodIsActive && editPeriodEndDate) {
      const start = parseUTCDate(editPeriodStartDate).getTime();
      const end = parseUTCDate(editPeriodEndDate).getTime();
      if (start > end) {
        notifications.show({
          title: 'Data Inválida',
          message: 'A data de início deve ser anterior à data de fim.',
          color: 'red',
        });
        return;
      }
      const days = (end - start) / (1000 * 60 * 60 * 24);
      if (days < 7) {
        notifications.show({
          title: 'Período Inválido',
          message: 'O período da equipe deve ter duração mínima de 1 semana (7 dias).',
          color: 'red',
        });
        return;
      }
    }

    setSavingPeriodEdit(true);
    try {
      const startDateIso = `${editPeriodStartDate}T12:00:00Z`;
      const endDateIso = editPeriodIsActive || !editPeriodEndDate ? null : `${editPeriodEndDate}T12:00:00Z`;

      await apiService.updateTeamMemberDates(editingPeriodMembership.teamId, selectedUser.userId, startDateIso, endDateIso);
      notifications.show({
        title: 'Período Atualizado',
        message: 'As datas da equipe foram atualizadas com sucesso!',
        color: 'green',
      });
      setEditPeriodModalOpen(false);
      fetchCalendar();
    } catch (err: any) {
      notifications.show({
        title: 'Erro ao salvar',
        message: err.message || 'Falha ao atualizar datas do período.',
        color: 'red',
      });
    } finally {
      setSavingPeriodEdit(false);
    }
  };

  // Fetch contract preview for modal/wizard
  const fetchPreview = useCallback(async (userId: string, dateStr: string) => {
    setPreviewLoading(true);
    try {
      const res = await apiService.getContractPreview(userId, dateStr);
      if (res.success && res.data) {
        setPreviewData(res.data);
      }
    } catch (err: any) {
      console.error('Failed to load contract preview', err);
    } finally {
      setPreviewLoading(false);
    }
  }, []);

  // Fetch wizard preview when entering step 2 or date changes
  const fetchWizardPreview = useCallback(async (userId: string, dateStr: string) => {
    setWizardPreviewLoading(true);
    try {
      const res = await apiService.getContractPreview(userId, dateStr);
      if (res.success && res.data) {
        setWizardPreviewData(res.data);
      }
    } catch (err) {
      console.error('Failed to load wizard preview', err);
    } finally {
      setWizardPreviewLoading(false);
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

  // Handle Date Input change in quick adjustment modal
  const handleBoundaryDateChange = (newDateStr: string) => {
    setBoundaryDateInput(newDateStr);
    if (transitionUser && newDateStr) {
      fetchPreview(transitionUser.userId, newDateStr);
    }
  };

  // Save boundary transition
  const handleSaveTransition = async () => {
    if (!transitionUser || !boundaryDateInput) return;

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

  // Submit Wizard Assignment
  const handleWizardSubmit = async () => {
    if (!selectedUser || !wizardSelectedTeamId || !wizardStartDate) return;

    const teamIdNum = parseInt(wizardSelectedTeamId, 10);
    if (isNaN(teamIdNum)) return;

    // Validate 1-week rule if active team exists
    if (activeMembership) {
      const activeStart = parseUTCDate(activeMembership.startDate).getTime();
      const newStart = parseUTCDate(wizardStartDate).getTime();
      const days = (newStart - activeStart) / (1000 * 60 * 60 * 24);
      if (days < 7) {
        notifications.show({
          title: 'Período Inválido',
          message: 'O período na equipe anterior deve ter duração mínima de 1 semana (7 dias).',
          color: 'red',
        });
        return;
      }
    }

    setWizardSaving(true);
    try {
      const req: AssignUserTeamRequest = {
        userId: selectedUser.userId,
        newTeamId: teamIdNum,
        startDate: `${wizardStartDate}T12:00:00Z`,
        updateParentUser: wizardUpdateParentUser,
      };

      const res = await apiService.assignUserTeam(req);
      if (res.success && res.data) {
        notifications.show({
          title: 'Equipe Atribuída',
          message: `O usuário foi atribuído à equipe com sucesso!`,
          color: 'green',
        });
        setWizardOpen(false);
        setUsers(prev =>
          prev.map(u => (u.userId === selectedUser.userId ? res.data! : u))
        );
      }
    } catch (err: any) {
      notifications.show({
        title: 'Erro ao atribuir equipe',
        message: err.message || 'Falha ao atribuir nova equipe.',
        color: 'red',
      });
    } finally {
      setWizardSaving(false);
    }
  };

  // Check short period warning for wizard (< 14 days)
  const wizardShortPeriodWarning = useMemo(() => {
    if (!activeMembership || !wizardStartDate) return null;
    const activeStart = parseUTCDate(activeMembership.startDate).getTime();
    const newStart = parseUTCDate(wizardStartDate).getTime();
    const days = Math.round((newStart - activeStart) / (1000 * 60 * 60 * 24));
    if (days >= 7 && days < 14) {
      return `A permanência na equipe anterior (${activeMembership.teamName}) ficará em apenas ${days} dias. Não é normal pertencer a uma equipe por tão poucos dias; você pode excluir este vínculo anterior ou ignorar este aviso se for intencional.`;
    }
    return null;
  }, [activeMembership, wizardStartDate]);

  // Previous team end date (1 day before wizardStartDate)
  const previousTeamEndDate = useMemo(() => {
    if (!wizardStartDate) return '';
    const newStart = parseUTCDate(wizardStartDate);
    const prevEnd = new Date(newStart.getTime() - 24 * 60 * 60 * 1000);
    return formatDateISO(prevEnd);
  }, [wizardStartDate]);

  // Selected team object in wizard
  const wizardSelectedTeam = useMemo(() => {
    if (!wizardSelectedTeamId) return null;
    return availableTeams.find(t => t.id.toString() === wizardSelectedTeamId) || null;
  }, [availableTeams, wizardSelectedTeamId]);

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

    const minDate = new Date(olderStart.getTime() + 7 * 24 * 60 * 60 * 1000);
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
              Linha do tempo, atribuição de equipes e histórico dos usuários por nível hierárquico
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

          {/* ── Right Pane: Details & Hero Card ────────────────────────────── */}
          <div className="team-calendar-details-pane">
            {!selectedUser ? (
              <div className="team-calendar-empty-state">
                <IconUsers size={48} stroke={1.5} />
                <Text size="lg" fw={500}>
                  Selecione um usuário
                </Text>
                <Text size="sm">
                  Escolha um membro na lista à esquerda para visualizar sua equipe atual e linha do tempo.
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
                        Superior direto: {normalizeName(selectedUser.parentUserName)}
                      </Text>
                    )}
                  </div>
                </div>

                {/* Hero Current Team Card */}
                {activeMembership ? (
                  <div className="team-calendar-current-card">
                    <div className="team-calendar-current-card__info">
                      <div className="team-calendar-current-card__icon">
                        <IconBuildingCommunity size={28} />
                      </div>
                      <div>
                        <Group gap="xs" mb={2}>
                          <Text size="xs" fw={700} c="#1d4ed8" tt="uppercase">
                            Equipe Atual
                          </Text>
                          <Badge color="green" size="xs" variant="filled">
                            Ativo
                          </Badge>
                        </Group>
                        <Text fw={700} size="lg" c="#111827">
                          {activeMembership.teamName}
                        </Text>
                        <Text size="xs" c="#6b7280">
                          Desde {formatDateBR(activeMembership.startDate)} (
                          {formatDuration(getDurationInDays(activeMembership.startDate, null))})
                        </Text>
                      </div>
                    </div>

                    <Button
                      size="sm"
                      color="blue"
                      leftSection={<IconArrowsRightLeft size={16} />}
                      onClick={openAssignTeamWizard}
                    >
                      Atribuir Nova Equipe
                    </Button>
                  </div>
                ) : (
                  <div className="team-calendar-current-card no-team">
                    <div className="team-calendar-current-card__info">
                      <div className="team-calendar-current-card__icon">
                        <IconUsers size={28} />
                      </div>
                      <div>
                        <Text size="xs" fw={700} c="#6b7280" tt="uppercase">
                          Status de Equipe
                        </Text>
                        <Text fw={600} size="md" c="#374151">
                          Nenhuma equipe ativa no momento
                        </Text>
                        <Text size="xs" c="#9ca3af">
                          Atribua este usuário a uma equipe subordinada na hierarquia.
                        </Text>
                      </div>
                    </div>

                    <Button
                      size="sm"
                      color="blue"
                      leftSection={<IconUserPlus size={16} />}
                      onClick={openAssignTeamWizard}
                    >
                      Atribuir Primeira Equipe
                    </Button>
                  </div>
                )}

                {/* Timeline Bar Section */}
                {selectedUser.teamHistory.length === 0 ? (
                  <div className="team-calendar-empty-state" style={{ height: 220 }}>
                    <IconCalendar size={36} stroke={1.5} />
                    <Text size="md" fw={500}>
                      Nenhum histórico anterior registrado
                    </Text>
                    <Text size="sm">
                      Use o botão acima para vincular este usuário à sua primeira equipe.
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
                        Clique em uma equipe para editar datas de início/fim, ou arraste a borda para ajustar a transição.
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
                            onClick={() => openEditPeriodModal(item)}
                            title={`Clique para editar datas de ${item.teamName} (${formatDateBR(item.startDate)} - ${formatDateBR(item.endDate)})`}
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
                        Primeiro registro: {formatDateBR(selectedUser.teamHistory[0].startDate)}
                      </Text>
                      <Text size="xs" c="#6b7280">
                        {selectedUser.teamHistory[selectedUser.teamHistory.length - 1].endDate
                          ? `Fim: ${formatDateBR(selectedUser.teamHistory[selectedUser.teamHistory.length - 1].endDate)}`
                          : 'Período atual em andamento'}
                      </Text>
                    </Group>
                  </div>
                ) : null}

                {/* Periods List */}
                {selectedUser.teamHistory.length > 0 && (
                  <div>
                    <Text size="sm" fw={600} c="#374151" mb="xs">
                      Histórico Cronológico de Períodos
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

                            <Group gap="xs">
                              <Button
                                size="xs"
                                variant="subtle"
                                color="gray"
                                leftSection={<IconEdit size={14} />}
                                onClick={() => openEditPeriodModal(item)}
                              >
                                Editar Datas
                              </Button>

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
                            </Group>
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

        {/* ── Wizard Modal: Atribuir Nova Equipe ──────────────────────────── */}
        <Modal
          opened={wizardOpen}
          onClose={() => !wizardSaving && setWizardOpen(false)}
          title={
            <Group gap="xs">
              <IconArrowsRightLeft size={22} color="#111827" />
              <Text fw={700} size="lg" c="#111827">
                {activeMembership ? 'Atribuir Nova Equipe' : 'Atribuir Primeira Equipe'}
              </Text>
            </Group>
          }
          size={860}
          centered
        >
          <div className="team-calendar-wizard-modal">
            {selectedUser && (
              <Card withBorder padding="xs" radius="md" bg="#f9fafb">
                <Group justify="space-between">
                  <div>
                    <Text size="xs" c="dimmed">
                      Membro selecionado
                    </Text>
                    <Text fw={600} size="sm" c="#111827">
                      {normalizeName(selectedUser.userName)} ({selectedUser.userEmail})
                    </Text>
                  </div>
                  {activeMembership && (
                    <Badge color="blue" size="sm">
                      Equipe Atual: {activeMembership.teamName}
                    </Badge>
                  )}
                </Group>
              </Card>
            )}

            <Stepper
              active={wizardStep}
              onStepClick={step => {
                if (step === 2 && selectedUser && wizardStartDate) {
                  fetchWizardPreview(selectedUser.userId, wizardStartDate);
                }
                setWizardStep(step);
              }}
              size="xs"
              className="team-calendar-stepper"
            >
              <Stepper.Step label="Equipe" description="Escolher nova equipe" />
              <Stepper.Step label="Data" description="Data de início" />
              <Stepper.Step label="Preview" description="Contratos afetados" />
              <Stepper.Step label="Confirmação" description="Resumo final" />
            </Stepper>

            <div className="team-calendar-wizard-step-content">
              {/* Step 0: Escolher Equipe */}
              {wizardStep === 0 && (
                <Stack gap="md">
                  <Text size="sm" c="#374151">
                    Selecione a equipe de destino para este usuário. A lista exibe equipes sob sua gestão e de subordinados até 3 níveis:
                  </Text>

                  <Select
                    label="Nova Equipe"
                    placeholder="Selecione uma equipe..."
                    searchable
                    nothingFoundMessage="Nenhuma equipe encontrada"
                    data={availableTeams
                      .filter(t => !activeMembership || t.id !== activeMembership.teamId)
                      .map(t => ({
                        value: t.id.toString(),
                        label: `${t.name}${t.ownerName ? ` (Gestor: ${t.ownerName})` : ''}${t.storeName ? ` - ${t.storeName}` : ''}`,
                      }))}
                    value={wizardSelectedTeamId}
                    onChange={val => setWizardSelectedTeamId(val)}
                    disabled={loadingAvailableTeams}
                    required
                  />

                  {wizardSelectedTeam && (
                    <Card withBorder padding="sm" radius="md" bg="#eff6ff">
                      <Text size="xs" fw={700} c="#1e40af" mb={2}>
                        Detalhes da Equipe Selecionada:
                      </Text>
                      <Text size="sm" fw={600} c="#111827">
                        {wizardSelectedTeam.name}
                      </Text>
                      {wizardSelectedTeam.ownerName && (
                        <Text size="xs" c="dimmed">
                          Gestor / Proprietário: {wizardSelectedTeam.ownerName}
                        </Text>
                      )}
                      {wizardSelectedTeam.storeName && (
                        <Text size="xs" c="dimmed">
                          Loja: {wizardSelectedTeam.storeName}
                        </Text>
                      )}
                      <Text size="xs" c="dimmed">
                        Membros ativos atuais: {wizardSelectedTeam.memberCount}
                      </Text>

                      <Divider my="xs" />

                      <Checkbox
                        label={
                          wizardSelectedTeam.ownerName
                            ? `Mudar também o superior direto (usuário pai) para ${wizardSelectedTeam.ownerName}`
                            : 'Mudar também o superior direto (Esta equipe não possui gestor definido)'
                        }
                        checked={wizardSelectedTeam.ownerName ? wizardUpdateParentUser : false}
                        onChange={e => setWizardUpdateParentUser(e.currentTarget.checked)}
                        disabled={!wizardSelectedTeam.ownerName}
                        size="xs"
                      />
                    </Card>
                  )}
                </Stack>
              )}

              {/* Step 1: Data de Início */}
              {wizardStep === 1 && (
                <Stack gap="md">
                  <Text size="sm" c="#374151">
                    Defina a data a partir da qual o usuário fará parte de{' '}
                    <strong>{wizardSelectedTeam?.name || 'Nova Equipe'}</strong>:
                  </Text>

                  <TextInput
                    label="Data de Início na Nova Equipe"
                    type="date"
                    value={wizardStartDate}
                    onChange={e => setWizardStartDate(e.target.value)}
                    required
                  />

                  {activeMembership && (
                    <Text size="xs" c="#6b7280">
                      ℹ️ A equipe atual (<strong>{activeMembership.teamName}</strong>) será encerrada no dia anterior ({formatDateBR(previousTeamEndDate)}).
                    </Text>
                  )}

                  {wizardShortPeriodWarning && (
                    <Alert icon={<IconAlertTriangle size={18} />} color="yellow" title="Atenção ao Período Curto">
                      {wizardShortPeriodWarning}
                    </Alert>
                  )}
                </Stack>
              )}

              {/* Step 2: Preview dos Contratos */}
              {wizardStep === 2 && (
                <Stack gap="md">
                  <div>
                    <Text size="sm" fw={600} c="#374151">
                      Preview dos Contratos Afetados
                    </Text>
                    <Text size="xs" c="#6b7280">
                      Veja a divisão dos contratos com base na data de corte{' '}
                      <strong>{formatDateBR(wizardStartDate)}</strong>:
                    </Text>
                  </div>

                  {wizardPreviewLoading ? (
                    <Stack align="center" py="xl">
                      <Loader size="sm" />
                      <Text size="xs" c="dimmed">
                        Carregando preview de contratos...
                      </Text>
                    </Stack>
                  ) : (
                    <div className="team-calendar-preview-columns">
                      {/* Left: Older Team Contracts */}
                      <div className="team-calendar-preview-col">
                        <div className="team-calendar-preview-col__header">
                          <Text size="xs" fw={700} c="#1e40af">
                            Últimos Contratos — {activeMembership?.teamName || 'Equipe Anterior'}
                          </Text>
                          <Badge size="xs" color="blue" variant="light">
                            Até {formatDateBR(previousTeamEndDate)}
                          </Badge>
                        </div>

                        {!wizardPreviewData?.olderTeamContracts ||
                        wizardPreviewData.olderTeamContracts.length === 0 ? (
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
                              {wizardPreviewData.olderTeamContracts.map(c => (
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

                      {/* Right: Newer Team Contracts */}
                      <div className="team-calendar-preview-col">
                        <div className="team-calendar-preview-col__header">
                          <Text size="xs" fw={700} c="#065f46">
                            Primeiros Contratos — {wizardSelectedTeam?.name || 'Nova Equipe'}
                          </Text>
                          <Badge size="xs" color="green" variant="light">
                            A partir de {formatDateBR(wizardStartDate)}
                          </Badge>
                        </div>

                        {!wizardPreviewData?.newerTeamContracts ||
                        wizardPreviewData.newerTeamContracts.length === 0 ? (
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
                              {wizardPreviewData.newerTeamContracts.map(c => (
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
                </Stack>
              )}

              {/* Step 3: Resumo & Confirmação */}
              {wizardStep === 3 && (
                <Stack gap="md">
                  <Alert icon={<IconInfoCircle size={18} />} color="blue" title="Confirmação de Mudança">
                    Por favor, revise o resumo da atribuição antes de confirmar:
                  </Alert>

                  <Card withBorder padding="md" radius="md">
                    <Stack gap="sm">
                      <Group justify="space-between">
                        <Text size="xs" c="dimmed">
                          Usuário:
                        </Text>
                        <Text size="sm" fw={600} c="#111827">
                          {selectedUser ? `${normalizeName(selectedUser.userName)} (${selectedUser.userEmail})` : ''}
                        </Text>
                      </Group>
                      <Divider />

                      {activeMembership && (
                        <Group justify="space-between">
                          <Text size="xs" c="dimmed">
                            Equipe Anterior:
                          </Text>
                          <div>
                            <Badge color="gray">{activeMembership.teamName}</Badge>
                            <Text size="xs" c="dimmed" ta="right" mt={2}>
                              Encerramento: {formatDateBR(previousTeamEndDate)}
                            </Text>
                          </div>
                        </Group>
                      )}

                      <Group justify="space-between">
                        <Text size="xs" c="dimmed">
                          Nova Equipe:
                        </Text>
                        <div>
                          <Badge color="green" size="md">
                            {wizardSelectedTeam?.name}
                          </Badge>
                          <Text size="xs" c="dimmed" ta="right" mt={2}>
                            Início: {formatDateBR(wizardStartDate)}
                          </Text>
                        </div>
                      </Group>

                      {wizardUpdateParentUser && wizardSelectedTeam?.ownerName && (
                        <Group justify="space-between">
                          <Text size="xs" c="dimmed">
                            Novo Superior Direto:
                          </Text>
                          <div>
                            <Badge color="blue" size="md">
                              {wizardSelectedTeam.ownerName}
                            </Badge>
                            <Text size="xs" c="dimmed" ta="right" mt={2}>
                              Gestor da Equipe
                            </Text>
                          </div>
                        </Group>
                      )}
                    </Stack>
                  </Card>
                </Stack>
              )}
            </div>

            {/* Wizard Navigation Buttons */}
            <Group justify="space-between" mt="md">
              <Button
                variant="default"
                onClick={() => {
                  if (wizardStep === 0) setWizardOpen(false);
                  else setWizardStep(prev => prev - 1);
                }}
                disabled={wizardSaving}
              >
                {wizardStep === 0 ? 'Cancelar' : 'Voltar'}
              </Button>

              {wizardStep < 3 ? (
                <Button
                  color="blue"
                  disabled={wizardStep === 0 && !wizardSelectedTeamId}
                  onClick={() => {
                    if (wizardStep === 1 && selectedUser && wizardStartDate) {
                      fetchWizardPreview(selectedUser.userId, wizardStartDate);
                    }
                    setWizardStep(prev => prev + 1);
                  }}
                  rightSection={<IconArrowRight size={16} />}
                >
                  Próximo
                </Button>
              ) : (
                <Button
                  color="green"
                  onClick={handleWizardSubmit}
                  loading={wizardSaving}
                  leftSection={<IconCheck size={16} />}
                >
                  Confirmar Atribuição
                </Button>
              )}
            </Group>
          </div>
        </Modal>

        {/* ── Direct Team Period Edit Modal (when clicking a team block) ────── */}
        <Modal
          opened={editPeriodModalOpen}
          onClose={() => !savingPeriodEdit && setEditPeriodModalOpen(false)}
          title={
            <Group gap="xs">
              <IconEdit size={20} color="#111827" />
              <Text fw={700} size="md" c="#111827">
                Editar Período da Equipe
              </Text>
            </Group>
          }
          size="md"
          centered
        >
          {editingPeriodMembership && selectedUser && (
            <Stack gap="md">
              <Card withBorder padding="xs" radius="md" bg="#f9fafb">
                <Group justify="space-between">
                  <div>
                    <Text size="xs" c="dimmed">
                      Equipe
                    </Text>
                    <Text fw={600} size="sm" c="#111827">
                      {editingPeriodMembership.teamName}
                    </Text>
                  </div>
                  <Badge color="blue">
                    {normalizeName(selectedUser.userName)}
                  </Badge>
                </Group>
              </Card>

              <TextInput
                label="Data de Início"
                type="date"
                value={editPeriodStartDate}
                onChange={e => setEditPeriodStartDate(e.target.value)}
                required
              />

              <Checkbox
                label="Equipe Ativa (Sem data de término definida)"
                checked={editPeriodIsActive}
                onChange={e => {
                  setEditPeriodIsActive(e.currentTarget.checked);
                  if (e.currentTarget.checked) setEditPeriodEndDate('');
                }}
              />

              {!editPeriodIsActive && (
                <TextInput
                  label="Data de Término"
                  type="date"
                  value={editPeriodEndDate}
                  onChange={e => setEditPeriodEndDate(e.target.value)}
                  required
                />
              )}

              <Alert color="blue" variant="light">
                <Text size="xs">
                  A alteração de datas sincroniza automaticamente as equipes vizinhas para manter a linha do tempo contínua (sem sobreposições e sem intervalos de dias).
                </Text>
              </Alert>

              <Group justify="flex-end" gap="sm" mt="md">
                <Button
                  variant="default"
                  onClick={() => setEditPeriodModalOpen(false)}
                  disabled={savingPeriodEdit}
                >
                  Cancelar
                </Button>
                <Button
                  color="blue"
                  onClick={handleSavePeriodEdit}
                  loading={savingPeriodEdit}
                  leftSection={<IconCheck size={16} />}
                >
                  Salvar Datas
                </Button>
              </Group>
            </Stack>
          )}
        </Modal>

        {/* ── Quick Adjustment & Contract Preview Modal (from Timeline Drag) ─── */}
        <Modal
          opened={modalOpen}
          onClose={() => !savingTransition && setModalOpen(false)}
          title={
            <Group gap="xs">
              <IconCalendar size={20} color="#111827" />
              <Text fw={700} size="md" c="#111827">
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
                    <Text fw={600} size="sm" c="#111827">
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
                label="Nova Data de Transição (Início da Nova Equipe)"
                type="date"
                value={boundaryDateInput}
                onChange={e => handleBoundaryDateChange(e.target.value)}
                description="A equipe anterior será encerrada no dia anterior. Mínimo de 1 semana (7 dias) de intervalo para cada período."
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
