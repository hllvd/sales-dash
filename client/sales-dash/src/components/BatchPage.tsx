import React, { useState, useEffect } from 'react'
import {
  Title, TextInput, Button, Switch, Group, Table, Text, Select, Loader, Tabs, Badge, Card, SimpleGrid, Textarea, Checkbox
} from '@mantine/core'
import { DatePickerInput } from '@mantine/dates'
import {
  IconUsers, IconWand, IconAlertTriangle, IconCheck, IconX, IconGitMerge, IconEye, IconUserOff, IconSearch, IconRefresh
} from '@tabler/icons-react'
import { notifications } from '@mantine/notifications'
import Menu from './Menu'
import { useCurrentUser } from '../contexts/CurrentUserContext'
import { useReferenceData } from '../contexts/ReferenceDataContext'
import {
  apiService, BatchUpdateParentResult, BatchAssignTeamResult, BatchAssignTeamRequest,
  MergeUserPair, MergeUsersRequest, MergeUsersResult,
  MergeMatriculaPair, MergeMatriculasRequest, MergeMatriculasResult,
  UnassignedUser, BatchAssignUnassignedResult
} from '../services/apiService'
import './BatchPage.css'

const BatchPage: React.FC = () => {
  const { currentUser, loading: loadingUser } = useCurrentUser()
  const { fetchTeams } = useReferenceData()
  
  // Tab 1: Parent Email Update
  const [parentEmail, setParentEmail] = useState('')
  const [teamId, setTeamId] = useState<string | null>(null)
  const [matricula, setMatricula] = useState('')
  const [overrideExisting, setOverrideExisting] = useState(false)
  const [result, setResult] = useState<BatchUpdateParentResult | null>(null)

  // Tab 2: Team Assignment
  const [assignParentEmail, setAssignParentEmail] = useState('')
  const [assignMatricula, setAssignMatricula] = useState('')
  const [assignTeamId, setAssignTeamId] = useState<string | null>(null)
  const [startDate, setStartDate] = useState<Date | null>(new Date())
  const [overrideExistingTeam, setOverrideExistingTeam] = useState(false)
  const [assignResult, setAssignResult] = useState<BatchAssignTeamResult | null>(null)

  // Tab 3: Merge Users
  const [mergeText, setMergeText] = useState('')
  const [deactivateDuplicate, setDeactivateDuplicate] = useState(false)
  const [mergeResult, setMergeResult] = useState<MergeUsersResult | null>(null)

  // Tab 4: Merge Matriculas
  const [mergeMatriculaText, setMergeMatriculaText] = useState('')
  const [deleteDuplicateMatricula, setDeleteDuplicateMatricula] = useState(false)
  const [mergeMatriculaResult, setMergeMatriculaResult] = useState<MergeMatriculasResult | null>(null)

  // Tab 5: Unassigned Users
  const [unassignedUsers, setUnassignedUsers] = useState<UnassignedUser[]>([])
  const [loadingUnassigned, setLoadingUnassigned] = useState(false)
  const [selectedUserIds, setSelectedUserIds] = useState<Set<string>>(new Set())
  const [assigningUnassigned, setAssigningUnassigned] = useState(false)
  const [unassignedResult, setUnassignedResult] = useState<BatchAssignUnassignedResult | null>(null)
  const [unassignedSearch, setUnassignedSearch] = useState('')
  const [unassignedEligibilityFilter, setUnassignedEligibilityFilter] = useState<'all' | 'eligible' | 'ineligible'>('all')

  const [activeTab, setActiveTab] = useState<string | null>('parent')
  const [teams, setTeams] = useState<Array<{ value: string; label: string }>>([])
  const [loadingTeams, setLoadingTeams] = useState(false)
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState('')



  useEffect(() => {
    const loadTeams = async () => {
      setLoadingTeams(true)
      try {
        const teamsData = await fetchTeams()
        const formattedTeams = teamsData.map(team => ({
          value: team.id.toString(),
          label: team.name
        }))
        setTeams(formattedTeams)
      } catch (err: any) {
        console.error('Failed to load teams:', err)
        notifications.show({
          title: 'Erro ao carregar equipes',
          message: err.message || 'Verifique sua conexão',
          color: 'red',
          icon: <IconX size={16} />
        })
      } finally {
        setLoadingTeams(false)
      }
    }

    if (currentUser?.role === 'superadmin') {
      loadTeams()
    }
  }, [currentUser, fetchTeams])

  const loadUnassignedUsers = async () => {
    setLoadingUnassigned(true)
    setError('')
    try {
      const res = await apiService.getUsersWithoutTeam()
      if (res.success && res.data) {
        setUnassignedUsers(res.data)
        setSelectedUserIds(new Set())
      }
    } catch (err: any) {
      console.error('Failed to load users without team:', err)
      setError(err.message || 'Erro ao carregar usuários sem equipe')
      notifications.show({
        title: 'Erro ao carregar usuários',
        message: err.message || 'Falha ao buscar usuários sem equipe',
        color: 'red',
        icon: <IconX size={16} />
      })
    } finally {
      setLoadingUnassigned(false)
    }
  }

  useEffect(() => {
    if (activeTab === 'unassigned' && currentUser && (currentUser.email === 'superadmin@salesapp.com' || currentUser.email === 'superadmin@test.com')) {
      loadUnassignedUsers()
    }
  }, [activeTab, currentUser])

  if (loadingUser) {
    return (
      <Menu>
        <div style={{ display: 'flex', justifyContent: 'center', alignItems: 'center', minHeight: '50vh' }}>
          <Loader size="xl" />
        </div>
      </Menu>
    )
  }

  // Restrict access to superadmin@salesapp.com (or superadmin@test.com for testing)
  if (!currentUser || (currentUser.email !== 'superadmin@salesapp.com' && currentUser.email !== 'superadmin@test.com')) {
    return (
      <Menu>
        <div style={{ display: 'flex', justifyContent: 'center', alignItems: 'center', minHeight: '50vh', padding: '24px' }}>
          <Card shadow="md" padding="xl" radius="md" style={{ maxWidth: 500, backgroundColor: '#ffffff', border: '1px solid #fca5a5' }}>
            <Group justify="center" mb="md">
              <IconAlertTriangle size={48} color="#ef4444" />
            </Group>
            <Title order={2} size="h3" style={{ color: '#111827', textAlign: 'center' }} mb="sm">
              Acesso Negado
            </Title>
            <Text size="sm" style={{ color: '#4b5563', textAlign: 'center' }} mb="lg">
              Apenas o superadmin principal (superadmin@salesapp.com) tem permissão para acessar o painel de modificação em lote.
            </Text>
            <Button fullWidth onClick={() => { window.location.hash = '#/my-contracts' }}>
              Voltar ao Início
            </Button>
          </Card>
        </div>
      </Menu>
    )
  }

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setError('')
    setResult(null)

    if (!parentEmail.trim()) {
      setError('O e-mail do superior é obrigatório.')
      return
    }

    if (!teamId && !matricula.trim()) {
      setError('Forneça pelo menos um filtro (Equipe ou Matrícula) para buscar os usuários.')
      return
    }

    setSubmitting(true)
    try {
      const payload = {
        parentEmail: parentEmail.trim(),
        overrideExisting,
        teamId: teamId ? parseInt(teamId, 10) : undefined,
        matricula: matricula.trim() || undefined
      }

      const response = await apiService.batchUpdateParent(payload)
      if (response.success && response.data) {
        setResult(response.data)
        notifications.show({
          title: 'Sucesso',
          message: response.message || 'Atualização em lote concluída.',
          color: 'green',
          icon: <IconCheck size={16} />
        })
      } else {
        setError(response.message || 'Falha ao executar alteração em lote.')
      }
    } catch (err: any) {
      setError(err.message || 'Um erro inesperado ocorreu durante a alteração em lote.')
      notifications.show({
        title: 'Erro na operação',
        message: err.message || 'Não foi possível concluir a ação',
        color: 'red',
        icon: <IconX size={16} />
      })
    } finally {
      setSubmitting(false)
    }
  }

  const handleClear = () => {
    setParentEmail('')
    setTeamId(null)
    setMatricula('')
    setOverrideExisting(false)
    setError('')
    setResult(null)
  }

  const handleAssignSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setError('')
    setAssignResult(null)

    const hasParentEmail = !!assignParentEmail.trim()
    const hasMatricula = !!assignMatricula.trim()

    if (!hasParentEmail && !hasMatricula) {
      setError('Informe o e-mail do superior ou a matrícula.')
      return
    }

    if (hasParentEmail && hasMatricula) {
      setError('Informe apenas o e-mail do superior ou a matrícula, não ambos.')
      return
    }

    if (!assignTeamId) {
      setError('A equipe de destino é obrigatória.')
      return
    }

    setSubmitting(true)
    try {
      const payload: BatchAssignTeamRequest = {
        parentEmail: hasParentEmail ? assignParentEmail.trim() : undefined,
        matricula: hasMatricula ? assignMatricula.trim() : undefined,
        teamId: parseInt(assignTeamId, 10),
        startDate: startDate ? startDate.toISOString() : undefined,
        overrideExisting: overrideExistingTeam
      }

      const response = await apiService.batchAssignTeam(payload)
      if (response.success && response.data) {
        setAssignResult(response.data)
        notifications.show({
          title: 'Sucesso',
          message: response.message || 'Membros atribuídos à equipe com sucesso.',
          color: 'green',
          icon: <IconCheck size={16} />
        })
      } else {
        setError(response.message || 'Falha ao atribuir usuários à equipe.')
      }
    } catch (err: any) {
      setError(err.message || 'Um erro inesperado ocorreu.')
      notifications.show({
        title: 'Erro na operação',
        message: err.message || 'Não foi possível concluir a ação',
        color: 'red',
        icon: <IconX size={16} />
      })
    } finally {
      setSubmitting(false)
    }
  }

  const handleAssignClear = () => {
    setAssignParentEmail('')
    setAssignMatricula('')
    setAssignTeamId(null)
    setStartDate(new Date())
    setOverrideExistingTeam(false)
    setError('')
    setAssignResult(null)
  }

  const splitLinePair = (line: string): [string, string] | null => {
    const trimmed = line.trim()
    if (!trimmed) return null

    let parts: string[] = []
    if (trimmed.includes(',')) {
      parts = trimmed.split(',').map(s => s.trim()).filter(Boolean)
    } else if (trimmed.includes('\t')) {
      parts = trimmed.split('\t').map(s => s.trim()).filter(Boolean)
    } else {
      parts = trimmed.split(/\s+/).map(s => s.trim()).filter(Boolean)
    }

    if (parts.length >= 2) {
      return [parts[0], parts[1]]
    }
    return null
  }

  const parseEmailPairs = (text: string): MergeUserPair[] => {
    const lines = text.split('\n')
    const pairs: MergeUserPair[] = []
    for (const line of lines) {
      const parsed = splitLinePair(line)
      if (parsed) {
        pairs.push({
          mainEmail: parsed[0],
          duplicateEmail: parsed[1]
        })
      }
    }
    return pairs
  }


  const handleMergePreview = async (e: React.FormEvent) => {
    e.preventDefault()
    setError('')
    setMergeResult(null)

    const pairs = parseEmailPairs(mergeText)
    if (pairs.length === 0) {
      setError('Insira pelo menos um par de e-mails válido (email1,email2).')
      return
    }

    setSubmitting(true)
    try {
      const payload: MergeUsersRequest = {
        pairs,
        deactivateDuplicate,
        dryRun: true
      }
      const response = await apiService.batchMergeUsers(payload)
      if (response.success && response.data) {
        setMergeResult(response.data)
        notifications.show({
          title: 'Pré-visualização gerada',
          message: response.message || 'Verifique o resumo antes de confirmar.',
          color: 'blue',
          icon: <IconCheck size={16} />
        })
      } else {
        setError(response.message || 'Falha ao pré-visualizar consolidação.')
      }
    } catch (err: any) {
      setError(err.message || 'Erro inesperado na pré-visualização.')
      notifications.show({
        title: 'Erro na operação',
        message: err.message || 'Não foi possível concluir a pré-visualização',
        color: 'red',
        icon: <IconX size={16} />
      })
    } finally {
      setSubmitting(false)
    }
  }

  const handleMergeConfirm = async () => {
    setError('')
    const pairs = parseEmailPairs(mergeText)
    if (pairs.length === 0) {
      setError('Insira pelo menos um par de e-mails válido.')
      return
    }

    setSubmitting(true)
    try {
      const payload: MergeUsersRequest = {
        pairs,
        deactivateDuplicate,
        dryRun: false
      }
      const response = await apiService.batchMergeUsers(payload)
      if (response.success && response.data) {
        setMergeResult(response.data)
        notifications.show({
          title: 'Sucesso',
          message: response.message || 'Consolidação de usuários realizada com sucesso.',
          color: 'green',
          icon: <IconCheck size={16} />
        })
      } else {
        setError(response.message || 'Falha ao executar consolidação.')
      }
    } catch (err: any) {
      setError(err.message || 'Erro inesperado ao aplicar consolidação.')
      notifications.show({
        title: 'Erro na operação',
        message: err.message || 'Não foi possível concluir a ação',
        color: 'red',
        icon: <IconX size={16} />
      })
    } finally {
      setSubmitting(false)
    }
  }

  const handleMergeClear = () => {
    setMergeText('')
    setDeactivateDuplicate(false)
    setError('')
    setMergeResult(null)
  }

  const parseMatriculaPairs = (text: string): MergeMatriculaPair[] => {
    const lines = text.split('\n')
    const pairs: MergeMatriculaPair[] = []
    for (const line of lines) {
      const parsed = splitLinePair(line)
      if (parsed) {
        pairs.push({
          mainMatricula: parsed[0],
          duplicateMatricula: parsed[1]
        })
      }
    }
    return pairs
  }

  const handleMergeMatriculaPreview = async (e: React.FormEvent) => {
    e.preventDefault()
    setError('')
    setMergeMatriculaResult(null)

    const pairs = parseMatriculaPairs(mergeMatriculaText)
    if (pairs.length === 0) {
      setError('Insira pelo menos um par de matrículas válido (mat1,mat2).')
      return
    }

    setSubmitting(true)
    try {
      const payload: MergeMatriculasRequest = {
        pairs,
        deleteDuplicate: deleteDuplicateMatricula,
        dryRun: true
      }
      const response = await apiService.batchMergeMatriculas(payload)
      if (response.success && response.data) {
        setMergeMatriculaResult(response.data)
        notifications.show({
          title: 'Pré-visualização gerada',
          message: response.message || 'Verifique o resumo antes de confirmar.',
          color: 'blue',
          icon: <IconCheck size={16} />
        })
      } else {
        setError(response.message || 'Falha ao pré-visualizar consolidação de matrículas.')
      }
    } catch (err: any) {
      setError(err.message || 'Erro inesperado na pré-visualização.')
      notifications.show({
        title: 'Erro na operação',
        message: err.message || 'Não foi possível concluir a pré-visualização',
        color: 'red',
        icon: <IconX size={16} />
      })
    } finally {
      setSubmitting(false)
    }
  }

  const handleMergeMatriculaConfirm = async () => {
    setError('')
    const pairs = parseMatriculaPairs(mergeMatriculaText)
    if (pairs.length === 0) {
      setError('Insira pelo menos um par de matrículas válido.')
      return
    }

    setSubmitting(true)
    try {
      const payload: MergeMatriculasRequest = {
        pairs,
        deleteDuplicate: deleteDuplicateMatricula,
        dryRun: false
      }
      const response = await apiService.batchMergeMatriculas(payload)
      if (response.success && response.data) {
        setMergeMatriculaResult(response.data)
        notifications.show({
          title: 'Sucesso',
          message: response.message || 'Consolidação de matrículas realizada com sucesso.',
          color: 'green',
          icon: <IconCheck size={16} />
        })
      } else {
        setError(response.message || 'Falha ao executar consolidação de matrículas.')
      }
    } catch (err: any) {
      setError(err.message || 'Erro inesperado ao aplicar consolidação de matrículas.')
      notifications.show({
        title: 'Erro na operação',
        message: err.message || 'Não foi possível concluir a ação',
        color: 'red',
        icon: <IconX size={16} />
      })
    } finally {
      setSubmitting(false)
    }
  }

  const handleMergeMatriculaClear = () => {
    setMergeMatriculaText('')
    setDeleteDuplicateMatricula(false)
    setError('')
    setMergeMatriculaResult(null)
  }

  const handleTabChange = (val: string | null) => {
    setActiveTab(val)
    setError('')
  }

  const filteredUnassignedUsers = unassignedUsers.filter(u => {
    if (unassignedEligibilityFilter === 'eligible' && !u.isEligible) return false
    if (unassignedEligibilityFilter === 'ineligible' && u.isEligible) return false
    if (unassignedSearch.trim()) {
      const q = unassignedSearch.toLowerCase().trim()
      const nameMatch = u.name.toLowerCase().includes(q)
      const emailMatch = u.email.toLowerCase().includes(q)
      const parentMatch = (u.parentUserName || '').toLowerCase().includes(q) || (u.parentUserEmail || '').toLowerCase().includes(q)
      const teamMatch = (u.targetTeamName || '').toLowerCase().includes(q)
      if (!nameMatch && !emailMatch && !parentMatch && !teamMatch) return false
    }
    return true
  })

  const allFilteredSelected = filteredUnassignedUsers.length > 0 && filteredUnassignedUsers.every(u => selectedUserIds.has(u.userId))
  const someFilteredSelected = filteredUnassignedUsers.some(u => selectedUserIds.has(u.userId))

  const handleSelectAll = () => {
    const newSet = new Set(selectedUserIds)
    filteredUnassignedUsers.forEach(u => newSet.add(u.userId))
    setSelectedUserIds(newSet)
  }

  const handleDeselectAll = () => {
    setSelectedUserIds(new Set())
  }

  const handleToggleUser = (userId: string) => {
    const newSet = new Set(selectedUserIds)
    if (newSet.has(userId)) {
      newSet.delete(userId)
    } else {
      newSet.add(userId)
    }
    setSelectedUserIds(newSet)
  }

  const handleToggleAllVisible = () => {
    if (allFilteredSelected) {
      const newSet = new Set(selectedUserIds)
      filteredUnassignedUsers.forEach(u => newSet.delete(u.userId))
      setSelectedUserIds(newSet)
    } else {
      const newSet = new Set(selectedUserIds)
      filteredUnassignedUsers.forEach(u => newSet.add(u.userId))
      setSelectedUserIds(newSet)
    }
  }

  const handleAssignUnassigned = async () => {
    if (selectedUserIds.size === 0) return
    setAssigningUnassigned(true)
    setError('')
    setUnassignedResult(null)

    try {
      const res = await apiService.assignUnassignedToOwnerTeams({
        userIds: Array.from(selectedUserIds)
      })
      if (res.success && res.data) {
        setUnassignedResult(res.data)
        notifications.show({
          title: 'Sucesso',
          message: res.message || 'Atribuição em lote concluída.',
          color: 'green',
          icon: <IconCheck size={16} />
        })
        await loadUnassignedUsers()
      }
    } catch (err: any) {
      console.error('Failed to assign unassigned users:', err)
      setError(err.message || 'Erro ao atribuir usuários à equipe do gestor.')
      notifications.show({
        title: 'Erro na operação',
        message: err.message || 'Falha ao processar solicitação.',
        color: 'red',
        icon: <IconX size={16} />
      })
    } finally {
      setAssigningUnassigned(false)
    }
  }

  return (
    <Menu>
      <div className="batch-container">
        <div className="batch-header">
          <Title className="batch-title">Modificação em Lote</Title>
          <Text className="batch-subtitle">
            Altere o supervisor de múltiplos usuários simultaneamente, atribua usuários a equipes, vincule usuários sem equipe ao gestor ou consolide contas/matrículas duplicadas.
          </Text>
        </div>

        <Tabs value={activeTab} onChange={handleTabChange} color="blue" mb="lg">
          <Tabs.List>
            <Tabs.Tab value="parent" leftSection={<IconUsers size={16} />}>
              Atualizar Superior
            </Tabs.Tab>
            <Tabs.Tab value="team" leftSection={<IconUsers size={16} />}>
              Atribuir a Equipe
            </Tabs.Tab>
            <Tabs.Tab value="unassigned" leftSection={<IconUserOff size={16} />}>
              Usuários Sem Equipe
            </Tabs.Tab>
            <Tabs.Tab value="merge" leftSection={<IconGitMerge size={16} />}>
              Consolidar Usuários
            </Tabs.Tab>
            <Tabs.Tab value="merge-matriculas" leftSection={<IconGitMerge size={16} />}>
              Consolidar Matrículas
            </Tabs.Tab>
          </Tabs.List>
        </Tabs>



        {error && <div className="error-banner">{error}</div>}

        {activeTab === 'parent' ? (
          <>
            <form onSubmit={handleSubmit} className="batch-card">
              <div className="batch-form-grid">
                <TextInput
                  label="E-mail do Superior (Novo)"
                  placeholder="exemplo@salesapp.com"
                  value={parentEmail}
                  onChange={(e) => setParentEmail(e.currentTarget.value)}
                  required
                  disabled={submitting}
                />

                <Select
                  label="Filtrar por Equipe"
                  placeholder={loadingTeams ? "Carregando equipes..." : "Selecione uma equipe"}
                  data={teams}
                  value={teamId}
                  onChange={setTeamId}
                  clearable
                  disabled={submitting || loadingTeams}
                />

                <TextInput
                  label="Filtrar por Matrícula"
                  placeholder="Digite a matrícula exata (opcional)"
                  value={matricula}
                  onChange={(e) => setMatricula(e.currentTarget.value)}
                  disabled={submitting}
                />

                <div style={{ display: 'flex', alignItems: 'center', height: '100%', paddingTop: '24px' }}>
                  <Switch
                    label="Sobrescrever superior existente?"
                    description="Se desmarcado, altera apenas usuários sem superior definido"
                    checked={overrideExisting}
                    onChange={(e) => setOverrideExisting(e.currentTarget.checked)}
                    disabled={submitting}
                  />
                </div>
              </div>

              <div className="batch-action-row">
                <Button variant="subtle" color="gray" onClick={handleClear} disabled={submitting}>
                  Limpar Filtros
                </Button>
                <Button
                  type="submit"
                  loading={submitting}
                  leftSection={<IconWand size={16} />}
                  color="blue"
                >
                  Aplicar Alterações
                </Button>
              </div>
            </form>

            {result && (
              <div className="batch-card" style={{ marginTop: '24px' }}>
                <Title order={3} className="batch-results-header">Resultado da Operação</Title>

                <SimpleGrid cols={{ base: 1, sm: 3 }} spacing="md" className="batch-stats-container">
                  <div className="batch-stat-card">
                    <Text className="batch-stat-title">Total Encontrados</Text>
                    <Text className="batch-stat-value total">
                      {result.modified.length + result.skipped.length}
                    </Text>
                  </div>
                  <div className="batch-stat-card">
                    <Text className="batch-stat-title">Atualizados com Sucesso</Text>
                    <Text className="batch-stat-value success">{result.modified.length}</Text>
                  </div>
                  <div className="batch-stat-card">
                    <Text className="batch-stat-title">Ignorados / Pulados</Text>
                    <Text className="batch-stat-value skipped">{result.skipped.length}</Text>
                  </div>
                </SimpleGrid>

                <Tabs defaultValue="modified" color="blue">
                  <Tabs.List>
                    <Tabs.Tab value="modified" leftSection={<IconCheck size={14} />}>
                      Atualizados ({result.modified.length})
                    </Tabs.Tab>
                    <Tabs.Tab value="skipped" leftSection={<IconX size={14} />}>
                      Ignorados ({result.skipped.length})
                    </Tabs.Tab>
                  </Tabs.List>

                  <Tabs.Panel value="modified">
                    {result.modified.length === 0 ? (
                      <Text size="sm" c="dimmed" style={{ padding: '24px', textAlign: 'center' }}>
                        Nenhum usuário foi atualizado.
                      </Text>
                    ) : (
                      <div className="batch-table-wrapper">
                        <Table className="batch-table">
                          <Table.Thead>
                            <Table.Tr>
                              <Table.Th>Nome</Table.Th>
                              <Table.Th>E-mail</Table.Th>
                              <Table.Th>Superior Anterior</Table.Th>
                              <Table.Th>Novo Superior</Table.Th>
                            </Table.Tr>
                          </Table.Thead>
                          <Table.Tbody>
                            {result.modified.map((u) => (
                              <Table.Tr key={u.id}>
                                <Table.Td>{u.name}</Table.Td>
                                <Table.Td>{u.email}</Table.Td>
                                <Table.Td>{u.oldParentEmail || 'Nenhum'}</Table.Td>
                                <Table.Td>{u.newParentEmail}</Table.Td>
                              </Table.Tr>
                            ))}
                          </Table.Tbody>
                        </Table>
                      </div>
                    )}
                  </Tabs.Panel>

                  <Tabs.Panel value="skipped">
                    {result.skipped.length === 0 ? (
                      <Text size="sm" c="dimmed" style={{ padding: '24px', textAlign: 'center' }}>
                        Nenhum usuário foi ignorado.
                      </Text>
                    ) : (
                      <div className="batch-table-wrapper">
                        <Table className="batch-table">
                          <Table.Thead>
                            <Table.Tr>
                              <Table.Th>Nome</Table.Th>
                              <Table.Th>E-mail</Table.Th>
                              <Table.Th>Superior Atual</Table.Th>
                              <Table.Th>Motivo</Table.Th>
                            </Table.Tr>
                          </Table.Thead>
                          <Table.Tbody>
                            {result.skipped.map((u) => (
                              <Table.Tr key={u.id}>
                                <Table.Td>{u.name}</Table.Td>
                                <Table.Td>{u.email}</Table.Td>
                                <Table.Td>{u.currentParentEmail || 'Nenhum'}</Table.Td>
                                <Table.Td>
                                  <Badge className="badge-skipped">{u.reason}</Badge>
                                </Table.Td>
                              </Table.Tr>
                            ))}
                          </Table.Tbody>
                        </Table>
                      </div>
                    )}
                  </Tabs.Panel>
                </Tabs>
              </div>
            )}
          </>
        ) : activeTab === 'team' ? (
          <>
            <form onSubmit={handleAssignSubmit} className="batch-card">

              <div className="batch-form-grid">
                <TextInput
                  label="E-mail do Superior (pai)"
                  placeholder="exemplo@salesapp.com"
                  value={assignParentEmail}
                  onChange={(e) => setAssignParentEmail(e.currentTarget.value)}
                  disabled={submitting}
                />

                <TextInput
                  label="Matrícula"
                  placeholder="Digite a matrícula exata"
                  value={assignMatricula}
                  onChange={(e) => setAssignMatricula(e.currentTarget.value)}
                  disabled={submitting}
                />

                <Select
                  label="Equipe de Destino"
                  placeholder={loadingTeams ? "Carregando equipes..." : "Selecione uma equipe"}
                  data={teams}
                  value={assignTeamId}
                  onChange={setAssignTeamId}
                  required
                  disabled={submitting || loadingTeams}
                />

                <DatePickerInput
                  label="Data de Início"
                  placeholder="Selecione a data de início"
                  value={startDate}
                  onChange={(val: any) => setStartDate(val)}
                  disabled={submitting}
                />

                <div style={{ display: 'flex', alignItems: 'center', height: '100%', paddingTop: '24px' }}>
                  <Switch
                    label="Sobrescrever membros existentes?"
                    description="Se marcado, atualiza a data de início de membros ativos"
                    checked={overrideExistingTeam}
                    onChange={(e) => setOverrideExistingTeam(e.currentTarget.checked)}
                    disabled={submitting}
                  />
                </div>
              </div>

              <div className="batch-action-row">
                <Button variant="subtle" color="gray" onClick={handleAssignClear} disabled={submitting}>
                  Limpar Filtros
                </Button>
                <Button
                  type="submit"
                  loading={submitting}
                  leftSection={<IconWand size={16} />}
                  color="blue"
                >
                  Atribuir a Equipe
                </Button>
              </div>
            </form>

            {assignResult && (
              <div className="batch-card" style={{ marginTop: '24px' }}>
                <Title order={3} className="batch-results-header">Resultado da Operação</Title>

                <SimpleGrid cols={{ base: 1, sm: 3 }} spacing="md" className="batch-stats-container">
                  <div className="batch-stat-card">
                    <Text className="batch-stat-title">Total Encontrados</Text>
                    <Text className="batch-stat-value total">
                      {assignResult.added.length + assignResult.skipped.length}
                    </Text>
                  </div>
                  <div className="batch-stat-card">
                    <Text className="batch-stat-title">Adicionados à Equipe</Text>
                    <Text className="batch-stat-value success">{assignResult.added.length}</Text>
                  </div>
                  <div className="batch-stat-card">
                    <Text className="batch-stat-title">Ignorados / Pulados</Text>
                    <Text className="batch-stat-value skipped">{assignResult.skipped.length}</Text>
                  </div>
                </SimpleGrid>

                <Tabs defaultValue="added" color="blue">
                  <Tabs.List>
                    <Tabs.Tab value="added" leftSection={<IconCheck size={14} />}>
                      Adicionados ({assignResult.added.length})
                    </Tabs.Tab>
                    <Tabs.Tab value="skipped" leftSection={<IconX size={14} />}>
                      Ignorados ({assignResult.skipped.length})
                    </Tabs.Tab>
                  </Tabs.List>

                  <Tabs.Panel value="added">
                    {assignResult.added.length === 0 ? (
                      <Text size="sm" c="dimmed" style={{ padding: '24px', textAlign: 'center' }}>
                        Nenhum usuário foi adicionado à equipe.
                      </Text>
                    ) : (
                      <div className="batch-table-wrapper">
                        <Table className="batch-table">
                          <Table.Thead>
                            <Table.Tr>
                              <Table.Th>Nome</Table.Th>
                              <Table.Th>E-mail</Table.Th>
                            </Table.Tr>
                          </Table.Thead>
                          <Table.Tbody>
                            {assignResult.added.map((u) => (
                              <Table.Tr key={u.id}>
                                <Table.Td>{u.name}</Table.Td>
                                <Table.Td>{u.email}</Table.Td>
                              </Table.Tr>
                            ))}
                          </Table.Tbody>
                        </Table>
                      </div>
                    )}
                  </Tabs.Panel>

                  <Tabs.Panel value="skipped">
                    {assignResult.skipped.length === 0 ? (
                      <Text size="sm" c="dimmed" style={{ padding: '24px', textAlign: 'center' }}>
                        Nenhum usuário foi ignorado.
                      </Text>
                    ) : (
                      <div className="batch-table-wrapper">
                        <Table className="batch-table">
                          <Table.Thead>
                            <Table.Tr>
                              <Table.Th>Nome</Table.Th>
                              <Table.Th>E-mail</Table.Th>
                              <Table.Th>Motivo</Table.Th>
                            </Table.Tr>
                          </Table.Thead>
                          <Table.Tbody>
                            {assignResult.skipped.map((u) => (
                              <Table.Tr key={u.id}>
                                <Table.Td>{u.name}</Table.Td>
                                <Table.Td>{u.email}</Table.Td>
                                <Table.Td>
                                  <Badge className="badge-skipped">{u.reason}</Badge>
                                </Table.Td>
                              </Table.Tr>
                            ))}
                          </Table.Tbody>
                        </Table>
                      </div>
                    )}
                  </Tabs.Panel>
                </Tabs>
              </div>
            )}
          </>
        ) : activeTab === 'unassigned' ? (
          <>
            <div className="batch-card">
              <Group justify="space-between" mb="md" wrap="wrap" gap="md">
                <Group gap="sm" wrap="wrap">
                  <TextInput
                    placeholder="Buscar por nome, e-mail ou gestor..."
                    leftSection={<IconSearch size={16} />}
                    value={unassignedSearch}
                    onChange={(e) => setUnassignedSearch(e.currentTarget.value)}
                    style={{ minWidth: 280 }}
                  />
                  <Select
                    data={[
                      { value: 'all', label: 'Todos os usuários' },
                      { value: 'eligible', label: 'Apenas Elegíveis' },
                      { value: 'ineligible', label: 'Apenas Inelegíveis' }
                    ]}
                    value={unassignedEligibilityFilter}
                    onChange={(val: any) => setUnassignedEligibilityFilter(val || 'all')}
                    style={{ width: 180 }}
                  />
                  <Button
                    variant="default"
                    onClick={loadUnassignedUsers}
                    loading={loadingUnassigned}
                    leftSection={<IconRefresh size={16} />}
                  >
                    Atualizar
                  </Button>
                </Group>

                <Group gap="sm" wrap="wrap">
                  <Button
                    variant="subtle"
                    color="gray"
                    onClick={handleSelectAll}
                    disabled={filteredUnassignedUsers.length === 0}
                  >
                    Selecionar Todos ({filteredUnassignedUsers.length})
                  </Button>
                  <Button
                    variant="subtle"
                    color="gray"
                    onClick={handleDeselectAll}
                    disabled={selectedUserIds.size === 0}
                  >
                    Desmarcar Todos
                  </Button>
                  <Button
                    color="blue"
                    leftSection={<IconWand size={16} />}
                    onClick={handleAssignUnassigned}
                    loading={assigningUnassigned}
                    disabled={selectedUserIds.size === 0}
                  >
                    Adicionar Selecionados à Equipe do Gestor ({selectedUserIds.size})
                  </Button>
                </Group>
              </Group>

              <Group gap="xs" mb="md">
                <Text size="sm" c="dimmed">
                  Total sem equipe: <b>{unassignedUsers.length}</b> | Elegíveis: <b>{unassignedUsers.filter(u => u.isEligible).length}</b> | Selecionados: <b>{selectedUserIds.size}</b>
                </Text>
              </Group>

              {loadingUnassigned ? (
                <div style={{ display: 'flex', justifyContent: 'center', padding: '40px' }}>
                  <Loader size="md" />
                </div>
              ) : filteredUnassignedUsers.length === 0 ? (
                <div style={{ textAlign: 'center', padding: '32px', color: '#64748b' }}>
                  <Text size="sm">Nenhum usuário sem equipe encontrado com os filtros aplicados.</Text>
                </div>
              ) : (
                <div className="batch-table-wrapper">
                  <Table className="batch-table">
                    <Table.Thead>
                      <Table.Tr>
                        <Table.Th style={{ width: 40 }}>
                          <Checkbox
                            checked={allFilteredSelected}
                            indeterminate={someFilteredSelected && !allFilteredSelected}
                            onChange={handleToggleAllVisible}
                            aria-label="Selecionar todos os visíveis"
                          />
                        </Table.Th>
                        <Table.Th>Nome</Table.Th>
                        <Table.Th>E-mail</Table.Th>
                        <Table.Th>Histórico de Equipe</Table.Th>
                        <Table.Th>Gestor Direto (parentUser)</Table.Th>
                        <Table.Th>Equipe do Gestor</Table.Th>
                        <Table.Th>Data Início</Table.Th>
                        <Table.Th>Status / Elegibilidade</Table.Th>
                      </Table.Tr>
                    </Table.Thead>
                    <Table.Tbody>
                      {filteredUnassignedUsers.map((u) => {
                        const isSelected = selectedUserIds.has(u.userId)
                        return (
                          <Table.Tr key={u.userId} style={{ backgroundColor: isSelected ? '#eff6ff' : undefined }}>
                            <Table.Td>
                              <Checkbox
                                checked={isSelected}
                                onChange={() => handleToggleUser(u.userId)}
                                aria-label={`Selecionar ${u.name}`}
                              />
                            </Table.Td>
                            <Table.Td style={{ fontWeight: 500 }}>{u.name}</Table.Td>
                            <Table.Td>{u.email}</Table.Td>
                            <Table.Td>
                              {u.historyStatus === 'never' ? (
                                <Badge color="gray" variant="light">Nunca teve equipe</Badge>
                              ) : (
                                <Badge color="orange" variant="light">Sem equipe atual (já pertenceu)</Badge>
                              )}
                            </Table.Td>
                            <Table.Td>
                              {u.parentUserName ? (
                                <div>
                                  <Text size="sm">{u.parentUserName}</Text>
                                  <Text size="xs" c="dimmed">{u.parentUserEmail}</Text>
                                </div>
                              ) : (
                                <Text size="sm" c="dimmed">Sem gestor</Text>
                              )}
                            </Table.Td>
                            <Table.Td>
                              {u.targetTeamName ? (
                                <Text size="sm" fw={500}>{u.targetTeamName}</Text>
                              ) : (
                                <Text size="sm" c="dimmed">-</Text>
                              )}
                            </Table.Td>
                            <Table.Td>
                              <Badge color="blue" variant="outline">01/01/2022</Badge>
                            </Table.Td>
                            <Table.Td>
                              {u.isEligible ? (
                                <Badge color="green" variant="filled">Elegível</Badge>
                              ) : (
                                <Badge color="red" variant="light">{u.statusText}</Badge>
                              )}
                            </Table.Td>
                          </Table.Tr>
                        )
                      })}
                    </Table.Tbody>
                  </Table>
                </div>
              )}
            </div>

            {unassignedResult && (
              <div className="batch-card" style={{ marginTop: '24px' }}>
                <Title order={3} className="batch-results-header">Resultado da Operação</Title>

                <SimpleGrid cols={{ base: 1, sm: 3 }} spacing="md" className="batch-stats-container">
                  <div className="batch-stat-card">
                    <Text className="batch-stat-title">Total Processados</Text>
                    <Text className="batch-stat-value total">
                      {unassignedResult.added.length + unassignedResult.skipped.length}
                    </Text>
                  </div>
                  <div className="batch-stat-card">
                    <Text className="batch-stat-title">Adicionados com Sucesso</Text>
                    <Text className="batch-stat-value success">{unassignedResult.added.length}</Text>
                  </div>
                  <div className="batch-stat-card">
                    <Text className="batch-stat-title">Ignorados / Pulados</Text>
                    <Text className="batch-stat-value skipped">{unassignedResult.skipped.length}</Text>
                  </div>
                </SimpleGrid>

                <Tabs defaultValue="added" color="blue">
                  <Tabs.List>
                    <Tabs.Tab value="added" leftSection={<IconCheck size={14} />}>
                      Adicionados ({unassignedResult.added.length})
                    </Tabs.Tab>
                    <Tabs.Tab value="skipped" leftSection={<IconX size={14} />}>
                      Ignorados ({unassignedResult.skipped.length})
                    </Tabs.Tab>
                  </Tabs.List>

                  <Tabs.Panel value="added">
                    {unassignedResult.added.length === 0 ? (
                      <Text size="sm" c="dimmed" style={{ padding: '24px', textAlign: 'center' }}>
                        Nenhum usuário foi adicionado.
                      </Text>
                    ) : (
                      <div className="batch-table-wrapper">
                        <Table className="batch-table">
                          <Table.Thead>
                            <Table.Tr>
                              <Table.Th>Nome</Table.Th>
                              <Table.Th>E-mail</Table.Th>
                              <Table.Th>Data Início</Table.Th>
                              <Table.Th>Status</Table.Th>
                            </Table.Tr>
                          </Table.Thead>
                          <Table.Tbody>
                            {unassignedResult.added.map((u) => (
                              <Table.Tr key={u.id}>
                                <Table.Td>{u.name}</Table.Td>
                                <Table.Td>{u.email}</Table.Td>
                                <Table.Td>01/01/2022</Table.Td>
                                <Table.Td>
                                  <Badge className="badge-modified">Adicionado à equipe do gestor</Badge>
                                </Table.Td>
                              </Table.Tr>
                            ))}
                          </Table.Tbody>
                        </Table>
                      </div>
                    )}
                  </Tabs.Panel>

                  <Tabs.Panel value="skipped">
                    {unassignedResult.skipped.length === 0 ? (
                      <Text size="sm" c="dimmed" style={{ padding: '24px', textAlign: 'center' }}>
                        Nenhum usuário foi ignorado.
                      </Text>
                    ) : (
                      <div className="batch-table-wrapper">
                        <Table className="batch-table">
                          <Table.Thead>
                            <Table.Tr>
                              <Table.Th>Nome</Table.Th>
                              <Table.Th>E-mail</Table.Th>
                              <Table.Th>Motivo</Table.Th>
                            </Table.Tr>
                          </Table.Thead>
                          <Table.Tbody>
                            {unassignedResult.skipped.map((u) => (
                              <Table.Tr key={u.id}>
                                <Table.Td>{u.name}</Table.Td>
                                <Table.Td>{u.email}</Table.Td>
                                <Table.Td>
                                  <Badge className="badge-skipped">{u.reason}</Badge>
                                </Table.Td>
                              </Table.Tr>
                            ))}
                          </Table.Tbody>
                        </Table>
                      </div>
                    )}
                  </Tabs.Panel>
                </Tabs>
              </div>
            )}
          </>
        ) : activeTab === 'merge' ? (
          <>

            <form onSubmit={handleMergePreview} className="batch-card">
              <Text size="sm" c="dimmed" mb="md">
                Consolide usuários duplicados em um único usuário principal. Insira um par por linha nos formatos:
                <Text span fw={600}> email1,email2</Text>, <Text span fw={600}>email1 email2</Text> ou <Text span fw={600}>email1 [tab] email2</Text>.
                O <Text span fw={600}>email1</Text> é o usuário principal (mantido) e <Text span fw={600}>email2</Text> é o duplicado.
              </Text>

              <Textarea
                label="Pares de E-mails (email1, email2)"
                placeholder={`email1@salesapp.com,email2@salesapp.com\nprincipal@test.com duplicado@test.com`}
                value={mergeText}
                onChange={(e) => {
                  setMergeText(e.currentTarget.value)
                  setMergeResult(null)
                }}
                rows={6}
                required
                disabled={submitting}
                mb="md"
              />

              <div style={{ marginBottom: '16px' }}>
                <Switch
                  label="Desativar usuário duplicado (email2) ao concluir?"
                  description="Se marcado, altera IsActive para falso no usuário duplicado após transferir os contratos e dados"
                  checked={deactivateDuplicate}
                  onChange={(e) => setDeactivateDuplicate(e.currentTarget.checked)}
                  disabled={submitting}
                />
              </div>

              <div className="batch-action-row">
                <Button variant="subtle" color="gray" onClick={handleMergeClear} disabled={submitting}>
                  Limpar
                </Button>
                <Button
                  type="submit"
                  loading={submitting}
                  leftSection={<IconEye size={16} />}
                  color="blue"
                >
                  Pré-visualizar Consolidação
                </Button>
              </div>
            </form>

            {mergeResult && (
              <div className="batch-card" style={{ marginTop: '24px' }}>
                <Group justify="space-between" align="center" mb="md">
                  <Title order={3} className="batch-results-header" style={{ margin: 0 }}>
                    {mergeResult.isDryRun ? 'Pré-visualização da Consolidação' : 'Resultado da Consolidação'}
                  </Title>
                  <Badge color={mergeResult.isDryRun ? 'blue' : 'green'} size="lg">
                    {mergeResult.isDryRun ? 'Modo Simulação (Dry-Run)' : 'Concluído'}
                  </Badge>
                </Group>

                <SimpleGrid cols={{ base: 1, sm: 3 }} spacing="md" className="batch-stats-container" mb="lg">
                  <div className="batch-stat-card">
                    <Text className="batch-stat-title">Total de Pares</Text>
                    <Text className="batch-stat-value total">{mergeResult.pairs.length}</Text>
                  </div>
                  <div className="batch-stat-card">
                    <Text className="batch-stat-title">Válidos</Text>
                    <Text className="batch-stat-value success">
                      {mergeResult.pairs.filter(p => !p.error).length}
                    </Text>
                  </div>
                  <div className="batch-stat-card">
                    <Text className="batch-stat-title">Com Erros</Text>
                    <Text className="batch-stat-value skipped">
                      {mergeResult.pairs.filter(p => p.error).length}
                    </Text>
                  </div>
                </SimpleGrid>

                <div className="batch-table-wrapper">
                  <Table className="batch-table">
                    <Table.Thead>
                      <Table.Tr>
                        <Table.Th>E-mail Principal (email1)</Table.Th>
                        <Table.Th>E-mail Duplicado (email2)</Table.Th>
                        <Table.Th>Contratos</Table.Th>
                        <Table.Th>Matrículas</Table.Th>
                        <Table.Th>Subordinados</Table.Th>
                        <Table.Th>Equipes</Table.Th>
                        <Table.Th>Desativar email2?</Table.Th>
                        <Table.Th>Status / Erro</Table.Th>
                      </Table.Tr>
                    </Table.Thead>
                    <Table.Tbody>
                      {mergeResult.pairs.map((p, idx) => (
                        <Table.Tr key={idx}>
                          <Table.Td>{p.mainEmail || '-'}</Table.Td>
                          <Table.Td>{p.duplicateEmail || '-'}</Table.Td>
                          <Table.Td>{p.contractsMigrated}</Table.Td>
                          <Table.Td>{p.matriculasMigrated}</Table.Td>
                          <Table.Td>{p.childUsersMigrated}</Table.Td>
                          <Table.Td>{p.teamMembershipsMigrated}</Table.Td>
                          <Table.Td>{p.duplicateDeactivated ? 'Sim' : 'Não'}</Table.Td>
                          <Table.Td>
                            {p.error ? (
                              <Badge color="red">{p.error}</Badge>
                            ) : (
                              <Badge color="green">Válido</Badge>
                            )}
                          </Table.Td>
                        </Table.Tr>
                      ))}
                    </Table.Tbody>
                  </Table>
                </div>

                {mergeResult.isDryRun && (
                  <Group justify="flex-end" mt="xl">
                    <Button
                      color="green"
                      leftSection={<IconWand size={16} />}
                      loading={submitting}
                      disabled={mergeResult.pairs.every(p => !!p.error)}
                      onClick={handleMergeConfirm}
                    >
                      Confirmar e Executar Consolidação
                    </Button>
                  </Group>
                )}
              </div>
            )}
          </>
        ) : activeTab === 'merge-matriculas' ? (
          <>
            <form onSubmit={handleMergeMatriculaPreview} className="batch-card">
              <Text size="sm" c="dimmed" mb="md">
                Consolide matrículas duplicadas em uma única matrícula principal. Insira um par por linha nos formatos:
                <Text span fw={600}> mat1,mat2</Text>, <Text span fw={600}>mat1 mat2</Text> ou <Text span fw={600}>mat1 [tab] mat2</Text>.
                A <Text span fw={600}>mat1</Text> é a matrícula principal (mantida) e <Text span fw={600}>mat2</Text> é a duplicada.
              </Text>

              <Textarea
                label="Pares de Matrículas (mat1, mat2)"
                placeholder={`02123,2123\nMAT-001 MAT-002`}
                value={mergeMatriculaText}
                onChange={(e) => {
                  setMergeMatriculaText(e.currentTarget.value)
                  setMergeMatriculaResult(null)
                }}
                rows={6}
                required
                disabled={submitting}
                mb="md"
              />

              <div style={{ marginBottom: '16px' }}>
                <Switch
                  label="Excluir matrícula duplicada (mat2) ao concluir?"
                  description="Se marcado, remove o registro da matrícula duplicada do banco de dados após reassociar todos os usuários e contratos"
                  checked={deleteDuplicateMatricula}
                  onChange={(e) => setDeleteDuplicateMatricula(e.currentTarget.checked)}
                  disabled={submitting}
                />
              </div>

              <div className="batch-action-row">
                <Button variant="subtle" color="gray" onClick={handleMergeMatriculaClear} disabled={submitting}>
                  Limpar
                </Button>
                <Button
                  type="submit"
                  loading={submitting}
                  leftSection={<IconEye size={16} />}
                  color="blue"
                >
                  Pré-visualizar Consolidação
                </Button>
              </div>
            </form>

            {mergeMatriculaResult && (
              <div className="batch-card" style={{ marginTop: '24px' }}>
                <Group justify="space-between" align="center" mb="md">
                  <Title order={3} className="batch-results-header" style={{ margin: 0 }}>
                    {mergeMatriculaResult.isDryRun ? 'Pré-visualização da Consolidação' : 'Resultado da Consolidação'}
                  </Title>
                  <Badge color={mergeMatriculaResult.isDryRun ? 'blue' : 'green'} size="lg">
                    {mergeMatriculaResult.isDryRun ? 'Modo Simulação (Dry-Run)' : 'Concluído'}
                  </Badge>
                </Group>

                <SimpleGrid cols={{ base: 1, sm: 3 }} spacing="md" className="batch-stats-container" mb="lg">
                  <div className="batch-stat-card">
                    <Text className="batch-stat-title">Total de Pares</Text>
                    <Text className="batch-stat-value total">{mergeMatriculaResult.pairs.length}</Text>
                  </div>
                  <div className="batch-stat-card">
                    <Text className="batch-stat-title">Válidos</Text>
                    <Text className="batch-stat-value success">
                      {mergeMatriculaResult.pairs.filter(p => !p.error).length}
                    </Text>
                  </div>
                  <div className="batch-stat-card">
                    <Text className="batch-stat-title">Com Erros</Text>
                    <Text className="batch-stat-value skipped">
                      {mergeMatriculaResult.pairs.filter(p => p.error).length}
                    </Text>
                  </div>
                </SimpleGrid>

                <div className="batch-table-wrapper">
                  <Table className="batch-table">
                    <Table.Thead>
                      <Table.Tr>
                        <Table.Th>Matrícula Principal (mat1)</Table.Th>
                        <Table.Th>Matrícula Duplicada (mat2)</Table.Th>
                        <Table.Th>Usuários Migrados</Table.Th>
                        <Table.Th>Contratos Migrados</Table.Th>
                        <Table.Th>Excluir mat2?</Table.Th>
                        <Table.Th>Status / Erro</Table.Th>
                      </Table.Tr>
                    </Table.Thead>
                    <Table.Tbody>
                      {mergeMatriculaResult.pairs.map((p, idx) => (
                        <Table.Tr key={idx}>
                          <Table.Td>{p.mainMatricula || '-'}</Table.Td>
                          <Table.Td>{p.duplicateMatricula || '-'}</Table.Td>
                          <Table.Td>{p.userLinksMigrated}</Table.Td>
                          <Table.Td>{p.contractsMigrated}</Table.Td>
                          <Table.Td>{p.duplicateDeleted ? 'Sim' : 'Não'}</Table.Td>
                          <Table.Td>
                            {p.error ? (
                              <Badge color="red">{p.error}</Badge>
                            ) : (
                              <Badge color="green">Válido</Badge>
                            )}
                          </Table.Td>
                        </Table.Tr>
                      ))}
                    </Table.Tbody>
                  </Table>
                </div>

                {mergeMatriculaResult.isDryRun && (
                  <Group justify="flex-end" mt="xl">
                    <Button
                      color="green"
                      leftSection={<IconWand size={16} />}
                      loading={submitting}
                      disabled={mergeMatriculaResult.pairs.every(p => !!p.error)}
                      onClick={handleMergeMatriculaConfirm}
                    >
                      Confirmar e Executar Consolidação
                    </Button>
                  </Group>
                )}
              </div>
            )}
          </>
        ) : null}
      </div>
    </Menu>


  )
}

export default BatchPage
