import React, { useState, useEffect, useCallback } from "react"
import { Title, Button, Table, ActionIcon, Group, Badge, Text } from '@mantine/core';
import { IconEdit, IconTrash, IconRefresh, IconPlus, IconUpload } from '@tabler/icons-react';
import "./UsersPage.css"
import Menu from "./Menu"
import UserForm from "./UserForm"
import BulkImportModal from "./BulkImportModal"
import StandardModal from "../shared/StandardModal"
import {
  apiService,
  User,
  CreateUserRequest,
  UpdateUserRequest,
} from "../services/apiService"
import { useUsers } from "../contexts/UsersContext"

const UsersPage: React.FC = () => {
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
  const [deleteConfirm, setDeleteConfirm] = useState<string | null>(null)
  const pageSize = 10

  // Fetch users
  const fetchUsers = useCallback(async () => {
    try {
      setLoading(true)
      setError("")
      const response = await apiService.getUsers(
        page,
        pageSize,
        searchDebounce || undefined
      )

      if (response.success && response.data) {
        setUsers(response.data.items)
        setCachedUsers(response.data.items) // Store in context
        setTotalCount(response.data.totalCount)
        setTotalPages(Math.ceil(response.data.totalCount / pageSize))
      }
    } catch (err: any) {
      setError(err.message || "Failed to load users")
    } finally {
      setLoading(false)
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
  }

  const handleUpdateUser = async (userData: UpdateUserRequest) => {
    if (editingUser) {
      await apiService.updateUser(editingUser.id, userData)
      setShowForm(false)
      setEditingUser(undefined)
      fetchUsers()
    }
  }

  const handleDeleteUser = async (id: string) => {
    try {
      await apiService.deleteUser(id)
      setDeleteConfirm(null)
      fetchUsers()
    } catch (err: any) {
      setError(err.message || "Failed to delete user")
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


  return (
    <Menu>
      <div className="users-container">
          <div className="users-header">
            <div>
              <Title order={2} size="h2" className="page-title-break">Gerenciamento de Usuários</Title>
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
                <Table striped highlightOnHover>
                  <Table.Thead>
                    <Table.Tr>
                      <Table.Th>Nome</Table.Th>
                      <Table.Th>Email</Table.Th>
                      <Table.Th>Função</Table.Th>
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
                            <span className="user-name">{user.name}</span>
                            {user.parentUserName && (
                              <span className="user-parent">
                                Pai: {user.parentUserName}
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
                          <Badge 
                            color={user.isActive ? 'teal' : 'gray'}
                          >
                            {user.isActive ? "Ativo" : "Inativo"}
                          </Badge>
                        </Table.Td>
                        <Table.Td>{formatDate(user.createdAt)}</Table.Td>
                        <Table.Td>
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
                                onClick={() => setDeleteConfirm(user.id)}
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
                        </Table.Td>
                      </Table.Tr>
                    ))}
                  </Table.Tbody>
                </Table>
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
        

      <StandardModal
        isOpen={showForm}
        onClose={closeForm}
        title={editingUser ? "Editar Usuário" : "Novo Usuário"}
        size="lg"
        className="form-body" // Using form-body instead of import-form
      >
        <UserForm
          user={editingUser}
          onSubmit={editingUser ? handleUpdateUser : handleCreateUser}
          onCancel={closeForm}
          isEdit={!!editingUser}
        />
      </StandardModal>

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

      <StandardModal
        isOpen={deleteConfirm !== null}
        onClose={() => setDeleteConfirm(null)}
        title="Confirmar Exclusão"
        size="md"
        footer={
          <>
            <button
              className="btn-cancel"
              onClick={() => setDeleteConfirm(null)}
            >
              Cancelar
            </button>
            <button
              className="btn-submit"
              onClick={() => handleDeleteUser(deleteConfirm!)}
              style={{ backgroundColor: "#dc2626" }}
            >
              Excluir
            </button>
          </>
        }
      >
        <div style={{ padding: '10px 0' }}>
          <Text size="sm">
            Tem certeza que deseja excluir este usuário? Esta ação irá
            desativá-lo.
          </Text>
        </div>
      </StandardModal>
    </div>
    </Menu>
  )
}

export default UsersPage
