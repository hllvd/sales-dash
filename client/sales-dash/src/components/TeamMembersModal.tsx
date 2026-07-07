import React, { useState, useEffect, useCallback, useMemo } from 'react';
import {
  Modal, Title, Text, Group, Badge, ActionIcon, TextInput,
  Loader, Stack, Divider, Tooltip, ScrollArea, Popover
} from '@mantine/core';
import { notifications } from '@mantine/notifications';
import {
  IconSearch, IconCrown, IconUserPlus, IconUserMinus,
  IconUsers, IconUser, IconCheck, IconCalendar, IconX, IconSitemap
} from '@tabler/icons-react';
import { apiService, Team, TeamMember, User } from '../services/apiService';
import './TeamMembersModal.css';

// ─── helpers ───────────────────────────────────────────────────────────────

function formatDate(dateStr: string): string {
  try {
    const d = new Date(dateStr);
    if (isNaN(d.getTime())) return dateStr;
    return d.toLocaleDateString('pt-BR', { timeZone: 'UTC' });
  } catch {
    return dateStr;
  }
}

function getEightYearsAgo(): string {
  const d = new Date();
  d.setFullYear(d.getFullYear() - 8);
  return d.toISOString();
}

/** BFS from ownerUserId → returns users ordered by proximity to owner, then createdAt.
 *  The owner is only omitted from the left (available) column when they are ALREADY an
 *  active team member (right column). If they have no active membership they must remain
 *  visible so they can be re-added.
 */
function sortByHierarchy(users: User[], ownerUserId: string | null, activeMemberIds?: Set<string>): User[] {
  if (!ownerUserId) return [...users].sort((a, b) => a.createdAt.localeCompare(b.createdAt));

  const byId = new Map(users.map(u => [u.id, u]));
  const childrenOf = new Map<string, User[]>();
  for (const u of users) {
    if (u.parentUserId) {
      if (!childrenOf.has(u.parentUserId)) childrenOf.set(u.parentUserId, []);
      childrenOf.get(u.parentUserId)!.push(u);
    }
  }
  // sort each bucket by createdAt
  childrenOf.forEach(kids => kids.sort((a, b) => a.createdAt.localeCompare(b.createdAt)));

  const visited = new Set<string>();
  const result: User[] = [];
  const queue: string[] = [ownerUserId];

  // The owner is only hidden from the left column when they are already an active member.
  // If not an active member they need to remain available so they can be re-added.
  const ownerIsAlreadyMember = activeMemberIds ? activeMemberIds.has(ownerUserId) : true;

  while (queue.length) {
    const id = queue.shift()!;
    if (visited.has(id)) continue;
    visited.add(id);
    const u = byId.get(id);
    if (u && !(id === ownerUserId && ownerIsAlreadyMember)) result.push(u);
    const kids = childrenOf.get(id) ?? [];
    for (const k of kids) queue.push(k.id);
  }

  // users not reachable from owner
  for (const u of users) {
    if (!visited.has(u.id) && u.id !== ownerUserId) result.push(u);
  }

  return result;
}

// ─── sub-components ────────────────────────────────────────────────────────

interface AvailableUserCardProps {
  user: User;
  onAdd: (user: User) => void;
  adding: boolean;
}

const AvailableUserCard: React.FC<AvailableUserCardProps> = ({ user, onAdd, adding }) => (
  <div
    className="tmc-user-card tmc-user-card--available"
    onClick={() => !adding && onAdd(user)}
    title="Clique para adicionar à equipe"
  >
    <div className="tmc-user-card__avatar">
      <IconUser size={16} color="#495057" />
    </div>
    <div className="tmc-user-card__info">
      <span className="tmc-user-card__name">{user.name}</span>
      <span className="tmc-user-card__email">{user.email}</span>
      {(user.currentTeamName || user.parentEmail) && (
        <div className="tmc-user-card__meta">
          {user.currentTeamName && (
            <span className="tmc-user-card__meta-item tmc-user-card__meta-item--team" title={`Equipe atual: ${user.currentTeamName}`}>
              <IconUsers size={10} style={{ marginRight: 3 }} />
              {user.currentTeamName}
            </span>
          )}
          {user.parentEmail && (
            <span className="tmc-user-card__meta-item tmc-user-card__meta-item--parent" title={`Superior: ${user.parentUserName || ''} (${user.parentEmail})`}>
              <IconSitemap size={10} style={{ marginRight: 3 }} />
              {user.parentEmail}
            </span>
          )}
        </div>
      )}
    </div>
    <div className="tmc-user-card__action">
      {adding ? (
        <Loader size="xs" />
      ) : (
        <Tooltip label="Adicionar à equipe" withArrow position="left">
          <ActionIcon variant="light" color="blue" size="sm" className="tmc-add-icon">
            <IconUserPlus size={14} />
          </ActionIcon>
        </Tooltip>
      )}
    </div>
  </div>
);

interface MemberCardProps {
  member: TeamMember;
  allUsers: User[];
  onRemove: (member: TeamMember) => void;
  onSetOwner: (member: TeamMember) => void;
  onUpdateDates: (member: TeamMember, startDate: string, endDate: string | null) => Promise<void>;
  removing: boolean;
  settingOwner: boolean;
}

const MemberCard: React.FC<MemberCardProps> = ({ member, allUsers, onRemove, onSetOwner, onUpdateDates, removing, settingOwner }) => {
  const [popoverOpened, setPopoverOpened] = useState(false);
  const [startDate, setStartDate] = useState('');
  const [endDate, setEndDate] = useState('');
  const [noEndDate, setNoEndDate] = useState(true);
  const [saveLoading, setSaveLoading] = useState(false);
  const [errorMsg, setErrorMsg] = useState('');

  const handleOpen = () => {
    setStartDate(member.startDate ? member.startDate.split('T')[0] : '');
    setEndDate(member.endDate ? member.endDate.split('T')[0] : '');
    setNoEndDate(!member.endDate);
    setErrorMsg('');
    setPopoverOpened(true);
  };

  const handleSaveDates = async () => {
    if (!noEndDate && endDate && startDate > endDate) {
      setErrorMsg('Início deve ser anterior ao Fim');
      return;
    }
    setErrorMsg('');
    setSaveLoading(true);
    try {
      await onUpdateDates(member, new Date(startDate).toISOString(), noEndDate ? null : new Date(endDate).toISOString());
      setPopoverOpened(false);
    } catch (e: any) {
      setErrorMsg(e.message || 'Falha ao salvar datas');
    } finally {
      setSaveLoading(false);
    }
  };

  const userDetail = allUsers.find(u => u.id === member.userId);

  return (
    <div className={`tmc-user-card tmc-user-card--member${member.isOwner ? ' tmc-user-card--owner' : ''}`}>
      <div className="tmc-user-card__avatar">
        {member.isOwner
          ? <IconCrown size={16} color="#f59f00" />
          : <IconUser size={16} color="#495057" />
        }
      </div>
      <div className="tmc-user-card__info">
        <Group gap={6} align="center">
          <span className="tmc-user-card__name">{member.userName}</span>
          {member.isOwner && (
            <Badge size="xs" color="yellow" variant="filled" leftSection={<IconCrown size={8} />}>
              Chefe
            </Badge>
          )}
        </Group>
        <span className="tmc-user-card__email">{member.userEmail}</span>
        {userDetail?.parentEmail && (
          <div className="tmc-user-card__meta">
            <span className="tmc-user-card__meta-item tmc-user-card__meta-item--parent" title={`Superior: ${userDetail.parentUserName || ''} (${userDetail.parentEmail})`}>
              <IconSitemap size={10} style={{ marginRight: 3 }} />
              {userDetail.parentEmail}
            </span>
          </div>
        )}
        
        <div style={{ marginTop: 4, display: 'flex', alignItems: 'center', gap: 6, fontSize: '11px', color: '#868e96' }}>
          <IconCalendar size={12} color="#adb5bd" />
          <span>
            {formatDate(member.startDate)} - {member.endDate ? formatDate(member.endDate) : 'Presente'}
          </span>

          <Popover width={280} position="bottom" withArrow shadow="md" opened={popoverOpened} onChange={setPopoverOpened}>
            <Popover.Target>
              <Tooltip label="Editar datas de vigência" withArrow position="top">
                <ActionIcon
                  variant="subtle"
                  color="gray"
                  size="xs"
                  onClick={handleOpen}
                  title="Editar datas de vigência"
                >
                  <IconCalendar size={12} />
                </ActionIcon>
              </Tooltip>
            </Popover.Target>
            <Popover.Dropdown style={{ padding: 12 }}>
              <Text size="xs" fw={700} mb={8} style={{ color: '#1c1c1e' }}>
                Editar Período na Equipe
              </Text>
              <Stack gap={8}>
                <div>
                  <Text size="xs" mb={4} style={{ color: '#495057' }}>Data de Início:</Text>
                  <input
                    type="date"
                    value={startDate}
                    onChange={(e) => setStartDate(e.target.value)}
                    style={{ width: '100%', padding: '6px', fontSize: '12px', border: '1px solid #ced4da', borderRadius: '4px' }}
                  />
                </div>
                <div>
                  <Group justify="space-between" mb={4}>
                    <Text size="xs" style={{ color: '#495057' }}>Data de Fim:</Text>
                    <label style={{ display: 'flex', alignItems: 'center', gap: 4, cursor: 'pointer', fontSize: '11px', color: '#495057' }}>
                      <input
                        type="checkbox"
                        checked={noEndDate}
                        onChange={(e) => {
                          setNoEndDate(e.target.checked);
                          if (e.target.checked) setEndDate('');
                        }}
                      />
                      Sem data de fim
                    </label>
                  </Group>
                  <input
                    type="date"
                    value={endDate}
                    onChange={(e) => setEndDate(e.target.value)}
                    disabled={noEndDate}
                    style={{ width: '100%', padding: '6px', fontSize: '12px', border: '1px solid #ced4da', borderRadius: '4px', backgroundColor: noEndDate ? '#f8f9fa' : 'white' }}
                  />
                </div>
                
                {errorMsg && (
                  <Text size="xs" color="red" fw={500}>{errorMsg}</Text>
                )}

                <Group gap={6} justify="flex-end" mt={4}>
                  <ActionIcon
                    variant="subtle"
                    color="gray"
                    size="sm"
                    onClick={() => setPopoverOpened(false)}
                    disabled={saveLoading}
                    title="Cancelar"
                  >
                    <IconX size={14} />
                  </ActionIcon>
                  <ActionIcon
                    variant="filled"
                    color="blue"
                    size="sm"
                    onClick={handleSaveDates}
                    loading={saveLoading}
                    disabled={!startDate || (!noEndDate && !endDate)}
                    title="Salvar datas"
                  >
                    <IconCheck size={14} />
                  </ActionIcon>
                </Group>
              </Stack>
            </Popover.Dropdown>
          </Popover>
        </div>
      </div>
      <div className="tmc-user-card__action" style={{ gap: 4 }}>
        <Tooltip label={member.isOwner ? 'Remover como Chefe' : 'Tornar Chefe'} withArrow position="left">
          <ActionIcon
            variant={member.isOwner ? 'filled' : 'light'}
            color="yellow"
            size="sm"
            loading={settingOwner}
            onClick={() => onSetOwner(member)}
            title={member.isOwner ? 'Remover como Chefe' : 'Tornar Chefe'}
          >
            <IconCrown size={13} />
          </ActionIcon>
        </Tooltip>
        <Tooltip label="Remover da equipe" withArrow position="left">
          <ActionIcon
            variant="light"
            color="red"
            size="sm"
            loading={removing}
            onClick={() => onRemove(member)}
            title="Remover da equipe"
          >
            <IconUserMinus size={14} />
          </ActionIcon>
        </Tooltip>
      </div>
    </div>
  );
};

// ─── main modal ────────────────────────────────────────────────────────────

interface Props {
  team: Team;
  allUsers: User[];
  currentUserRole: string;
  currentUserId: string;
  onClose: () => void;
  onTeamChanged: (updated: Team) => void;
}

const TeamMembersModal: React.FC<Props> = ({
  team: initialTeam,
  allUsers,
  currentUserRole,
  currentUserId,
  onClose,
  onTeamChanged,
}) => {
  const [team, setTeam] = useState<Team>(initialTeam);
  const [leftSearch, setLeftSearch] = useState('');
  const [rightSearch, setRightSearch] = useState('');
  const [addingId, setAddingId] = useState<string | null>(null);
  const [removingId, setRemovingId] = useState<string | null>(null);
  const [settingOwnerId, setSettingOwnerId] = useState<string | null>(null);

  const [tempName, setTempName] = useState(initialTeam.name);
  const [savingName, setSavingName] = useState(false);
  const [nameError, setNameError] = useState('');

  useEffect(() => {
    setTempName(team.name);
  }, [team.name]);

  const handleSaveName = async () => {
    if (!tempName.trim()) {
      setNameError('O nome da equipe é obrigatório');
      return;
    }
    setSavingName(true);
    setNameError('');
    try {
      const res = await apiService.updateTeam(team.id, tempName.trim());
      applyTeamUpdate(res);
      notifications.show({ message: 'Nome da equipe atualizado com sucesso', color: 'green', autoClose: 2000 });
    } catch (e: any) {
      setNameError(e.message || 'Falha ao atualizar nome');
      notifications.show({ title: 'Erro', message: e.message || 'Falha ao atualizar nome', color: 'red' });
    } finally {
      setSavingName(false);
    }
  };

  // ids of current active members
  const activeMembers = useMemo(() => team.members.filter(m => m.isActive), [team.members]);
  const activeMemberIds = useMemo(() => new Set(activeMembers.map(m => m.userId)), [activeMembers]);

  // visible user pool (admin = children only; superadmin = all)
  const userPool = useMemo(() => {
    const isSuperAdmin = currentUserRole?.toLowerCase() === 'superadmin';
    if (isSuperAdmin) return allUsers;

    // BFS to collect admin's children
    const visited = new Set<string>([currentUserId]);
    const queue = [currentUserId];
    while (queue.length) {
      const id = queue.shift()!;
      for (const u of allUsers) {
        if (u.parentUserId === id && !visited.has(u.id)) {
          visited.add(u.id);
          queue.push(u.id);
        }
      }
    }
    return allUsers.filter(u => visited.has(u.id) && u.id !== currentUserId);
  }, [allUsers, currentUserRole, currentUserId]);

  // available = in pool, active, NOT already a team member
  const ownerUserId = team.owner?.userId ?? null;

  const available = useMemo(() => {
    const pool = userPool.filter(u => u.isActive && !activeMemberIds.has(u.id));
    return sortByHierarchy(pool, ownerUserId, activeMemberIds);
  }, [userPool, activeMemberIds, ownerUserId]);

  const filteredAvailable = useMemo(() => {
    const q = leftSearch.toLowerCase();
    return q ? available.filter(u => u.name.toLowerCase().includes(q) || u.email.toLowerCase().includes(q)) : available;
  }, [available, leftSearch]);

  const filteredMembers = useMemo(() => {
    const q = rightSearch.toLowerCase();
    return q ? activeMembers.filter(m => m.userName.toLowerCase().includes(q) || m.userEmail.toLowerCase().includes(q)) : activeMembers;
  }, [activeMembers, rightSearch]);

  const applyTeamUpdate = useCallback((res: { data?: Team | null }) => {
    if (res.data) {
      setTeam(res.data);
      onTeamChanged(res.data);
    }
  }, [onTeamChanged]);

  const handleAdd = useCallback(async (user: User) => {
    setAddingId(user.id);
    try {
      const res = await apiService.addTeamMembers(team.id, [{ userId: user.id, startDate: getEightYearsAgo() }]);
      applyTeamUpdate(res);
      if (res.data?.warnings && res.data.warnings.length > 0) {
        res.data.warnings.forEach((warn: string) => {
          notifications.show({
            title: 'Aviso de Conflito',
            message: warn,
            color: 'yellow',
            autoClose: 8000
          });
        });
      } else {
        notifications.show({ message: `${user.name} adicionado à equipe`, color: 'green', autoClose: 2000 });
      }
    } catch (e: any) {
      notifications.show({ title: 'Erro', message: e.message || 'Falha ao adicionar membro', color: 'red' });
    } finally {
      setAddingId(null);
    }
  }, [team.id, applyTeamUpdate]);

  const handleRemove = useCallback(async (member: TeamMember) => {
    setRemovingId(member.userId);
    try {
      const res = await apiService.removeTeamMember(team.id, member.userId);
      applyTeamUpdate(res);
      notifications.show({ message: `${member.userName} removido da equipe`, color: 'orange', autoClose: 2000 });
    } catch (e: any) {
      notifications.show({ title: 'Erro', message: e.message || 'Falha ao remover membro', color: 'red' });
    } finally {
      setRemovingId(null);
    }
  }, [team.id, applyTeamUpdate]);

  const handleSetOwner = useCallback(async (member: TeamMember) => {
    if (member.isOwner) return; // already owner
    setSettingOwnerId(member.userId);
    try {
      const res = await apiService.setTeamOwner(team.id, member.userId);
      applyTeamUpdate(res);
      notifications.show({ message: `${member.userName} é o novo Chefe da equipe`, color: 'yellow', autoClose: 2500 });
    } catch (e: any) {
      notifications.show({ title: 'Erro', message: e.message || 'Falha ao definir chefe', color: 'red' });
    } finally {
      setSettingOwnerId(null);
    }
  }, [team.id, applyTeamUpdate]);

  const handleUpdateDates = useCallback(async (member: TeamMember, startDate: string, endDate: string | null) => {
    try {
      const res = await apiService.updateTeamMemberDates(team.id, member.userId, startDate, endDate);
      applyTeamUpdate(res);
      notifications.show({ message: `Datas de vigência de ${member.userName} atualizadas`, color: 'green', autoClose: 2000 });
    } catch (e: any) {
      notifications.show({ title: 'Erro', message: e.message || 'Falha ao atualizar datas', color: 'red' });
      throw e;
    }
  }, [team.id, applyTeamUpdate]);

  return (
    <Modal
      opened
      onClose={onClose}
      size="75%"
      title={
        <Group gap="sm">
          <IconUsers size={22} color="#228be6" />
          <Title order={3} style={{ color: '#1c1c1e', fontWeight: 700 }}>
            Gerenciar Membros — {team.name}
          </Title>
          {team.owner && (
            <Badge color="yellow" variant="light" leftSection={<IconCrown size={11} />}>
              Chefe: {team.owner.userName}
            </Badge>
          )}
        </Group>
      }
      styles={{
        header: {
          backgroundColor: '#ffffff',
          borderBottom: '1px solid #e9ecef',
          padding: '20px 28px',
        },
        body: {
          backgroundColor: '#f8f9fa',
          padding: '24px 28px',
        },
        content: {
          backgroundColor: '#f8f9fa',
          borderRadius: '12px',
          boxShadow: '0 20px 60px rgba(0,0,0,0.15)',
        },
        close: {
          color: '#495057',
          '&:hover': { backgroundColor: '#f1f3f5', color: '#212529' },
        },
      }}
    >
      <div style={{
        marginBottom: '20px',
        backgroundColor: '#ffffff',
        padding: '16px 20px',
        borderRadius: '8px',
        border: '1px solid #e9ecef',
        boxShadow: '0 1px 3px rgba(0,0,0,0.02)'
      }}>
        <TextInput
          label="Nome da Equipe"
          placeholder="Digite o nome da equipe..."
          value={tempName}
          onChange={(e) => setTempName(e.target.value)}
          size="sm"
          error={nameError}
          rightSection={
            tempName.trim() !== team.name && tempName.trim() !== "" ? (
              <Tooltip label="Salvar novo nome" withArrow>
                <ActionIcon
                  variant="filled"
                  color="blue"
                  size="sm"
                  onClick={handleSaveName}
                  loading={savingName}
                  title="Salvar novo nome"
                >
                  <IconCheck size={14} />
                </ActionIcon>
              </Tooltip>
            ) : null
          }
          styles={{
            input: {
              fontWeight: 600,
              color: '#212529',
              fontSize: '14px',
            },
            label: {
              fontWeight: 700,
              color: '#495057',
              marginBottom: '6px',
            }
          }}
        />
      </div>

      <div className="tmc-layout">
        {/* ── Left: Available Users ── */}
        <div className="tmc-column tmc-column--left">
          <div className="tmc-column__header">
            <Text fw={700} size="sm" c="#212529">Usuários Disponíveis</Text>
            <Badge variant="light" color="gray" size="sm">{filteredAvailable.length}</Badge>
          </div>
          <TextInput
            placeholder="Buscar usuário..."
            leftSection={<IconSearch size={14} />}
            value={leftSearch}
            onChange={e => setLeftSearch(e.currentTarget.value)}
            size="sm"
            className="tmc-search"
          />
          <ScrollArea className="tmc-scroll" type="scroll">
            {filteredAvailable.length === 0 ? (
              <div className="tmc-empty">
                <IconUser size={28} color="#adb5bd" />
                <Text size="sm" c="dimmed" mt={8}>Nenhum usuário disponível</Text>
              </div>
            ) : (
              <Stack gap={6}>
                {filteredAvailable.map(user => (
                  <AvailableUserCard
                    key={user.id}
                    user={user}
                    onAdd={handleAdd}
                    adding={addingId === user.id}
                  />
                ))}
              </Stack>
            )}
          </ScrollArea>
        </div>

        <Divider orientation="vertical" style={{ borderColor: '#dee2e6' }} />

        {/* ── Right: Team Members ── */}
        <div className="tmc-column tmc-column--right">
          <div className="tmc-column__header">
            <Text fw={700} size="sm" c="#212529">Membros da Equipe</Text>
            <Badge variant="light" color="blue" size="sm">{activeMembers.length}</Badge>
          </div>
          <TextInput
            placeholder="Buscar membro..."
            leftSection={<IconSearch size={14} />}
            value={rightSearch}
            onChange={e => setRightSearch(e.currentTarget.value)}
            size="sm"
            className="tmc-search"
          />
          <ScrollArea className="tmc-scroll" type="scroll">
            {filteredMembers.length === 0 ? (
              <div className="tmc-empty">
                <IconUsers size={28} color="#adb5bd" />
                <Text size="sm" c="dimmed" mt={8}>Nenhum membro nesta equipe</Text>
              </div>
            ) : (
              <Stack gap={6}>
                {filteredMembers.map(member => (
                  <MemberCard
                    key={member.userId}
                    member={member}
                    allUsers={allUsers}
                    onRemove={handleRemove}
                    onSetOwner={handleSetOwner}
                    onUpdateDates={handleUpdateDates}
                    removing={removingId === member.userId}
                    settingOwner={settingOwnerId === member.userId}
                  />
                ))}
              </Stack>
            )}
          </ScrollArea>
        </div>
      </div>
    </Modal>
  );
};

export default TeamMembersModal;
