import { CareerPlanRequirement, CareerRawMetrics } from '../services/careerPlanService'

export type MetricValueKind = 'percent' | 'currency' | 'boolean' | 'number'

export interface MetricDefinition {
  key: keyof CareerRawMetrics
  label: string
  tooltip: string
  kind: MetricValueKind
}

const RETENTION_NOTE = 'Considera perda apenas contratos com status Defaulted. Exclui Desistentes e AwaitingPayment.'
const STRICT_NOTE = 'Considera perda: Defaulted, Late1, Late2, Late3. Exclui Desistentes e AwaitingPayment.'

export const METRIC_DEFINITIONS: readonly MetricDefinition[] = [
  { key: 'retention6m', label: 'Retenção Normal (6 meses)', kind: 'percent', tooltip: `Percentual financeiro de retenção padrão na janela dos últimos 6 meses fechados. ${RETENTION_NOTE}` },
  { key: 'retention12m', label: 'Retenção Normal (12 meses)', kind: 'percent', tooltip: `Percentual financeiro de retenção padrão na janela dos últimos 12 meses fechados. ${RETENTION_NOTE}` },
  { key: 'retention15m', label: 'Retenção Normal (15 meses)', kind: 'percent', tooltip: `Percentual financeiro de retenção padrão na janela dos últimos 15 meses fechados. ${RETENTION_NOTE}` },
  { key: 'strictRetention6m', label: 'Retenção Estrita (6 meses)', kind: 'percent', tooltip: `Percentual de retenção estrita na janela de 6 meses. ${STRICT_NOTE}` },
  { key: 'strictRetention12m', label: 'Retenção Estrita (12 meses)', kind: 'percent', tooltip: `Percentual de retenção estrita na janela de 12 meses. ${STRICT_NOTE}` },
  { key: 'strictRetention15m', label: 'Retenção Estrita (15 meses)', kind: 'percent', tooltip: `Percentual de retenção estrita na janela de 15 meses. ${STRICT_NOTE}` },
  { key: 'production12m', label: 'Produção (12 meses)', kind: 'currency', tooltip: 'Volume financeiro total vendido nos últimos 12 meses fechados. Exclui Desistente e AwaitingPayment.' },
  { key: 'production15m', label: 'Produção (15 meses)', kind: 'currency', tooltip: 'Volume financeiro total vendido nos últimos 15 meses fechados. Exclui Desistente e AwaitingPayment.' },
  { key: 'activeProduction12mMinPaid2', label: 'Prod. Ativa 12m (≥ 2 pagas)', kind: 'currency', tooltip: 'Volume de contratos vendidos nos últimos 12 meses fechados com idade >= 2 meses e que inferem ter ao menos 2 parcelas pagas, elegíveis como produção ativa.' },
  { key: 'commercializedContracts12mMinAge2', label: 'Contratos Comercializados', kind: 'currency', tooltip: 'Volume financeiro total de contratos vendidos nos últimos 12 meses fechados com idade >= 2 meses (Ativo, Atrasados 1 a 3 e Cancelado). Exclui Desistente e Aguardando Pagamento.' },
  { key: 'isTeamOwner', label: 'Líder de Equipe', kind: 'boolean', tooltip: 'Indica se o usuário é proprietário ativo de ao menos uma equipe comercial cadastrada no sistema.' },
  { key: 'activeTeamMembers', label: 'Membros Ativos na Equipe', kind: 'number', tooltip: 'Número total de membros ativos vinculados às equipes que este usuário lidera diretamente.' },
  { key: 'directSubTeams', label: 'Equipes Abaixo (Filhos Diretos)', kind: 'number', tooltip: 'Quantidade de equipes lideradas por consultores que são subordinados diretos deste usuário na hierarquia.' },
]

/** Pure: set of consolidated-metric keys used by the promotion requirements. */
export const getRequirementMetricKeys = (
  requirements: readonly CareerPlanRequirement[]
): ReadonlySet<string> =>
  new Set(requirements.map(r => r.rawMetricKey).filter((k): k is string => !!k))
