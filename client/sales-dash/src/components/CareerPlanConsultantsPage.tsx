import React, { useEffect, useState, useMemo } from 'react'
import {
  Card,
  Table,
  Badge,
  Text,
  TextInput,
  Select,
  Button,
  Group,
  Stack,
  Loader,
  Alert,
  Pagination,
  Progress,
  Tooltip,
  Paper,
  Title
} from '@mantine/core'
import {
  IconSearch,
  IconRefresh,
  IconTrophy,
  IconAlertCircle,
  IconExternalLink,
  IconCheck,
  IconArrowRight
} from '@tabler/icons-react'
import {
  careerPlanService,
  CareerPlanConsultantSnapshot
} from '../services/careerPlanService'
import Menu from './Menu'

const fmtCurrency = (val?: number | null) => {
  if (val == null) return 'R$ 0,00'
  return new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format(val)
}

const fmtPercent = (val?: number | null) => {
  if (val == null) return '0,0%'
  return `${(val * 100).toLocaleString('pt-BR', { minimumFractionDigits: 1, maximumFractionDigits: 1 })}%`
}

const fmtDateTime = (isoDate?: string | null) => {
  if (!isoDate) return 'Não calculado'
  const d = new Date(isoDate)
  return d.toLocaleString('pt-BR', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit'
  })
}

export const CareerPlanConsultantsPage: React.FC = () => {
  const [consultants, setConsultants] = useState<CareerPlanConsultantSnapshot[]>([])
  const [lastCalculatedAt, setLastCalculatedAt] = useState<string | null>(null)
  const [loading, setLoading] = useState<boolean>(true)
  const [recalculating, setRecalculating] = useState<boolean>(false)
  const [error, setError] = useState<string | null>(null)
  const [successMessage, setSuccessMessage] = useState<string | null>(null)

  // Filters
  const [search, setSearch] = useState<string>('')
  const [selectedLevel, setSelectedLevel] = useState<string | null>('ALL')

  // Pagination
  const [page, setPage] = useState<number>(1)
  const pageSize = 20

  const loadData = async () => {
    try {
      setLoading(true)
      setError(null)
      const res = await careerPlanService.getConsultantsList()
      setConsultants(res.consultants || [])
      setLastCalculatedAt(res.lastCalculatedAt)
    } catch (err: any) {
      setError(err?.message || 'Falha ao carregar lista de consultores.')
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    loadData()
  }, [])

  const handleRecalculate = async () => {
    try {
      setRecalculating(true)
      setError(null)
      setSuccessMessage(null)
      const res = await careerPlanService.recalculateConsultants()
      setSuccessMessage(`Recálculo concluído com sucesso! ${res.processedCount} consultores avaliados.`)
      await loadData()
    } catch (err: any) {
      setError(err?.message || 'Falha ao recalcular consultores.')
    } finally {
      setRecalculating(false)
    }
  }

  // Filtered consultants
  const filtered = useMemo(() => {
    let result = consultants

    if (search.trim()) {
      const q = search.trim().toLowerCase()
      result = result.filter(
        c =>
          c.userName.toLowerCase().includes(q) ||
          c.userEmail.toLowerCase().includes(q)
      )
    }

    if (selectedLevel && selectedLevel !== 'ALL') {
      result = result.filter(c => {
        if (selectedLevel === 'UNCLASSIFIED') {
          return c.currentLevelName === 'Sem Classificação' || !c.currentLevelName
        }
        return c.currentLevelName.toLowerCase() === selectedLevel.toLowerCase()
      })
    }

    return result
  }, [consultants, search, selectedLevel])

  const totalPages = Math.max(1, Math.ceil(filtered.length / pageSize))
  const pagedItems = useMemo(() => {
    const start = (page - 1) * pageSize
    return filtered.slice(start, start + pageSize)
  }, [filtered, page, pageSize])

  const navigateToConsultant = (userId: string) => {
    // Navigates to the individual performance page passing userId
    window.location.hash = `#/career-plan?userId=${userId}`
  }

  return (
    <Menu>
      <div className="career-plan-container" style={{ padding: '24px', maxWidth: '1400px', margin: '0 auto' }}>
        {/* Header */}
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', marginBottom: 24, flexWrap: 'wrap', gap: 16 }}>
          <div>
            <Group gap="xs" align="center">
              <IconTrophy size={28} color="#6366f1" />
              <Title order={2} style={{ color: '#1c1c1e', fontWeight: 700 }}>
                Lista de consultores
              </Title>
            </Group>
            <Text size="sm" c="dimmed" mt={4}>
              Acompanhamento consolidado e pré-calculado de consultores (Sem classificação, Alef e Beth)
            </Text>
          </div>

        <Group gap="sm" align="center">
          <Badge color="gray" variant="light" size="lg">
            Último cálculo: {fmtDateTime(lastCalculatedAt)}
          </Badge>
          <Button
            leftSection={<IconRefresh size={16} />}
            variant="filled"
            color="indigo"
            onClick={handleRecalculate}
            loading={recalculating}
          >
            Recalcular agora
          </Button>
        </Group>
      </div>

      {error && (
        <Alert icon={<IconAlertCircle size={16} />} color="red" mb="md" withCloseButton onClose={() => setError(null)}>
          {error}
        </Alert>
      )}

      {successMessage && (
        <Alert icon={<IconCheck size={16} />} color="green" mb="md" withCloseButton onClose={() => setSuccessMessage(null)}>
          {successMessage}
        </Alert>
      )}

      {/* Filter Card */}
      <Card withBorder radius="md" p="md" mb="lg">
        <Group grow align="flex-end">
          <TextInput
            label="Buscar Consultor"
            placeholder="Nome ou e-mail..."
            leftSection={<IconSearch size={16} />}
            value={search}
            onChange={e => {
              setSearch(e.currentTarget.value)
              setPage(1)
            }}
          />

          <Select
            label="Classificação Atual"
            value={selectedLevel}
            onChange={val => {
              setSelectedLevel(val)
              setPage(1)
            }}
            data={[
              { value: 'ALL', label: 'Todas as classificações' },
              { value: 'UNCLASSIFIED', label: 'Sem Classificação' },
              { value: 'Alef', label: 'Alef' },
              { value: 'Beth', label: 'Beth' }
            ]}
          />
        </Group>
      </Card>

      {/* Data Table */}
      <Card withBorder radius="md" p="md">
        {loading ? (
          <Group justify="center" p="xl">
            <Loader size="md" color="indigo" />
            <Text size="sm" c="dimmed">Carregando consultores...</Text>
          </Group>
        ) : filtered.length === 0 ? (
          <Paper p="xl" ta="center">
            <Text c="dimmed" size="md">
              Nenhum consultor encontrado para os filtros selecionados.
            </Text>
          </Paper>
        ) : (
          <Stack gap="md">
            <div style={{ overflowX: 'auto' }}>
              <Table striped highlightOnHover verticalSpacing="sm" horizontalSpacing="md">
                <Table.Thead>
                  <Table.Tr>
                    <Table.Th>Consultor</Table.Th>
                    <Table.Th>Classificação Atual</Table.Th>
                    <Table.Th>Target de Classificação</Table.Th>
                    <Table.Th style={{ minWidth: 220 }}>Produção Atual (12m)</Table.Th>
                    <Table.Th>Retenção Atual (15m)</Table.Th>
                    <Table.Th style={{ textAlign: 'center' }}>Ação</Table.Th>
                  </Table.Tr>
                </Table.Thead>
                <Table.Tbody>
                  {pagedItems.map(c => {
                    const pctVal = Math.round(c.productionProgressPct * 100)
                    const isRetentionOk = c.strictRetention15m >= 0.79

                    return (
                      <Table.Tr key={c.userId}>
                        <Table.Td>
                          <Text fw={600} size="sm">
                            {c.userName}
                          </Text>
                          <Text size="xs" c="dimmed">
                            {c.userEmail}
                          </Text>
                        </Table.Td>

                        <Table.Td>
                          <Badge
                            color={
                              c.currentLevelName === 'Beth'
                                ? 'violet'
                                : c.currentLevelName === 'Alef'
                                ? 'blue'
                                : 'gray'
                            }
                            variant="light"
                          >
                            {c.currentLevelName}
                          </Badge>
                        </Table.Td>

                        <Table.Td>
                          <Group gap={4} wrap="nowrap">
                            <IconArrowRight size={14} color="#10b981" />
                            <Badge color="teal" variant="light">
                              {c.targetLevelName}
                            </Badge>
                            {c.salesGoal != null && (
                              <Text size="xs" c="dimmed">
                                ({fmtCurrency(c.salesGoal)})
                              </Text>
                            )}
                          </Group>
                        </Table.Td>

                        <Table.Td>
                          <Group justify="space-between" mb={4}>
                            <Text size="sm" fw={600}>
                              {fmtCurrency(c.activeProduction12m)}
                            </Text>
                            <Badge color={pctVal >= 100 ? 'teal' : 'indigo'} variant="outline" size="sm">
                              {pctVal}%
                            </Badge>
                          </Group>
                          <Progress
                            value={pctVal}
                            color={pctVal >= 100 ? 'teal' : 'indigo'}
                            size="sm"
                            radius="xl"
                          />
                        </Table.Td>

                        <Table.Td>
                          <Badge
                            color={isRetentionOk ? 'teal' : 'orange'}
                            variant="light"
                            size="md"
                          >
                            {fmtPercent(c.strictRetention15m)}
                          </Badge>
                        </Table.Td>

                        <Table.Td style={{ textAlign: 'center' }}>
                          <Tooltip label="Ver Performance do Consultor">
                            <Button
                              variant="subtle"
                              color="indigo"
                              size="xs"
                              rightSection={<IconExternalLink size={14} />}
                              onClick={() => navigateToConsultant(c.userId)}
                            >
                              Ver
                            </Button>
                          </Tooltip>
                        </Table.Td>
                      </Table.Tr>
                    )
                  })}
                </Table.Tbody>
              </Table>
            </div>

            <Group justify="space-between" align="center" mt="md">
              <Text size="xs" c="dimmed">
                Exibindo {pagedItems.length} de {filtered.length} consultores
              </Text>
              {totalPages > 1 && (
                <Pagination
                  value={page}
                  onChange={setPage}
                  total={totalPages}
                  color="indigo"
                  size="sm"
                />
              )}
            </Group>
          </Stack>
        )}
      </Card>
    </div>
  </Menu>
)
}
