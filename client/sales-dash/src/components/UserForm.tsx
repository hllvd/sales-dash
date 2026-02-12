import React, { useState, useEffect } from "react"
import { TextInput, PasswordInput, Select, Checkbox, Button, Group, Autocomplete } from '@mantine/core';
import { User, apiService } from "../services/apiService"
import StyledModal from './StyledModal';
import { toast } from '../utils/toast';
import FormField from './FormField';

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
    matriculaNumber: "",
    isMatriculaOwner: false,
  })
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState("")
  const [users, setUsers] = useState<User[]>([])
  const [parentUserSearch, setParentUserSearch] = useState("")
  const [debouncedSearch, setDebouncedSearch] = useState("")

  // Debounce search input
  useEffect(() => {
    const timer = setTimeout(() => {
      setDebouncedSearch(parentUserSearch)
    }, 300) // 300ms delay

    return () => clearTimeout(timer)
  }, [parentUserSearch])

  // Load users for parent selection
  useEffect(() => {
    const loadUsers = async () => {
      try {
        const response = await apiService.getUsers(1, 100)
        if (response.success && response.data) {
          setUsers(response.data.items)
          
          // Set initial parent user search if editing
          if (user?.parentUserId) {
            const parentUser = response.data.items.find(u => u.id === user.parentUserId)
            if (parentUser) {
              setParentUserSearch(`${parentUser.name} (${parentUser.email})`)
            }
          }
        }
      } catch (err) {
        console.error('Failed to load users:', err)
        toast.error('Falha ao carregar lista de usuários')
      }
    }
    loadUsers()
  }, [user?.parentUserId])

  const handleChange = (name: string, value: any) => {
    setFormData((prev) => ({
      ...prev,
      [name]: value,
    }))
  }

  const handleParentUserSelect = (value: string) => {
    setParentUserSearch(value)
    
    // Find user by name or email
    const selectedUser = users.find(u => 
      `${u.name} (${u.email})` === value
    )
    
    if (selectedUser) {
      setFormData(prev => ({
        ...prev,
        parentUserId: selectedUser.id
      }))
    }
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

      // Only include matricula fields when creating a new user
      if (!isEdit) {
        if (formData.matriculaNumber) {
          userData.matriculaNumber = formData.matriculaNumber
          userData.isMatriculaOwner = formData.isMatriculaOwner
        }
      }

      if (isEdit) {
        userData.isActive = formData.isActive
      }

      await onSubmit(userData)
    } catch (err: any) {
      const errorMessage = err.message || "Ocorreu um erro"
      setError(errorMessage)
      toast.error(errorMessage)
    } finally {
      setLoading(false)
    }
  }

  return (
    <StyledModal 
      opened={true} 
      onClose={onCancel} 
      title={isEdit ? "Editar Usuário" : "Criar Novo Usuário"}
      size="md"
    >
      <form onSubmit={handleSubmit}>
        {error && <div style={{ color: 'red', marginBottom: '1rem' }}>{error}</div>}

        {isEdit && (
          <FormField label="ID do Usuário">
            <TextInput
              value={user?.id || ""}
              readOnly
              disabled
            />
          </FormField>
        )}

        <FormField label="Nome" required>
          <TextInput
            required
            value={formData.name}
            onChange={(e) => handleChange('name', e.target.value)}
            placeholder="Nome completo"
          />
        </FormField>

        <FormField label="Email" required>
          <TextInput
            required
            type="email"
            value={formData.email}
            onChange={(e) => handleChange('email', e.target.value)}
            placeholder="email@exemplo.com"
          />
        </FormField>

        <FormField 
          label="Senha" 
          required={!isEdit}
          description={isEdit ? "Deixe em branco para manter a atual" : "Mínimo 12 caracteres"}
        >
          <PasswordInput
            required={!isEdit}
            value={formData.password}
            onChange={(e) => handleChange('password', e.target.value)}
            placeholder="Senha"
          />
        </FormField>

        <FormField label="Função" required>
          <Select
            required
            value={formData.role}
            onChange={(value) => handleChange('role', value)}
            data={[
              { value: 'user', label: 'Usuário' },
              { value: 'admin', label: 'Administrador' },
              { value: 'superadmin', label: 'Super Administrador' },
            ]}
          />
        </FormField>

        <FormField 
          label="Usuário Pai"
          description="Opcional - busque por nome ou email"
        >
          <Autocomplete
            placeholder="Digite para buscar..."
            value={parentUserSearch}
            onChange={handleParentUserSelect}
            data={users
              .filter(u => {
                if (!debouncedSearch) return true
                const searchLower = debouncedSearch.toLowerCase()
                return u.name.toLowerCase().includes(searchLower) || 
                       u.email.toLowerCase().includes(searchLower)
              })
              .map(u => `${u.name} (${u.email})`)}
            limit={10}
          />
        </FormField>

        {!isEdit && (
          <>
            <FormField 
              label="Matrícula"
              description="Opcional - número da matrícula"
            >
              <TextInput
                value={formData.matriculaNumber}
                onChange={(e) => handleChange('matriculaNumber', e.target.value)}
                placeholder="Número da matrícula"
              />
            </FormField>

            <FormField label="Proprietário da Matrícula">
              <Checkbox
                checked={formData.isMatriculaOwner}
                onChange={(e) => handleChange('isMatriculaOwner', e.currentTarget.checked)}
              />
            </FormField>
          </>
        )}

        {isEdit && (
          <FormField label="Usuário Ativo">
            <Checkbox
              checked={formData.isActive}
              onChange={(e) => handleChange('isActive', e.currentTarget.checked)}
            />
          </FormField>
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
    </StyledModal>
  )
}

export default UserForm
