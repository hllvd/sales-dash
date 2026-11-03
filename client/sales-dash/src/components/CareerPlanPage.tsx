import React, { useState, useEffect, useMemo, useCallback } from 'react'
import {
  Title, Text, Loader, Alert, Select, Button, Group,
  Card, Stack
} from '@mantine/core'
import {
  IconTrophy, IconAlertCircle, IconRefresh
} from '@tabler/icons-react'
import Menu from './Menu'
import { useCurrentUser } from '../contexts/CurrentUserContext'
import { useReferenceData } from '../contexts/ReferenceDataContext'
import {
  careerPlanService,
  CareerPlanEvaluation
} from '../services/careerPlanService'
import { User } from '../services/apiService'
import ContractRtTable from './ContractRtTable'
import CareerPlanEvaluationView from './CareerPlanEvaluationView'
import './CareerPlanPage.css'

const getUserIdFromHash = (): string | null => {
  try {
    const queryStr = window.location.hash.split('?')[1]
    if (queryStr) {
      return new URLSearchParams(queryStr).get('userId') || null
    }
  } catch {}
  return null
}

const CareerPlanPage: React.FC = () => {
  const { currentUser } = useCurrentUser()
  const { fetchAllUsers } = useReferenceData()

  const [users, setUsers] = useState<User[]>([])
  const [selectedUserId, setSelectedUserId] = useState<string | null>(() => getUserIdFromHash())
  const [evaluation, setEvaluation] = useState<CareerPlanEvaluation | null>(null)
  const [loadingUsers, setLoadingUsers] = useState(true)
  const [evaluating, setEvaluating] = useState(false)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    const handleHashChange = () => {
      const uId = getUserIdFromHash()
      setSelectedUserId(uId)
    }
    window.addEventListener('hashchange', handleHashChange)
    return () => window.removeEventListener('hashchange', handleHashChange)
  }, [])

  // Verify role/permissions (SuperAdmin or Admin)
  const { isSuperAdmin, isAdmin, canAccess } = useMemo(() => {
    const role = (localStorage.getItem('userRole') || currentUser?.role || '').toLowerCase()
    let perms: string[] = []
    try {
      perms = JSON.parse(localStorage.getItem('userPermissions') || '[]')
    } catch {
      perms = []
    }
    const isSuper = role === 'superadmin' || perms.includes('system:superadmin')
    const isAdminUser = role === 'admin'
    return {
      isSuperAdmin: isSuper,
      isAdmin: isAdminUser,
      canAccess: isSuper || isAdminUser
    }
  }, [currentUser])

  // Load active users list for selector
  const loadUsers = useCallback(async () => {
    if (!canAccess) return
    try {
      setLoadingUsers(true)
      const all = await fetchAllUsers()
      let allowed = all.filter(u => u.isActive)

      if (!isSuperAdmin && currentUser?.id) {
        // Admin can only view themselves and direct 1st-level subordinates
        allowed = allowed.filter(
          u => u.id === currentUser.id || u.parentUserId === currentUser.id
        )
      }

      const sorted = allowed.sort((a, b) => a.name.localeCompare(b.name))
      setUsers(sorted)
    } catch (err: any) {
      setError(err.message || 'Erro ao carregar lista de usuários.')
    } finally {
      setLoadingUsers(false)
    }
  }, [canAccess, isSuperAdmin, currentUser, fetchAllUsers])

  useEffect(() => {
    loadUsers()
  }, [loadUsers])

  // Auto-select Admin's own user by default when no user is passed in hash
  useEffect(() => {
    if (!selectedUserId && isAdmin && !isSuperAdmin && currentUser?.id) {
      setSelectedUserId(currentUser.id)
      window.location.hash = `#/career-plan?userId=${currentUser.id}`
    }
  }, [selectedUserId, isAdmin, isSuperAdmin, currentUser])

  // Evaluate selected user
  const evaluateUser = useCallback(async (userId: string) => {
    try {
      setEvaluating(true)
      setError(null)
      const result = await careerPlanService.evaluateUser(userId)
      setEvaluation(result)
    } catch (err: any) {
      setError(err.message || 'Erro ao avaliar plano de carreira.')
      setEvaluation(null)
    } finally {
      setEvaluating(false)
    }
  }, [])

  useEffect(() => {
    if (!selectedUserId) {
      setEvaluation(null)
      return
    }
    evaluateUser(selectedUserId)
  }, [selectedUserId, evaluateUser])

  const handleUserChange = (val: string | null) => {
    setSelectedUserId(val)
    if (val) {
      window.location.hash = `#/career-plan?userId=${val}`
    } else {
      window.location.hash = `#/career-plan`
    }
  }

  const handleRefresh = async () => {
    if (selectedUserId) {
      await evaluateUser(selectedUserId)
    } else {
      await loadUsers()
    }
  }

  const userSelectOptions = useMemo(() => {
    return users.map(u => ({
      value: u.id,
      label: `${u.name} (${u.email})`
    }))
  }, [users])

  if (!canAccess) {
    return (
      <Menu>
        <div className="career-plan-container">
          <Alert icon={<IconAlertCircle size={16} />} title="Acesso Restrito" color="red" variant="light">
            Esta funcionalidade está disponível exclusivamente para Administradores e SuperAdmins.
          </Alert>
        </div>
      </Menu>
    )
  }

  return (
    <Menu>
      <div className="career-plan-container">
        {/* Header */}
        <div className="career-plan-header">
          <div>
            <Title order={2} style={{ color: '#1c1c1e', fontWeight: 700 }}>
              Plano de Carreira
            </Title>
            <p className="career-plan-subtitle">
              Avaliação de requisitos e progresso para o próximo nível de classificação do usuário
            </p>
          </div>
          <Group gap="sm">
            <Button
              variant="default"
              onClick={handleRefresh}
              loading={evaluating || loadingUsers}
              leftSection={<IconRefresh size={16} />}
            >
              Atualizar
            </Button>
          </Group>
        </div>

        {/* User Search Card */}
        <Card withBorder radius="md" p="md" mb="lg" className="career-plan-search-card">
          <Select
            label="Selecione um Usuário"
            placeholder={loadingUsers ? 'Carregando usuários...' : 'Busque pelo nome ou e-mail...'}
            data={userSelectOptions}
            value={selectedUserId}
            onChange={handleUserChange}
            searchable
            clearable
            nothingFoundMessage="Nenhum usuário encontrado"
            disabled={loadingUsers}
            styles={{
              input: { borderRadius: '8px' }
            }}
          />
        </Card>

        {error && (
          <Alert icon={<IconAlertCircle size={16} />} title="Erro" color="red" variant="light" mb="lg">
            {error}
          </Alert>
        )}

        {evaluating && (
          <div style={{ textAlign: 'center', padding: '60px 0' }}>
            <Loader size="lg" variant="dots" />
            <Text mt="md" c="dimmed">Calculando métricas e avaliando requisitos...</Text>
          </div>
        )}

        {!evaluating && evaluation && (
          <Stack gap="lg">
            <CareerPlanEvaluationView evaluation={evaluation} />
            <ContractRtTable userId={evaluation.userId} />
          </Stack>
        )}

        {!evaluating && !evaluation && !selectedUserId && (
          <div className="career-plan-empty-state">
            <IconTrophy size={48} stroke={1.5} color="#9ca3af" style={{ marginBottom: 12, opacity: 0.7 }} />
            <Text size="lg" fw={600} style={{ color: '#374151' }}>Nenhum usuário selecionado</Text>
            <Text size="sm" c="dimmed">Selecione um usuário acima para calcular e visualizar seu plano de carreira.</Text>
          </div>
        )}
      </div>
    </Menu>
  )
}

export default CareerPlanPage
