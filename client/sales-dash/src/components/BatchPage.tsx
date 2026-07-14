import React, { useState, useEffect } from 'react'
import {
  Title, TextInput, Button, Switch, Group, Table, Text, Select, Loader, Tabs, Badge, Card, SimpleGrid
} from '@mantine/core'
import { DatePickerInput } from '@mantine/dates'
import {
  IconUsers, IconWand, IconAlertTriangle, IconCheck, IconX
} from '@tabler/icons-react'
import { notifications } from '@mantine/notifications'
import Menu from './Menu'
import { useCurrentUser } from '../contexts/CurrentUserContext'
import { useReferenceData } from '../contexts/ReferenceDataContext'
import { apiService, BatchUpdateParentResult, BatchAssignTeamResult, BatchAssignTeamRequest } from '../services/apiService'
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

  const handleTabChange = (val: string | null) => {
    setActiveTab(val)
    setError('')
  }

  return (
    <Menu>
      <div className="batch-container">
        <div className="batch-header">
          <Title className="batch-title">Modificação em Lote</Title>
          <Text className="batch-subtitle">
            Altere o supervisor de múltiplos usuários simultaneamente ou atribua usuários de um superior a uma equipe.
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
        ) : (
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
        )}
      </div>
    </Menu>
  )
}

export default BatchPage
