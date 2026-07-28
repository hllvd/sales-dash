import React, { useState, useEffect, useCallback, useRef } from "react"
import { Title, Button, Table, ActionIcon, Group, Badge } from '@mantine/core';
import { IconEdit, IconTrash, IconRefresh, IconPlus, IconUpload, IconMedal } from '@tabler/icons-react';
import "./UsersPage.css"
import Menu from "./Menu"
import UserForm from "./UserForm"
import BulkImportModal from "./BulkImportModal"

import {
  apiService,
  User,
  CreateUserRequest,
  UpdateUserRequest,
} from "../services/apiService"
import { useUsers } from "../contexts/UsersContext"
import { UserProfileModal } from "./UserProfile"
import { DeleteUserModal } from "./DeleteUserModal"


import { normalizeName } from "../utils/normalization"

const UsersPage: React.FC = () => {
  // Track latest API request to prevent race conditions
  const requestCountRef = useRef(0);

  const { setUsers: setCachedUsers, getUserById } = useUsers()
  const [users, setUsers] = useState<User[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState("")
  const [page, setPage] = useState(1)
  const [totalPages, setTotalPages] = useState(1)
  const [totalCount, setTotalCount] = useState(0)
  const [search, setSearch] = useState("")
  const [searchDebounce, setSearchDebounce] = useState("")
  const [showForm, setShowForm] = useState(false)
  const [editingUser, setEditingUser] = useState<User | undefined>(undefined)
  const [showImportModal, setShowImportModal] = useState(false)
  const [currentUserRole, setCurrentUserRole] = useState<string>("")
  const [deleteConfirm, setDeleteConfirm] = useState<User | null>(null)
  const [selectedProfileUserId, setSelectedProfileUserId] = useState<string | null>(null)
  const [descendantUsers, setDescendantUsers] = useState<User[]>([])
  const [allowedUserIds, setAllowedUserIds] = useState<Set<string>>(new Set())

  const fetchAllowedUserIds = useCallback(async () => {
    try {
      const response = await apiService.getUsers(1, 1000, undefined, undefined, true)
      if (response.success && response.data) {
        const items = response.data.items
        setDescendantUsers(items)
        setAllowedUserIds(new Set(items.map(u => u.id)))
      }
    } catch (err) {
      console.error("Failed to fetch allowed descendant user IDs", err)
    }
  }, [])

  useEffect(() => {
    if (currentUserRole === "admin") {
      fetchAllowedUserIds()
    }
  }, [currentUserRole, fetchAllowedUserIds])

  const pageSize = 10

  const fetchUsers = useCallback(async () => {
    setLoading(true)
    setError("")
    const requestId = ++requestCountRef.current

    try {
      const response = await apiService.getUsers(
        page,
        pageSize,
        searchDebounce || undefined
      )

      if (requestId !== requestCountRef.current) return

      if (response.success && response.data) {
        setUsers(response.data.items)
        setCachedUsers(response.data.items) // Store in context
        setTotalCount(response.data.totalCount)
        setTotalPages(Math.ceil(response.data.totalCount / pageSize))
      }
    } catch (err: any) {
      if (requestId !== requestCountRef.current) return
      setError(err.message || "Failed to load users")
    } finally {
      if (requestId === requestCountRef.current) {
        setLoading(false)
      }
    }
  }, [page, searchDebounce, setCachedUsers])

  // Debounce search input
  useEffect(() => {
    const timer = setTimeout(() => {
      setSearchDebounce(search)
      setPage(1) // Reset to first page on search
    }, 500)

    return () => clearTimeout(timer)
  }, [search])

  // Call fetchUsers when page or searchDebounce changes
  useEffect(() => {
    fetchUsers()
  }, [fetchUsers])

  useEffect(() => {
    const user = JSON.parse(localStorage.getItem("user") || "{}")
    setCurrentUserRole(user.role || "")
  }, [])

  const handleCreateUser = async (userData: CreateUserRequest) => {
    await apiService.createUser(userData)
    setShowForm(false)
    fetchUsers()
    if (currentUserRole === "admin") {
      fetchAllowedUserIds()
    }
  }

  const handleUpdateUser = async (userData: UpdateUserRequest) => {
    if (editingUser) {
      await apiService.updateUser(editingUser.id, userData)
      setShowForm(false)
      setEditingUser(undefined)
      fetchUsers()
      if (currentUserRole === "admin") {
        fetchAllowedUserIds()
      }
    }
  }



  // Reactivate user by calling API and refreshing list
  const handleReactivateUser = async (id: string) => {
    try {
      setError("")
      await apiService.updateUser(id, { isActive: true })
      // Refresh users from API to reflect persisted change
      fetchUsers()
    } catch (err: any) {
      setError(err.message || "Failed to reactivate user")
    }
  }

  const openEditForm = (user: User) => {
    // Try to get fresh data from cache first
    const cachedUser = getUserById(user.id)
    setEditingUser(cachedUser || user)
    setShowForm(true)
  }

  const closeForm = () => {
    setShowForm(false)
    setEditingUser(undefined)
  }

  const formatDate = (dateString: string) => {
    return new Date(dateString).toLocaleDateString("pt-BR", {
      day: "2-digit",
      month: "2-digit",
      year: "numeric",
    })
  }


  const adminFromStorage = JSON.parse(localStorage.getItem("user") || "{}")
  const rawAllowed = currentUserRole === "admin" ? [
    {
      id: adminFromStorage.id,
      name: adminFromStorage.name || adminFromStorage.email,
      email: adminFromStorage.email,
      role: adminFromStorage.role || "admin",
      isActive: true,
      createdAt: "",
      updatedAt: ""
    } as User,
    ...descendantUsers
  ] : []

  // Ensure unique by email to prevent Mantine Autocomplete duplicate key crash
  const seenEmails = new Set<string>()
  const allowedParentUsers: User[] = []
  for (const u of rawAllowed) {
    if (u.email) {
      const emailLower = u.email.toLowerCase().trim()
      if (!seenEmails.has(emailLower)) {
        seenEmails.add(emailLower)
        allowedParentUsers.push(u)
      }
    }
  }

  return (
    <Menu>
      <div className="users-container">
          <div className="users-header">
            <div>
              <Title order={2} size="h2">Gerenciamento de Usuários</Title>
              <p className="users-subtitle">
                {totalCount} {totalCount === 1 ? "usuário" : "usuários"}{" "}
                cadastrado{totalCount === 1 ? "" : "s"}
              </p>
            </div>
            <div style={{ display: "flex", gap: 8 }}>
              {currentUserRole === "superadmin" && (
                <Button
                  onClick={() => setShowImportModal(true)}
                  leftSection={<IconUpload size={16} />}
                >
                  Importar
                </Button>
              )}

              <Button onClick={() => setShowForm(true)} leftSection={<IconPlus size={16} />}>
                Criar
              </Button>
            </div>
          </div>

          <div className="search-bar">
            <input
              type="text"
              placeholder="Buscar por nome ou email..."
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              className="search-input"
            />
          </div>

          {error && <div className="error-banner">{error}</div>}

          {loading ? (
            <div className="loading-container">
              <div className="spinner"></div>
              <p>Carregando usuários...</p>
            </div>
          ) : users.length === 0 ? (
            <div className="empty-state">
              <p>Nenhum usuário encontrado</p>
              {search && (
                <button
                  onClick={() => setSearch("")}
                  className="btn-clear-search"
                >
                  Limpar busca
                </button>
              )}
            </div>
          ) : (
            <>
              <div className="table-container">
                <Table.ScrollContainer minWidth={800}>
                  <Table striped highlightOnHover>
                    <Table.Thead>
                      <Table.Tr>
                        <Table.Th>Nome</Table.Th>
                        <Table.Th>Email</Table.Th>
                        <Table.Th>Função</Table.Th>
                        <Table.Th>Nível</Table.Th>
                        <Table.Th>Status</Table.Th>
                        <Table.Th>Criado em</Table.Th>
                        <Table.Th>Ações</Table.Th>
                      </Table.Tr>
                    </Table.Thead>
                    <Table.Tbody>
                      {users.map((user) => (
                        <Table.Tr key={user.id}>
                          <Table.Td>
                            <div className="user-name-cell">
                              <span 
                                className="user-name" 
                                style={{ cursor: 'pointer', color: '#6366f1', fontWeight: 600 }}
                                onClick={() => setSelectedProfileUserId(user.id)}
                              >
                                {normalizeName(user.name)}
                              </span>
                              {user.parentUserName && (
                                <span className="user-parent">
                                  Pai: {normalizeName(user.parentUserName)}
                                </span>
                              )}
                            </div>
                          </Table.Td>
                          <Table.Td>{user.email}</Table.Td>
                          <Table.Td>
                            <Badge 
                              color={
                                user.role === 'superadmin' ? 'red' : 
                                user.role === 'admin' ? 'orange' : 'green'
                              }
                            >
                              {user.role === "superadmin"
                                ? "Super Admin"
                                : user.role === "admin"
                                ? "Admin"
                                : "Usuário"}
                            </Badge>
                          </Table.Td>
                          <Table.Td>
                            {user.currentLevelName ? (
                              <Badge color="indigo" variant="light" leftSection={<IconMedal size={12} />}>
                                {user.currentLevelName}
                              </Badge>
                            ) : (
                              <span style={{ color: '#9ca3af', fontSize: 12, fontStyle: 'italic' }}>—</span>
                            )}
                          </Table.Td>
                          <Table.Td>
                            <Badge 
                              color={user.isActive ? 'teal' : 'gray'}
                            >
                              {user.isActive ? "Ativo" : "Inativo"}
                            </Badge>
                          </Table.Td>
                          <Table.Td>{formatDate(user.createdAt)}</Table.Td>
                          <Table.Td>
                            {currentUserRole === "superadmin" || (currentUserRole === "admin" && allowedUserIds.has(user.id)) ? (
                              <Group gap="xs">
                                <ActionIcon
                                  variant="subtle"
                                  color="blue"
                                  onClick={() => openEditForm(user)}
                                  title="Editar"
                                >
                                  <IconEdit size={16} />
                                </ActionIcon>
                                {user.isActive ? (
                                  <ActionIcon
                                    variant="subtle"
                                    color="red"
                                    onClick={() => setDeleteConfirm(user)}
                                    title="Excluir"
                                  >
                                    <IconTrash size={16} />
                                  </ActionIcon>
                                ) : (
                                  <ActionIcon
                                    variant="subtle"
                                    color="green"
                                    onClick={() => handleReactivateUser(user.id)}
                                    title="Reativar"
                                  >
                                    <IconRefresh size={16} />
                                  </ActionIcon>
                                )}
                              </Group>
                            ) : (
                              <span style={{ color: '#9ca3af', fontSize: 12, fontStyle: 'italic' }}>Apenas leitura</span>
                            )}
                          </Table.Td>
                        </Table.Tr>
                      ))}
                    </Table.Tbody>
                  </Table>
                </Table.ScrollContainer>
              </div>

              {totalPages > 1 && (
                <div className="pagination">
                  <Button
                    onClick={() => setPage((p) => Math.max(1, p - 1))}
                    disabled={page === 1}
                  >
                    ← Anterior
                  </Button>
                  <span className="pagination-info">
                    Página {page} de {totalPages}
                  </span>
                  <Button
                    onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
                    disabled={page === totalPages}
                  >
                    Próxima →
                  </Button>
                </div>
              )}
            </>
          )}
        

      {showForm && (
        <UserForm
          user={editingUser}
          onSubmit={editingUser ? handleUpdateUser : handleCreateUser}
          onCancel={closeForm}
          isEdit={!!editingUser}
          isAdminRestricted={currentUserRole === "admin"}
          allowedParentUsers={allowedParentUsers}
        />
      )}

      {showImportModal && (
        <BulkImportModal
          onClose={() => setShowImportModal(false)}
          onSuccess={() => {
            setShowImportModal(false)
            fetchUsers()
          }}
          templateId={1}
          title="Importar Usuários em Lote"
        />
      )}

      <DeleteUserModal
        user={deleteConfirm}
        isOpen={deleteConfirm !== null}
        onClose={() => setDeleteConfirm(null)}
        onSuccess={() => {
          fetchUsers()
          if (currentUserRole === "admin") {
            fetchAllowedUserIds()
          }
        }}
      />

      <UserProfileModal
        userId={selectedProfileUserId}
        opened={selectedProfileUserId !== null}
        onClose={() => setSelectedProfileUserId(null)}
      />
    </div>
    </Menu>
  )
}

export default UsersPage
