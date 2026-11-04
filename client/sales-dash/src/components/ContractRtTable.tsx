import React, { useState, useEffect, useCallback } from 'react'
import {
  Card, Table, Text, Badge, Loader, Alert, Group, Pagination, Tooltip, Stack, Box, Button
} from '@mantine/core'
import {
  IconAlertCircle,
  IconReceipt2,
  IconAdjustments,
  IconRotateClockwise,
  IconX
} from '@tabler/icons-react'
import {
  careerPlanService,
  CareerPlanEvaluation,
  ContractRtPage,
  ContractRtOverride,
  RtCellStatus,
  RtCurrentStatus
} from '../services/careerPlanService'

interface ContractRtTableProps {
  userId?: string
  isMe?: boolean
  asOfDate?: string
  onSimulationChange?: (simulatedEvaluation: CareerPlanEvaluation | null) => void
  resetTrigger?: number
}

const fmtCurrency = (val?: number | null) =>
  val != null
    ? new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format(val)
    : '—'

const fmtDate = (dateStr?: string) => {
  if (!dateStr) return '—'
  const d = new Date(dateStr)
  return d.toLocaleDateString('pt-BR', { day: '2-digit', month: '2-digit', year: 'numeric' })
}

const formatStatusLabel = (status: string) => {
  switch (status?.toLowerCase()) {
    case 'active':
      return 'Ativo'
    case 'defaulted':
      return 'Cancelado'
    case 'late1':
      return 'Atrasado 1'
    case 'late2':
      return 'Atrasado 2'
    case 'late3':
      return 'Atrasado 3'
    case 'transferred':
      return 'Transferido'
    case 'desistente':
      return 'Desistente'
    case 'awaitingpayment':
      return 'Aguardando Pagamento'
    default:
      return status || '—'
  }
}

const getStatusColor = (status: string) => {
  switch (status?.toLowerCase()) {
    case 'active':
      return 'green'
    case 'late1':
    case 'late2':
    case 'late3':
      return 'yellow'
    case 'defaulted':
      return 'red'
    default:
      return 'gray'
  }
}

const SignalCircle: React.FC<{
  type: 'rt' | 'current'
  rtStatus?: RtCellStatus
  currentStatus?: RtCurrentStatus
}> = ({ type, rtStatus, currentStatus }) => {
  if (type === 'current') {
    const isOverdue = currentStatus === 'overdue'
    const color = isOverdue ? '#ef4444' : '#22c55e'
    const label = isOverdue ? 'RT Atual: Com parcelas em atraso' : 'RT Atual: Contrato 100% em dia'
    return (
      <Tooltip label={label} withArrow position="top">
        <span
          style={{
            display: 'inline-block',
            width: 14,
            height: 14,
            borderRadius: '50%',
            backgroundColor: color,
            boxShadow: `0 0 0 2px ${isOverdue ? '#fee2e2' : '#dcfce7'}`,
            cursor: 'help'
          }}
        />
      </Tooltip>
    )
  }

  // RT1..RT6
  let color = '#ffffff'
  let border = '1.5px solid #d1d5db'
  let label = 'Não vencido'

  if (rtStatus === 'paid') {
    color = '#22c55e'
    border = '1.5px solid #16a34a'
    label = 'Pago'
  } else if (rtStatus === 'unpaid') {
    color = '#eab308'
    border = '1.5px solid #ca8a04'
    label = 'Não pago / em atraso'
  }

  return (
    <Tooltip label={label} withArrow position="top">
      <span
        style={{
          display: 'inline-block',
          width: 14,
          height: 14,
          borderRadius: '50%',
          backgroundColor: color,
          border,
          cursor: 'help'
        }}
      />
    </Tooltip>
  )
}

export const ContractRtTable: React.FC<ContractRtTableProps> = ({
  userId,
  isMe = false,
  asOfDate,
  onSimulationChange,
  resetTrigger
}) => {
  const [loading, setLoading] = useState<boolean>(true)
  const [error, setError] = useState<string | null>(null)
  const [pageData, setPageData] = useState<ContractRtPage | null>(null)
  const [page, setPage] = useState<number>(1)
  const [isSimulatorActive, setIsSimulatorActive] = useState<boolean>(false)
  const [overrides, setOverrides] = useState<Record<string, RtCurrentStatus>>({})
  const [simulating, setSimulating] = useState<boolean>(false)

  const fetchData = useCallback(async (targetPage: number) => {
    if (!isMe && !userId) return
    setLoading(true)
    setError(null)
    try {
      const data = isMe
        ? await careerPlanService.getMyContractRtTracking(targetPage, 100, asOfDate)
        : await careerPlanService.getContractRtTracking(userId!, targetPage, 100, asOfDate)
      setPageData(data)
    } catch (err: any) {
      setError(err?.message || 'Erro ao carregar o acompanhamento de RT dos contratos.')
    } finally {
      setLoading(false)
    }
  }, [userId, isMe, asOfDate])

  // Reset page and simulation when user or asOfDate changes, or on explicit resetTrigger
  useEffect(() => {
    setPage(1)
    fetchData(1)
  }, [fetchData])

  useEffect(() => {
    setOverrides({})
    setIsSimulatorActive(false)
  }, [userId, asOfDate, resetTrigger])

  const handleClearSimulation = useCallback(() => {
    setOverrides({})
    onSimulationChange?.(null)
  }, [onSimulationChange])

  const handleToggleSimulator = () => {
    if (isSimulatorActive) {
      setIsSimulatorActive(false)
      handleClearSimulation()
    } else {
      setIsSimulatorActive(true)
    }
  }

  const handleCellToggle = async (contractNumber: string, originalCurrent: RtCurrentStatus) => {
    const effectiveCurrent = overrides[contractNumber] || originalCurrent
    const nextCurrent: RtCurrentStatus = effectiveCurrent === 'upToDate' ? 'overdue' : 'upToDate'

    const updatedOverrides = { ...overrides }
    if (nextCurrent === originalCurrent) {
      delete updatedOverrides[contractNumber]
    } else {
      updatedOverrides[contractNumber] = nextCurrent
    }

    setOverrides(updatedOverrides)

    const overrideKeys = Object.keys(updatedOverrides)
    if (overrideKeys.length === 0) {
      onSimulationChange?.(null)
      return
    }

    const overrideList: ContractRtOverride[] = overrideKeys.map(cNum => ({
      contractNumber: cNum,
      simulatedCurrentStatus: updatedOverrides[cNum]
    }))

    try {
      setSimulating(true)
      const simResult = isMe
        ? await careerPlanService.simulateMyCareerPlan(overrideList, asOfDate)
        : await careerPlanService.simulateUserCareerPlan(userId!, overrideList, asOfDate)
      onSimulationChange?.(simResult)
    } catch (err: any) {
      console.error('Simulation error:', err)
    } finally {
      setSimulating(false)
    }
  }

  const handlePageChange = (newPage: number) => {
    setPage(newPage)
    fetchData(newPage)
  }

  // If user is Daleth or above, table should not render
  if (!loading && pageData && !pageData.isApplicable) {
    return null
  }

  const modifiedCount = Object.keys(overrides).length

  return (
    <Card className="career-plan-card" style={{ marginTop: 24, padding: 24 }}>
      <Group justify="space-between" align="flex-start" mb="md" wrap="wrap" gap="sm">
        <div>
          <Group gap="xs">
            <IconReceipt2 size={22} style={{ color: '#6366f1' }} />
            <Text fw={700} fz={18} c="#111827">
              Acompanhamento de Contratos (RT1 a RT6 e RT Atual)
            </Text>
          </Group>
          <Text fz={13} c="#6b7280" mt={4}>
            Contratos dos últimos 15 meses para consultores até o nível Guimel (Alef, Beth e Guimel).
          </Text>
        </div>

        <Group gap="xs" align="center">
          {pageData && (
            <Badge variant="light" color="indigo" size="lg">
              {pageData.totalCount} contrato{pageData.totalCount === 1 ? '' : 's'}
            </Badge>
          )}

          {!isSimulatorActive ? (
            <Button
              size="xs"
              variant="light"
              color="indigo"
              leftSection={<IconAdjustments size={15} />}
              onClick={handleToggleSimulator}
            >
              Simulador
            </Button>
          ) : (
            <>
              {modifiedCount > 0 && (
                <Button
                  size="xs"
                  variant="light"
                  color="yellow"
                  loading={simulating}
                  leftSection={<IconRotateClockwise size={15} />}
                  onClick={handleClearSimulation}
                >
                  Limpar ({modifiedCount})
                </Button>
              )}
              <Button
                size="xs"
                variant="subtle"
                color="red"
                leftSection={<IconX size={15} />}
                onClick={handleToggleSimulator}
              >
                Sair do simulador
              </Button>
            </>
          )}
        </Group>
      </Group>

      {/* Legenda visual dos RTs */}
      <Box
        p="xs"
        mb="md"
        style={{
          background: '#f8fafc',
          borderRadius: 8,
          border: '1px solid #e2e8f0',
          fontSize: 12,
          color: '#475569'
        }}
      >
        <Group gap="lg" wrap="wrap">
          <Text fw={600} fz={12} c="#1e293b">Legenda:</Text>
          <Group gap={6}>
            <span style={{ display: 'inline-block', width: 10, height: 10, borderRadius: '50%', backgroundColor: '#22c55e' }} />
            <span>Verde: Pago / Em dia</span>
          </Group>
          <Group gap={6}>
            <span style={{ display: 'inline-block', width: 10, height: 10, borderRadius: '50%', backgroundColor: '#eab308' }} />
            <span>Amarelo: Mês não pago / atrasado</span>
          </Group>
          <Group gap={6}>
            <span style={{ display: 'inline-block', width: 10, height: 10, borderRadius: '50%', backgroundColor: '#ffffff', border: '1.5px solid #d1d5db' }} />
            <span>Branco: Mês não vencido</span>
          </Group>
          <Group gap={6}>
            <span style={{ display: 'inline-block', width: 10, height: 10, borderRadius: '50%', backgroundColor: '#ef4444' }} />
            <span>Vermelho: RT Atual com pendência</span>
          </Group>
        </Group>
      </Box>

      {isSimulatorActive && (
        <Alert
          icon={<IconAdjustments size={16} />}
          title="Modo Simulador Ativo"
          color="indigo"
          variant="light"
          mb="md"
          style={{ fontSize: 13 }}
        >
          Clique nas células pulsantes da coluna <b>RT ATUAL</b> para alternar entre Em Dia (Verde) e Atrasado (Vermelho).
          A retenção e os requisitos de carreira são recalculados instantaneamente sem gravar dados.
          As linhas modificadas recebem destaque em amarelo.
        </Alert>
      )}

      {loading && (
        <Group justify="center" p="xl">
          <Loader size="md" color="indigo" />
          <Text fz={14} c="#6b7280">Carregando contratos...</Text>
        </Group>
      )}

      {error && !loading && (
        <Alert icon={<IconAlertCircle size={16} />} title="Atenção" color="red">
          {error}
        </Alert>
      )}

      {!loading && !error && pageData && pageData.items.length === 0 && (
        <Text fz={14} c="#6b7280" ta="center" py="xl">
          Nenhum contrato elegível encontrado nos últimos 15 meses.
        </Text>
      )}

      {!loading && !error && pageData && pageData.items.length > 0 && (
        <Stack gap="md">
          <div style={{ overflowX: 'auto' }}>
            <Table striped highlightOnHover withTableBorder withColumnBorders style={{ minWidth: 900 }}>
              <Table.Thead style={{ backgroundColor: '#f1f5f9' }}>
                <Table.Tr>
                  <Table.Th style={{ fontSize: 13 }}>Nº do Contrato</Table.Th>
                  <Table.Th style={{ fontSize: 13 }}>Cliente</Table.Th>
                  <Table.Th style={{ fontSize: 13, textAlign: 'right' }}>Valor</Table.Th>
                  <Table.Th style={{ fontSize: 13 }}>Data do Contrato</Table.Th>
                  <Table.Th style={{ fontSize: 13 }}>Status</Table.Th>
                  <Table.Th style={{ fontSize: 13, textAlign: 'center', width: 50 }}>RT1</Table.Th>
                  <Table.Th style={{ fontSize: 13, textAlign: 'center', width: 50 }}>RT2</Table.Th>
                  <Table.Th style={{ fontSize: 13, textAlign: 'center', width: 50 }}>RT3</Table.Th>
                  <Table.Th style={{ fontSize: 13, textAlign: 'center', width: 50 }}>RT4</Table.Th>
                  <Table.Th style={{ fontSize: 13, textAlign: 'center', width: 50 }}>RT5</Table.Th>
                  <Table.Th style={{ fontSize: 13, textAlign: 'center', width: 50 }}>RT6</Table.Th>
                  <Table.Th style={{ fontSize: 13, textAlign: 'center', width: 85 }}>RT ATUAL</Table.Th>
                </Table.Tr>
              </Table.Thead>
              <Table.Tbody>
                {pageData.items.map((row) => {
                  const isModified = Boolean(overrides[row.contractNumber])
                  const effectiveCurrent = overrides[row.contractNumber] || row.current
                  return (
                    <Table.Tr
                      key={row.contractNumber}
                      className={isModified ? 'rt-row-modified' : undefined}
                    >
                      <Table.Td style={{ fontWeight: 600, fontSize: 13 }}>
                        <Group gap={6} wrap="nowrap">
                          <span>{row.contractNumber}</span>
                          {isModified && (
                            <Badge size="xs" color="yellow" variant="filled">
                              Simulado
                            </Badge>
                          )}
                        </Group>
                      </Table.Td>
                      <Table.Td style={{ fontSize: 13 }}>{row.customerName || '—'}</Table.Td>
                      <Table.Td style={{ fontSize: 13, textAlign: 'right', fontWeight: 500 }}>
                        {fmtCurrency(row.totalAmount)}
                      </Table.Td>
                      <Table.Td style={{ fontSize: 13 }}>{fmtDate(row.saleStartDate)}</Table.Td>
                      <Table.Td style={{ fontSize: 13 }}>
                        <Badge
                          size="sm"
                          variant="dot"
                          color={getStatusColor(row.status)}
                        >
                          {formatStatusLabel(row.status)}
                        </Badge>
                      </Table.Td>
                      {row.rts.map((rtStatus, idx) => (
                        <Table.Td key={idx} style={{ textAlign: 'center' }}>
                          <SignalCircle type="rt" rtStatus={rtStatus} />
                        </Table.Td>
                      ))}
                      <Table.Td style={{ textAlign: 'center' }}>
                        {isSimulatorActive ? (
                          <Tooltip
                            label={`Clique para simular: ${effectiveCurrent === 'upToDate' ? 'Marcar como Atrasado (Vermelho)' : 'Marcar como Em Dia (Verde)'}`}
                            withArrow
                            position="top"
                          >
                            <button
                              type="button"
                              className="rt-interactive-cell"
                              onClick={() => handleCellToggle(row.contractNumber, row.current)}
                              aria-label={`Simular status do contrato ${row.contractNumber}`}
                              style={{
                                border: isModified ? '1.5px dashed #ca8a04' : '1.5px dashed #6366f1',
                                backgroundColor: isModified ? '#fef9c3' : '#f5f3ff',
                                outline: 'none'
                              }}
                            >
                              <span
                                style={{
                                  display: 'inline-block',
                                  width: 14,
                                  height: 14,
                                  borderRadius: '50%',
                                  backgroundColor: effectiveCurrent === 'overdue' ? '#ef4444' : '#22c55e',
                                  boxShadow: `0 0 0 2px ${effectiveCurrent === 'overdue' ? '#fee2e2' : '#dcfce7'}`
                                }}
                              />
                            </button>
                          </Tooltip>
                        ) : (
                          <SignalCircle type="current" currentStatus={row.current} />
                        )}
                      </Table.Td>
                    </Table.Tr>
                  )
                })}
              </Table.Tbody>
            </Table>
          </div>

          {pageData.totalPages > 1 && (
            <Group justify="center" mt="md">
              <Pagination
                total={pageData.totalPages}
                value={page}
                onChange={handlePageChange}
                color="indigo"
                size="sm"
              />
            </Group>
          )}
        </Stack>
      )}
    </Card>
  )
}

export default ContractRtTable
