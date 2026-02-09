import React, { useState, useEffect, useRef } from "react"
import { TextInput, Select, Checkbox, Button, Group, Loader, Text } from '@mantine/core';
import { UserMatricula, apiService, User } from "../services/apiService"
import StyledModal from './StyledModal';
import FormField from './FormField';
import { MatriculaStatus, MatriculaStatusLabels } from '../types/MatriculaStatus';

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
    status: matricula?.status || MatriculaStatus.Active,
  })
  
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState("")
  const isEdit = !!matricula

  // User search states
  const [userSearch, setUserSearch] = useState("")
  const [remoteUsers, setRemoteUsers] = useState<User[]>([])
  const [loadingUsers, setLoadingUsers] = useState(false)
  const userCache = useRef<Record<string, User[]>>({})

  // Debounced user search
  useEffect(() => {
    if (isEdit || !userSearch || userSearch.length < 2) {
      setRemoteUsers([]);
      return;
    }

    // Check cache
    if (userCache.current[userSearch]) {
      setRemoteUsers(userCache.current[userSearch]);
      return;
    }

    setLoadingUsers(true);
    const handler = setTimeout(async () => {
      try {
        const response = await apiService.getUsers(1, 20, userSearch);
        if (response.success && response.data) {
          const results = response.data.items;
          userCache.current[userSearch] = results;
          setRemoteUsers(results);
        }
      } catch (err) {
        console.error("Failed to search users:", err);
      } finally {
        setLoadingUsers(false);
      }
    }, 3000); // 3 seconds debounce per user request

    return () => clearTimeout(handler);
  }, [userSearch, isEdit]);

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
        status: formData.status,
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

  const userOptions = remoteUsers.map(user => ({
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
          <FormField 
            label="Usuário" 
            required
            description={loadingUsers ? "Buscando usuários..." : "Digite o nome ou e-mail para buscar"}
          >
            <Select
              required
              value={formData.userId}
              onChange={(value) => handleChange('userId', value)}
              onSearchChange={setUserSearch}
              searchValue={userSearch}
              data={userOptions}
              placeholder="Digite para buscar um usuário"
              searchable
              nothingFoundMessage={userSearch.length < 2 ? "Digite pelo menos 2 caracteres" : (loadingUsers ? "Buscando..." : "Nenhum usuário encontrado")}
              rightSection={loadingUsers ? <Loader size="xs" /> : null}
            />
          </FormField>
        )}

        {isEdit && (
          <FormField label="Usuário">
            <TextInput
              value={matricula?.userName || ""}
              readOnly
              disabled
            />
          </FormField>
        )}

        <FormField label="Número da Matrícula" required>
          <TextInput
            required
            value={formData.matriculaNumber}
            onChange={(e) => handleChange('matriculaNumber', e.target.value)}
            placeholder="Ex: MAT-001"
          />
        </FormField>

        <FormField label="Data de Início" required>
          <input
            type="date"
            required
            value={formData.startDate}
            onChange={(e) => handleChange('startDate', e.target.value)}
            style={{
              width: '100%',
              padding: '10px 12px',
              borderRadius: '6px',
              border: '1px solid #ced4da',
              fontSize: '14px',
              backgroundColor: 'white',
              color: '#212529',
              transition: 'border-color 0.15s ease-in-out'
            }}
          />
        </FormField>

        <FormField label="Data de Fim (Opcional)">
          <input
            type="date"
            value={formData.endDate}
            onChange={(e) => handleChange('endDate', e.target.value)}
            style={{
              width: '100%',
              padding: '10px 12px',
              borderRadius: '6px',
              border: '1px solid #ced4da',
              fontSize: '14px',
              backgroundColor: 'white',
              color: '#212529',
              transition: 'border-color 0.15s ease-in-out'
            }}
          />
        </FormField>

        <FormField label="Matrícula Ativa">
          <Checkbox
            checked={formData.isActive}
            onChange={(e) => handleChange('isActive', e.currentTarget.checked)}
          />
        </FormField>

        <FormField 
          label="Proprietário da Matrícula"
          description="Apenas um usuário pode ser proprietário de uma matrícula"
        >
          <Checkbox
            checked={formData.isOwner}
            onChange={(e) => handleChange('isOwner', e.currentTarget.checked)}
          />
        </FormField>

        <FormField label="Status" required>
          <Select
            required
            value={formData.status}
            onChange={(value) => handleChange('status', value)}
            data={[
              { value: MatriculaStatus.Active, label: MatriculaStatusLabels[MatriculaStatus.Active] },
              { value: MatriculaStatus.Pending, label: MatriculaStatusLabels[MatriculaStatus.Pending] },
            ]}
          />
        </FormField>

        <Group justify="flex-end" mt="xl" style={{ 
          paddingTop: '16px', 
          borderTop: '1px solid #dee2e6',
          marginTop: '24px'
        }}>
          <Button 
            variant="subtle" 
            onClick={onClose} 
            disabled={loading}
            color="gray"
            size="md"
          >
            Cancelar
          </Button>
          <Button 
            type="submit" 
            loading={loading}
            size="md"
            style={{
              fontWeight: 600,
              textTransform: 'uppercase',
              letterSpacing: '0.5px'
            }}
          >
            {isEdit ? "Salvar Alterações" : "Criar Matrícula"}
          </Button>
        </Group>
      </form>
    </StyledModal>
  )
}

export default MatriculaForm
