import React, { useState, useEffect } from "react"
import { Button, Checkbox, Table, Group, Text, Loader, Alert, Select } from "@mantine/core"
import StandardModal from "../shared/StandardModal"
import { apiService, ContractMigrationPreviewItem, User } from "../services/apiService"
import { toast } from "../utils/toast"

interface DeleteUserModalProps {
  user: User | null
  isOpen: boolean
  onClose: () => void
  onSuccess: () => void
}

export const DeleteUserModal: React.FC<DeleteUserModalProps> = ({
  user,
  isOpen,
  onClose,
  onSuccess,
}) => {
  const [step, setStep] = useState<1 | 2>(1)
  const [loading, setLoading] = useState(false)
  const [loadingPreview, setLoadingPreview] = useState(false)
  const [previewError, setPreviewError] = useState<string | null>(null)
  const [previewItems, setPreviewItems] = useState<ContractMigrationPreviewItem[]>([])
  const [selectedMappings, setSelectedMappings] = useState<{ [contractId: number]: number | null }>({})

  interface ContractMigrationGroup {
    contractId: number
    contractNumber: string
    totalAmount: number
    status: string
    currentMatriculaNumber: string
    options: { targetMatriculaId: number; targetMatriculaNumber: string }[]
    selectedTargetMatriculaId: number | null
  }

  const hasParent = !!user?.parentUserId
  const parentName = user?.parentUserName || ""
  const hasContracts = previewItems.length > 0
  const isMatriculaOwner = (user as any)?.userMatriculas?.some((m: any) => m.isOwner) || (user as any)?.activeMatriculas?.some((m: any) => m.isOwner) || false

  useEffect(() => {
    if (isOpen && user) {
      setStep(1)
      setPreviewItems([])
      setPreviewError(null)
      setLoadingPreview(true)

      apiService
        .getMigrationPreview(user.id)
        .then((response) => {
          if (response.success && response.data) {
            setPreviewItems(response.data)
          } else {
            let msg = response.message || "Falha ao obter dados de migração."
            if (msg.includes("Parent user does not have any active owned matricula")) {
              msg = "O usuário superior não possui nenhuma matrícula ativa sob sua titularidade."
            }
            setPreviewError(msg)
          }
        })
        .catch((err: any) => {
          let msg = err.message || ""
          if (msg.includes("Parent user does not have any active owned matricula")) {
            msg = "O usuário superior não possui nenhuma matrícula ativa sob sua titularidade."
          }
          if (msg.includes("No contracts found") || msg.includes("não possui contratos") || msg.includes("no contracts")) {
            setPreviewItems([])
          } else {
            setPreviewError(msg || "Erro ao verificar contratos do usuário.")
          }
        })
        .finally(() => {
          setLoadingPreview(false)
        })
    } else {
      setStep(1)
      setPreviewItems([])
      setPreviewError(null)
      setLoadingPreview(false)
    }
  }, [isOpen, user])

  useEffect(() => {
    if (previewItems.length > 0) {
      const initialMappings: { [contractId: number]: number | null } = {}
      const groups: { [contractId: number]: ContractMigrationPreviewItem[] } = {}

      previewItems.forEach((item) => {
        if (!groups[item.contractId]) {
          groups[item.contractId] = []
        }
        groups[item.contractId].push(item)
      })

      Object.keys(groups).forEach((idStr) => {
        const id = parseInt(idStr)
        const items = groups[id]
        const autoSelectedItem = items.find((item) => item.isAutoSelected)
        if (autoSelectedItem) {
          initialMappings[id] = autoSelectedItem.targetMatriculaId
        } else if (items.length === 1) {
          initialMappings[id] = items[0].targetMatriculaId
        } else {
          initialMappings[id] = null
        }
      })
      setSelectedMappings(initialMappings)
    } else {
      setSelectedMappings({})
    }
  }, [previewItems])

  if (!user) return null

  const getContractGroups = (): ContractMigrationGroup[] => {
    const groupsMap: { [contractId: number]: ContractMigrationGroup } = {}
    previewItems.forEach((item) => {
      if (!groupsMap[item.contractId]) {
        groupsMap[item.contractId] = {
          contractId: item.contractId,
          contractNumber: item.contractNumber,
          totalAmount: item.totalAmount,
          status: item.status,
          currentMatriculaNumber: item.currentMatriculaNumber || "Sem Matrícula",
          options: [],
          selectedTargetMatriculaId: selectedMappings[item.contractId] ?? null,
        }
      }
      if (!groupsMap[item.contractId].options.some((opt) => opt.targetMatriculaId === item.targetMatriculaId)) {
        groupsMap[item.contractId].options.push({
          targetMatriculaId: item.targetMatriculaId,
          targetMatriculaNumber: item.targetMatriculaNumber || "Sem Matrícula",
        })
      }
    })
    return Object.values(groupsMap)
  }

  const handleDeleteOnly = async () => {
    setLoading(true)
    try {
      const response = await apiService.deleteUser(user.id)
      if (response.success) {
        toast.success(`Usuário ${user.name} excluído com sucesso.`)
        onSuccess()
        onClose()
      } else {
        toast.error(response.message || "Falha ao excluir usuário.")
      }
    } catch (err: any) {
      toast.error(err.message || "Falha ao excluir usuário.")
    } finally {
      setLoading(false)
    }
  }

  const handleMigrateAndConfirm = async () => {
    setLoading(true)
    try {
      const mappings = Object.entries(selectedMappings)
        .filter(([_, targetId]) => targetId !== null)
        .map(([contractId, targetId]) => ({
          contractId: parseInt(contractId),
          targetMatriculaId: targetId!,
        }))

      const migrateResponse = await apiService.migrateContracts(user.id, mappings)
      if (!migrateResponse.success) {
        let msg = migrateResponse.message || "Falha ao migrar contratos."
        if (msg.includes("Parent user does not have any active owned matricula")) {
          msg = "O usuário superior não possui nenhuma matrícula ativa sob sua titularidade."
        }
        toast.error(msg)
        setLoading(false)
        return
      }

      const count = migrateResponse.data?.migratedCount ?? previewItems.length

      const deleteResponse = await apiService.deleteUser(user.id)
      if (deleteResponse.success) {
        toast.success(
          `${count} contratos migrados e usuário ${user.name} excluído com sucesso.`
        )
        onSuccess()
        onClose()
      } else {
        toast.warning(
          `Contratos migrados (${count}), mas houve um erro ao excluir o usuário: ${deleteResponse.message}`
        )
      }
    } catch (err: any) {
      let msg = err.message || "Erro ao realizar o processo de migração e exclusão."
      if (msg.includes("Parent user does not have any active owned matricula")) {
        msg = "O usuário superior não possui nenhuma matrícula ativa sob sua titularidade."
      }
      toast.error(msg)
    } finally {
      setLoading(false)
    }
  }

  const handleContinue = () => {
    if (hasContracts) {
      setStep(2)
    } else {
      handleDeleteOnly()
    }
  }

  const renderContent = () => {
    if (loadingPreview) {
      return (
        <Group justify="center" p="xl">
          <Loader size="md" />
          <Text size="sm" style={{ color: "#4b5563" }}>
            Verificando contratos e superior...
          </Text>
        </Group>
      )
    }

    if (step === 1) {
      return (
        <div style={{ display: "flex", flexDirection: "column", gap: "16px" }}>
          <Text size="sm">
            Tem certeza que deseja excluir o usuário <strong>{user.name}</strong>? Esta ação irá desativá-lo.
          </Text>

          {previewError && (
            <Alert color="red" title="Atenção">
              {previewError}
            </Alert>
          )}

          {isMatriculaOwner && (
            <Alert color="red" title="Titular de Matrícula" style={{ marginTop: "8px" }}>
              <Text size="sm">
                Por favor, defina a matrícula para outro proprietário.
              </Text>
            </Alert>
          )}

          {!hasParent && (
            <>
              {hasContracts ? (
                <Alert color="red" title="Erro: Superior Mandatório" style={{ marginTop: "8px" }}>
                  <Text size="sm">
                    Este usuário possui <strong>{previewItems.length}</strong> contrato(s) ativo(s) em seu nome. Para desativá-lo, é obrigatório que ele possua um usuário superior para que os contratos possam ser migrados. Edite o usuário e atribua um superior antes de prosseguir.
                  </Text>
                </Alert>
              ) : (
                <Alert color="yellow" title="Sem Superior Cadastrado" style={{ marginTop: "8px" }}>
                  <Text size="sm">
                    O usuário <strong>{user.name}</strong> não possui um superior cadastrado. Como ele não possui contratos, você pode prosseguir com a exclusão direta.
                  </Text>
                </Alert>
              )}
            </>
          )}

          {!previewError && hasParent && hasContracts && (
            <Alert color="yellow" title="Contratos Detectados" style={{ marginTop: "8px" }}>
              <div style={{ display: "flex", flexDirection: "column", gap: "12px" }}>
                <Text size="sm">
                  Detectamos que este usuário possui <strong>{previewItems.length}</strong> contrato(s) atribuído(s).
                  A migração destes contratos para o superior <strong>{parentName}</strong> é obrigatória para prosseguir com a exclusão.
                </Text>
              </div>
            </Alert>
          )}
        </div>
      )
    }

    const contractGroups = getContractGroups()
    return (
      <div style={{ display: "flex", flexDirection: "column", gap: "16px" }}>
        <Text size="sm">
          Selecione a matrícula de destino de <strong>{parentName}</strong> para cada contrato antes da exclusão:
        </Text>

        <Table border={1} style={{ borderCollapse: "collapse", width: "100%" }}>
          <Table.Thead>
            <Table.Tr>
              <Table.Th style={{ padding: "8px" }}>Contrato</Table.Th>
              <Table.Th style={{ padding: "8px" }}>Matrícula Atual</Table.Th>
              <Table.Th style={{ padding: "8px" }}>Matrícula de Destino</Table.Th>
            </Table.Tr>
          </Table.Thead>
          <Table.Tbody>
            {contractGroups.map((group) => (
              <Table.Tr key={group.contractId}>
                <Table.Td style={{ padding: "8px" }}>{group.contractNumber}</Table.Td>
                <Table.Td style={{ padding: "8px" }}>{group.currentMatriculaNumber}</Table.Td>
                <Table.Td style={{ padding: "8px" }}>
                  {group.options.length === 1 ? (
                    <Text size="sm">{group.options[0].targetMatriculaNumber}</Text>
                  ) : (
                    <Select
                      placeholder="Selecione a matrícula"
                      data={group.options.map((opt) => ({
                        value: opt.targetMatriculaId.toString(),
                        label: opt.targetMatriculaNumber,
                      }))}
                      value={selectedMappings[group.contractId]?.toString() || null}
                      onChange={(val) => {
                        setSelectedMappings((prev) => ({
                          ...prev,
                          [group.contractId]: val ? parseInt(val) : null,
                        }))
                      }}
                      size="xs"
                    />
                  )}
                </Table.Td>
              </Table.Tr>
            ))}
          </Table.Tbody>
        </Table>

        <Text size="sm" style={{ fontWeight: 600, textAlign: "right" }}>
          Total de contratos a migrar: {contractGroups.length}
        </Text>
      </div>
    )
  }

  const renderFooter = () => {
    if (loadingPreview) return null

    const isBlocked = (!hasParent && hasContracts) || !!previewError

    if (isBlocked) {
      return (
        <Button variant="default" onClick={onClose}>
          Fechar
        </Button>
      )
    }

    if (step === 1) {
      return (
        <>
          <Button variant="default" onClick={onClose} disabled={loading}>
            Cancelar
          </Button>
          <Button
            color={hasContracts ? "blue" : "red"}
            loading={loading}
            onClick={handleContinue}
          >
            {hasContracts ? "Continuar" : "Excluir"}
          </Button>
        </>
      )
    }

    const contractGroups = getContractGroups()
    const isExecutionDisabled = contractGroups.some((g) => g.selectedTargetMatriculaId === null)

    return (
      <>
        <Button variant="default" onClick={() => setStep(1)} disabled={loading}>
          Voltar
        </Button>
        <Button
          color="red"
          loading={loading}
          disabled={isExecutionDisabled}
          onClick={handleMigrateAndConfirm}
        >
          Executar
        </Button>
      </>
    )
  }

  return (
    <StandardModal
      isOpen={isOpen}
      onClose={onClose}
      title={
        step === 1
          ? "Confirmar Exclusão"
          : "Prévia da Migração de Contratos"
      }
      size="md"
      footer={renderFooter()}
    >
      {renderContent()}
    </StandardModal>
  )
}
