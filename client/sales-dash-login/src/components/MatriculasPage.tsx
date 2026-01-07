import React, { useState, useEffect, useCallback } from "react"
import { Title, Button, Table, ActionIcon, Group, Badge, TextInput } from '@mantine/core';
import { IconEdit, IconTrash, IconRefresh, IconPlus, IconUpload } from '@tabler/icons-react';
import "./MatriculasPage.css"
import Menu from "./Menu"
import MatriculaForm from "./MatriculaForm"
import MatriculaImportModal from "./MatriculaImportModal"
import { MatriculaStatus, MatriculaStatusLabels, ActiveState, ActiveStateLabels } from '../types/MatriculaStatus';
import {
  apiService,
  UserMatricula,
  CreateMatriculaRequest,
  UpdateMatriculaRequest,
} from "../services/apiService"

const MatriculasPage: React.FC = () => {
  const [matriculas, setMatriculas] = useState<UserMatricula[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState("")
  const [search, setSearch] = useState("")
  const [searchDebounce, setSearchDebounce] = useState("")
  const [showForm, setShowForm] = useState(false)
  const [showImportModal, setShowImportModal] = useState(false)
  const [editingMatricula, setEditingMatricula] = useState<UserMatricula | undefined>(undefined)
  const [deleteConfirm, setDeleteConfirm] = useState<number | null>(null)

  // Fetch matriculas
  const fetchMatriculas = useCallback(async () => {
    try {
      setLoading(true)
      setError("")
      const response = await apiService.getAllMatriculas()

      if (response.success && response.data) {
        let filtered = response.data
        
        // Client-side filtering
        if (searchDebounce) {
          const searchLower = searchDebounce.toLowerCase()
          filtered = filtered.filter(m => 
            m.matriculaNumber.toLowerCase().includes(searchLower) ||
            m.userName.toLowerCase().includes(searchLower)
          )
        }
        
        setMatriculas(filtered)
      }
    } catch (err: any) {
      setError(err.message || "Failed to load matriculas")
    } finally {
      setLoading(false)
    }
  }, [searchDebounce])

  // Debounce search input
  useEffect(() => {
    const timer = setTimeout(() => {
      setSearchDebounce(search)
    }, 500)

    return () => clearTimeout(timer)
  }, [search])

  // Call fetchMatriculas when searchDebounce changes
  useEffect(() => {
    fetchMatriculas()
  }, [fetchMatriculas])

  const handleCreateMatricula = async (data: CreateMatriculaRequest) => {
    await apiService.createMatricula(data)
    setShowForm(false)
    fetchMatriculas()
  }

  const handleUpdateMatricula = async (data: UpdateMatriculaRequest) => {
    if (editingMatricula) {
      await apiService.updateMatricula(editingMatricula.id, data)
      setShowForm(false)
      setEditingMatricula(undefined)
      fetchMatriculas()
    }
  }

  const handleDeleteMatricula = async (id: number) => {
    try {
      await apiService.deleteMatricula(id)
      setDeleteConfirm(null)
      fetchMatriculas()
    } catch (err: any) {
      setError(err.message || "Failed to delete matricula")
    }
  }

  const openEditForm = (matricula: UserMatricula) => {
    setEditingMatricula(matricula)
    setShowForm(true)
  }

  const closeForm = () => {
    setShowForm(false)
    setEditingMatricula(undefined)
  }

  const formatDate = (dateString: string) => {
    return new Date(dateString).toLocaleDateString("pt-BR", {
      day: "2-digit",
      month: "2-digit",
      year: "numeric",
    })
  }

  const isActive = (matricula: UserMatricula) => {
    if (!matricula.isActive) return false
    if (!matricula.endDate) return true
    return new Date(matricula.endDate) > new Date()
  }

  return (
    <Menu>
      <div className="matriculas-container">
        <div className="matriculas-header">
          <div>
            <Title order={2} size="h2" className="page-title-break">Gerenciamento de Matrículas</Title>
            <p className="matriculas-subtitle">
              {matriculas.length} {matriculas.length === 1 ? "matrícula" : "matrículas"}{" "}
              cadastrada{matriculas.length === 1 ? "" : "s"}
            </p>
          </div>
          <Group>
            <Button
              leftSection={<IconRefresh size={16} />}
              onClick={fetchMatriculas}
              variant="light"
            >
              Atualizar
            </Button>
            <Button
              leftSection={<IconUpload size={16} />}
              onClick={() => setShowImportModal(true)}
              variant="light"
            >
              Importar CSV
            </Button>
            <Button
              leftSection={<IconPlus size={16} />}
              onClick={() => setShowForm(true)}
            >
              Nova Matrícula
            </Button>
          </Group>
        </div>

        {error && <div className="error-message">{error}</div>}

        <div className="search-container">
          <TextInput
            placeholder="Buscar por número de matrícula ou usuário..."
            value={search}
            onChange={(e) => setSearch(e.currentTarget.value)}
            style={{ maxWidth: 400 }}
          />
        </div>

        {loading ? (
          <div className="loading">Carregando matrículas...</div>
        ) : (
          <div className="table-container">
            <Table striped highlightOnHover>
              <Table.Thead>
                <Table.Tr>
                  <Table.Th>Número da Matrícula</Table.Th>
                  <Table.Th>Usuário</Table.Th>
                  <Table.Th>Data Início</Table.Th>
                  <Table.Th>Data Fim</Table.Th>
                  <Table.Th>Ativo</Table.Th>
                  <Table.Th>Status</Table.Th>
                  <Table.Th>Proprietário</Table.Th>
                  <Table.Th>Ações</Table.Th>
                </Table.Tr>
              </Table.Thead>
              <Table.Tbody>
                {matriculas.length === 0 ? (
                  <Table.Tr>
                    <Table.Td colSpan={8} style={{ textAlign: "center" }}>
                      Nenhuma matrícula encontrada
                    </Table.Td>
                  </Table.Tr>
                ) : (
                  matriculas.map((matricula) => (
                    <Table.Tr key={matricula.id}>
                      <Table.Td>
                        <strong>{matricula.matriculaNumber}</strong>
                      </Table.Td>
                      <Table.Td>{matricula.userName}</Table.Td>
                      <Table.Td>{formatDate(matricula.startDate)}</Table.Td>
                      <Table.Td>
                        {matricula.endDate ? formatDate(matricula.endDate) : "-"}
                      </Table.Td>
                      <Table.Td>
                        <Badge color={isActive(matricula) ? "green" : "gray"}>
                          {ActiveStateLabels[isActive(matricula) ? ActiveState.Active : ActiveState.Inactive]}
                        </Badge>
                      </Table.Td>
                      <Table.Td>
                        <Badge color={matricula.status === MatriculaStatus.Active ? "blue" : "yellow"}>
                          {MatriculaStatusLabels[matricula.status as MatriculaStatus]}
                        </Badge>
                      </Table.Td>
                      <Table.Td>
                        {matricula.isOwner && (
                          <Badge color="blue" variant="light">
                            Proprietário
                          </Badge>
                        )}
                      </Table.Td>
                      <Table.Td>
                        <Group gap="xs">
                          <ActionIcon
                            variant="light"
                            color="blue"
                            onClick={() => openEditForm(matricula)}
                          >
                            <IconEdit size={16} />
                          </ActionIcon>
                          {deleteConfirm === matricula.id ? (
                            <Group gap="xs">
                              <Button
                                size="xs"
                                color="red"
                                onClick={() => handleDeleteMatricula(matricula.id)}
                              >
                                Confirmar
                              </Button>
                              <Button
                                size="xs"
                                variant="light"
                                onClick={() => setDeleteConfirm(null)}
                              >
                                Cancelar
                              </Button>
                            </Group>
                          ) : (
                            <ActionIcon
                              variant="light"
                              color="red"
                              onClick={() => setDeleteConfirm(matricula.id)}
                            >
                              <IconTrash size={16} />
                            </ActionIcon>
                          )}
                        </Group>
                      </Table.Td>
                    </Table.Tr>
                  ))
                )}
              </Table.Tbody>
            </Table>
          </div>
        )}

        {showForm && (
          <MatriculaForm
            matricula={editingMatricula}
            onSubmit={editingMatricula ? handleUpdateMatricula : handleCreateMatricula}
            onClose={closeForm}
          />
        )}

        {showImportModal && (
          <MatriculaImportModal
            onClose={() => setShowImportModal(false)}
            onSuccess={() => {
              setShowImportModal(false);
              fetchMatriculas();
            }}
          />
        )}
      </div>
    </Menu>
  )
}

export default MatriculasPage
