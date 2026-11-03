import config from '../config'
import { authenticatedFetch, getAuthHeaders } from '../utils/httpInterceptor'

const API_BASE_URL = config.apiUrl

export interface CareerPlanRequirement {
  label: string
  metric: string
  currentValue: number
  targetValue: number | null
  formattedCurrent: string
  formattedTarget: string
  op: string
  passed: boolean
  progress: number
}

export interface CareerPlanLevelSummary {
  id: number
  name: string
  description: string | null
  prize: string | null
  salesGoal: number | null
  retention: number | null
}

export interface CareerRawMetrics {
  retention6m: number
  retention12m: number
  retention15m: number
  strictRetention6m: number
  strictRetention12m: number
  strictRetention15m: number
  strictRetention15mMinAge2: number
  production12m: number
  production15m: number
  activeProduction12mMinPaid2: number
  isTeamOwner: boolean
  activeTeamMembers: number
  directSubTeams: number
}

export interface CareerPlanEvaluation {
  userId: string
  userInternalId: number
  userName: string
  userEmail: string
  referenceDate: string
  closingDay: number
  currentLevel: CareerPlanLevelSummary | null
  targetLevel: CareerPlanLevelSummary | null
  hasTargetLevel: boolean
  hasRulesConfigured: boolean
  requirements: CareerPlanRequirement[]
  rawMetrics: CareerRawMetrics
}

export interface CareerPlanConfigSummary {
  closingDay: number
  configuredLevels: string[]
}

export type RtCellStatus = 'paid' | 'unpaid' | 'blank'
export type RtCurrentStatus = 'upToDate' | 'overdue'

export interface ContractRtRow {
  contractNumber: string
  customerName: string | null
  totalAmount: number
  saleStartDate: string
  status: string
  rts: RtCellStatus[]
  current: RtCurrentStatus
  ageMonths: number
  overdueMonths: number
  paidMonths: number
}

export interface ContractRtPage {
  isApplicable: boolean
  currentLevelName: string | null
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
  items: ContractRtRow[]
}

export interface CareerPlanConsultantSnapshot {
  userId: string
  userName: string
  userEmail: string
  currentLevelName: string
  targetLevelName: string
  salesGoal: number | null
  retentionTarget: number | null
  activeProduction12m: number
  productionProgressPct: number
  strictRetention15m: number
  calculatedAt: string
}

export interface CareerPlanConsultantsListResponse {
  consultants: CareerPlanConsultantSnapshot[]
  totalCount: number
  lastCalculatedAt: string | null
}

export interface RecalculateCareerPlanResponse {
  success: boolean
  processedCount: number
  calculatedAt: string
}

export interface ApiResponse<T> {
  success: boolean
  data?: T
  message?: string
}

export const careerPlanService = {
  async evaluateUser(userId: string, asOfDate?: string): Promise<CareerPlanEvaluation> {
    const query = asOfDate ? `?asOfDate=${encodeURIComponent(asOfDate)}` : ''
    const response = await authenticatedFetch(`${API_BASE_URL}/career-plan/users/${userId}${query}`, {
      headers: getAuthHeaders()
    })

    if (!response.ok) {
      const err = await response.json().catch(() => ({}))
      throw new Error(err.message || 'Falha ao avaliar o plano de carreira do usuário.')
    }

    const result: ApiResponse<CareerPlanEvaluation> = await response.json()
    if (!result.success || !result.data) {
      throw new Error(result.message || 'Dados inválidos retornados pela API.')
    }

    return result.data
  },

  async getConfigSummary(): Promise<CareerPlanConfigSummary> {
    const response = await authenticatedFetch(`${API_BASE_URL}/career-plan/config`, {
      headers: getAuthHeaders()
    })

    if (!response.ok) {
      const err = await response.json().catch(() => ({}))
      throw new Error(err.message || 'Falha ao obter configurações do plano de carreira.')
    }

    const result: ApiResponse<CareerPlanConfigSummary> = await response.json()
    if (!result.success || !result.data) {
      throw new Error(result.message || 'Dados de configuração inválidos.')
    }

    return result.data
  },

  async getContractRtTracking(userId: string, page = 1, pageSize = 100, asOfDate?: string): Promise<ContractRtPage> {
    const params = new URLSearchParams({
      page: page.toString(),
      pageSize: pageSize.toString()
    })
    if (asOfDate) {
      params.append('asOfDate', asOfDate)
    }
    const response = await authenticatedFetch(`${API_BASE_URL}/career-plan/users/${userId}/contracts-rt?${params.toString()}`, {
      headers: getAuthHeaders()
    })

    if (!response.ok) {
      const err = await response.json().catch(() => ({}))
      throw new Error(err.message || 'Falha ao obter acompanhamento de RT dos contratos.')
    }

    const result: ApiResponse<ContractRtPage> = await response.json()
    if (!result.success || !result.data) {
      throw new Error(result.message || 'Dados inválidos retornados pela API.')
    }

    return result.data
  },

  async getConsultantsList(): Promise<CareerPlanConsultantsListResponse> {
    const response = await authenticatedFetch(`${API_BASE_URL}/career-plan/consultants`, {
      headers: getAuthHeaders()
    })

    if (!response.ok) {
      const err = await response.json().catch(() => ({}))
      throw new Error(err.message || 'Falha ao obter lista de consultores do plano de carreira.')
    }

    const result: ApiResponse<CareerPlanConsultantsListResponse> = await response.json()
    if (!result.success || !result.data) {
      throw new Error(result.message || 'Dados inválidos retornados pela API.')
    }

    return result.data
  },

  async recalculateConsultants(): Promise<RecalculateCareerPlanResponse> {
    const response = await authenticatedFetch(`${API_BASE_URL}/career-plan/consultants/recalculate`, {
      method: 'POST',
      headers: getAuthHeaders()
    })

    if (!response.ok) {
      const err = await response.json().catch(() => ({}))
      throw new Error(err.message || 'Falha ao recalcular lista de consultores.')
    }

    const result: ApiResponse<RecalculateCareerPlanResponse> = await response.json()
    if (!result.success || !result.data) {
      throw new Error(result.message || 'Dados inválidos retornados pela API.')
    }

    return result.data
  }
}

