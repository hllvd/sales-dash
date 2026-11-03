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

export const CareerPlanEvaluationView: React.FC<CareerPlanEvaluationViewProps> = ({ evaluation }) => {
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

        <div className="career-plan-metrics-grid">
          <Tooltip
            label="Percentual financeiro de retenção padrão na janela dos últimos 6 meses fechados. Considera perda apenas contratos com status Defaulted. Exclui Desistentes e AwaitingPayment."
            multiline
            w={300}
            withArrow
            position="top"
          >
            <div className="career-plan-metric-box" style={{ cursor: 'help' }}>
              <div className="career-plan-metric-box-label" style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                <span>Retenção Normal (6 meses)</span>
                <IconAlertCircle size={14} style={{ opacity: 0.6 }} />
              </div>
              <div className="career-plan-metric-box-value">{fmtPercent(evaluation.rawMetrics.retention6m)}</div>
            </div>
          </Tooltip>

          <Tooltip
            label="Percentual financeiro de retenção padrão na janela dos últimos 12 meses fechados. Considera perda apenas contratos com status Defaulted. Exclui Desistentes e AwaitingPayment."
            multiline
            w={300}
            withArrow
            position="top"
          >
            <div className="career-plan-metric-box" style={{ cursor: 'help' }}>
              <div className="career-plan-metric-box-label" style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                <span>Retenção Normal (12 meses)</span>
                <IconAlertCircle size={14} style={{ opacity: 0.6 }} />
              </div>
              <div className="career-plan-metric-box-value">{fmtPercent(evaluation.rawMetrics.retention12m)}</div>
            </div>
          </Tooltip>

          <Tooltip
            label="Percentual financeiro de retenção padrão na janela dos últimos 15 meses fechados. Considera perda apenas contratos com status Defaulted. Exclui Desistentes e AwaitingPayment."
            multiline
            w={300}
            withArrow
            position="top"
          >
            <div className="career-plan-metric-box" style={{ cursor: 'help' }}>
              <div className="career-plan-metric-box-label" style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                <span>Retenção Normal (15 meses)</span>
                <IconAlertCircle size={14} style={{ opacity: 0.6 }} />
              </div>
              <div className="career-plan-metric-box-value">{fmtPercent(evaluation.rawMetrics.retention15m)}</div>
            </div>
          </Tooltip>

          <Tooltip
            label="Percentual de retenção estrita na janela de 6 meses. Considera perda: Defaulted, Late1, Late2, Late3. Exclui Desistentes e AwaitingPayment."
            multiline
            w={300}
            withArrow
            position="top"
          >
            <div className="career-plan-metric-box" style={{ cursor: 'help' }}>
              <div className="career-plan-metric-box-label" style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                <span>Retenção Estrita (6 meses)</span>
                <IconAlertCircle size={14} style={{ opacity: 0.6 }} />
              </div>
              <div className="career-plan-metric-box-value">{fmtPercent(evaluation.rawMetrics.strictRetention6m)}</div>
            </div>
          </Tooltip>

          <Tooltip
            label="Percentual de retenção estrita na janela de 12 meses. Considera perda: Defaulted, Late1, Late2, Late3. Exclui Desistentes e AwaitingPayment."
            multiline
            w={300}
            withArrow
            position="top"
          >
            <div className="career-plan-metric-box" style={{ cursor: 'help' }}>
              <div className="career-plan-metric-box-label" style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                <span>Retenção Estrita (12 meses)</span>
                <IconAlertCircle size={14} style={{ opacity: 0.6 }} />
              </div>
              <div className="career-plan-metric-box-value">{fmtPercent(evaluation.rawMetrics.strictRetention12m)}</div>
            </div>
          </Tooltip>

          <Tooltip
            label="Percentual de retenção estrita na janela de 15 meses. Considera perda: Defaulted, Late1, Late2, Late3. Exclui Desistentes e AwaitingPayment."
            multiline
            w={300}
            withArrow
            position="top"
          >
            <div className="career-plan-metric-box" style={{ cursor: 'help' }}>
              <div className="career-plan-metric-box-label" style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                <span>Retenção Estrita (15 meses)</span>
                <IconAlertCircle size={14} style={{ opacity: 0.6 }} />
              </div>
              <div className="career-plan-metric-box-value">{fmtPercent(evaluation.rawMetrics.strictRetention15m)}</div>
            </div>
          </Tooltip>

          <Tooltip
            label="Volume financeiro total vendido nos últimos 12 meses fechados. Exclui Desistente e AwaitingPayment."
            multiline
            w={300}
            withArrow
            position="top"
          >
            <div className="career-plan-metric-box" style={{ cursor: 'help' }}>
              <div className="career-plan-metric-box-label" style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                <span>Produção (12 meses)</span>
                <IconAlertCircle size={14} style={{ opacity: 0.6 }} />
              </div>
              <div className="career-plan-metric-box-value">{fmtCurrency(evaluation.rawMetrics.production12m)}</div>
            </div>
          </Tooltip>

          <Tooltip
            label="Volume financeiro total vendido nos últimos 15 meses fechados. Exclui Desistente e AwaitingPayment."
            multiline
            w={300}
            withArrow
            position="top"
          >
            <div className="career-plan-metric-box" style={{ cursor: 'help' }}>
              <div className="career-plan-metric-box-label" style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                <span>Produção (15 meses)</span>
                <IconAlertCircle size={14} style={{ opacity: 0.6 }} />
              </div>
              <div className="career-plan-metric-box-value">{fmtCurrency(evaluation.rawMetrics.production15m)}</div>
            </div>
          </Tooltip>

          <Tooltip
            label="Volume de contratos vendidos nos últimos 12 meses fechados com idade >= 2 meses e que inferem ter ao menos 2 parcelas pagas, elegíveis como produção ativa."
            multiline
            w={300}
            withArrow
            position="top"
          >
            <div className="career-plan-metric-box highlight" style={{ cursor: 'help' }}>
              <div className="career-plan-metric-box-label" style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                <span>Prod. Ativa 12m (≥ 2 pagas)</span>
                <IconAlertCircle size={14} style={{ opacity: 0.6 }} />
              </div>
              <div className="career-plan-metric-box-value">{fmtCurrency(evaluation.rawMetrics.activeProduction12mMinPaid2)}</div>
            </div>
          </Tooltip>

          <Tooltip
            label="Indica se o usuário é proprietário ativo de ao menos uma equipe comercial cadastrada no sistema."
            multiline
            w={300}
            withArrow
            position="top"
          >
            <div className="career-plan-metric-box" style={{ cursor: 'help' }}>
              <div className="career-plan-metric-box-label" style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                <span>Líder de Equipe</span>
                <IconAlertCircle size={14} style={{ opacity: 0.6 }} />
              </div>
              <div className="career-plan-metric-box-value">
                {evaluation.rawMetrics.isTeamOwner ? (
                  <Badge color="green" size="sm">Sim</Badge>
                ) : (
                  <Badge color="gray" size="sm">Não</Badge>
                )}
              </div>
            </div>
          </Tooltip>

          <Tooltip
            label="Número total de membros ativos vinculados às equipes que este usuário lidera diretamente."
            multiline
            w={300}
            withArrow
            position="top"
          >
            <div className="career-plan-metric-box" style={{ cursor: 'help' }}>
              <div className="career-plan-metric-box-label" style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                <span>Membros Ativos na Equipe</span>
                <IconAlertCircle size={14} style={{ opacity: 0.6 }} />
              </div>
              <div className="career-plan-metric-box-value">
                {evaluation.rawMetrics.activeTeamMembers}
              </div>
            </div>
          </Tooltip>

          <Tooltip
            label="Quantidade de equipes lideradas por consultores que são subordinados diretos deste usuário na hierarquia."
            multiline
            w={300}
            withArrow
            position="top"
          >
            <div className="career-plan-metric-box" style={{ cursor: 'help' }}>
              <div className="career-plan-metric-box-label" style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                <span>Equipes Abaixo (Filhos Diretos)</span>
                <IconAlertCircle size={14} style={{ opacity: 0.6 }} />
              </div>
              <div className="career-plan-metric-box-value">
                {evaluation.rawMetrics.directSubTeams}
              </div>
            </div>
          </Tooltip>
        </div>
      </Card>
    </>
  )
}
export default CareerPlanEvaluationView
