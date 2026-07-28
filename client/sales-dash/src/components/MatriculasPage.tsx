import React, { useState, useEffect, useCallback } from "react"
import { Title, Button, Table, ActionIcon, Group, Badge, TextInput, Text, Select, Modal, Alert } from '@mantine/core';
import { IconEdit, IconTrash, IconRefresh, IconPlus, IconUpload, IconSend, IconAlertCircle } from '@tabler/icons-react';
import "./MatriculasPage.css"
import Menu from "./Menu"
import MatriculaForm from "./MatriculaForm"
import MatriculaImportModal from "./MatriculaImportModal"
import StandardModal from '../shared/StandardModal';
import { MatriculaStatus, MatriculaStatusLabels, ActiveState, ActiveStateLabels } from '../types/MatriculaStatus';
import { useCurrentUser } from '../contexts/CurrentUserContext';
import { useReferenceData } from "../contexts/ReferenceDataContext"
import {
  apiService,
  UserMatricula,
  CreateMatriculaRequest,
  UpdateMatriculaRequest,
} from "../services/apiService"

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

const MatriculasPage: React.FC = () => {
  const { currentUser, refreshCurrentUser } = useCurrentUser();
  const { fetchAllMatriculas: getCachedMatriculas, invalidateAllMatriculas, invalidateAllUsers } = useReferenceData()
  const isSuperAdmin = currentUser?.role === 'superadmin';
  const [rawMatriculas, setRawMatriculas] = useState<UserMatricula[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState("")
  const [search, setSearch] = useState("")
  const [searchDebounce, setSearchDebounce] = useState("")
  const [showForm, setShowForm] = useState(false)
  const [showImportModal, setShowImportModal] = useState(false)
  const [editingMatricula, setEditingMatricula] = useState<UserMatricula | undefined>(undefined)
  const [deleteConfirm, setDeleteConfirm] = useState<number | null>(null)
  const [activeFilter, setActiveFilter] = useState<string>("all")

  // Request Matricula state
  const [showRequestModal, setShowRequestModal] = useState(false)
  const [requestMatriculaNumber, setRequestMatriculaNumber] = useState("")
  const [requestSubmitting, setRequestSubmitting] = useState(false)
  const [requestError, setRequestError] = useState<string | null>(null)
  const [requestSuccess, setRequestSuccess] = useState<string | null>(null)

  // Fetch matriculas
  const fetchMatriculas = useCallback(async (forceRefresh?: boolean) => {
    try {
      setLoading(true)
      setError("")
      const matriculasData = await getCachedMatriculas(forceRefresh)
      setRawMatriculas(matriculasData)
    } catch (err: any) {
      setError(err.message || "Failed to load matriculas")
    } finally {
      setLoading(false)
    }
  }, [getCachedMatriculas])

  // Debounce search input
  useEffect(() => {
    const timer = setTimeout(() => {
      setSearchDebounce(search.trim())
    }, 500)

    return () => clearTimeout(timer)
  }, [search])

  // Call fetchMatriculas once on mount
  useEffect(() => {
    fetchMatriculas()
  }, [fetchMatriculas])

  // Derive filtered matriculas list client-side
  const matriculas = React.useMemo(() => {
    let filtered = rawMatriculas

    // Filter by ActiveState (active/inactive)
    if (activeFilter !== 'all') {
      const wantActive = activeFilter === 'active'
      filtered = filtered.filter(m => isActive(m) === wantActive)
    }

    // Filter by search tokens (comma separated support)
    const tokens = searchDebounce
      .split(',')
      .map(s => s.trim().toLowerCase())
      .filter(Boolean)

    if (tokens.length > 0) {
      filtered = filtered.filter(m =>
        tokens.some(token =>
          m.matriculaNumber.toLowerCase().includes(token) ||
          m.userName.toLowerCase().includes(token)
        )
      )
    }

    return filtered
  }, [rawMatriculas, searchDebounce, activeFilter])

  const handleCreateMatricula = async (data: CreateMatriculaRequest) => {
    await apiService.createMatricula(data)
    setShowForm(false)
    invalidateAllMatriculas()
    invalidateAllUsers()
    fetchMatriculas(true)
    refreshCurrentUser() // Refresh current user context
  }

  const handleUpdateMatricula = async (data: UpdateMatriculaRequest) => {
    if (editingMatricula) {
      await apiService.updateMatricula(editingMatricula.id, data)
      setShowForm(false)
      setEditingMatricula(undefined)
      invalidateAllMatriculas()
      invalidateAllUsers()
      fetchMatriculas(true)
      refreshCurrentUser() // Refresh current user context
    }
  }

  const handleDeleteMatricula = async (id: number) => {
    try {
      await apiService.deleteMatricula(id)
      setDeleteConfirm(null)
      invalidateAllMatriculas()
      invalidateAllUsers()
      fetchMatriculas(true)
      refreshCurrentUser() // Refresh current user context
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

  const handleSendRequestMatricula = async () => {
    if (!requestMatriculaNumber.trim()) {
      setRequestError('O número da matrícula é obrigatório.');
      return;
    }
    setRequestError(null);
    setRequestSubmitting(true);
    try {
      const type = currentUser?.role === 'admin' ? 'AdminRequestMatricula' : 'RequestMatricula';
      await apiService.createApprovalRequest({
        requestType: type,
        payloadJson: JSON.stringify({ matriculaNumber: requestMatriculaNumber.trim() }),
      });
      setRequestSuccess('Solicitação de matrícula enviada com sucesso!');
      setShowRequestModal(false);
      setRequestMatriculaNumber('');
    } catch (err: any) {
      setRequestError(err.message || 'Erro ao solicitar matrícula.');
    } finally {
      setRequestSubmitting(false);
    }
  };

  return (
    <Menu>
      <div className="matriculas-container">
        <div className="matriculas-header">
          <div>
            <Title order={2} size="h2">Gerenciamento de Matrículas</Title>
            <p className="matriculas-subtitle">
              {matriculas.length} {matriculas.length === 1 ? "matrícula" : "matrículas"}{" "}
              cadastrada{matriculas.length === 1 ? "" : "s"}
            </p>
          </div>
          <Group className="matriculas-header-actions">
            <Button
              leftSection={<IconRefresh size={16} />}
              onClick={() => fetchMatriculas(true)}
              variant="light"
            >
              Atualizar
            </Button>
            {isSuperAdmin ? (
              <>
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
              </>
            ) : (
              <Button
                leftSection={<IconPlus size={16} />}
                color="red"
                onClick={() => {
                  setRequestError(null);
                  setShowRequestModal(true);
                }}
              >
                Solicitar Matrícula
              </Button>
            )}
          </Group>
        </div>

        {error && <div className="error-message">{error}</div>}
        {requestSuccess && (
          <Alert color="green" mb="md" withCloseButton onClose={() => setRequestSuccess(null)}>
            {requestSuccess}
          </Alert>
        )}

        <div className="search-container">
          <Group gap="md" align="flex-end">
            <TextInput
              label="Pesquisar"
              placeholder="Buscar por número de matrícula (ou separadas por vírgula) ou usuário..."
              value={search}
              onChange={(e) => setSearch(e.currentTarget.value)}
              style={{ flexGrow: 1, maxWidth: 500 }}
            />
            <Select
              label="Status da Matrícula"
              placeholder="Filtrar por status..."
              data={[
                { value: 'all', label: 'Todas' },
                { value: 'active', label: 'Ativas' },
                { value: 'inactive', label: 'Inativas' }
              ]}
              value={activeFilter}
              onChange={(val) => setActiveFilter(val || 'all')}
              style={{ width: 200 }}
            />
          </Group>
        </div>

        {loading ? (
          <div className="loading">Carregando matrículas...</div>
        ) : (
          <div className="table-container">
            <Table.ScrollContainer minWidth={800}>
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
                    {isSuperAdmin && <Table.Th>Ações</Table.Th>}
                  </Table.Tr>
                </Table.Thead>
                <Table.Tbody>
                  {matriculas.length === 0 ? (
                    <Table.Tr>
                      <Table.Td colSpan={isSuperAdmin ? 8 : 7} style={{ textAlign: "center" }}>
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
                        {isSuperAdmin && (
                          <Table.Td>
                            <Group gap="xs">
                              <ActionIcon
                                variant="light"
                                color="blue"
                                onClick={() => openEditForm(matricula)}
                              >
                                <IconEdit size={16} />
                              </ActionIcon>
                              <ActionIcon
                                variant="light"
                                color="red"
                                onClick={() => setDeleteConfirm(matricula.id)}
                              >
                                <IconTrash size={16} />
                              </ActionIcon>
                            </Group>
                          </Table.Td>
                        )}
                      </Table.Tr>
                    ))
                  )}
                </Table.Tbody>
              </Table>
            </Table.ScrollContainer>
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
              refreshCurrentUser(); // Refresh current user context
            }}
          />
        )}

        <StandardModal
          isOpen={deleteConfirm !== null}
          onClose={() => setDeleteConfirm(null)}
          title="Confirmar Exclusão"
          size="md"
          footer={
            <>
              <Button variant="default" onClick={() => setDeleteConfirm(null)}>
                Cancelar
              </Button>
              <Button
                color="red"
                onClick={() => handleDeleteMatricula(deleteConfirm!)}
              >
                Excluir
              </Button>
            </>
          }
        >
          <div style={{ padding: '10px 0' }}>
            <Text size="sm">Tem certeza que deseja excluir esta matrícula?</Text>
          </div>
        </StandardModal>

        <Modal
          opened={showRequestModal}
          onClose={() => setShowRequestModal(false)}
          title={currentUser?.role === 'admin' ? 'Solicitar Criação de Matrícula (Admin)' : 'Solicitar Nova Matrícula'}
          centered
        >
          {requestError && (
            <Alert icon={<IconAlertCircle size={16} />} color="red" mb="md">
              {requestError}
            </Alert>
          )}

          <TextInput
            label="Número da Matrícula"
            placeholder="Ex: 123456"
            required
            value={requestMatriculaNumber}
            onChange={(e) => setRequestMatriculaNumber(e.target.value)}
            mb="md"
          />

          <Group justify="flex-end">
            <Button variant="default" onClick={() => setShowRequestModal(false)}>
              Cancelar
            </Button>
            <Button color="red" leftSection={<IconSend size={16} />} loading={requestSubmitting} onClick={handleSendRequestMatricula}>
              Enviar Solicitação
            </Button>
          </Group>
        </Modal>
      </div>
    </Menu>
  )
}

export default MatriculasPage
