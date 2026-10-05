import React, { useState, useEffect, useCallback } from 'react'
import {
  Title,
  Button,
  Group,
  Text,
  TextInput,
  Select,
  Alert,
  Stack,
  Table,
  Modal,
  Checkbox,
  Loader,
  Pagination,
  Paper,
  Badge,
} from '@mantine/core'
import {
  IconSearch,
  IconArrowBackUp,
  IconAlertTriangle,
  IconX,
} from '@tabler/icons-react'
import Menu from '../Menu'
import { apiService, Team } from '../../services/apiService'
import { Contract } from '../../services/contractService'
import ContractStatusBadge from '../../shared/ContractStatusBadge'
import { toast } from '../../utils/toast'

const PAGE_SIZE = 50

function formatDate(dateStr?: string | null): string {
  if (!dateStr) return '—'
  try {
    const d = new Date(dateStr)
    if (isNaN(d.getTime())) return dateStr
    return d.toLocaleDateString('pt-BR', { timeZone: 'UTC' })
  } catch {
    return dateStr
  }
}

function formatCurrency(amount?: number | null): string {
  if (amount === undefined || amount === null) return 'R$ 0,00'
  return amount.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })
}

export const DeletedContractsTool: React.FC = () => {
  const [contracts, setContracts] = useState<Contract[]>([])
  const [teams, setTeams] = useState<Team[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  // Filter states
  const [contractNumberInput, setContractNumberInput] = useState('')
  const [searchContractNumber, setSearchContractNumber] = useState('')
  const [partialMatch, setPartialMatch] = useState(false)
  const [selectedTeamId, setSelectedTeamId] = useState<string>('')

  // Pagination states
  const [page, setPage] = useState(1)
  const [totalCount, setTotalCount] = useState(0)

  // Restore Modal states
  const [selectedContract, setSelectedContract] = useState<Contract | null>(null)
  const [restoring, setRestoring] = useState(false)
  const [restoreError, setRestoreError] = useState<string | null>(null)

  // Fetch teams for the dropdown
  useEffect(() => {
    let isMounted = true
    const loadTeams = async () => {
      try {
        const res = await apiService.getTeams()
        if (isMounted && res.success && res.data) {
          setTeams(res.data)
        }
      } catch {
        // Ignora erro ao carregar equipes se falhar
      }
    }
    loadTeams()
    return () => {
      isMounted = false
    }
  }, [])

  // Fetch deleted contracts
  const fetchDeletedContracts = useCallback(async (
    targetPage: number,
    num: string,
    isPartial: boolean,
    teamIdStr: string
  ) => {
    setLoading(true)
    setError(null)
    try {
      const teamId = teamIdStr ? parseInt(teamIdStr, 10) : undefined
      const res = await apiService.getDeletedContracts({
        page: targetPage,
        pageSize: PAGE_SIZE,
        contractNumber: num.trim() || undefined,
        exactMatch: !isPartial,
        teamId: !isNaN(teamId as number) ? teamId : undefined,
      })

      if (res.success && res.data) {
        setContracts(res.data.items || [])
        setTotalCount(res.data.totalCount || 0)
      } else {
        setError(res.message || 'Falha ao buscar contratos deletados')
      }
    } catch (err: any) {
      setError(err.message || 'Erro inesperado ao consultar dados')
    } finally {
      setLoading(false)
    }
  }, [])

  // Refetch when page, searchContractNumber, partialMatch, or selectedTeamId changes
  useEffect(() => {
    fetchDeletedContracts(page, searchContractNumber, partialMatch, selectedTeamId)
  }, [page, searchContractNumber, partialMatch, selectedTeamId, fetchDeletedContracts])

  const handleSearchSubmit = (e?: React.FormEvent) => {
    if (e) e.preventDefault()
    setPage(1)
    setSearchContractNumber(contractNumberInput)
  }

  const handleClearFilters = () => {
    setContractNumberInput('')
    setSearchContractNumber('')
    setPartialMatch(false)
    setSelectedTeamId('')
    setPage(1)
  }

  const handleOpenRestoreModal = (contract: Contract) => {
    setSelectedContract(contract)
    setRestoreError(null)
  }

  const handleCloseRestoreModal = () => {
    if (restoring) return
    setSelectedContract(null)
    setRestoreError(null)
  }

  const handleConfirmRestore = async () => {
    if (!selectedContract) return
    setRestoring(true)
    setRestoreError(null)

    try {
      const res = await apiService.restoreDeletedContract(selectedContract.id)
      if (res.success) {
        toast.success(res.message || `Contrato ${selectedContract.contractNumber} restaurado com sucesso!`)
        setSelectedContract(null)
        // Refresh the current page
        fetchDeletedContracts(page, searchContractNumber, partialMatch, selectedTeamId)
      } else {
        setRestoreError(res.message || 'Falha ao restaurar contrato.')
      }
    } catch (err: any) {
      const msg = err.message || 'Erro ao restaurar contrato.'
      setRestoreError(msg)
      toast.error(msg)
    } finally {
      setRestoring(false)
    }
  }

  const totalPages = Math.max(1, Math.ceil(totalCount / PAGE_SIZE))

  const teamOptions = [
    { value: '', label: 'Todas as equipes' },
    ...teams.map((t) => ({
      value: t.id.toString(),
      label: t.name,
    })),
  ]

  return (
    <Menu>
      <div style={{ maxWidth: '1400px', margin: '0 auto', padding: '24px 16px' }}>
        <Stack gap="lg">
          <div>
            <Group justify="space-between" align="flex-start">
              <div>
                <Group gap="xs" align="center">
                  <Title order={2}>Contratos Deletados</Title>
                  <Badge color="red" variant="light" size="lg">
                    {totalCount} {totalCount === 1 ? 'contrato' : 'contratos'}
                  </Badge>
                </Group>
                <Text c="dimmed" size="sm" mt="xs">
                  Visualização de contratos excluídos (soft delete) com opção de restauração (undo).
                </Text>
              </div>
            </Group>
          </div>

          {error && (
            <Alert icon={<IconAlertTriangle size={16} />} title="Erro" color="red">
              {error}
            </Alert>
          )}

          {/* Filters Bar */}
          <Paper withBorder p="md" radius="md">
            <form onSubmit={handleSearchSubmit}>
              <Group gap="md" align="flex-end">
                <TextInput
                  label="Número de Contrato"
                  placeholder="Ex: 123456"
                  value={contractNumberInput}
                  onChange={(e) => setContractNumberInput(e.currentTarget.value)}
                  style={{ minWidth: '220px', flex: 1 }}
                />

                <Select
                  label="Equipe"
                  placeholder="Selecione uma equipe"
                  data={teamOptions}
                  value={selectedTeamId}
                  onChange={(val) => {
                    setSelectedTeamId(val || '')
                    setPage(1)
                  }}
                  searchable
                  clearable
                  style={{ minWidth: '220px', flex: 1 }}
                />

                <div style={{ paddingBottom: '8px' }}>
                  <Checkbox
                    label="Busca parcial"
                    description="Permite buscar partes do número"
                    checked={partialMatch}
                    onChange={(e) => {
                      setPartialMatch(e.currentTarget.checked)
                      setPage(1)
                    }}
                  />
                </div>

                <Group gap="xs" style={{ paddingBottom: '2px' }}>
                  <Button
                    type="submit"
                    leftSection={<IconSearch size={16} />}
                    loading={loading}
                  >
                    Buscar
                  </Button>
                  <Button
                    variant="default"
                    leftSection={<IconX size={16} />}
                    onClick={handleClearFilters}
                  >
                    Limpar
                  </Button>
                </Group>
              </Group>
            </form>
          </Paper>

          {/* Data Table */}
          <Paper withBorder radius="md" style={{ overflow: 'hidden' }}>
            <Table highlightOnHover striped verticalSpacing="sm" horizontalSpacing="md">
              <Table.Thead style={{ backgroundColor: 'var(--mantine-color-gray-0)' }}>
                <Table.Tr>
                  <Table.Th style={{ width: '130px' }}>Nº Contrato</Table.Th>
                  <Table.Th>Cliente</Table.Th>
                  <Table.Th>Vendedor</Table.Th>
                  <Table.Th style={{ width: '150px' }}>Status</Table.Th>
                  <Table.Th>Equipe</Table.Th>
                  <Table.Th style={{ width: '80px' }}>Cota</Table.Th>
                  <Table.Th style={{ width: '130px' }}>Total</Table.Th>
                  <Table.Th style={{ width: '120px' }}>Data Início</Table.Th>
                  <Table.Th style={{ width: '110px', textAlign: 'center' }}>Ações</Table.Th>
                </Table.Tr>
              </Table.Thead>
              <Table.Tbody>
                {loading ? (
                  <Table.Tr>
                    <Table.Td colSpan={9} style={{ textAlign: 'center', padding: '40px' }}>
                      <Loader size="md" />
                      <Text size="sm" c="dimmed" mt="xs">Carregando contratos deletados...</Text>
                    </Table.Td>
                  </Table.Tr>
                ) : contracts.length === 0 ? (
                  <Table.Tr>
                    <Table.Td colSpan={9} style={{ textAlign: 'center', padding: '40px' }}>
                      <Text fw={500} c="dimmed">Nenhum contrato deletado encontrado.</Text>
                      <Text size="xs" c="dimmed" mt={4}>Tente ajustar os filtros de busca acima.</Text>
                    </Table.Td>
                  </Table.Tr>
                ) : (
                  contracts.map((c) => (
                    <Table.Tr key={c.id}>
                      <Table.Td>
                        <Text fw={600} size="sm">{c.contractNumber}</Text>
                      </Table.Td>
                      <Table.Td>
                        <Text size="sm">{c.customerName || '—'}</Text>
                      </Table.Td>
                      <Table.Td>
                        <Text size="sm">{c.userName || '—'}</Text>
                      </Table.Td>
                      <Table.Td>
                        <ContractStatusBadge
                          status={c.status}
                          rawStatus={c.rawStatus}
                          isRemappedToAwaitingPayment={c.isRemappedToAwaitingPayment}
                        />
                      </Table.Td>
                      <Table.Td>
                        {c.teamName ? (
                          <Badge color="indigo" variant="light" size="sm">
                            {c.teamName}
                          </Badge>
                        ) : (
                          <Text size="xs" c="dimmed">—</Text>
                        )}
                      </Table.Td>
                      <Table.Td>
                        <Text size="sm">{c.quota != null ? c.quota : '—'}</Text>
                      </Table.Td>
                      <Table.Td>
                        <Text fw={500} size="sm">{formatCurrency(c.totalAmount)}</Text>
                      </Table.Td>
                      <Table.Td>
                        <Text size="sm">{formatDate(c.contractStartDate)}</Text>
                      </Table.Td>
                      <Table.Td style={{ textAlign: 'center' }}>
                        <Button
                          size="xs"
                          variant="light"
                          color="blue"
                          leftSection={<IconArrowBackUp size={14} />}
                          onClick={() => handleOpenRestoreModal(c)}
                        >
                          Restaurar
                        </Button>
                      </Table.Td>
                    </Table.Tr>
                  ))
                )}
              </Table.Tbody>
            </Table>
          </Paper>

          {/* Pagination */}
          {totalCount > 0 && (
            <Group justify="space-between" align="center" mt="xs">
              <Text size="sm" c="dimmed">
                Exibindo {Math.min((page - 1) * PAGE_SIZE + 1, totalCount)} a{' '}
                {Math.min(page * PAGE_SIZE, totalCount)} de {totalCount} contratos
              </Text>
              {totalPages > 1 && (
                <Pagination
                  value={page}
                  onChange={setPage}
                  total={totalPages}
                  color="blue"
                />
              )}
            </Group>
          )}
        </Stack>

        {/* Confirmation Modal */}
        <Modal
          opened={!!selectedContract}
          onClose={handleCloseRestoreModal}
          title="Confirmar Restauração"
          centered
        >
          <Stack gap="md">
            {restoreError && (
              <Alert icon={<IconAlertTriangle size={16} />} title="Atenção" color="red">
                {restoreError}
              </Alert>
            )}

            <Text size="sm">
              Tem certeza que deseja restaurar o contrato nº{' '}
              <strong>{selectedContract?.contractNumber}</strong>?
            </Text>

            {selectedContract && (
              <Paper withBorder p="sm" radius="sm" bg="gray.0">
                <Stack gap={4}>
                  <Text size="xs"><strong>Cliente:</strong> {selectedContract.customerName || '—'}</Text>
                  <Text size="xs"><strong>Vendedor:</strong> {selectedContract.userName || '—'}</Text>
                  <Text size="xs"><strong>Equipe:</strong> {selectedContract.teamName || '—'}</Text>
                  <Text size="xs"><strong>Valor Total:</strong> {formatCurrency(selectedContract.totalAmount)}</Text>
                  <Text size="xs"><strong>Data Início:</strong> {formatDate(selectedContract.contractStartDate)}</Text>
                </Stack>
              </Paper>
            )}

            <Text size="xs" c="dimmed">
              Ao restaurar, o contrato voltará a ficar ativo no sistema e será removido desta lista de contratos deletados.
            </Text>

            <Group justify="flex-end" gap="xs" mt="sm">
              <Button
                variant="default"
                onClick={handleCloseRestoreModal}
                disabled={restoring}
              >
                Cancelar
              </Button>
              <Button
                color="blue"
                leftSection={<IconArrowBackUp size={16} />}
                onClick={handleConfirmRestore}
                loading={restoring}
              >
                Restaurar Contrato
              </Button>
            </Group>
          </Stack>
        </Modal>
      </div>
    </Menu>
  )
}

export default DeletedContractsTool
