import React, { useState } from "react"
import { TextInput, Select, Checkbox, Button, Group } from '@mantine/core';
import { UserMatricula, apiService } from "../services/apiService"
import StyledModal from './StyledModal';
import { useUsers } from '../contexts/UsersContext';

interface MatriculaFormProps {
  matricula?: UserMatricula
  onSubmit: (data: any) => Promise<void>
  onClose: () => void
}

const MatriculaForm: React.FC<MatriculaFormProps> = ({
  matricula,
  onSubmit,
  onClose,
}) => {
  const [formData, setFormData] = useState({
    userId: matricula?.userId || "",
    matriculaNumber: matricula?.matriculaNumber || "",
    startDate: matricula?.startDate ? matricula.startDate.split('T')[0] : new Date().toISOString().split('T')[0],
    endDate: matricula?.endDate ? matricula.endDate.split('T')[0] : "",
    isActive: matricula?.isActive ?? true,
    isOwner: matricula?.isOwner ?? false,
  })
  const { users } = useUsers() // Use UsersContext instead of local state
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState("")
  const isEdit = !!matricula

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
      const data: any = {
        matriculaNumber: formData.matriculaNumber,
        startDate: formData.startDate, // Already in YYYY-MM-DD format
        isActive: formData.isActive,
        isOwner: formData.isOwner,
      }

      if (!isEdit) {
        data.userId = formData.userId
      }

      if (formData.endDate) {
        data.endDate = formData.endDate // Already in YYYY-MM-DD format
      }

      await onSubmit(data)
    } catch (err: any) {
      setError(err.message || "An error occurred")
    } finally {
      setLoading(false)
    }
  }

  const userOptions = users.map(user => ({
    value: user.id,
    label: `${user.name} (${user.email})`,
  }))

  return (
    <StyledModal 
      opened={true} 
      onClose={onClose} 
      title={isEdit ? "Editar Matrícula" : "Nova Matrícula"}
      size="md"
    >
      <form onSubmit={handleSubmit}>
        {error && <div style={{ color: 'red', marginBottom: '1rem' }}>{error}</div>}

        {!isEdit && (
          <Select
            label="Usuário"
            required
            value={formData.userId}
            onChange={(value) => handleChange('userId', value)}
            data={userOptions}
            placeholder="Selecione um usuário"
            searchable
            mb="md"
          />
        )}

        {isEdit && (
          <TextInput
            label="Usuário"
            value={matricula?.userName || ""}
            readOnly
            disabled
            mb="md"
          />
        )}

        <TextInput
          label="Número da Matrícula"
          required
          value={formData.matriculaNumber}
          onChange={(e) => handleChange('matriculaNumber', e.target.value)}
          placeholder="Ex: MAT-001"
          mb="md"
        />


        <div style={{ marginBottom: '1rem' }}>
          <label style={{ display: 'block', marginBottom: '0.25rem', fontSize: '14px', fontWeight: 500 }}>
            Data de Início <span style={{ color: 'red' }}>*</span>
          </label>
          <input
            type="date"
            required
            value={formData.startDate}
            onChange={(e) => handleChange('startDate', e.target.value)}
            style={{
              width: '100%',
              padding: '8px',
              borderRadius: '4px',
              border: '1px solid #ced4da',
              fontSize: '14px'
            }}
          />
        </div>

        <div style={{ marginBottom: '1rem' }}>
          <label style={{ display: 'block', marginBottom: '0.25rem', fontSize: '14px', fontWeight: 500 }}>
            Data de Fim (Opcional)
          </label>
          <input
            type="date"
            value={formData.endDate}
            onChange={(e) => handleChange('endDate', e.target.value)}
            style={{
              width: '100%',
              padding: '8px',
              borderRadius: '4px',
              border: '1px solid #ced4da',
              fontSize: '14px'
            }}
          />
        </div>

        <Checkbox
          label="Matrícula Ativa"
          checked={formData.isActive}
          onChange={(e) => handleChange('isActive', e.currentTarget.checked)}
          mb="md"
        />

        <Checkbox
          label="Proprietário da Matrícula"
          checked={formData.isOwner}
          onChange={(e) => handleChange('isOwner', e.currentTarget.checked)}
          mb="md"
          description="Apenas um usuário pode ser proprietário de uma matrícula"
        />

        <Group justify="flex-end" mt="xl">
          <Button variant="light" onClick={onClose} disabled={loading}>
            Cancelar
          </Button>
          <Button type="submit" loading={loading}>
            {isEdit ? "Salvar Alterações" : "Criar Matrícula"}
          </Button>
        </Group>
      </form>
    </StyledModal>
  )
}

export default MatriculaForm
