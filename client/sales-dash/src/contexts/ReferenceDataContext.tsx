import React, { createContext, useContext, useState, useCallback, ReactNode } from 'react'
import { Team, PV, ClassificationLevel, UserMatricula, User as ApiUser, apiService } from '../services/apiService'
import { getFilterCandidateUsers, User as ContractUser } from '../services/contractService'

interface CacheEntry<T> {
  data: T
  fetchedAt: number
}

interface ReferenceDataContextType {
  teams: CacheEntry<Team[]> | null
  pvs: CacheEntry<PV[]> | null
  classificationLevels: CacheEntry<ClassificationLevel[]> | null
  allMatriculas: CacheEntry<UserMatricula[]> | null
  allUsers: CacheEntry<ApiUser[]> | null
  contractFilterUsers: CacheEntry<ContractUser[]> | null

  fetchTeams: (forceRefresh?: boolean, status?: string) => Promise<Team[]>
  fetchPVs: (forceRefresh?: boolean) => Promise<PV[]>
  fetchClassificationLevels: (forceRefresh?: boolean) => Promise<ClassificationLevel[]>
  fetchAllMatriculas: (forceRefresh?: boolean) => Promise<UserMatricula[]>
  fetchAllUsers: (forceRefresh?: boolean) => Promise<ApiUser[]>
  fetchContractFilterUsers: (forceRefresh?: boolean) => Promise<ContractUser[]>

  invalidateTeams: () => void
  invalidatePVs: () => void
  invalidateClassificationLevels: () => void
  invalidateAllMatriculas: () => void
  invalidateAllUsers: () => void
  invalidateContractFilterUsers: () => void
}

const ReferenceDataContext = createContext<ReferenceDataContextType | undefined>(undefined)

export const ReferenceDataProvider: React.FC<{ children: ReactNode }> = ({ children }) => {
  const [teams, setTeams] = useState<CacheEntry<Team[]> | null>(null)
  const [pvs, setPvs] = useState<CacheEntry<PV[]> | null>(null)
  const [classificationLevels, setClassificationLevels] = useState<CacheEntry<ClassificationLevel[]> | null>(null)
  const [allMatriculas, setAllMatriculas] = useState<CacheEntry<UserMatricula[]> | null>(null)
  const [allUsers, setAllUsers] = useState<CacheEntry<ApiUser[]> | null>(null)
  const [contractFilterUsers, setContractFilterUsers] = useState<CacheEntry<ContractUser[]> | null>(null)

  const fetchTeams = useCallback(async (forceRefresh?: boolean, status?: string) => {
    const isDefault = !status || status === 'active'
    if (!forceRefresh && isDefault && teams) {
      return teams.data
    }
    const response = await apiService.getTeams(status)
    if (!response.success || !response.data) {
      throw new Error(response.message || 'Falha ao carregar equipes')
    }
    const freshData = response.data
    if (isDefault) {
      setTeams({ data: freshData, fetchedAt: Date.now() })
    }
    return freshData
  }, [teams])

  const fetchPVs = useCallback(async (forceRefresh?: boolean) => {
    if (!forceRefresh && pvs) {
      return pvs.data
    }
    const response = await apiService.getPVs()
    if (!response.success || !response.data) {
      throw new Error(response.message || 'Falha ao carregar PVs')
    }
    const freshData = response.data
    setPvs({ data: freshData, fetchedAt: Date.now() })
    return freshData
  }, [pvs])

  const fetchClassificationLevels = useCallback(async (forceRefresh?: boolean) => {
    if (!forceRefresh && classificationLevels) {
      return classificationLevels.data
    }
    const response = await apiService.getClassificationLevels()
    if (!response.success || !response.data) {
      throw new Error(response.message || 'Falha ao carregar classificações')
    }
    const freshData = response.data
    setClassificationLevels({ data: freshData, fetchedAt: Date.now() })
    return freshData
  }, [classificationLevels])

  const fetchAllMatriculas = useCallback(async (forceRefresh?: boolean) => {
    if (!forceRefresh && allMatriculas) {
      return allMatriculas.data
    }
    const response = await apiService.getAllMatriculas()
    if (!response.success || !response.data) {
      throw new Error(response.message || 'Falha ao carregar matrículas')
    }
    const freshData = response.data
    setAllMatriculas({ data: freshData, fetchedAt: Date.now() })
    return freshData
  }, [allMatriculas])

  const fetchAllUsers = useCallback(async (forceRefresh?: boolean) => {
    if (!forceRefresh && allUsers) {
      return allUsers.data
    }
    // Fetch all active users across all pages to prevent silent truncation
    // when total users exceed a single page boundary.
    const PAGE_SIZE = 1000
    let page = 1
    let accumulated: ApiUser[] = []
    let totalCount = Infinity

    while (accumulated.length < totalCount) {
      const response = await apiService.getUsers(page, PAGE_SIZE, undefined, undefined, false, true)
      if (!response.success || !response.data) {
        throw new Error(response.message || 'Falha ao carregar usuários')
      }
      totalCount = response.data.totalCount
      accumulated = accumulated.concat(response.data.items)
      if (accumulated.length >= totalCount) break
      page++
    }

    setAllUsers({ data: accumulated, fetchedAt: Date.now() })
    return accumulated
  }, [allUsers])

  const invalidateTeams = useCallback(() => setTeams(null), [])
  const invalidatePVs = useCallback(() => setPvs(null), [])
  const invalidateClassificationLevels = useCallback(() => setClassificationLevels(null), [])
  const invalidateAllMatriculas = useCallback(() => setAllMatriculas(null), [])
  const invalidateAllUsers = useCallback(() => setAllUsers(null), [])

  const CONTRACT_FILTER_USERS_TTL_MS = 30 * 60 * 1000 // 30 minutes

  const fetchContractFilterUsers = useCallback(async (forceRefresh?: boolean) => {
    const now = Date.now()
    if (!forceRefresh && contractFilterUsers && (now - contractFilterUsers.fetchedAt < CONTRACT_FILTER_USERS_TTL_MS)) {
      return contractFilterUsers.data
    }
    const freshData = await getFilterCandidateUsers()
    setContractFilterUsers({ data: freshData, fetchedAt: now })
    return freshData
  }, [contractFilterUsers])

  const invalidateContractFilterUsers = useCallback(() => setContractFilterUsers(null), [])

  const value: ReferenceDataContextType = {
    teams,
    pvs,
    classificationLevels,
    allMatriculas,
    allUsers,
    contractFilterUsers,
    fetchTeams,
    fetchPVs,
    fetchClassificationLevels,
    fetchAllMatriculas,
    fetchAllUsers,
    fetchContractFilterUsers,
    invalidateTeams,
    invalidatePVs,
    invalidateClassificationLevels,
    invalidateAllMatriculas,
    invalidateAllUsers,
    invalidateContractFilterUsers,
  }

  return (
    <ReferenceDataContext.Provider value={value}>
      {children}
    </ReferenceDataContext.Provider>
  )
}

export const useReferenceData = () => {
  const context = useContext(ReferenceDataContext)
  if (context === undefined) {
    throw new Error('useReferenceData must be used within a ReferenceDataProvider')
  }
  return context
}
