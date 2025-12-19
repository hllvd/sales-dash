import React, { createContext, useContext, useState, ReactNode } from 'react'
import { User } from '../services/apiService'

interface UsersContextType {
  users: User[]
  setUsers: (users: User[]) => void
  getUserById: (id: string) => User | undefined
  updateUser: (user: User) => void
  addUser: (user: User) => void
  removeUser: (id: string) => void
}

const UsersContext = createContext<UsersContextType | undefined>(undefined)

export const UsersProvider: React.FC<{ children: ReactNode }> = ({ children }) => {
  const [users, setUsers] = useState<User[]>([])

  const getUserById = (id: string) => {
    return users.find(u => u.id === id)
  }

  const updateUser = (updatedUser: User) => {
    setUsers(prev => prev.map(u => u.id === updatedUser.id ? updatedUser : u))
  }

  const addUser = (newUser: User) => {
    setUsers(prev => [...prev, newUser])
  }

  const removeUser = (id: string) => {
    setUsers(prev => prev.filter(u => u.id !== id))
  }

  return (
    <UsersContext.Provider value={{
      users,
      setUsers,
      getUserById,
      updateUser,
      addUser,
      removeUser
    }}>
      {children}
    </UsersContext.Provider>
  )
}

export const useUsers = () => {
  const context = useContext(UsersContext)
  if (!context) {
    throw new Error('useUsers must be used within a UsersProvider')
  }
  return context
}
