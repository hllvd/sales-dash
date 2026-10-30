import React, { useState, useEffect, useMemo, useCallback } from 'react'
import {
  Title, Text, Badge, Loader, Alert, Tooltip, Select, Button, Group,
  Card, Table, Progress, Stack
} from '@mantine/core'
import {
  IconTrophy, IconArrowRight, IconCheck, IconX, IconCalendar,
  IconAward, IconAlertCircle, IconChartBar, IconShieldCheck, IconRefresh
} from '@tabler/icons-react'
import Menu from './Menu'
import { useCurrentUser } from '../contexts/CurrentUserContext'
import { useReferenceData } from '../contexts/ReferenceDataContext'
import {
  careerPlanService,
  CareerPlanEvaluation
} from '../services/careerPlanService'
import { User } from '../services/apiService'
import './CareerPlanPage.css'

const fmtCurrency = (val?: number | null) =>
  val != null
    ? new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format(val)
    : '—'

const fmtPercent = (val?: number | null) =>
  val != null
    ? new Intl.NumberFormat('pt-BR', { style: 'percent', minimumFractionDigits: 1, maximumFractionDigits: 1 }).format(val)
    : '—'

const fmtDate = (dateStr?: string) => {
  if (!dateStr) return '—'
  const d = new Date(dateStr)
  return d.toLocaleDateString('pt-BR', { day: '2-digit', month: '2-digit', year: 'numeric' })
}

const CareerPlanPage: React.FC = () => {
  const { currentUser } = useCurrentUser()
  const { fetchAllUsers } = useReferenceData()

  const [users, setUsers] = useState<User[]>([])
  const [selectedUserId, setSelectedUserId] = useState<string | null>(null)
  const [evaluation, setEvaluation] = useState<CareerPlanEvaluation | null>(null)
  const [loadingUsers, setLoadingUsers] = useState(true)
  const [evaluating, setEvaluating] = useState(false)
  const [error, setError] = useState<string | null>(null)

  // Verify SuperAdmin role/permissions
  const isSuperAdmin = useMemo(() => {
    const role = (localStorage.getItem('userRole') || currentUser?.role || '').toLowerCase()
    let perms: string[] = []
    try {
      perms = JSON.parse(localStorage.getItem('userPermissions') || '[]')
    } catch {
      perms = []
    }
    return role === 'superadmin' || perms.includes('system:superadmin')
  }, [currentUser])

  // Load active users list for selector
  const loadUsers = useCallback(async () => {
    if (!isSuperAdmin) return
    try {
      setLoadingUsers(true)
      const all = await fetchAllUsers()
      const sorted = all
        .filter(u => u.isActive)
        .sort((a, b) => a.name.localeCompare(b.name))
      setUsers(sorted)
    } catch (err: any) {
      setError(err.message || 'Erro ao carregar lista de usuários.')
    } finally {
      setLoadingUsers(false)
    }
  }, [isSuperAdmin, fetchAllUsers])

  useEffect(() => {
    loadUsers()
  }, [loadUsers])

  // Evaluate selected user
  const evaluateUser = useCallback(async (userId: string) => {
    try {
      setEvaluating(true)
      setError(null)
      const result = await careerPlanService.evaluateUser(userId)
      setEvaluation(result)
    } catch (err: any) {
      setError(err.message || 'Erro ao avaliar plano de carreira.')
      setEvaluation(null)
    } finally {
      setEvaluating(false)
    }
  }, [])

  useEffect(() => {
    if (!selectedUserId) {
      setEvaluation(null)
      return
    }
    evaluateUser(selectedUserId)
  }, [selectedUserId, evaluateUser])

  const handleRefresh = async () => {
    if (selectedUserId) {
      await evaluateUser(selectedUserId)
    } else {
      await loadUsers()
    }
  }

  const userSelectOptions = useMemo(() => {
    return users.map(u => ({
      value: u.id,
      label: `${u.name} (${u.email})`
    }))
  }, [users])

  if (!isSuperAdmin) {
    return (
      <Menu>
        <div className="career-plan-container">
          <Alert icon={<IconAlertCircle size={16} />} title="Acesso Restrito" color="red" variant="light">
            Esta funcionalidade está disponível exclusivamente para SuperAdmins.
          </Alert>
        </div>
      </Menu>
    )
  }

  return (
    <Menu>
      <div className="career-plan-container">
        {/* Header */}
        <div className="career-plan-header">
          <div>
            <Title order={2} style={{ color: '#1c1c1e', fontWeight: 700 }}>
              Plano de Carreira
            </Title>
            <p className="career-plan-subtitle">
              Avaliação de requisitos e progresso para o próximo nível de classificação do usuário
            </p>
          </div>
          <Group gap="sm">
            <Button
              variant="default"
              onClick={handleRefresh}
              loading={evaluating || loadingUsers}
              leftSection={<IconRefresh size={16} />}
            >
              Atualizar
            </Button>
          </Group>
        </div>

        {/* User Search Card */}
        <Card withBorder radius="md" p="md" mb="lg" className="career-plan-search-card">
          <Select
            label="Selecione um Usuário"
            placeholder={loadingUsers ? 'Carregando usuários...' : 'Busque pelo nome ou e-mail...'}
            data={userSelectOptions}
            value={selectedUserId}
            onChange={setSelectedUserId}
            searchable
            clearable
            nothingFoundMessage="Nenhum usuário encontrado"
            disabled={loadingUsers}
            styles={{
              input: { borderRadius: '8px' }
            }}
          />
        </Card>

        {error && (
          <Alert icon={<IconAlertCircle size={16} />} title="Erro" color="red" variant="light" mb="lg">
            {error}
          </Alert>
        )}

        {evaluating && (
          <div style={{ textAlign: 'center', padding: '60px 0' }}>
            <Loader size="lg" variant="dots" />
            <Text mt="md" c="dimmed">Calculando métricas e avaliando requisitos...</Text>
          </div>
        )}

        {!evaluating && evaluation && (
          <Stack gap="lg">
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
                  label="Percentual financeiro de retenção estrita na janela dos últimos 6 meses fechados. Considera como perda contratos Defaulted e em atraso (Late1, Late2, Late3). Exclui Desistentes e AwaitingPayment."
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
                  label="Percentual financeiro de retenção estrita na janela dos últimos 12 meses fechados. Considera como perda contratos Defaulted e em atraso (Late1, Late2, Late3). Exclui Desistentes e AwaitingPayment."
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
                  label="Percentual financeiro de retenção estrita na janela dos últimos 15 meses fechados. Considera como perda contratos Defaulted e em atraso (Late1, Late2, Late3). Exclui Desistentes e AwaitingPayment."
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
                  label="Percentual financeiro de retenção estrita na janela de 15 meses considerando exclusivamente contratos com mais de 2 meses de venda. Contratos recentes (≤ 2 meses) são desconsiderados do cálculo."
                  multiline
                  w={300}
                  withArrow
                  position="top"
                >
                  <div className="career-plan-metric-box highlight" style={{ cursor: 'help' }}>
                    <div className="career-plan-metric-box-label" style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                      <span>Ret. Estrita 15m (contratos &gt; 2 meses)</span>
                      <IconAlertCircle size={14} style={{ opacity: 0.6 }} />
                    </div>
                    <div className="career-plan-metric-box-value" style={{ color: '#6366f1' }}>
                      {fmtPercent(evaluation.rawMetrics.strictRetention15mMinAge2)}
                    </div>
                  </div>
                </Tooltip>

                <Tooltip
                  label="Soma do valor total (R$) dos contratos vendidos na janela dos últimos 12 meses fechados. Exclui apenas contratos Desistentes e AwaitingPayment."
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
                  label="Soma do valor total (R$) dos contratos vendidos na janela dos últimos 15 meses fechados. Exclui apenas contratos Desistentes e AwaitingPayment."
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
                  label="Produção dos últimos 12 meses considerando apenas contratos com status Ativo que possuam ao menos 2 parcelas pagas inferidas (idade em meses menos atrasos ≥ 2). Soma o valor total (R$) dos contratos elegíveis."
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
                    <div className="career-plan-metric-box-value" style={{ color: '#0d9488' }}>
                      {fmtCurrency(evaluation.rawMetrics.activeProduction12mMinPaid2)}
                    </div>
                  </div>
                </Tooltip>

                <Tooltip
                  label="Indica se o consultor é proprietário/líder ativo de ao menos uma equipe comercial no sistema."
                  multiline
                  w={300}
                  withArrow
                  position="top"
                >
                  <div className="career-plan-metric-box" style={{ cursor: 'help' }}>
                    <div className="career-plan-metric-box-label" style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                      <span>Líder de Equipe Ativa</span>
                      <IconAlertCircle size={14} style={{ opacity: 0.6 }} />
                    </div>
                    <div className="career-plan-metric-box-value">
                      {evaluation.rawMetrics.isTeamOwner ? 'Sim' : 'Não'}
                    </div>
                  </div>
                </Tooltip>

                <Tooltip
                  label="Contagem distinta de membros com cadastro ativo vinculados às equipes lideradas pelo consultor."
                  multiline
                  w={300}
                  withArrow
                  position="top"
                >
                  <div className="career-plan-metric-box" style={{ cursor: 'help' }}>
                    <div className="career-plan-metric-box-label" style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                      <span>Membros Ativos nas Equipes</span>
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
          </Stack>
        )}

        {!evaluating && !evaluation && !selectedUserId && (
          <div className="career-plan-empty-state">
            <IconTrophy size={48} stroke={1.5} color="#9ca3af" style={{ marginBottom: 12, opacity: 0.7 }} />
            <Text size="lg" fw={600} style={{ color: '#374151' }}>Nenhum usuário selecionado</Text>
            <Text size="sm" c="dimmed">Selecione um usuário acima para calcular e visualizar seu plano de carreira.</Text>
          </div>
        )}
      </div>
    </Menu>
  )
}

export default CareerPlanPage
