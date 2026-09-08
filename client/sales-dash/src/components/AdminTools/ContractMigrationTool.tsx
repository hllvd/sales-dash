import React, { useState, useEffect, useCallback } from 'react'
import {
  Title,
  Autocomplete,
  Button,
  Switch,
  Group,
  Text,
  Card,
  Alert,
  Loader,
  Stack,
} from '@mantine/core'
import { IconArrowsExchange, IconCheck, IconAlertTriangle, IconUser } from '@tabler/icons-react'
import Menu from '../Menu'
import { apiService, AdminUserSearchItem, AdminMigrateContractsResult } from '../../services/apiService'
import { toast } from '../../utils/toast'

export const ContractMigrationTool: React.FC = () => {
  const [fromSearch, setFromSearch] = useState('')
  const [toSearch, setToSearch] = useState('')
  const [migrateMatricula, setMigrateMatricula] = useState(true)

  const [fromUsers, setFromUsers] = useState<AdminUserSearchItem[]>([])
  const [toUsers, setToUsers] = useState<AdminUserSearchItem[]>([])

  const [loadingFrom, setLoadingFrom] = useState(false)
  const [loadingTo, setLoadingTo] = useState(false)
  const [submitting, setSubmitting] = useState(false)

  const [lastResult, setLastResult] = useState<AdminMigrateContractsResult | null>(null)
  const [errorMessage, setErrorMessage] = useState<string | null>(null)

  // Extrai o email caso o valor esteja formatado como "Nome (email@dominio.com)"
  const extractEmail = (input: string): string => {
    const match = input.match(/\(([^)]+@[^)]+)\)$/)
    if (match) return match[1].trim()
    return input.trim()
  }

  // Busca usuários para o campo From
  const searchFrom = useCallback(async (query: string) => {
    setLoadingFrom(true)
    try {
      const res = await apiService.searchAdminUsers(query)
      if (res.success && res.data) {
        setFromUsers(res.data)
      }
    } catch {
      // Falha silenciosa no autocomplete
    } finally {
      setLoadingFrom(false)
    }
  }, [])

  // Busca usuários para o campo To
  const searchTo = useCallback(async (query: string) => {
    setLoadingTo(true)
    try {
      const res = await apiService.searchAdminUsers(query)
      if (res.success && res.data) {
        setToUsers(res.data)
      }
    } catch {
      // Falha silenciosa no autocomplete
    } finally {
      setLoadingTo(false)
    }
  }, [])

  // Busca inicial para ambos os campos
  useEffect(() => {
    searchFrom('')
    searchTo('')
  }, [searchFrom, searchTo])

  const handleFromChange = (val: string) => {
    setFromSearch(val)
    searchFrom(extractEmail(val))
  }

  const handleToChange = (val: string) => {
    setToSearch(val)
    searchTo(extractEmail(val))
  }

  const fromOptions = fromUsers.map((u) => `${u.name} (${u.email})`)
  const toOptions = toUsers.map((u) => `${u.name} (${u.email})`)

  const handleMigrate = async (e: React.FormEvent) => {
    e.preventDefault()
    const fromEmail = extractEmail(fromSearch)
    const toEmail = extractEmail(toSearch)

    if (!fromEmail || !toEmail) {
      toast.error('Informe os e-mails de origem e de destino.')
      return
    }

    if (fromEmail.toLowerCase() === toEmail.toLowerCase()) {
      toast.error('O e-mail de origem e destino devem ser diferentes.')
      return
    }

    setSubmitting(true)
    setErrorMessage(null)
    setLastResult(null)

    try {
      const res = await apiService.adminMigrateContracts({
        fromEmail,
        toEmail,
        migrateMatricula,
      })

      if (res.success && res.data) {
        setLastResult(res.data)
        toast.success(`${res.data.contractsMigrated} contratos migrados com sucesso!`)
      } else {
        setErrorMessage(res.message || 'Falha ao migrar contratos.')
        toast.error(res.message || 'Falha ao migrar contratos.')
      }
    } catch (err: any) {
      const msg = err.message || 'Erro inesperado ao realizar a migração.'
      setErrorMessage(msg)
      toast.error(msg)
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <Menu>
      <div style={{ maxWidth: '800px', margin: '0 auto', padding: '24px 16px' }}>
        <Stack gap="lg">
          <div>
            <Title order={2}>Migração de Contratos</Title>
            <Text c="dimmed" size="sm" mt="xs">
              Ferramenta administrativa para transferir todos os contratos de um consultor de origem (From) para um consultor de destino (To).
            </Text>
          </div>

          {errorMessage && (
            <Alert icon={<IconAlertTriangle size={16} />} title="Atenção" color="red">
              {errorMessage}
            </Alert>
          )}

          {lastResult && (
            <Alert icon={<IconCheck size={16} />} title="Migração Concluída" color="green">
              <Text size="sm">
                <strong>Origem:</strong> {lastResult.fromUser}
              </Text>
              <Text size="sm">
                <strong>Destino:</strong> {lastResult.toUser}
              </Text>
              <Text size="sm" mt="xs">
                Contratos transferidos: <strong>{lastResult.contractsMigrated}</strong>
              </Text>
              {migrateMatricula && (
                <Text size="sm">
                  Matrículas transferidas/unificadas: <strong>{lastResult.matriculasMigrated}</strong>
                </Text>
              )}
            </Alert>
          )}

          <Card shadow="sm" padding="lg" radius="md" withBorder>
            <form onSubmit={handleMigrate}>
              <Stack gap="md">
                <Autocomplete
                  label="Consultor de Origem (From)"
                  description="Selecione ou digite o e-mail do consultor cujos contratos serão migrados"
                  placeholder="Buscar por nome ou e-mail..."
                  leftSection={loadingFrom ? <Loader size="xs" /> : <IconUser size={16} />}
                  data={fromOptions}
                  value={fromSearch}
                  onChange={handleFromChange}
                  required
                />

                <Autocomplete
                  label="Consultor de Destino (To)"
                  description="Selecione ou digite o e-mail do consultor que receberá os contratos"
                  placeholder="Buscar por nome ou e-mail..."
                  leftSection={loadingTo ? <Loader size="xs" /> : <IconUser size={16} />}
                  data={toOptions}
                  value={toSearch}
                  onChange={handleToChange}
                  required
                />

                <Switch
                  label="Migrar Matrícula(s)"
                  description="Transfere também as matrículas ativas do consultor de origem para o consultor de destino."
                  checked={migrateMatricula}
                  onChange={(e) => setMigrateMatricula(e.currentTarget.checked)}
                  mt="xs"
                />

                <Group justify="flex-end" mt="md">
                  <Button
                    type="submit"
                    color="blue"
                    leftSection={<IconArrowsExchange size={16} />}
                    loading={submitting}
                  >
                    Migrar Contratos
                  </Button>
                </Group>
              </Stack>
            </form>
          </Card>
        </Stack>
      </div>
    </Menu>
  )
}

export default ContractMigrationTool
