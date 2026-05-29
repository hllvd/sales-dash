import React, { useState, useEffect, useCallback } from 'react'
import {
  Title, Button, ActionIcon, Group, Badge, Text, TextInput, Textarea,
  NumberInput, Modal, Stack, ScrollArea, Tooltip, Loader, Divider, Checkbox
} from '@mantine/core'
import {
  IconPlus, IconEdit, IconTrash, IconStar, IconUsers, IconTrophy,
  IconHistory, IconUserPlus, IconCheck, IconX, IconMedal,
  IconChevronDown, IconChevronUp
} from '@tabler/icons-react'
import { notifications } from '@mantine/notifications'
import Menu from './Menu'
import {
  apiService, ClassificationLevel, UserClassification, User,
  CreateClassificationLevelRequest
} from '../services/apiService'
import './ClassificationsPage.css'

// ── Colour helper for level accent ──────────────────────────────────────────
const LEVEL_COLORS = ['#6366f1', '#8b5cf6', '#ec4899', '#f59e0b', '#10b981', '#3b82f6']
const levelColor = (id: number) => LEVEL_COLORS[id % LEVEL_COLORS.length]

// ── Date helpers ─────────────────────────────────────────────────────────────
const fmtDate = (d: string | null | undefined) =>
  d ? new Date(d).toLocaleDateString('pt-BR', { day: '2-digit', month: '2-digit', year: 'numeric' }) : '—'

const todayISO = () => new Date().toISOString().split('T')[0]

// ────────────────────────────────────────────────────────────────────────────
const ClassificationsPage: React.FC = () => {
  const [levels, setLevels] = useState<ClassificationLevel[]>([])
  const [allUsers, setAllUsers] = useState<User[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')

  // Level form
  const [showLevelForm, setShowLevelForm] = useState(false)
  const [editingLevel, setEditingLevel] = useState<ClassificationLevel | null>(null)
  const [saving, setSaving] = useState(false)
  const [deleteConfirm, setDeleteConfirm] = useState<number | null>(null)

  // Form fields
  const [fName, setFName] = useState('')
  const [fDesc, setFDesc] = useState('')
  const [fPrize, setFPrize] = useState('')
  const [fGoal, setFGoal] = useState<number | string>('')
  const [formError, setFormError] = useState('')

  // Members / assign modal
  const [membersLevel, setMembersLevel] = useState<ClassificationLevel | null>(null)
  const [levelMembers, setLevelMembers] = useState<UserClassification[]>([])
  const [inactiveLevelMembers, setInactiveLevelMembers] = useState<UserClassification[]>([])
  const [inactiveCollapsed, setInactiveCollapsed] = useState(true)
  const [membersLoading, setMembersLoading] = useState(false)

  // Assign multiple users states inside members modal
  const [selectedUserIds, setSelectedUserIds] = useState<string[]>([])
  const [assignStart, setAssignStart] = useState(todayISO())
  const [assignEnd, setAssignEnd] = useState('')
  const [assigning, setAssigning] = useState(false)
  const [userSearch, setUserSearch] = useState('')

  // History modal
  const [historyUser, setHistoryUser] = useState<User | null>(null)
  const [history, setHistory] = useState<UserClassification[]>([])
  const [historyLoading, setHistoryLoading] = useState(false)

  // ── Fetches ──────────────────────────────────────────────────────────────
  const fetchLevels = useCallback(async () => {
    try {
      setLoading(true)
      setError('')
      const res = await apiService.getClassificationLevels()
      if (res.success && res.data) setLevels(res.data)
    } catch (e: any) {
      setError(e.message || 'Erro ao carregar níveis')
    } finally {
      setLoading(false)
    }
  }, [])

  const fetchUsers = useCallback(async () => {
    try {
      const res = await apiService.getUsers(1, 1000)
      if (res.success && res.data) setAllUsers(res.data.items.filter(u => u.isActive))
    } catch { /* silent */ }
  }, [])

  useEffect(() => {
    fetchLevels()
    fetchUsers()
  }, [fetchLevels, fetchUsers])

  // ── Level form helpers ───────────────────────────────────────────────────
  const openCreate = () => {
    setEditingLevel(null)
    setFName(''); setFDesc(''); setFPrize(''); setFGoal(''); setFormError('')
    setShowLevelForm(true)
  }

  const openEdit = (level: ClassificationLevel) => {
    setEditingLevel(level)
    setFName(level.name)
    setFDesc(level.description ?? '')
    setFPrize(level.prize ?? '')
    setFGoal(level.salesGoal ?? '')
    setFormError('')
    setShowLevelForm(true)
  }

  const handleSaveLevel = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!fName.trim()) { setFormError('Nome é obrigatório'); return }
    setSaving(true); setFormError('')
    const payload: CreateClassificationLevelRequest = {
      name: fName.trim(),
      description: fDesc.trim() || undefined,
      prize: fPrize.trim() || undefined,
      salesGoal: fGoal !== '' ? Number(fGoal) : undefined
    }
    try {
      if (editingLevel) {
        await apiService.updateClassificationLevel(editingLevel.id, payload)
        notifications.show({ title: 'Nível atualizado', message: fName, color: 'green', icon: <IconCheck size={16} /> })
      } else {
        await apiService.createClassificationLevel(payload)
        notifications.show({ title: 'Nível criado', message: fName, color: 'green', icon: <IconCheck size={16} /> })
      }
      setShowLevelForm(false)
      fetchLevels()
    } catch (e: any) {
      setFormError(e.message || 'Erro ao salvar')
    } finally {
      setSaving(false)
    }
  }

  const handleDelete = async (id: number) => {
    try {
      await apiService.deleteClassificationLevel(id)
      notifications.show({ title: 'Nível excluído', message: '', color: 'green', icon: <IconCheck size={16} /> })
      setDeleteConfirm(null)
      fetchLevels()
    } catch (e: any) {
      notifications.show({ title: 'Erro', message: e.message || 'Não foi possível excluir', color: 'red', icon: <IconX size={16} /> })
    }
  }

  // ── Members modal ────────────────────────────────────────────────────────
  const openMembers = async (level: ClassificationLevel) => {
    setMembersLevel(level)
    setSelectedUserIds([])
    setUserSearch('')
    setInactiveCollapsed(true)
    setMembersLoading(true)
    try {
      const res = await apiService.getClassificationLevels() // reload for count
      if (res.success && res.data) setLevels(res.data)
      
      const membersRes = await apiService.getLevelMembers(level.id)
      if (membersRes.success && membersRes.data) {
        const activeMembers = membersRes.data.filter(m => m.isActive)
        const inactiveMembers = membersRes.data.filter(m => !m.isActive)
        setLevelMembers(activeMembers)
        setInactiveLevelMembers(inactiveMembers)
      }
    } catch { /* silent */ } finally {
      setMembersLoading(false)
    }
  }

  const handleRemoveMember = async (assignmentId: number) => {
    try {
      await apiService.removeUserClassification(assignmentId)
      notifications.show({ title: 'Usuário removido do nível', message: '', color: 'orange', icon: <IconCheck size={16} /> })
      openMembers(membersLevel!)
    } catch (e: any) {
      notifications.show({ title: 'Erro', message: e.message, color: 'red', icon: <IconX size={16} /> })
    }
  }

  const handleAssignBulk = async (e: React.FormEvent) => {
    e.preventDefault()
    if (selectedUserIds.length === 0) return
    setAssigning(true)
    try {
      await Promise.all(
        selectedUserIds.map(userId =>
          apiService.assignUserLevel({
            userId,
            levelId: membersLevel!.id,
            startDate: new Date(assignStart).toISOString(),
            endDate: assignEnd ? new Date(assignEnd).toISOString() : null
          })
        )
      )
      notifications.show({
        title: 'Níveis atribuídos!',
        message: `${selectedUserIds.length} usuários foram classificados com sucesso.`,
        color: 'green',
        icon: <IconCheck size={16} />
      })
      setSelectedUserIds([])
      openMembers(membersLevel!)
      fetchLevels()
    } catch (e: any) {
      notifications.show({ title: 'Erro na atribuição', message: e.message, color: 'red', icon: <IconX size={16} /> })
    } finally {
      setAssigning(false)
    }
  }

  const toggleUserSelection = (userId: string) => {
    setSelectedUserIds(prev =>
      prev.includes(userId) ? prev.filter(id => id !== userId) : [...prev, userId]
    )
  }

  // ── History modal ────────────────────────────────────────────────────────
  const openHistory = async (user: User) => {
    setHistoryUser(user)
    setHistoryLoading(true)
    setHistory([])
    try {
      const res = await apiService.getUserClassificationHistory(user.id)
      if (res.success && res.data) setHistory(res.data)
    } catch { /* silent */ } finally {
      setHistoryLoading(false)
    }
  }

  // ── Filtered users for assign panel ──────────────────────────────────────
  const memberIds = new Set(levelMembers.map(m => m.userId))
  const filteredUsers = allUsers
    .filter(u => !memberIds.has(u.id))
    .filter(u =>
      !userSearch ||
      u.name.toLowerCase().includes(userSearch.toLowerCase()) ||
      u.email.toLowerCase().includes(userSearch.toLowerCase())
    )

  // ── Order levels by salesGoal ASCENDING ──────────────────────────────
  const sortedLevels = [...levels].sort((a, b) => {
    const goalA = a.salesGoal ?? 0
    const goalB = b.salesGoal ?? 0
    return goalA - goalB
  })

  // ── Render ───────────────────────────────────────────────────────────────
  return (
    <Menu>
      <div className="cls-container">
        {/* Header */}
        <div className="cls-header">
          <div>
            <Title order={2}>Níveis de Classificação</Title>
            <p className="cls-subtitle">Gerencie os níveis de performance e atribua usuários a cada categoria.</p>
          </div>
          <Button leftSection={<IconPlus size={16} />} onClick={openCreate}>
            Novo Nível
          </Button>
        </div>

        {error && <div className="error-banner" style={{ marginBottom: 20 }}>{error}</div>}

        {/* Level Cards */}
        {loading ? (
          <div className="loading-container"><div className="spinner" /><p>Carregando níveis...</p></div>
        ) : sortedLevels.length === 0 ? (
          <div className="empty-state">
            <IconMedal size={40} color="#d1d5db" />
            <p>Nenhum nível cadastrado. Crie o primeiro!</p>
          </div>
        ) : (
          <div className="cls-levels-grid">
            {sortedLevels.map(level => (
              <div key={level.id} className="cls-level-card">
                <div className="cls-level-card__accent" style={{ background: `linear-gradient(90deg, ${levelColor(level.id)}, ${levelColor(level.id + 2)})` }} />
                <div className="cls-level-card__header">
                  <h3 className="cls-level-card__name">
                    <IconTrophy size={16} color={levelColor(level.id)} style={{ marginRight: 6, verticalAlign: 'middle' }} />
                    {level.name}
                  </h3>
                </div>
                {level.description && <p className="cls-level-card__desc">{level.description}</p>}
                <div className="cls-level-card__stats">
                  <Badge color="indigo" variant="light" leftSection={<IconUsers size={12} />}>
                    {level.activeUsersCount} ativo{level.activeUsersCount !== 1 ? 's' : ''}
                  </Badge>
                  {level.prize && (
                    <Badge color="yellow" variant="light" leftSection={<IconStar size={12} />}>
                      {level.prize}
                    </Badge>
                  )}
                  {level.salesGoal != null && (
                    <Badge color="teal" variant="light">
                      Meta: R$ {level.salesGoal.toLocaleString('pt-BR')}
                    </Badge>
                  )}
                </div>
                <div className="cls-level-card__actions">
                  <Tooltip label="Gerenciar membros" withArrow position="top">
                    <ActionIcon variant="light" color="indigo" onClick={() => openMembers(level)}>
                      <IconUsers size={16} />
                    </ActionIcon>
                  </Tooltip>
                  <Tooltip label="Editar nível" withArrow position="top">
                    <ActionIcon variant="light" color="blue" onClick={() => openEdit(level)}>
                      <IconEdit size={16} />
                    </ActionIcon>
                  </Tooltip>
                  <Tooltip label="Excluir nível" withArrow position="top">
                    <ActionIcon variant="light" color="red" onClick={() => setDeleteConfirm(level.id)}>
                      <IconTrash size={16} />
                    </ActionIcon>
                  </Tooltip>
                </div>
              </div>
            ))}
          </div>
        )}

        {/* ── Level Create/Edit Modal (Standard Light) ───────────────────────── */}
        {showLevelForm && (
          <Modal
            opened={showLevelForm}
            onClose={() => setShowLevelForm(false)}
            title={
              <Title order={3} style={{ color: '#1c1c1e', fontWeight: 700 }}>
                {editingLevel ? `Editar: ${editingLevel.name}` : 'Novo Nível de Classificação'}
              </Title>
            }
            size="md"
            styles={{
              header: { backgroundColor: '#ffffff', borderBottom: '1px solid #e9ecef', padding: '20px 24px' },
              body: { backgroundColor: '#ffffff', padding: '24px' },
              content: { borderRadius: '12px', boxShadow: '0 20px 60px rgba(0,0,0,0.12)' },
            }}
          >
            <form onSubmit={handleSaveLevel}>
              {formError && <div style={{ color: '#ef4444', marginBottom: 14, fontWeight: 500 }}>{formError}</div>}
              <Stack gap="md">
                <TextInput
                  label="Nome do Nível"
                  required
                  placeholder="Ex: Prata, Ouro, Estrela..."
                  value={fName}
                  onChange={e => setFName(e.target.value)}
                />
                <Textarea
                  label="Descrição"
                  placeholder="Descrição opcional do nível..."
                  value={fDesc}
                  onChange={e => setFDesc(e.target.value)}
                  rows={3}
                />
                <TextInput
                  label="Prêmio / Benefício"
                  placeholder="Ex: Bônus de R$ 500, Viagem..."
                  value={fPrize}
                  onChange={e => setFPrize(e.target.value)}
                  leftSection={<IconStar size={14} />}
                />
                <NumberInput
                  label="Meta de Vendas (R$)"
                  placeholder="Ex: 50000"
                  value={fGoal}
                  onChange={setFGoal}
                  min={0}
                  decimalScale={2}
                  prefix="R$ "
                  thousandSeparator="."
                  decimalSeparator=","
                />
                <Group justify="flex-end" pt="md" style={{ borderTop: '1px solid #e5e7eb' }}>
                  <Button variant="subtle" color="gray" onClick={() => setShowLevelForm(false)} disabled={saving}>
                    Cancelar
                  </Button>
                  <Button type="submit" loading={saving}>
                    {editingLevel ? 'Salvar Alterações' : 'Criar Nível'}
                  </Button>
                </Group>
              </Stack>
            </form>
          </Modal>
        )}

        {/* ── Delete Confirmation (Standard Light) ───────────────────────────── */}
        {deleteConfirm !== null && (
          <Modal
            opened={deleteConfirm !== null}
            onClose={() => setDeleteConfirm(null)}
            title={
              <Title order={3} style={{ color: '#1c1c1e', fontWeight: 700 }}>
                Confirmar Exclusão
              </Title>
            }
            size="sm"
            styles={{
              header: { backgroundColor: '#ffffff', borderBottom: '1px solid #e9ecef', padding: '20px 24px' },
              body: { backgroundColor: '#ffffff', padding: '24px' },
              content: { borderRadius: '12px', boxShadow: '0 20px 60px rgba(0,0,0,0.12)' },
            }}
          >
            <Text size="sm">Tem certeza que deseja excluir este nível? Níveis associados a usuários não podem ser excluídos.</Text>
            <Group justify="flex-end" mt="xl" style={{ borderTop: '1px solid #e9ecef', paddingTop: 16 }}>
              <Button variant="subtle" color="gray" onClick={() => setDeleteConfirm(null)}>Cancelar</Button>
              <Button color="red" onClick={() => handleDelete(deleteConfirm)}>Excluir</Button>
            </Group>
          </Modal>
        )}

        {/* ── Members Modal (Two-Column Split, Always Shows User List, Multi-Select) ── */}
        {membersLevel && (
          <Modal
            opened={!!membersLevel}
            onClose={() => { setMembersLevel(null); setLevelMembers([]); setSelectedUserIds([]) }}
            title={
              <Group gap="xs">
                <IconTrophy size={20} color={levelColor(membersLevel.id)} />
                <Text fw={700} size="lg" style={{ color: '#1c1c1e' }}>{membersLevel.name}</Text>
                <Badge color="indigo" size="sm">{levelMembers.length} membros</Badge>
              </Group>
            }
            size="xl"
            styles={{
              header: { backgroundColor: '#ffffff', borderBottom: '1px solid #e9ecef', padding: '20px 28px' },
              body: { backgroundColor: '#ffffff', padding: '24px 28px' },
              content: { borderRadius: '12px', boxShadow: '0 20px 60px rgba(0,0,0,0.12)' },
            }}
          >
            <div className="cls-modal-grid">
              
              {/* Left Column: Active Members */}
              <div className="cls-modal-col">
                <Title order={5} style={{ color: '#374151', display: 'flex', alignItems: 'center', gap: 6 }}>
                  <IconUsers size={18} color="#6366f1" /> Membros Ativos
                </Title>
                <Divider />
                
                {membersLoading ? (
                  <Group justify="center" p="md"><Loader size="sm" /></Group>
                ) : levelMembers.length === 0 ? (
                  <Text size="sm" c="dimmed" ta="center" py="xl">Nenhum membro ativo neste nível</Text>
                ) : (
                  <ScrollArea h={inactiveCollapsed ? 340 : 200}>
                    <Stack gap={8}>
                      {levelMembers.map(m => (
                        <div key={m.id} className="cls-member-card">
                          <div className="cls-member-card__info">
                            <div className="cls-member-card__name">{m.userName}</div>
                            <div className="cls-member-card__email">{m.userEmail}</div>
                            <div className="cls-member-card__dates">
                              Desde {fmtDate(m.startDate)} {m.endDate ? `até ${fmtDate(m.endDate)}` : '(sem data de fim)'}
                            </div>
                          </div>
                          <Tooltip label="Ver histórico" withArrow>
                            <ActionIcon variant="subtle" color="indigo" size="sm"
                              onClick={() => { const user = allUsers.find(u => u.id === m.userId); if (user) openHistory(user) }}>
                              <IconHistory size={14} />
                            </ActionIcon>
                          </Tooltip>
                          <Tooltip label="Remover do nível" withArrow>
                            <ActionIcon variant="subtle" color="red" size="sm" onClick={() => handleRemoveMember(m.id)}>
                              <IconX size={14} />
                            </ActionIcon>
                          </Tooltip>
                        </div>
                      ))}
                    </Stack>
                  </ScrollArea>
                )}

                {/* Collapsible Membros Inativos Section */}
                <Stack gap="xs" mt="md">
                  <Group
                    justify="space-between"
                    style={{ cursor: 'pointer', padding: '4px 0' }}
                    onClick={() => setInactiveCollapsed(!inactiveCollapsed)}
                  >
                    <Title order={5} style={{ color: '#6b7280', display: 'flex', alignItems: 'center', gap: 6 }}>
                      <IconUsers size={18} color="#9ca3af" /> Membros Inativos ({inactiveLevelMembers.length})
                    </Title>
                    {inactiveCollapsed ? <IconChevronDown size={16} color="#9ca3af" /> : <IconChevronUp size={16} color="#9ca3af" />}
                  </Group>
                  <Divider />
                  
                  {!inactiveCollapsed && (
                    membersLoading ? (
                      <Group justify="center" p="md"><Loader size="sm" /></Group>
                    ) : inactiveLevelMembers.length === 0 ? (
                      <Text size="sm" c="dimmed" ta="center" py="xl">Nenhum membro inativo neste nível</Text>
                    ) : (
                      <ScrollArea h={180}>
                        <Stack gap={8}>
                          {inactiveLevelMembers.map(m => (
                            <div key={m.id} className="cls-member-card inactive">
                              <div className="cls-member-card__info" style={{ opacity: 0.7 }}>
                                <div className="cls-member-card__name">{m.userName}</div>
                                <div className="cls-member-card__email">{m.userEmail}</div>
                                <div className="cls-member-card__dates">
                                  Período: {fmtDate(m.startDate)} até {fmtDate(m.endDate)}
                                </div>
                              </div>
                              <Tooltip label="Ver histórico" withArrow>
                                <ActionIcon variant="subtle" color="gray" size="sm"
                                  onClick={() => { const user = allUsers.find(u => u.id === m.userId); if (user) openHistory(user) }}>
                                  <IconHistory size={14} />
                                </ActionIcon>
                              </Tooltip>
                            </div>
                          ))}
                        </Stack>
                      </ScrollArea>
                    )
                  )}
                </Stack>
              </div>

              {/* Right Column: Multi-Select Assignment */}
              <div className="cls-modal-col">
                <Title order={5} style={{ color: '#374151', display: 'flex', alignItems: 'center', gap: 6 }}>
                  <IconUserPlus size={18} color="#10b981" /> Atribuir Novos Membros
                </Title>
                <Divider />
                
                <form onSubmit={handleAssignBulk}>
                  <Stack gap="sm">
                    <TextInput
                      placeholder="Buscar usuário..."
                      value={userSearch}
                      onChange={e => setUserSearch(e.target.value)}
                      size="sm"
                    />
                    
                    <ScrollArea h={320} offsetScrollbars>
                      <Stack gap={6}>
                        {filteredUsers.map(u => {
                          const isSelected = selectedUserIds.includes(u.id)
                          return (
                            <div
                              key={u.id}
                              className={`cls-select-user-card ${isSelected ? 'selected' : ''}`}
                              onClick={() => toggleUserSelection(u.id)}
                            >
                              <Checkbox
                                checked={isSelected}
                                onChange={() => {}} // toggled by outer click
                                tabIndex={-1}
                                color="indigo"
                              />
                              <div className="cls-member-card__info">
                                <div className="cls-member-card__name">{u.name}</div>
                                <div className="cls-member-card__email">{u.email}</div>
                                {u.currentLevelName && (
                                  <Badge size="xs" color="indigo" variant="light" mt={2}>
                                    Nível atual: {u.currentLevelName}
                                  </Badge>
                                )}
                              </div>
                            </div>
                          )
                        })}
                        {filteredUsers.length === 0 && (
                          <Text size="sm" c="dimmed" ta="center" py="md">Nenhum usuário disponível</Text>
                        )}
                      </Stack>
                    </ScrollArea>

                    <Divider />

                    <Group grow>
                      <div>
                        <Text size="xs" fw={600} mb={4} c="dimmed">Data de Início *</Text>
                        <input
                          type="date"
                          required
                          value={assignStart}
                          onChange={e => setAssignStart(e.target.value)}
                          className="member-date-picker-input"
                          style={{ width: '100%' }}
                        />
                      </div>
                      <div>
                        <Text size="xs" fw={600} mb={4} c="dimmed">Data de Fim (opcional)</Text>
                        <input
                          type="date"
                          value={assignEnd}
                          onChange={e => setAssignEnd(e.target.value)}
                          className="member-date-picker-input"
                          style={{ width: '100%' }}
                        />
                      </div>
                    </Group>

                    <Button
                      type="submit"
                      color="indigo"
                      loading={assigning}
                      disabled={selectedUserIds.length === 0}
                      fullWidth
                    >
                      {selectedUserIds.length === 0
                        ? 'Selecione Usuários para Atribuir'
                        : `Atribuir Nível a ${selectedUserIds.length} usuário(s)`}
                    </Button>
                  </Stack>
                </form>
              </div>
              
            </div>
          </Modal>
        )}

        {/* ── History Modal ───────────────────────────────────────────────────── */}
        {historyUser && (
          <Modal
            opened={!!historyUser}
            onClose={() => { setHistoryUser(null); setHistory([]) }}
            title={
              <Group gap="xs">
                <IconHistory size={20} color="#6366f1" />
                <Text fw={700} style={{ color: '#1c1c1e' }}>Histórico de Níveis — {historyUser.name}</Text>
              </Group>
            }
            size="lg"
            styles={{
              header: { backgroundColor: '#ffffff', borderBottom: '1px solid #e9ecef', padding: '20px 28px' },
              body: { backgroundColor: '#ffffff', padding: '24px 28px' },
              content: { borderRadius: '12px', boxShadow: '0 20px 60px rgba(0,0,0,0.12)' },
            }}
          >
            {historyLoading ? (
              <Group justify="center" p="xl"><Loader /></Group>
            ) : history.length === 0 ? (
              <Text c="dimmed" ta="center" py="xl">Nenhum nível atribuído ainda.</Text>
            ) : (
              <Stack gap={8}>
                {history.map(h => (
                  <div key={h.id} className={`cls-history-row ${h.isActive ? 'active' : ''}`}>
                    <div style={{ flex: 1 }}>
                      <Group gap={8}>
                        <Badge
                          color={h.isActive ? 'green' : 'gray'}
                          variant={h.isActive ? 'filled' : 'outline'}
                          leftSection={h.isActive ? <IconCheck size={10} /> : undefined}
                        >
                          {h.levelName}
                        </Badge>
                        {h.isActive && <Badge size="xs" color="green" variant="light">Atual</Badge>}
                      </Group>
                      <Text size="xs" c="dimmed" mt={4}>
                        {fmtDate(h.startDate)} → {h.endDate ? fmtDate(h.endDate) : 'presente'}
                      </Text>
                    </div>
                    {h.levelPrize && (
                      <Badge color="yellow" variant="light" size="xs" leftSection={<IconStar size={10} />}>
                        {h.levelPrize}
                      </Badge>
                    )}
                  </div>
                ))}
              </Stack>
            )}
          </Modal>
        )}
      </div>
    </Menu>
  )
}

export default ClassificationsPage
