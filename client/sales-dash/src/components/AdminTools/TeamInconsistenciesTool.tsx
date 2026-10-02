import React, { useState, useEffect, useCallback, useMemo } from 'react'
import {
  Title,
  Button,
  Group,
  Badge,
  Text,
  TextInput,
  Select,
  Alert,
  Stack,
  Table,
  Modal,
  Checkbox,
  Loader,
} from '@mantine/core'
import {
  IconAlertTriangle,
  IconRefresh,
  IconDownload,
  IconCrown,
  IconSitemap,
  IconArrowsExchange,
  IconSearch,
  IconSparkles,
} from '@tabler/icons-react'
import Menu from '../Menu'
import {
  apiService,
  AdminTeamInconsistencyItem,
  Team,
} from '../../services/apiService'
import { normalizeName } from '../../utils/normalization'
import { toast } from '../../utils/toast'
import './TeamInconsistenciesTool.css'

function formatDate(dateStr?: string | null): string {
  if (!dateStr) return '—'
  try {
    const d = new Date(dateStr)
    if (isNaN(d.getTime())) return dateStr
    return d.toLocaleDateString('pt-BR', { timeZone: 'UTC' })
  } catch {
    return dateStr
  }
}

export const TeamInconsistenciesTool: React.FC = () => {
  const [items, setItems] = useState<AdminTeamInconsistencyItem[]>([])
  const [teams, setTeams] = useState<Team[]>([])
  const [loading, setLoading] = useState(true)
  const [exporting, setExporting] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [search, setSearch] = useState('')
  const [typeFilter, setTypeFilter] = useState<string>('all')

  // Migration modal state
  const [selectedItem, setSelectedItem] = useState<AdminTeamInconsistencyItem | null>(null)
  const [targetTeamId, setTargetTeamId] = useState<string>('')
  const [startDate, setStartDate] = useState<string>('')
  const [endDate, setEndDate] = useState<string>('')
  const [setAsOwner, setSetAsOwner] = useState<boolean>(false)
  const [closeConflicts, setCloseConflicts] = useState<boolean>(true)
  const [migrating, setMigrating] = useState(false)

  const fetchInconsistencies = useCallback(async () => {
    setLoading(true)
    setError(null)
    try {
      const res = await apiService.getTeamInconsistencies()
      if (res.success && res.data) {
        setItems(res.data)
      } else {
        setError(res.message || 'Falha ao buscar inconsistências')
      }
    } catch (err: any) {
      setError(err.message || 'Erro inesperado ao consultar dados')
    } finally {
      setLoading(false)
    }
  }, [])

  const fetchActiveTeams = useCallback(async () => {
    try {
      const res = await apiService.getTeams('active')
      if (res.success && res.data) {
        setTeams(res.data)
      }
    } catch (err) {
      console.error('Falha ao carregar equipes ativas:', err)
    }
  }, [])

  useEffect(() => {
    fetchInconsistencies()
    fetchActiveTeams()
  }, [fetchInconsistencies, fetchActiveTeams])

  // Open modal with prefilled recommendation
  const openMigrationModal = (item: AdminTeamInconsistencyItem) => {
    setSelectedItem(item)
    const rec = item.recommendation
    if (rec && rec.suggestedTeamId) {
      setTargetTeamId(rec.suggestedTeamId.toString())
    } else if (item.ownedTeamId) {
      setTargetTeamId(item.ownedTeamId.toString())
    } else {
      setTargetTeamId('')
    }

    if (rec && rec.suggestedStartDate) {
      setStartDate(rec.suggestedStartDate.split('T')[0])
    } else if (item.earliestContractDate) {
      setStartDate(item.earliestContractDate.split('T')[0])
    } else {
      setStartDate(new Date().toISOString().split('T')[0])
    }

    setEndDate('')
    setSetAsOwner(rec?.suggestSetAsOwner ?? false)
    setCloseConflicts(true)
  }

  const handleExecuteMigration = async () => {
    if (!selectedItem) return
    if (!targetTeamId) {
      toast.error('Selecione a equipe de destino')
      return
    }
    if (!startDate) {
      toast.error('Informe a data de início da vigência')
      return
    }

    setMigrating(true)
    try {
      const res = await apiService.migrateTeamMemberInconsistency({
        userId: selectedItem.userId,
        targetTeamId: parseInt(targetTeamId, 10),
        startDate,
        endDate: endDate ? endDate : null,
        setAsOwner,
        closeConflictingPeriods: closeConflicts,
      })

      if (res.success) {
        toast.success(res.data?.message || 'Migração realizada com sucesso!')
        setSelectedItem(null)
        fetchInconsistencies()
      } else {
        toast.error(res.message || 'Falha ao migrar usuário')
      }
    } catch (err: any) {
      toast.error(err.message || 'Erro inesperado ao realizar migração')
    } finally {
      setMigrating(false)
    }
  }

  const handleExportXlsx = async () => {
    setExporting(true)
    try {
      const blob = await apiService.exportTeamInconsistenciesXlsx()
      const url = window.URL.createObjectURL(blob)
      const a = document.createElement('a')
      a.href = url
      a.download = `inconsistencias_equipes_${new Date().toISOString().slice(0, 10)}.xlsx`
      document.body.appendChild(a)
      a.click()
      a.remove()
      window.URL.revokeObjectURL(url)
      toast.success('Planilha XLSX exportada com sucesso!')
    } catch (err: any) {
      toast.error(err.message || 'Falha ao exportar planilha')
    } finally {
      setExporting(false)
    }
  }

  // Filtered items
  const filteredItems = useMemo(() => {
    return items.filter((item) => {
      // Type filter
      if (typeFilter !== 'all') {
        const hasType = item.inconsistencies.some((inc) => inc.type === typeFilter)
        if (!hasType) return false
      }

      // Text search
      if (search.trim()) {
        const q = search.toLowerCase()
        const matchName = item.userName.toLowerCase().includes(q)
        const matchEmail = item.userEmail.toLowerCase().includes(q)
        const matchOwned = item.ownedTeamName?.toLowerCase().includes(q) ?? false
        const matchCurrent = item.currentTeamName?.toLowerCase().includes(q) ?? false
        const matchParent = item.parentEmail?.toLowerCase().includes(q) ?? false
        if (!matchName && !matchEmail && !matchOwned && !matchCurrent && !matchParent) {
          return false
        }
      }

      return true
    })
  }, [items, typeFilter, search])

  // Stats calculation
  const stats = useMemo(() => {
    const total = items.length
    const ownersWithoutActive = items.filter((i) =>
      i.inconsistencies.some((inc) => inc.type === 'OwnerWithoutActiveMembership')
    ).length
    const multipleActive = items.filter((i) =>
      i.inconsistencies.some((inc) => inc.type === 'MultipleActiveMemberships')
    ).length
    const inactiveTeam = items.filter((i) =>
      i.inconsistencies.some((inc) => inc.type === 'ActiveInInactiveTeam')
    ).length
    const contractsWithoutTeam = items.filter((i) =>
      i.inconsistencies.some((inc) => inc.type === 'ContractsWithoutActiveTeam')
    ).length

    return { total, ownersWithoutActive, multipleActive, inactiveTeam, contractsWithoutTeam }
  }, [items])

  return (
    <Menu>
      <div className="inconsistencies-page">
        <div className="inconsistencies-header">
          <div>
            <Title order={2} size="h2" style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
              <IconAlertTriangle size={26} color="#f59f00" />
              Inconsistências de Equipes
            </Title>
            <p className="inconsistencies-subtitle">
              Mapeamento de usuários com inconsistências em donos e períodos de equipes. Nenhuma alteração é feita de forma automática; clique em "Migrar" para resolver pontualmente.
            </p>
          </div>
          <Group gap="sm">
            <Button
              variant="default"
              onClick={fetchInconsistencies}
              loading={loading}
              leftSection={<IconRefresh size={16} />}
            >
              Atualizar
            </Button>
            <Button
              variant="filled"
              color="green"
              onClick={handleExportXlsx}
              loading={exporting}
              leftSection={<IconDownload size={16} />}
              disabled={items.length === 0}
            >
              Exportar XLSX
            </Button>
          </Group>
        </div>

        {error && (
          <Alert icon={<IconAlertTriangle size={16} />} title="Erro" color="red" mb="lg">
            {error}
          </Alert>
        )}

        <div className="inconsistencies-stats">
          <div className="stat-card">
            <div className="stat-card__title">Total de Inconsistências</div>
            <div className="stat-card__value">{stats.total}</div>
          </div>
          <div className="stat-card">
            <div className="stat-card__title">Donos Sem Vínculo Ativo</div>
            <div className="stat-card__value" style={{ color: '#e03131' }}>{stats.ownersWithoutActive}</div>
          </div>
          <div className="stat-card">
            <div className="stat-card__title">Múltiplos Vínculos Ativos</div>
            <div className="stat-card__value" style={{ color: '#f59f00' }}>{stats.multipleActive}</div>
          </div>
          <div className="stat-card">
            <div className="stat-card__title">Vínculo em Equipe Inativa</div>
            <div className="stat-card__value" style={{ color: '#ae3ec9' }}>{stats.inactiveTeam}</div>
          </div>
          <div className="stat-card">
            <div className="stat-card__title">Contratos Sem Equipe</div>
            <div className="stat-card__value" style={{ color: '#1971c2' }}>{stats.contractsWithoutTeam}</div>
          </div>
        </div>

        <div className="inconsistencies-controls">
          <Group gap="md" align="flex-end" style={{ flexWrap: 'wrap' }}>
            <TextInput
              placeholder="Buscar por usuário, email, equipe, supervisor..."
              value={search}
              onChange={(e) => setSearch(e.currentTarget.value)}
              leftSection={<IconSearch size={16} />}
              style={{ minWidth: 320, flex: 1 }}
            />
            <Select
              label="Tipo de Inconsistência"
              value={typeFilter}
              onChange={(val) => setTypeFilter(val || 'all')}
              data={[
                { value: 'all', label: 'Todas as inconsistências' },
                { value: 'OwnerWithoutActiveMembership', label: 'Dono sem vínculo ativo' },
                { value: 'MultipleActiveMemberships', label: 'Múltiplos vínculos ativos' },
                { value: 'ActiveInInactiveTeam', label: 'Vínculo em equipe inativa' },
                { value: 'ContractsWithoutActiveTeam', label: 'Contratos sem equipe ativa' },
              ]}
              style={{ minWidth: 260 }}
            />
          </Group>
        </div>

        {loading ? (
          <div style={{ textAlign: 'center', padding: '60px 0' }}>
            <Loader size="md" />
            <Text size="sm" c="dimmed" mt="sm">Analisando inconsistências no banco de dados...</Text>
          </div>
        ) : filteredItems.length === 0 ? (
          <div className="inconsistencies-table-container" style={{ padding: 40, textAlign: 'center' }}>
            <IconAlertTriangle size={36} color="#40c057" style={{ marginBottom: 10 }} />
            <Title order={4} c="green">Nenhuma inconsistência encontrada!</Title>
            <Text size="sm" c="dimmed" mt={4}>
              {items.length === 0
                ? 'Todos os usuários, proprietários e vínculos de equipes estão consistentes.'
                : 'Nenhum usuário corresponde aos filtros aplicados.'}
            </Text>
          </div>
        ) : (
          <div className="inconsistencies-table-container">
            <Table.ScrollContainer minWidth={1100}>
              <Table striped highlightOnHover>
                <Table.Thead>
                  <Table.Tr>
                    <Table.Th>Usuário</Table.Th>
                    <Table.Th>Inconsistências Detectadas</Table.Th>
                    <Table.Th>Supervisor</Table.Th>
                    <Table.Th>Equipe Proprietário</Table.Th>
                    <Table.Th>Equipe Atual</Table.Th>
                    <Table.Th>Histórico de Equipes</Table.Th>
                    <Table.Th style={{ textAlign: 'center' }}>Ação</Table.Th>
                  </Table.Tr>
                </Table.Thead>
                <Table.Tbody>
                  {filteredItems.map((item) => (
                    <Table.Tr key={item.userId}>
                      <Table.Td style={{ minWidth: 180 }}>
                        <div className="user-name-title">{normalizeName(item.userName)}</div>
                        <div className="user-email-subtitle">{item.userEmail}</div>
                        <Badge
                          size="xs"
                          color={item.isActive ? 'teal' : 'gray'}
                          variant="light"
                          mt={4}
                        >
                          {item.isActive ? 'Ativo' : 'Inativo'}
                        </Badge>
                        {item.totalContractsCount > 0 && (
                          <Text size="xs" c="dimmed" mt={2}>
                            {item.totalContractsCount} contrato(s) (1º: {formatDate(item.earliestContractDate)})
                          </Text>
                        )}
                      </Table.Td>

                      <Table.Td style={{ maxWidth: 280 }}>
                        <Stack gap={6}>
                          {item.inconsistencies.map((inc, i) => (
                            <Badge
                              key={i}
                              color={inc.severity === 'error' ? 'red' : 'yellow'}
                              variant="filled"
                              size="sm"
                              style={{ textTransform: 'none', height: 'auto', padding: '4px 8px', whiteSpace: 'normal', textAlign: 'left' }}
                            >
                              {inc.description}
                            </Badge>
                          ))}
                        </Stack>
                      </Table.Td>

                      <Table.Td style={{ minWidth: 170 }}>
                        {item.parentEmail ? (
                          <div>
                            <Group gap={4}>
                              <IconSitemap size={12} color="#868e96" />
                              <Text size="xs" fw={500}>{normalizeName(item.parentUserName)}</Text>
                            </Group>
                            <Text size="xs" c="dimmed">{item.parentEmail}</Text>
                            {item.parentTeamName && (
                              <Badge size="xs" variant="outline" color="indigo" mt={4}>
                                {item.parentTeamName}
                              </Badge>
                            )}
                          </div>
                        ) : (
                          <Text size="xs" c="dimmed" style={{ fontStyle: 'italic' }}>Sem supervisor</Text>
                        )}
                      </Table.Td>

                      <Table.Td>
                        {item.ownedTeamName ? (
                          <Badge color="yellow" variant="light" leftSection={<IconCrown size={12} />}>
                            {item.ownedTeamName}
                          </Badge>
                        ) : (
                          <Text size="xs" c="dimmed">—</Text>
                        )}
                      </Table.Td>

                      <Table.Td>
                        {item.currentTeamName ? (
                          <Badge color="blue" variant="filled">
                            {item.currentTeamName}
                          </Badge>
                        ) : (
                          <Badge color="gray" variant="light">
                            Sem equipe
                          </Badge>
                        )}
                      </Table.Td>

                      <Table.Td style={{ minWidth: 240, maxWidth: 320 }}>
                        {item.teamHistory.length === 0 ? (
                          <Text size="xs" c="dimmed" style={{ fontStyle: 'italic' }}>Nenhum período registrado</Text>
                        ) : (
                          item.teamHistory.map((h) => (
                            <div key={h.userTeamId} className="history-item">
                              <Badge
                                size="xs"
                                color={h.isActivePeriod ? (h.teamIsActive ? 'teal' : 'grape') : 'gray'}
                                variant={h.isActivePeriod ? 'filled' : 'outline'}
                              >
                                {h.teamName}
                              </Badge>
                              {!h.teamIsActive && (
                                <Badge size="xs" color="red" variant="subtle" p={0}>[Inativa]</Badge>
                              )}
                              <Text size="xs" c="dimmed">
                                {formatDate(h.startDate)} - {h.endDate ? formatDate(h.endDate) : 'Presente'}
                              </Text>
                            </div>
                          ))
                        )}
                      </Table.Td>

                      <Table.Td style={{ textAlign: 'center' }}>
                        <Button
                          size="xs"
                          color="blue"
                          variant="light"
                          leftSection={<IconArrowsExchange size={14} />}
                          onClick={() => openMigrationModal(item)}
                        >
                          Migrar
                        </Button>
                      </Table.Td>
                    </Table.Tr>
                  ))}
                </Table.Tbody>
              </Table>
            </Table.ScrollContainer>
          </div>
        )}

        {/* Migration / Resolution Modal */}
        <Modal
          opened={selectedItem !== null}
          onClose={() => !migrating && setSelectedItem(null)}
          title={
            <Group gap="xs">
              <IconArrowsExchange size={20} color="#228be6" />
              <Title order={4}>Resolução de Inconsistência — {normalizeName(selectedItem?.userName)}</Title>
            </Group>
          }
          size="lg"
        >
          {selectedItem && (
            <Stack gap="md">
              <div style={{ background: '#f8f9fa', padding: '10px 14px', borderRadius: 6, fontSize: 13 }}>
                <div><strong>Usuário:</strong> {normalizeName(selectedItem.userName)} ({selectedItem.userEmail})</div>
                {selectedItem.ownedTeamName && (
                  <div style={{ marginTop: 4 }}>
                    <strong>Proprietário de:</strong> {selectedItem.ownedTeamName}
                  </div>
                )}
                {selectedItem.earliestContractDate && (
                  <div style={{ marginTop: 4 }}>
                    <strong>1º Contrato:</strong> {formatDate(selectedItem.earliestContractDate)} (Total: {selectedItem.totalContractsCount})
                  </div>
                )}
              </div>

              {selectedItem.recommendation && (
                <div className="recommendation-box">
                  <div className="recommendation-title">
                    <IconSparkles size={16} />
                    Recomendação do Sistema
                  </div>
                  <div className="recommendation-text">
                    {selectedItem.recommendation.reason}
                  </div>
                </div>
              )}

              <Select
                label="Equipe de Destino"
                placeholder="Selecione a equipe de destino"
                value={targetTeamId}
                onChange={(val) => setTargetTeamId(val || '')}
                data={teams.map((t) => ({
                  value: t.id.toString(),
                  label: `${t.name.toUpperCase()}${t.storeName ? ` (${t.storeName})` : ''}`,
                }))}
                required
                searchable
              />

              <Group grow>
                <div>
                  <Text size="xs" fw={500} mb={4}>Data de Início da Vigência:</Text>
                  <input
                    type="date"
                    value={startDate}
                    onChange={(e) => setStartDate(e.target.value)}
                    style={{
                      width: '100%',
                      padding: '8px 10px',
                      fontSize: 13,
                      border: '1px solid #ced4da',
                      borderRadius: 4,
                    }}
                    required
                  />
                </div>
                <div>
                  <Text size="xs" fw={500} mb={4}>Data de Término (Opcional):</Text>
                  <input
                    type="date"
                    value={endDate}
                    onChange={(e) => setEndDate(e.target.value)}
                    placeholder="Sem data fim (ativo)"
                    style={{
                      width: '100%',
                      padding: '8px 10px',
                      fontSize: 13,
                      border: '1px solid #ced4da',
                      borderRadius: 4,
                    }}
                  />
                </div>
              </Group>

              <Checkbox
                label="Definir este usuário como Dono (Proprietário) da equipe de destino"
                checked={setAsOwner}
                onChange={(e) => setSetAsOwner(e.currentTarget.checked)}
              />

              <Checkbox
                label="Encerrar períodos ativos conflitantes anteriores automaticamente"
                checked={closeConflicts}
                onChange={(e) => setCloseConflicts(e.currentTarget.checked)}
              />

              <Group justify="flex-end" mt="md">
                <Button variant="default" onClick={() => setSelectedItem(null)} disabled={migrating}>
                  Cancelar
                </Button>
                <Button color="blue" onClick={handleExecuteMigration} loading={migrating}>
                  Confirmar Migração
                </Button>
              </Group>
            </Stack>
          )}
        </Modal>
      </div>
    </Menu>
  )
}

export default TeamInconsistenciesTool
