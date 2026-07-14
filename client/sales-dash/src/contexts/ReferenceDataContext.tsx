import React, { createContext, useContext, useState, useCallback, ReactNode } from 'react'
import { Team, PV, ClassificationLevel, UserMatricula, User, apiService } from '../services/apiService'

interface CacheEntry<T> {
  data: T
  fetchedAt: number
}

interface ReferenceDataContextType {
  teams: CacheEntry<Team[]> | null
  pvs: CacheEntry<PV[]> | null
  classificationLevels: CacheEntry<ClassificationLevel[]> | null
  allMatriculas: CacheEntry<UserMatricula[]> | null
  allUsers: CacheEntry<User[]> | null

  fetchTeams: (forceRefresh?: boolean) => Promise<Team[]>
  fetchPVs: (forceRefresh?: boolean) => Promise<PV[]>
  fetchClassificationLevels: (forceRefresh?: boolean) => Promise<ClassificationLevel[]>
  fetchAllMatriculas: (forceRefresh?: boolean) => Promise<UserMatricula[]>
  fetchAllUsers: (forceRefresh?: boolean) => Promise<User[]>

  invalidateTeams: () => void
  invalidatePVs: () => void
  invalidateClassificationLevels: () => void
  invalidateAllMatriculas: () => void
  invalidateAllUsers: () => void
}

const ReferenceDataContext = createContext<ReferenceDataContextType | undefined>(undefined)

export const ReferenceDataProvider: React.FC<{ children: ReactNode }> = ({ children }) => {
  const [teams, setTeams] = useState<CacheEntry<Team[]> | null>(null)
  const [pvs, setPvs] = useState<CacheEntry<PV[]> | null>(null)
  const [classificationLevels, setClassificationLevels] = useState<CacheEntry<ClassificationLevel[]> | null>(null)
  const [allMatriculas, setAllMatriculas] = useState<CacheEntry<UserMatricula[]> | null>(null)
  const [allUsers, setAllUsers] = useState<CacheEntry<User[]> | null>(null)

  const fetchTeams = useCallback(async (forceRefresh?: boolean) => {
    if (!forceRefresh && teams) {
      return teams.data
    }
    const response = await apiService.getTeams()
    if (!response.success || !response.data) {
      throw new Error(response.message || 'Falha ao carregar equipes')
    }
    const freshData = response.data
    setTeams({ data: freshData, fetchedAt: Date.now() })
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
    const response = await apiService.getUsers(1, 1000)
    if (!response.success || !response.data) {
      throw new Error(response.message || 'Falha ao carregar usuários')
    }
    const freshData = response.data.items
    setAllUsers({ data: freshData, fetchedAt: Date.now() })
    return freshData
  }, [allUsers])

  const invalidateTeams = useCallback(() => setTeams(null), [])
  const invalidatePVs = useCallback(() => setPvs(null), [])
  const invalidateClassificationLevels = useCallback(() => setClassificationLevels(null), [])
  const invalidateAllMatriculas = useCallback(() => setAllMatriculas(null), [])
  const invalidateAllUsers = useCallback(() => setAllUsers(null), [])

  const value: ReferenceDataContextType = {
    teams,
    pvs,
    classificationLevels,
    allMatriculas,
    allUsers,
    fetchTeams,
    fetchPVs,
    fetchClassificationLevels,
    fetchAllMatriculas,
    fetchAllUsers,
    invalidateTeams,
    invalidatePVs,
    invalidateClassificationLevels,
    invalidateAllMatriculas,
    invalidateAllUsers,
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
