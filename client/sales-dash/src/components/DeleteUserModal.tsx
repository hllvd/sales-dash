import React, { useState, useEffect } from "react"
import { Button, Checkbox, Table, Group, Text, Loader, Alert } from "@mantine/core"
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
  const [migrateChecked, setMigrateChecked] = useState(true)

  const hasParent = !!user?.parentUserId
  const parentName = user?.parentUserName || ""
  const hasContracts = previewItems.length > 0

  useEffect(() => {
    if (isOpen && user && hasParent) {
      setStep(1)
      setMigrateChecked(true)
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
      setMigrateChecked(true)
      setPreviewItems([])
      setPreviewError(null)
      setLoadingPreview(false)
    }
  }, [isOpen, user, hasParent])

  if (!user) return null

  const getMatriculaSummary = () => {
    const summary: { [key: string]: { number: string; count: number } } = {}
    previewItems.forEach((item) => {
      const key = item.targetMatriculaNumber || "Sem Matrícula"
      if (!summary[key]) {
        summary[key] = { number: key, count: 0 }
      }
      summary[key].count++
    })
    return Object.values(summary)
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
      const migrateResponse = await apiService.migrateContracts(user.id)
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
    if (migrateChecked && hasContracts) {
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

          {!hasParent && (
            <Alert color="yellow" title="Sem Superior Cadastrado" style={{ marginTop: "8px" }}>
              <Text size="sm">
                O usuário <strong>{user.name}</strong> não possui um superior cadastrado. Caso queira migrar seus contratos antes de excluí-lo, é necessário definir um superior direto nas configurações do usuário.
              </Text>
            </Alert>
          )}

          {!previewError && hasParent && hasContracts && (
            <Alert color="yellow" title="Contratos Detectados" style={{ marginTop: "8px" }}>
              <div style={{ display: "flex", flexDirection: "column", gap: "12px" }}>
                <Text size="sm">
                  Detectamos que este usuário possui <strong>{previewItems.length}</strong> contrato(s) atribuído(s).
                  Gostaria de atribuir estes contratos ao superior <strong>{parentName}</strong>?
                </Text>
                <Checkbox
                  label="Sim, transferir contratos para o superior"
                  checked={migrateChecked}
                  onChange={(e) => setMigrateChecked(e.currentTarget.checked)}
                  styles={{ label: { fontWeight: 500 } }}
                />
              </div>
            </Alert>
          )}
        </div>
      )
    }

    const summaryList = getMatriculaSummary()
    return (
      <div style={{ display: "flex", flexDirection: "column", gap: "16px" }}>
        <Text size="sm">
          Os contratos serão migrados para as seguintes matrículas de <strong>{parentName}</strong> antes da exclusão:
        </Text>

        <Table border={1} style={{ borderCollapse: "collapse", width: "100%" }}>
          <Table.Thead>
            <Table.Tr>
              <Table.Th style={{ padding: "8px" }}>Matrícula</Table.Th>
              <Table.Th style={{ padding: "8px", textAlign: "right" }}>Quantidade de Contratos</Table.Th>
            </Table.Tr>
          </Table.Thead>
          <Table.Tbody>
            {summaryList.map((row) => (
              <Table.Tr key={row.number}>
                <Table.Td style={{ padding: "8px" }}>{row.number}</Table.Td>
                <Table.Td style={{ padding: "8px", textAlign: "right" }}>{row.count}</Table.Td>
              </Table.Tr>
            ))}
          </Table.Tbody>
        </Table>

        <Text size="sm" style={{ fontWeight: 600, textAlign: "right" }}>
          Total de contratos a migrar: {previewItems.length}
        </Text>
      </div>
    )
  }

  const renderFooter = () => {
    if (loadingPreview) return null

    if (step === 1) {
      return (
        <>
          <Button variant="default" onClick={onClose} disabled={loading}>
            Cancelar
          </Button>
          <Button
            color={migrateChecked && hasContracts ? "blue" : "red"}
            loading={loading}
            onClick={handleContinue}
          >
            {migrateChecked && hasContracts ? "Continuar" : "Excluir"}
          </Button>
        </>
      )
    }

    return (
      <>
        <Button variant="default" onClick={() => setStep(1)} disabled={loading}>
          Voltar
        </Button>
        <Button color="red" loading={loading} onClick={handleMigrateAndConfirm}>
          Executar
        </Button>
      </>
    )
  }

  return (
    <StandardModal
      isOpen={isOpen}
      onClose={onClose}
      title={step === 1 ? "Confirmar Exclusão" : "Prévia da Migração de Contratos"}
      size="md"
      footer={renderFooter()}
    >
      {renderContent()}
    </StandardModal>
  )
}
