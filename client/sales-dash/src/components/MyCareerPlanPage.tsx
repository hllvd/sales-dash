import React, { useState, useEffect, useCallback } from 'react'
import {
  Title,
  Text,
  Badge,
  Loader,
  Alert,
  Button,
  Group,
  Stack
} from '@mantine/core'
import {
  IconTrophy,
  IconRefresh,
  IconAlertCircle
} from '@tabler/icons-react'
import Menu from './Menu'
import {
  careerPlanService,
  CareerPlanEvaluation
} from '../services/careerPlanService'
import CareerPlanEvaluationView from './CareerPlanEvaluationView'
import ContractRtTable from './ContractRtTable'
import './CareerPlanPage.css'

const fmtDateTime = (isoDate?: string | null) => {
  if (!isoDate) return 'Não calculado'
  const d = new Date(isoDate)
  return d.toLocaleString('pt-BR', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit'
  })
}

export const MyCareerPlanPage: React.FC = () => {
  const [evaluation, setEvaluation] = useState<CareerPlanEvaluation | null>(null)
  const [simulatedEvaluation, setSimulatedEvaluation] = useState<CareerPlanEvaluation | null>(null)
  const [resetTrigger, setResetTrigger] = useState<number>(0)
  const [calculatedAt, setCalculatedAt] = useState<string | null>(null)
  const [fromCache, setFromCache] = useState<boolean>(false)
  const [loading, setLoading] = useState<boolean>(true)
  const [refreshing, setRefreshing] = useState<boolean>(false)
  const [error, setError] = useState<string | null>(null)

  const loadData = useCallback(async (forceRefresh = false) => {
    try {
      if (forceRefresh) {
        setRefreshing(true)
      } else {
        setLoading(true)
      }
      setError(null)

      const res = forceRefresh
        ? await careerPlanService.refreshMyCareerPlan()
        : await careerPlanService.getMyCareerPlan(false)

      setEvaluation(res.evaluation)
      setSimulatedEvaluation(null)
      setCalculatedAt(res.calculatedAt)
      setFromCache(res.fromCache)
    } catch (err: any) {
      setError(err?.message || 'Falha ao carregar seu plano de carreira.')
    } finally {
      setLoading(false)
      setRefreshing(false)
    }
  }, [])

  useEffect(() => {
    loadData(false)
  }, [loadData])

  const handleRefresh = () => {
    setResetTrigger(prev => prev + 1)
    loadData(true)
  }

  return (
    <Menu>
      <div className="career-plan-container">
        {/* Header */}
        <div className="career-plan-header">
          <div>
            <Group gap="xs" align="center">
              <IconTrophy size={28} color="#6366f1" />
              <Title order={2} style={{ color: '#1c1c1e', fontWeight: 700 }}>
                Meu Plano de Carreira
              </Title>
            </Group>
            <p className="career-plan-subtitle">
              Acompanhamento pessoal de requisitos e progresso para o próximo nível de classificação
            </p>
          </div>

          <Group gap="sm" align="center">
            {calculatedAt && (
              <Badge color={fromCache ? 'indigo' : 'gray'} variant="light" size="md">
                Último cálculo: {fmtDateTime(calculatedAt)}
              </Badge>
            )}
            <Button
              variant="default"
              onClick={handleRefresh}
              loading={refreshing || loading}
              leftSection={<IconRefresh size={16} />}
            >
              Atualizar
            </Button>
          </Group>
        </div>

        {error && (
          <Alert icon={<IconAlertCircle size={16} />} title="Erro" color="red" variant="light" mb="lg">
            {error}
          </Alert>
        )}

        {loading && (
          <div style={{ textAlign: 'center', padding: '60px 0' }}>
            <Loader size="lg" variant="dots" />
            <Text mt="md" c="dimmed">Carregando seu plano de carreira...</Text>
          </div>
        )}

        {!loading && evaluation && (
          <Stack gap="lg">
            <CareerPlanEvaluationView
              evaluation={simulatedEvaluation || evaluation}
              isSimulated={Boolean(simulatedEvaluation)}
              onResetSimulation={() => {
                setSimulatedEvaluation(null)
                setResetTrigger(prev => prev + 1)
              }}
            />
            <ContractRtTable
              isMe={true}
              onSimulationChange={setSimulatedEvaluation}
              resetTrigger={resetTrigger}
            />
          </Stack>
        )}
      </div>
    </Menu>
  )
}

export default MyCareerPlanPage
