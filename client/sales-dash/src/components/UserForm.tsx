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
  isAdminRestricted?: boolean
  allowedParentUsers?: User[]
}

const normalizeRole = (r?: string) => {
  if (!r) return undefined;
  const normalized = r.toLowerCase().replace(/[^a-z]/g, "");
  if (normalized === "superadmin" || normalized === "admin" || normalized === "user") {
    return normalized;
  }
  return undefined;
};

const getStoredAdmin = (): User | null => {
  try {
    const stored = JSON.parse(localStorage.getItem("user") || "{}");
    const role = (stored.role || stored.Role || "").toLowerCase();
    const id = stored.id || stored.Id;
    const email = stored.email || stored.Email;
    const name = stored.name || stored.Name || email;
    if (id && role === "admin") {
      return {
        id,
        name,
        email,
        role: "admin",
        isActive: true,
        createdAt: "",
        updatedAt: ""
      } as User;
    }
  } catch (e) {
    // ignore
  }
  return null;
};

const UserForm: React.FC<UserFormProps> = ({
  user,
  onSubmit,
  onCancel,
  isEdit = false,
  isAdminRestricted = false,
  allowedParentUsers = [],
}) => {
  const isCurrentAdmin = isAdminRestricted || getStoredAdmin() !== null;
  const initialAdmin = (!isEdit && isCurrentAdmin)
    ? (allowedParentUsers.length > 0 ? allowedParentUsers[0] : getStoredAdmin())
    : null;

  const [formData, setFormData] = useState({
    name: user?.name || "",
    email: user?.email || "",
    password: "",
    role: normalizeRole(user?.role) || (isAdminRestricted ? "user" : "user"),
    parentUserId: user?.parentUserId || initialAdmin?.id || "",
    isActive: user?.isActive ?? true,
    matriculaNumber: "",
    isMatriculaOwner: false,
  })
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState("")
  const [users, setUsers] = useState<User[]>(() => {
    if (isAdminRestricted && allowedParentUsers.length > 0) {
      return allowedParentUsers
    }
    if (initialAdmin) {
      return [initialAdmin]
    }
    return []
  })
  const [parentUserSearch, setParentUserSearch] = useState(
    initialAdmin ? `${initialAdmin.name || initialAdmin.email} (${initialAdmin.email})` : ""
  )
  const [debouncedSearch, setDebouncedSearch] = useState("")
  const [hasContracts, setHasContracts] = useState(false)

  // Gestor matriculas & team states
  const [parentOwnedMatriculas, setParentOwnedMatriculas] = useState<{ id: number; matriculaNumber: string }[]>([])
  const [parentTeamName, setParentTeamName] = useState<string | null>(null)
  const [useGestorMatricula, setUseGestorMatricula] = useState<boolean>(true)
  const [joinParentTeam, setJoinParentTeam] = useState<boolean>(true)
  const [loadingParentDetails, setLoadingParentDetails] = useState<boolean>(false)

  useEffect(() => {
    if (user && isEdit) {
      apiService.getMigrationPreview(user.id)
        .then((res) => {
          if (res.success && res.data && res.data.length > 0) {
            setHasContracts(true)
          }
        })
        .catch(() => {
          setHasContracts(false)
        })
    } else {
      setHasContracts(false)
    }
  }, [user, isEdit])

  // Force role to "user" if Admin restricted and not editing
  useEffect(() => {
    if (isAdminRestricted && !isEdit) {
      setFormData(prev => ({
        ...prev,
        role: "user"
      }))
    }
  }, [isAdminRestricted, isEdit])

  // Debounce search input
  useEffect(() => {
    const timer = setTimeout(() => {
      setDebouncedSearch(parentUserSearch.trim())
    }, 300)

    return () => clearTimeout(timer)
  }, [parentUserSearch])

  // Load users for parent selection
  useEffect(() => {
    if (isAdminRestricted && allowedParentUsers.length > 0) {
      setUsers(allowedParentUsers)
      if (user?.parentUserId) {
        const parentUser = allowedParentUsers.find(u => u.id === user.parentUserId)
        if (parentUser) {
          setParentUserSearch(`${parentUser.name} (${parentUser.email})`)
        }
      }
      return
    }

    const loadUsers = async () => {
      try {
        const response = await apiService.getUsers(1, 1000)
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
  }, [user?.parentUserId, isAdminRestricted, allowedParentUsers])

  // Ensure parent user defaults for Admin creating new user
  useEffect(() => {
    const isCurAdmin = isAdminRestricted || getStoredAdmin() !== null;
    if (isCurAdmin && !isEdit) {
      const admin = (allowedParentUsers && allowedParentUsers.length > 0) ? allowedParentUsers[0] : getStoredAdmin();
      if (admin && admin.id) {
        setFormData(prev => prev.parentUserId ? prev : ({ ...prev, parentUserId: admin.id }));
        setParentUserSearch(prev => prev || `${admin.name || admin.email} (${admin.email})`);
      }
    }
  }, [isAdminRestricted, allowedParentUsers, isEdit]);

  // Fetch parent details (owned matriculas & active team) dynamically
  useEffect(() => {
    if (isEdit || !formData.parentUserId) {
      setParentOwnedMatriculas([])
      setParentTeamName(null)
      return
    }

    let isMounted = true
    setLoadingParentDetails(true)

    apiService.getUser(formData.parentUserId)
      .then((res) => {
        if (!isMounted) return
        if (res.success && res.data) {
          const owned = (res.data.activeMatriculas || []).filter(m => m.isOwner)
          setParentOwnedMatriculas(owned)
          setParentTeamName(res.data.currentTeamName || null)

          if (useGestorMatricula && owned.length > 0) {
            setFormData(prev => ({
              ...prev,
              matriculaNumber: owned[0].matriculaNumber
            }))
          }
        }
      })
      .catch((err) => {
        console.error('Failed to fetch parent user details:', err)
      })
      .finally(() => {
        if (isMounted) {
          setLoadingParentDetails(false)
        }
      })

    return () => {
      isMounted = false
    }
  }, [formData.parentUserId, isEdit, useGestorMatricula])

  const handleChange = (name: string, value: any) => {
    setFormData((prev) => ({
      ...prev,
      [name]: value,
    }))
  }

  const handleParentUserSelect = (value: string) => {
    setParentUserSearch(value)
    
    // Find user by formatted label or email
    const selectedUser = users.find(u => 
      `${u.name} (${u.email})` === value || u.email === value || (u.email && value.includes(u.email))
    ) || (initialAdmin && (initialAdmin.email === value || value.includes(initialAdmin.email)) ? initialAdmin : null)
    
    if (selectedUser) {
      setFormData(prev => ({
        ...prev,
        parentUserId: selectedUser.id
      }))
    } else if (!value.trim()) {
      setFormData(prev => ({
        ...prev,
        parentUserId: ""
      }))
    }
  }

  const handleUseGestorMatriculaChange = (checked: boolean) => {
    setUseGestorMatricula(checked)
    if (checked && parentOwnedMatriculas.length > 0) {
      setFormData(prev => ({
        ...prev,
        matriculaNumber: parentOwnedMatriculas[0].matriculaNumber
      }))
    } else if (!checked) {
      setFormData(prev => ({
        ...prev,
        matriculaNumber: ''
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

      // Only include matricula and team fields when creating a new user
      if (!isEdit) {
        let chosenMatricula = formData.matriculaNumber
        if (useGestorMatricula && parentOwnedMatriculas.length > 0) {
          chosenMatricula = formData.matriculaNumber || parentOwnedMatriculas[0].matriculaNumber
        }

        if (chosenMatricula) {
          userData.matriculaNumber = chosenMatricula
          userData.isMatriculaOwner = false
        }

        if (parentTeamName && joinParentTeam) {
          userData.joinParentTeam = true
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
          description={isEdit ? "Deixe em branco para manter a atual" : "Mínimo 6 caracteres (letras e números)"}
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
            disabled={isAdminRestricted}
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
            data={Array.from(
              new Set(
                (users.length === 0 && initialAdmin ? [initialAdmin] : users)
                  .filter(u => {
                    const searchLower = debouncedSearch.trim().toLowerCase()
                    if (!searchLower) return true
                    return (u.name || "").toLowerCase().includes(searchLower) || 
                           (u.email || "").toLowerCase().includes(searchLower)
                  })
                  .map(u => `${u.name || u.email} (${u.email})`)
              )
            )}
            limit={10}
          />
        </FormField>

        {!isEdit && (
          <>
            {formData.parentUserId ? (
              <>
                {parentOwnedMatriculas.length > 0 ? (
                  <div style={{ marginBottom: '1rem' }}>
                    <Checkbox
                      label="Usar matrícula do gestor"
                      checked={useGestorMatricula}
                      onChange={(e) => handleUseGestorMatriculaChange(e.currentTarget.checked)}
                      mb={useGestorMatricula ? "xs" : 0}
                    />

                    {useGestorMatricula && (
                      parentOwnedMatriculas.length === 1 ? (
                        <FormField label="Matrícula do Gestor">
                          <TextInput
                            value={parentOwnedMatriculas[0].matriculaNumber}
                            readOnly
                            disabled
                            description="Matrícula do gestor selecionada automaticamente"
                          />
                        </FormField>
                      ) : (
                        <FormField label="Selecione a Matrícula do Gestor" required>
                          <Select
                            data={parentOwnedMatriculas.map(m => ({
                              value: m.matriculaNumber,
                              label: m.matriculaNumber
                            }))}
                            value={formData.matriculaNumber}
                            onChange={(value) => handleChange('matriculaNumber', value || '')}
                            placeholder="Escolha uma matrícula"
                          />
                        </FormField>
                      )
                    )}

                    {!useGestorMatricula && (
                      <FormField 
                        label="Matrícula"
                        description="Opcional - informe uma matrícula existente"
                      >
                        <TextInput
                          value={formData.matriculaNumber}
                          onChange={(e) => handleChange('matriculaNumber', e.target.value)}
                          placeholder="Número da matrícula"
                        />
                      </FormField>
                    )}
                  </div>
                ) : (
                  <div style={{ marginBottom: '1rem' }}>
                    {!loadingParentDetails && (
                      <p style={{ fontSize: 13, color: '#6b7280', marginBottom: 8 }}>
                        O gestor selecionado não possui matrículas como proprietário.
                      </p>
                    )}
                    <FormField 
                      label="Matrícula"
                      description="Opcional - informe uma matrícula existente"
                    >
                      <TextInput
                        value={formData.matriculaNumber}
                        onChange={(e) => handleChange('matriculaNumber', e.target.value)}
                        placeholder="Número da matrícula"
                      />
                    </FormField>
                  </div>
                )}

                {parentTeamName && (
                  <div style={{ marginBottom: '1rem' }}>
                    <Checkbox
                      label={`Participar da equipe ${parentTeamName}`}
                      checked={joinParentTeam}
                      onChange={(e) => setJoinParentTeam(e.currentTarget.checked)}
                    />
                  </div>
                )}
              </>
            ) : (
              <FormField 
                label="Matrícula"
                description="Opcional - informe uma matrícula existente"
              >
                <TextInput
                  value={formData.matriculaNumber}
                  onChange={(e) => handleChange('matriculaNumber', e.target.value)}
                  placeholder="Número da matrícula"
                />
              </FormField>
            )}
          </>
        )}

        {isEdit && (
          <FormField 
            label="Usuário Ativo"
            description={hasContracts ? "Usuários com contratos ativos não podem ser desativados por aqui. Use a opção de exclusão (Delete) na listagem para realizar a migração obrigatória dos contratos." : undefined}
          >
            <Checkbox
              checked={formData.isActive}
              disabled={hasContracts}
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
