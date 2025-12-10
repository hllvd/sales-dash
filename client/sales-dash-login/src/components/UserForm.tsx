import React, { useState } from "react"
import { Modal, TextInput, PasswordInput, Select, Checkbox, Button, Group, Title } from '@mantine/core';
import { User } from "../services/apiService"

interface UserFormProps {
  user?: User
  onSubmit: (userData: any) => Promise<void>
  onCancel: () => void
  isEdit?: boolean
}

const UserForm: React.FC<UserFormProps> = ({
  user,
  onSubmit,
  onCancel,
  isEdit = false,
}) => {
  const [formData, setFormData] = useState({
    name: user?.name || "",
    email: user?.email || "",
    password: "",
    role: user?.role || "user",
    parentUserId: user?.parentUserId || "",
    isActive: user?.isActive ?? true,
    matricula: user?.matricula || "",
    isMatriculaOwner: user?.isMatriculaOwner ?? false,
  })
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState("")

  const handleChange = (name: string, value: any) => {
    setFormData((prev) => ({
      ...prev,
      [name]: value,
    }))
  }

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setError("")
    setLoading(true)

    try {
      const userData: any = {
        name: formData.name,
        email: formData.email,
        role: formData.role,
      }

      if (formData.password) {
        userData.password = formData.password
      }

      if (formData.parentUserId) {
        userData.parentUserId = formData.parentUserId
      }

      if (formData.matricula) {
        userData.matricula = formData.matricula
      }

      userData.isMatriculaOwner = formData.isMatriculaOwner

      if (isEdit) {
        userData.isActive = formData.isActive
      }

      await onSubmit(userData)
    } catch (err: any) {
      setError(err.message || "An error occurred")
    } finally {
      setLoading(false)
    }
  }

  return (
    <Modal opened={true} onClose={onCancel} title={<Title order={2} c="rgb(30, 28, 28)">{isEdit ? "Editar Usuário" : "Criar Novo Usuário"}</Title>} size="md" className="styled-form">
      <form onSubmit={handleSubmit}>
        {error && <div style={{ color: 'red', marginBottom: '1rem' }}>{error}</div>}

        {isEdit && (
          <TextInput
            label="ID do Usuário"
            value={user?.id || ""}
            readOnly
            disabled
            mb="md"
          />
        )}

        <TextInput
          label="Nome"
          required
          value={formData.name}
          onChange={(e) => handleChange('name', e.target.value)}
          placeholder="Nome completo"
          mb="md"
        />

        <TextInput
          label="Email"
          required
          type="email"
          value={formData.email}
          onChange={(e) => handleChange('email', e.target.value)}
          placeholder="email@exemplo.com"
          mb="md"
        />

        <PasswordInput
          label="Senha"
          required={!isEdit}
          description={isEdit ? "Deixe em branco para manter a atual" : "Mínimo 6 caracteres"}
          value={formData.password}
          onChange={(e) => handleChange('password', e.target.value)}
          placeholder="Senha"
          mb="md"
        />

        <Select
          label="Função"
          required
          value={formData.role}
          onChange={(value) => handleChange('role', value)}
          data={[
            { value: 'user', label: 'Usuário' },
            { value: 'admin', label: 'Administrador' },
            { value: 'superadmin', label: 'Super Administrador' },
          ]}
          mb="md"
        />

        <TextInput
          label="ID do Usuário Pai"
          description="Opcional"
          value={formData.parentUserId}
          onChange={(e) => handleChange('parentUserId', e.target.value)}
          placeholder="UUID do usuário pai"
          mb="md"
        />

        <TextInput
          label="Matrícula"
          description="Opcional"
          value={formData.matricula}
          onChange={(e) => handleChange('matricula', e.target.value)}
          placeholder="Número da matrícula"
          mb="md"
        />

        <Checkbox
          label="Proprietário da Matrícula"
          checked={formData.isMatriculaOwner}
          onChange={(e) => handleChange('isMatriculaOwner', e.currentTarget.checked)}
          mb="md"
        />

        {isEdit && (
          <Checkbox
            label="Usuário Ativo"
            checked={formData.isActive}
            onChange={(e) => handleChange('isActive', e.currentTarget.checked)}
            mb="md"
          />
        )}

        <Group justify="flex-end" mt="xl">
          <Button variant="default" onClick={onCancel} disabled={loading}>
            Cancelar
          </Button>
          <Button type="submit" loading={loading}>
            {isEdit ? "Salvar Alterações" : "Criar Usuário"}
          </Button>
        </Group>
      </form>
    </Modal>
  )
}

export default UserForm
