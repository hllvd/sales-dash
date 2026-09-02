import React, { useState } from "react"
import { Button, Text, Alert, Group } from "@mantine/core"
import StandardModal from "../shared/StandardModal"
import { apiService, User } from "../services/apiService"
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
  const [loading, setLoading] = useState(false)

  if (!user) return null

  const isMatriculaOwner =
    (user as any)?.userMatriculas?.some((m: any) => m.isOwner) ||
    (user as any)?.activeMatriculas?.some((m: any) => m.isOwner) ||
    false

  const handleDeactivate = async () => {
    setLoading(true)
    try {
      const response = await apiService.deleteUser(user.id)
      if (response.success) {
        toast.success(`Usuário ${user.name} desativado com sucesso.`)
        onSuccess()
        onClose()
      } else {
        toast.error(response.message || "Falha ao desativar usuário.")
      }
    } catch (err: any) {
      toast.error(err.message || "Falha ao desativar usuário.")
    } finally {
      setLoading(false)
    }
  }

  return (
    <StandardModal
      isOpen={isOpen}
      onClose={onClose}
      title="Confirmar Desativação"
      size="sm"
      footer={
        <Group justify="flex-end" gap="xs">
          <Button variant="default" onClick={onClose} disabled={loading}>
            Cancelar
          </Button>
          <Button color="red" loading={loading} onClick={handleDeactivate}>
            Desativar
          </Button>
        </Group>
      }
    >
      <div style={{ display: "flex", flexDirection: "column", gap: "12px", padding: "8px 0" }}>
        <Text size="sm">
          Tem certeza que deseja desativar o usuário <strong>{user.name}</strong>?
        </Text>
        <Text size="sm" c="dimmed">
          O usuário não poderá mais acessar o sistema e não constará como membro ativo na equipe. Seus contratos permanecerão registrados e visíveis nos relatórios com seu nome.
        </Text>

        {isMatriculaOwner && (
          <Alert color="yellow" title="Titular de Matrícula" style={{ marginTop: "4px" }}>
            <Text size="xs">
              Este usuário é titular de matrícula. Lembre-se de definir a titularidade para outro consultor se a matrícula continuar em uso.
            </Text>
          </Alert>
        )}
      </div>
    </StandardModal>
  )
}
