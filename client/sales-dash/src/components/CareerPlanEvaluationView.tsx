import React from 'react'
import {
  Card,
  Table,
  Badge,
  Text,
  Group,
  Progress,
  Tooltip,
  Title,
  Alert
} from '@mantine/core'
import {
  IconTrophy,
  IconAward,
  IconArrowRight,
  IconCheck,
  IconX,
  IconShieldCheck,
  IconChartBar,
  IconAlertCircle,
  IconCalendar
} from '@tabler/icons-react'
import { CareerPlanEvaluation } from '../services/careerPlanService'
import {
  METRIC_DEFINITIONS,
  MetricDefinition,
  MetricValueKind,
  getRequirementMetricKeys
} from './careerPlanMetrics'

const fmtCurrency = (val?: number | null) =>
  val != null
    ? new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format(val)
    : '—'

const fmtPercent = (val?: number | null) =>
  val != null
    ? `${(val * 100).toLocaleString('pt-BR', { minimumFractionDigits: 1, maximumFractionDigits: 1 })}%`
    : '—'

const fmtDate = (dateStr?: string) => {
  if (!dateStr) return '—'
  const d = new Date(dateStr)
  return d.toLocaleDateString('pt-BR', { day: '2-digit', month: '2-digit', year: 'numeric' })
}

interface CareerPlanEvaluationViewProps {
  evaluation: CareerPlanEvaluation
}

const formatMetricValue = (kind: MetricValueKind, value: number | boolean): React.ReactNode => {
  switch (kind) {
    case 'percent':
      return fmtPercent(value as number)
    case 'currency':
      return fmtCurrency(value as number)
    case 'boolean':
      return value ? <Badge color="green" size="sm">Sim</Badge> : <Badge color="gray" size="sm">Não</Badge>
    default:
      return value as number
  }
}

interface MetricBoxProps {
  definition: MetricDefinition
  rawValue: number | boolean
  isRequirement: boolean
}

const MetricBox: React.FC<MetricBoxProps> = ({ definition, rawValue, isRequirement }) => (
  <Tooltip label={definition.tooltip} multiline w={300} withArrow position="top">
    <div
      className={`career-plan-metric-box${isRequirement ? ' requirement' : ''}`}
      data-requirement={isRequirement ? 'true' : 'false'}
      data-metric-key={definition.key}
      style={{ cursor: 'help' }}
    >
      <div className="career-plan-metric-box-label" style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <span>{definition.label}</span>
        <Group gap={4} wrap="nowrap">
          {isRequirement && <IconShieldCheck size={14} color="#2563eb" />}
          <IconAlertCircle size={14} style={{ opacity: 0.6 }} />
        </Group>
      </div>
      <div className="career-plan-metric-box-value">{formatMetricValue(definition.kind, rawValue)}</div>
    </div>
  </Tooltip>
)

export const CareerPlanEvaluationView: React.FC<CareerPlanEvaluationViewProps> = ({ evaluation }) => {
  const requirementKeys = getRequirementMetricKeys(evaluation.requirements)
  return (
    <>
      {/* Level progression cards */}
      <div className="career-plan-level-progression">
        {/* Current level */}
        <Card withBorder radius="md" p="lg" className="career-plan-card current">
          <div className="career-plan-card-tag">Nível Atual</div>
          <div className="career-plan-card-name">
            <IconTrophy size={26} color="#6366f1" />
            <span>{evaluation.currentLevel?.name || 'Sem nível ativo'}</span>
          </div>
          {evaluation.currentLevel ? (
            <div className="career-plan-card-details">
              {evaluation.currentLevel.salesGoal != null && (
                <Badge color="blue" variant="light">
                  Meta: {fmtCurrency(evaluation.currentLevel.salesGoal)}
                </Badge>
              )}
              {evaluation.currentLevel.retention != null && (
                <Badge color="orange" variant="light">
                  Retenção: {evaluation.currentLevel.retention}%
                </Badge>
              )}
              {evaluation.currentLevel.prize && (
                <Badge color="violet" variant="light">
                  Prêmio: {evaluation.currentLevel.prize}
                </Badge>
              )}
            </div>
          ) : (
            <Text size="sm" c="dimmed">Usuário ainda não possui uma classificação cadastrada.</Text>
          )}
        </Card>

        {/* Arrow & Reference Closing Info */}
        <div className="career-plan-arrow-col">
          <IconArrowRight size={32} color="#6366f1" />
          <Tooltip label={`Data de fechamento (dia ${evaluation.closingDay}) considerada para meses fechados`}>
            <Badge color="gray" variant="outline" leftSection={<IconCalendar size={12} />}>
              Ref: {fmtDate(evaluation.referenceDate)}
            </Badge>
          </Tooltip>
        </div>

        {/* Target level */}
        <Card withBorder radius="md" p="lg" className="career-plan-card target">
          <div className="career-plan-card-tag">Próximo Nível (Objetivo)</div>
          <div className="career-plan-card-name">
            <IconAward size={26} color="#10b981" />
            <span>{evaluation.targetLevel?.name || 'Nível Máximo Atingido'}</span>
          </div>
          {evaluation.targetLevel ? (
            <div className="career-plan-card-details">
              {evaluation.targetLevel.salesGoal != null && (
                <Badge color="teal" variant="light">
                  Meta: {fmtCurrency(evaluation.targetLevel.salesGoal)}
                </Badge>
              )}
              {evaluation.targetLevel.retention != null && (
                <Badge color="orange" variant="light">
                  Retenção: {evaluation.targetLevel.retention}%
                </Badge>
              )}
              {evaluation.targetLevel.prize && (
                <Badge color="grape" variant="light">
                  Prêmio: {evaluation.targetLevel.prize}
                </Badge>
              )}
            </div>
          ) : (
            <Text size="sm" c="dimmed">Não há próximo nível configurado após o atual.</Text>
          )}
        </Card>
      </div>

      {/* Requirements Table Section */}
      <Card withBorder radius="md" p="lg" className="career-plan-section-card">
        <Group gap="xs" mb="md">
          <IconShieldCheck size={22} color="#6366f1" />
          <Title order={3} style={{ color: '#1c1c1e', fontWeight: 700, fontSize: '18px' }}>
            Requisitos para Promoção
          </Title>
        </Group>

        {!evaluation.hasRulesConfigured ? (
          <Alert icon={<IconAlertCircle size={16} />} color="blue" variant="light">
            Nenhum requisito configurado no plano de carreira para este nível de destino.
          </Alert>
        ) : (
          <Table.ScrollContainer minWidth={700}>
            <Table striped highlightOnHover verticalSpacing="sm">
              <Table.Thead>
                <Table.Tr>
                  <Table.Th>Requisito</Table.Th>
                  <Table.Th>Métrica</Table.Th>
                  <Table.Th>Valor Atual</Table.Th>
                  <Table.Th>Meta / Regra</Table.Th>
                  <Table.Th>Status</Table.Th>
                  <Table.Th style={{ width: 180 }}>Progresso</Table.Th>
                </Table.Tr>
              </Table.Thead>
              <Table.Tbody>
                {evaluation.requirements.map((req, idx) => {
                  const progressPercent = Math.min(100, Math.round(req.progress * 100))
                  return (
                    <Table.Tr key={idx}>
                      <Table.Td style={{ fontWeight: 600 }}>{req.label}</Table.Td>
                      <Table.Td>
                        <Badge size="sm" variant="dot" color="indigo">
                          {req.metric}
                        </Badge>
                      </Table.Td>
                      <Table.Td style={{ fontWeight: 600 }}>{req.formattedCurrent}</Table.Td>
                      <Table.Td>{req.formattedTarget}</Table.Td>
                      <Table.Td>
                        {req.passed ? (
                          <Badge color="green" variant="light" leftSection={<IconCheck size={12} />}>
                            Atingido
                          </Badge>
                        ) : (
                          <Badge color="red" variant="light" leftSection={<IconX size={12} />}>
                            Não atingido
                          </Badge>
                        )}
                      </Table.Td>
                      <Table.Td>
                        <Group gap="xs" wrap="nowrap">
                          <Progress
                            value={progressPercent}
                            color={req.passed ? 'teal' : 'orange'}
                            size="sm"
                            radius="xl"
                            style={{ flex: 1 }}
                          />
                          <Text size="xs" c="dimmed" style={{ minWidth: 34, textAlign: 'right' }}>
                            {progressPercent}%
                          </Text>
                        </Group>
                      </Table.Td>
                    </Table.Tr>
                  )
                })}
              </Table.Tbody>
            </Table>
          </Table.ScrollContainer>
        )}
      </Card>

      {/* Consolidated Raw Metrics Section */}
      <Card withBorder radius="md" p="lg" className="career-plan-section-card">
        <Group gap="xs" mb={4}>
          <IconChartBar size={22} color="#6366f1" />
          <Title order={3} style={{ color: '#1c1c1e', fontWeight: 700, fontSize: '18px' }}>
            Métricas Consolidadas do Usuário
          </Title>
        </Group>
        <Text size="xs" c="dimmed" mb="md">
          Calculadas exclusivamente a partir dos contratos e vínculos diretos do consultor nos períodos fechados.
        </Text>

        <Group gap={6} mb="md" className="career-plan-metrics-legend" data-testid="requirement-legend">
          <span className="career-plan-metrics-legend-swatch" />
          <IconShieldCheck size={14} color="#2563eb" />
          <Text size="xs" c="dimmed">
            Métrica usada em um requisito de promoção
          </Text>
        </Group>

        <div className="career-plan-metrics-grid">
          {METRIC_DEFINITIONS.map(def => (
            <MetricBox
              key={def.key}
              definition={def}
              rawValue={evaluation.rawMetrics[def.key]}
              isRequirement={requirementKeys.has(def.key)}
            />
          ))}
        </div>
      </Card>
    </>
  )
}
export default CareerPlanEvaluationView
