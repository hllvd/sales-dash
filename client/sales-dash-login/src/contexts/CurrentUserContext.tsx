import React, { createContext, useContext, useState, useEffect, ReactNode } from 'react'
import { apiService } from '../services/apiService'

export interface UserMatriculaInfo {
  id: number
  matriculaNumber: string
  isOwner: boolean
  status: string
  startDate: string
  endDate: string | null
}

export interface CurrentUser {
  id: string
  name: string
  email: string
  role: string
  parentUserId?: string
  parentUserName?: string
  isActive: boolean
  createdAt: string
  updatedAt: string
  matriculaId?: number
  matriculaNumber?: string
  isMatriculaOwner: boolean
  activeMatriculas: UserMatriculaInfo[]
}

interface CurrentUserContextType {
  currentUser: CurrentUser | null
  loading: boolean
  error: string | null
  refreshCurrentUser: () => Promise<void>
}

const CurrentUserContext = createContext<CurrentUserContextType | undefined>(undefined)

export const CurrentUserProvider: React.FC<{ children: ReactNode }> = ({ children }) => {
  const [currentUser, setCurrentUser] = useState<CurrentUser | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  const refreshCurrentUser = async () => {
    try {
      setLoading(true)
      setError(null)
      const response = await apiService.getCurrentUser()
      if (response.success && response.data) {
        // Ensure activeMatriculas is always an array
        const userData = {
          ...response.data,
          activeMatriculas: response.data.activeMatriculas || []
        }
        setCurrentUser(userData as CurrentUser)
      }
    } catch (err: any) {
      console.error('Failed to load current user:', err)
      setError(err.message || 'Failed to load current user')
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    // Only load if we have a token
    const token = localStorage.getItem('token')
    if (token) {
      refreshCurrentUser()
    } else {
      setLoading(false)
    }
  }, [])

  return (
    <CurrentUserContext.Provider value={{
      currentUser,
      loading,
      error,
      refreshCurrentUser
    }}>
      {children}
    </CurrentUserContext.Provider>
  )
}

export const useCurrentUser = () => {
  const context = useContext(CurrentUserContext)
  if (!context) {
    throw new Error('useCurrentUser must be used within a CurrentUserProvider')
  }
  return context
}
