import React, { useState, useEffect, useCallback } from "react"
import { Title, Button, Card, ActionIcon, Group, Badge, Text, TextInput, MultiSelect, Select, Alert, Stack, Table } from '@mantine/core';
import { IconEdit, IconTrash, IconPlus, IconAlertTriangle, IconUser, IconCrown, IconTrashX, IconUsers } from '@tabler/icons-react';
import Menu from "./Menu"
import StyledModal from './StyledModal';
import FormField from './FormField';
import { apiService, Team, User } from "../services/apiService"
import "./TeamsPage.css"

const TeamsPage: React.FC = () => {
  const [teams, setTeams] = useState<Team[]>([])
  const [allUsers, setAllUsers] = useState<User[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState("")
  const [warnings, setWarnings] = useState<string[]>([])
  const [showForm, setShowForm] = useState(false)
  const [editingTeam, setEditingTeam] = useState<Team | null>(null)
  
  // Form states
  const [teamName, setTeamName] = useState("")
  const [selectedMemberIds, setSelectedMemberIds] = useState<string[]>([])
  const [memberStartDates, setMemberStartDates] = useState<Record<string, string>>({})
  const [ownerUserId, setOwnerUserId] = useState<string>("")
  const [saving, setSaving] = useState(false)
  const [deleteConfirm, setDeleteConfirm] = useState<number | null>(null)
  const [search, setSearch] = useState("")

  // Fetch teams
  const fetchTeams = useCallback(async () => {
    try {
      setLoading(true)
      setError("")
      const response = await apiService.getTeams()
      if (response.success && response.data) {
        setTeams(response.data)
      }
    } catch (err: any) {
      setError(err.message || "Falha ao carregar equipes")
    } finally {
      setLoading(false)
    }
  }, [])

  // Fetch all users once for member selection
  const fetchUsers = useCallback(async () => {
    try {
      const response = await apiService.getUsers(1, 1000)
      if (response.success && response.data) {
        // Filter out inactive users
        setAllUsers(response.data.items.filter(u => u.isActive))
      }
    } catch (err) {
      console.error("Falha ao carregar usuários para membros", err)
    }
  }, [])

  useEffect(() => {
    fetchTeams()
    fetchUsers()
  }, [fetchTeams, fetchUsers])

  // Get 8 years ago today in YYYY-MM-DD
  const getEightYearsAgoDateString = () => {
    const d = new Date()
    d.setFullYear(d.getFullYear() - 8)
    return d.toISOString().split('T')[0]
  }

  // Open Form for Create
  const openCreateForm = () => {
    setEditingTeam(null)
    setTeamName("")
    setSelectedMemberIds([])
    setMemberStartDates({})
    setOwnerUserId("")
    setError("")
    setShowForm(true)
  }

  // Open Form for Edit
  const openEditForm = (team: Team) => {
    setEditingTeam(team)
    setTeamName(team.name)
    
    // Set members Guid strings
    const activeMembers = team.members.filter(m => m.isActive)
    const memberIds = activeMembers.map(m => m.userId)
    setSelectedMemberIds(memberIds)
    
    // Set member start dates mapped by Guid
    const dates: Record<string, string> = {}
    activeMembers.forEach(m => {
      dates[m.userId] = m.startDate.split('T')[0]
    })
    setMemberStartDates(dates)

    // Set owner Guid if exists and is active member
    const activeOwner = activeMembers.find(m => m.isOwner)
    setOwnerUserId(activeOwner ? activeOwner.userId : "")
    
    setError("")
    setShowForm(true)
  }

  // Handle MultiSelect Change
  const handleMembersSelectChange = (values: string[]) => {
    setSelectedMemberIds(values)
    
    // Initialize default start dates for newly selected members (8 years ago today)
    const updatedDates = { ...memberStartDates }
    values.forEach(id => {
      if (!updatedDates[id]) {
        updatedDates[id] = getEightYearsAgoDateString()
      }
    })
    setMemberStartDates(updatedDates)

    // If owner was unselected, clear owner selection
    if (ownerUserId && !values.includes(ownerUserId)) {
      setOwnerUserId("")
    }
  }

  // Remove a member from local state list
  const removeLocalMember = (userId: string) => {
    const updated = selectedMemberIds.filter(id => id !== userId)
    setSelectedMemberIds(updated)
    if (ownerUserId === userId) {
      setOwnerUserId("")
    }
  }

  // Set date for member in local state
  const handleMemberDateChange = (userId: string, dateString: string) => {
    setMemberStartDates(prev => ({
      ...prev,
      [userId]: dateString
    }))
  }

  // Submit create or edit
  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!teamName.trim()) {
      setError("O nome da equipe é obrigatório")
      return
    }

    setSaving(true)
    setError("")
    setWarnings([])

    try {
      const payloadMembers = selectedMemberIds.map(userId => ({
        userId,
        startDate: memberStartDates[userId]
      }))

      if (editingTeam) {
        // 1. Update team basic info
        await apiService.updateTeam(editingTeam.id, teamName.trim())

        // 2. Refresh members list using set-members/add-members endpoint
        // To update completely, let's remove any members who are no longer selected
        const currentActiveMemberIds = editingTeam.members.filter(m => m.isActive).map(m => m.userId)
        const toRemove = currentActiveMemberIds.filter(id => !selectedMemberIds.includes(id))
        
        for (const userId of toRemove) {
          await apiService.removeTeamMember(editingTeam.id, userId)
        }

        // Add/Update current members
        const addResponse = await apiService.addTeamMembers(editingTeam.id, payloadMembers)

        // 3. Set Owner if selected
        if (ownerUserId) {
          await apiService.setTeamOwner(editingTeam.id, ownerUserId)
        } else {
          // If owner cleared, unset it
          await apiService.updateTeam(editingTeam.id, undefined, undefined)
        }

        if (addResponse.success && addResponse.data) {
          if (addResponse.data.warnings && addResponse.data.warnings.length > 0) {
            setWarnings(addResponse.data.warnings)
          }
        }
      } else {
        // Create Team
        const response = await apiService.createTeam(teamName.trim(), payloadMembers)
        
        if (response.success && response.data) {
          // Set Owner if selected (since team is created)
          if (ownerUserId) {
            await apiService.setTeamOwner(response.data.id, ownerUserId)
          }
          if (response.data.warnings && response.data.warnings.length > 0) {
            setWarnings(response.data.warnings)
          }
        }
      }

      setShowForm(false)
      fetchTeams()
    } catch (err: any) {
      setError(err.message || "Erro ao salvar equipe")
    } finally {
      setSaving(false)
    }
  }

  // Delete team
  const handleDeleteTeam = async (id: number) => {
    try {
      setError("")
      await apiService.deleteTeam(id)
      setDeleteConfirm(null)
      fetchTeams()
    } catch (err: any) {
      setError(err.message || "Falha ao excluir equipe")
    }
  }

  // Render member options for MultiSelect
  const userOptions = allUsers.map(user => ({
    value: user.id,
    label: `${user.name} (${user.email})`
  }))

  const formatDate = (dateString: string) => {
    return new Date(dateString).toLocaleDateString("pt-BR", {
      day: "2-digit",
      month: "2-digit",
      year: "numeric",
    })
  }

  // Filter teams based on search input
  const filteredTeams = teams.filter((team) => {
    const searchLower = search.toLowerCase();
    const nameMatch = team.name.toLowerCase().includes(searchLower);
    const ownerMatch = team.owner ? team.owner.userName.toLowerCase().includes(searchLower) : false;
    const memberMatch = team.members.some((m) => m.userName.toLowerCase().includes(searchLower));
    return nameMatch || ownerMatch || memberMatch;
  });

  return (
    <Menu>
      <div className="teams-container">
        <div className="teams-header">
          <div>
            <Title order={2} size="h2">Gerenciamento de Equipes (Equipes)</Title>
            <p className="teams-subtitle">
              Configure e gerencie as equipes de vendas e seus respectivos membros.
            </p>
          </div>
          <Button onClick={openCreateForm} leftSection={<IconPlus size={16} />}>
            Nova Equipe
          </Button>
        </div>

        {error && <div className="error-banner">{error}</div>}

        {warnings.length > 0 && (
          <Alert 
            icon={<IconAlertTriangle size={16} />} 
            title="Conflitos de Associação Resolvidos!" 
            color="yellow" 
            variant="filled"
            withCloseButton 
            onClose={() => setWarnings([])}
            mb="lg"
            styles={{
              root: { border: '1px solid rgba(253, 224, 71, 0.4)' },
              title: { fontWeight: 700 }
            }}
          >
            <Stack gap="xs">
              <Text size="sm">
                Os seguintes usuários foram removidos de suas equipes anteriores porque foram associados a um novo período sobreposto:
              </Text>
              {warnings.map((warn, i) => (
                <Badge key={i} color="dark" size="md" radius="sm" style={{ alignSelf: 'flex-start', color: '#fef08a' }}>
                  • {warn}
                </Badge>
              ))}
            </Stack>
          </Alert>
        )}

        <div className="search-container" style={{ marginBottom: '20px' }}>
          <TextInput
            placeholder="Buscar por equipe, proprietário ou membro..."
            value={search}
            onChange={(e) => setSearch(e.currentTarget.value)}
            style={{ maxWidth: 400 }}
          />
        </div>

        {loading ? (
          <div className="loading-container">
            <div className="spinner"></div>
            <p>Carregando equipes...</p>
          </div>
        ) : filteredTeams.length === 0 ? (
          <div className="empty-state">
            <p>Nenhuma equipe encontrada para a sua busca ou cadastrada.</p>
          </div>
        ) : (
          <div className="table-container">
            <Table.ScrollContainer minWidth={800}>
              <Table striped highlightOnHover>
                <Table.Thead>
                  <Table.Tr>
                    <Table.Th>Nome da Equipe</Table.Th>
                    <Table.Th>Proprietário (Owner)</Table.Th>
                    <Table.Th>Membros</Table.Th>
                    <Table.Th>Criado em</Table.Th>
                    <Table.Th>Ações</Table.Th>
                  </Table.Tr>
                </Table.Thead>
                <Table.Tbody>
                  {filteredTeams.map((team) => {
                    const activeMembers = team.members.filter(m => m.isActive)
                    return (
                      <Table.Tr key={team.id}>
                        <Table.Td style={{ fontWeight: 600, fontSize: '15px' }}>{team.name}</Table.Td>
                        <Table.Td>
                          {team.owner ? (
                            <Group gap="xs">
                              <Badge color="yellow" variant="light" leftSection={<IconCrown size={12} />}>
                                {team.owner.userName}
                              </Badge>
                              <Text size="xs" c="dimmed">({team.owner.userEmail})</Text>
                            </Group>
                          ) : (
                            <Text size="sm" c="dimmed" style={{ fontStyle: 'italic' }}>Sem Proprietário</Text>
                          )}
                        </Table.Td>
                        <Table.Td>
                          <Group gap="xs">
                            <Badge color="blue" size="md">
                              {activeMembers.length} membros
                            </Badge>
                            {activeMembers.length > 0 && (
                              <Text size="xs" c="dimmed" truncate style={{ maxWidth: '300px' }}>
                                {activeMembers.map(m => m.userName).join(', ')}
                              </Text>
                            )}
                          </Group>
                        </Table.Td>
                        <Table.Td>{formatDate(team.createdAt)}</Table.Td>
                        <Table.Td>
                          <Group gap="xs">
                            <ActionIcon
                              variant="subtle"
                              color="blue"
                              onClick={() => openEditForm(team)}
                              title="Editar"
                            >
                              <IconEdit size={16} />
                            </ActionIcon>
                            <ActionIcon
                              variant="subtle"
                              color="red"
                              onClick={() => setDeleteConfirm(team.id)}
                              title="Excluir"
                            >
                              <IconTrash size={16} />
                            </ActionIcon>
                          </Group>
                        </Table.Td>
                      </Table.Tr>
                    )
                  })}
                </Table.Tbody>
              </Table>
            </Table.ScrollContainer>
          </div>
        )}

        {/* Create/Edit Team Modal */}
        {showForm && (
          <StyledModal
            opened={showForm}
            onClose={() => setShowForm(false)}
            title={editingTeam ? "Editar Equipe" : "Nova Equipe"}
            size="lg"
          >
            <form onSubmit={handleSubmit}>
              {error && <div style={{ color: '#ef4444', marginBottom: '1.25rem', fontWeight: 500 }}>{error}</div>}

              <FormField label="Nome da Equipe" required>
                <TextInput
                  required
                  placeholder="Ex: Equipe Fênix"
                  value={teamName}
                  onChange={(e) => setTeamName(e.target.value)}
                  size="md"
                />
              </FormField>

              <FormField 
                label="Selecionar Membros da Equipe" 
                description="Busque e selecione múltiplos usuários para pertencerem a esta equipe."
              >
                <MultiSelect
                  data={userOptions}
                  value={selectedMemberIds}
                  onChange={handleMembersSelectChange}
                  placeholder="Selecione usuários..."
                  searchable
                  clearable
                  nothingFoundMessage="Nenhum usuário encontrado"
                  size="md"
                />
              </FormField>

              {selectedMemberIds.length > 0 && (
                <div style={{ marginTop: '20px', borderTop: '1px solid #373a40', paddingTop: '20px' }}>
                  <Text size="sm" fw={600} mb="sm" style={{ color: '#fff' }}>
                    Configuração de Membros e Datas de Início
                  </Text>
                  
                  <div style={{ maxHeight: '350px', overflowY: 'auto', paddingRight: '4px' }} className="modal-table-scroll-container">
                    <table className="modal-members-table">
                      <thead>
                        <tr>
                          <th>Nome</th>
                          <th>Data de Início</th>
                          <th>Data de Fim</th>
                          <th style={{ textAlign: 'right' }}>Ações</th>
                        </tr>
                      </thead>
                      <tbody>
                        {selectedMemberIds.map(userId => {
                          const user = allUsers.find(u => u.id === userId)
                          if (!user) return null

                          const isSelectedOwner = ownerUserId === userId
                          const existingMember = editingTeam?.members.find(m => m.userId === userId)
                          const endDateString = existingMember?.endDate ? formatDate(existingMember.endDate) : "-"

                          return (
                            <tr key={userId} className={isSelectedOwner ? "is-owner-row" : ""}>
                              <td>
                                <div style={{ display: 'flex', flexDirection: 'column' }}>
                                  <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                                    <IconUser size={14} color={isSelectedOwner ? "#eab308" : "#9ca3af"} />
                                    <span style={{ fontWeight: 600, color: isSelectedOwner ? "#fef08a" : "#fff" }}>
                                      {user.name}
                                    </span>
                                    {isSelectedOwner && (
                                      <Badge color="yellow" size="xs" variant="filled" leftSection={<IconCrown size={8} />}>
                                        Dono
                                      </Badge>
                                    )}
                                  </div>
                                  <span style={{ fontSize: '11px', color: '#9ca3af', marginLeft: '22px' }}>
                                    {user.email}
                                  </span>
                                </div>
                              </td>
                              
                              <td>
                                <input
                                  type="date"
                                  required
                                  value={memberStartDates[userId] || getEightYearsAgoDateString()}
                                  onChange={(e) => handleMemberDateChange(userId, e.target.value)}
                                  className="member-date-picker-input"
                                />
                              </td>

                              <td style={{ color: isSelectedOwner ? "#fef08a" : "#9ca3af", fontWeight: 500 }}>
                                {endDateString}
                              </td>

                              <td>
                                <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'flex-end', gap: '8px' }}>
                                  <Button
                                    size="xs"
                                    variant={isSelectedOwner ? "filled" : "light"}
                                    color="yellow"
                                    leftSection={<IconCrown size={12} />}
                                    onClick={() => setOwnerUserId(isSelectedOwner ? "" : userId)}
                                    title={isSelectedOwner ? "Remover cargo de proprietário" : "Tornar proprietário"}
                                  >
                                    {isSelectedOwner ? "Dono" : "Tornar Dono"}
                                  </Button>

                                  <ActionIcon
                                    color="red"
                                    variant="subtle"
                                    onClick={() => removeLocalMember(userId)}
                                    title="Remover membro da equipe"
                                  >
                                    <IconTrashX size={18} />
                                  </ActionIcon>
                                </div>
                              </td>
                            </tr>
                          )
                        })}
                      </tbody>
                    </table>
                  </div>
                </div>
              )}

              <Group justify="flex-end" mt="xl" style={{ 
                paddingTop: '16px', 
                borderTop: '1px solid #373A40',
                marginTop: '24px'
              }}>
                <Button 
                  variant="subtle" 
                  onClick={() => setShowForm(false)} 
                  disabled={saving}
                  color="gray"
                  size="md"
                >
                  Cancelar
                </Button>
                <Button 
                  type="submit" 
                  loading={saving}
                  size="md"
                  style={{
                    fontWeight: 600,
                    textTransform: 'uppercase',
                    letterSpacing: '0.5px'
                  }}
                >
                  {editingTeam ? "Salvar Equipe" : "Criar Equipe"}
                </Button>
              </Group>
            </form>
          </StyledModal>
        )}

        {/* Delete Confirmation Modal */}
        {deleteConfirm !== null && (
          <StyledModal
            opened={deleteConfirm !== null}
            onClose={() => setDeleteConfirm(null)}
            title="Confirmar Exclusão"
            size="md"
          >
            <div style={{ padding: '10px 0' }}>
              <Text size="sm">
                Tem certeza que deseja excluir esta equipe? Esta ação é irreversível e desassociará todos os membros dela.
              </Text>
            </div>
            <Group justify="flex-end" mt="xl" style={{ paddingTop: '16px', borderTop: '1px solid #373A40' }}>
              <Button variant="subtle" color="gray" onClick={() => setDeleteConfirm(null)}>
                Cancelar
              </Button>
              <Button color="red" onClick={() => handleDeleteTeam(deleteConfirm)}>
                Excluir
              </Button>
            </Group>
          </StyledModal>
        )}
      </div>
    </Menu>
  )
}

export default TeamsPage
