import React, { useState, useEffect, useCallback } from 'react'
import {
  Title,
  Text,
  Card,
  Group,
  Badge,
  Button,
  Table,
  Alert,
  Loader,
  Tooltip,
  Paper,
  SimpleGrid,
  Tabs,
  Stack,
} from '@mantine/core'
import {
  IconCloud,
  IconRefresh,
  IconCheck,
  IconTrash,
  IconDownload,
  IconAlertCircle,
  IconClock,
  IconLayersLinked,
  IconInbox,
  IconPlayerPlay,
  IconCloudDownload,
  IconLock,
  IconBuildingStore,
  IconCalendar,
} from '@tabler/icons-react'
import Menu from '../Menu'
import {
  apiService,
  SqsQueueStats,
  SqsMessageItem,
} from '../../services/apiService'
import { toast } from '../../utils/toast'

export const SqsQueuePanel: React.FC = () => {
  const [activeTab, setActiveTab] = useState<'jobs' | 'results'>('jobs')
  const [stats, setStats] = useState<SqsQueueStats | null>(null)
  const [messages, setMessages] = useState<SqsMessageItem[]>([])
  const [loadingStats, setLoadingStats] = useState(false)
  const [loadingMessages, setLoadingMessages] = useState(false)
  const [processingReceipt, setProcessingReceipt] = useState<string | null>(null)
  const [discardingReceipt, setDiscardingReceipt] = useState<string | null>(null)
  const [errorMessage, setErrorMessage] = useState<string | null>(null)

  const fetchStats = useCallback(async (queueType: 'jobs' | 'results') => {
    setLoadingStats(true)
    setErrorMessage(null)
    try {
      const res = await apiService.getSqsQueueStats(queueType)
      setStats(res)
    } catch (err: any) {
      setErrorMessage(err.message || 'Falha ao buscar estatísticas da fila.')
    } finally {
      setLoadingStats(false)
    }
  }, [])

  const fetchMessages = useCallback(async (queueType: 'jobs' | 'results') => {
    setLoadingMessages(true)
    try {
      const res = await apiService.peekSqsMessages(10, queueType)
      setMessages(res || [])
    } catch (err: any) {
      toast.error(err.message || 'Falha ao consultar mensagens da fila.')
    } finally {
      setLoadingMessages(false)
    }
  }, [])

  const handleRefreshAll = useCallback(() => {
    fetchStats(activeTab)
    fetchMessages(activeTab)
  }, [activeTab, fetchStats, fetchMessages])

  useEffect(() => {
    handleRefreshAll()
    const interval = setInterval(() => fetchStats(activeTab), 30000)
    return () => clearInterval(interval)
  }, [handleRefreshAll, fetchStats, activeTab])

  const handleTabChange = (val: string | null) => {
    if (!val) return
    const nextTab = val as 'jobs' | 'results'
    setActiveTab(nextTab)
    setStats(null)
    setMessages([])
    fetchStats(nextTab)
    fetchMessages(nextTab)
  }

  const handleProcess = async (msg: SqsMessageItem) => {
    setProcessingReceipt(msg.receiptHandle)
    try {
      const res = await apiService.processSqsMessage(msg.receiptHandle, JSON.stringify(msg), activeTab)
      toast.success(res.message || 'Mensagem importada com sucesso!')
      setMessages((prev) => prev.filter((m) => m.receiptHandle !== msg.receiptHandle))
      fetchStats(activeTab)
    } catch (err: any) {
      toast.error(err.message || 'Falha ao importar mensagem.')
    } finally {
      setProcessingReceipt(null)
    }
  }

  const handleDiscard = async (receiptHandle: string) => {
    const isJobs = activeTab === 'jobs'
    const confirmText = isJobs
      ? 'Tem certeza que deseja cancelar e descartar esta tarefa da fila de scraping?'
      : 'Tem certeza que deseja descartar este resultado da fila sem importar para o banco?'

    if (!window.confirm(confirmText)) {
      return
    }
    setDiscardingReceipt(receiptHandle)
    try {
      await apiService.discardSqsMessage(receiptHandle, activeTab)
      toast.success(isJobs ? 'Tarefa removida da fila de scraping.' : 'Resultado removido da fila.')
      setMessages((prev) => prev.filter((m) => m.receiptHandle !== receiptHandle))
      fetchStats(activeTab)
    } catch (err: any) {
      toast.error(err.message || 'Falha ao descartar mensagem.')
    } finally {
      setDiscardingReceipt(null)
    }
  }

  const isJobs = activeTab === 'jobs'

  return (
    <Menu>
      <div
        style={{
          backgroundColor: '#ffffff',
          minHeight: 'calc(100vh - 60px)',
          padding: '28px 36px',
        }}
      >
        <div style={{ maxWidth: 1300, margin: '0 auto' }}>
          {/* Header */}
          <Group justify="space-between" align="flex-start" mb="lg">
            <div>
              <Group gap="xs" align="center" mb={6}>
                <IconCloud size={30} color="#2563eb" />
                <Title order={2} style={{ color: '#111827', fontWeight: 700, margin: 0 }}>
                  Monitoramento de Filas AWS (SQS)
                </Title>
                {stats?.isConfigured ? (
                  <Badge color="green" variant="light" size="md">
                    {stats.queueName || 'SQS Conectado'}
                  </Badge>
                ) : (
                  <Badge color="gray" variant="light" size="md">
                    Não Configurado
                  </Badge>
                )}
              </Group>
              <Text style={{ color: '#6b7280' }} size="sm">
                Gerencie o fluxo de mensagens assíncronas: ordens de scraping para os workers Fargate e resultados processados disponíveis no S3.
              </Text>
            </div>
            <Button
              leftSection={<IconRefresh size={16} />}
              variant="default"
              onClick={handleRefreshAll}
              loading={loadingStats || loadingMessages}
            >
              Atualizar
            </Button>
          </Group>

          {/* Abas de Navegação entre Filas */}
          <Tabs
            value={activeTab}
            onChange={handleTabChange}
            variant="outline"
            radius="md"
            mb="xl"
            styles={{
              tab: {
                backgroundColor: '#ffffff',
                fontWeight: 600,
                fontSize: '0.95rem',
                padding: '10px 20px',
                '&[data-active]': {
                  borderColor: '#2563eb',
                  color: '#2563eb',
                  backgroundColor: '#eff6ff',
                },
              },
            }}
          >
            <Tabs.List>
              <Tabs.Tab value="jobs" leftSection={<IconPlayerPlay size={18} color="#2563eb" />}>
                Fila de Tarefas (Jobs / Workers)
              </Tabs.Tab>
              <Tabs.Tab value="results" leftSection={<IconCloudDownload size={18} color="#059669" />}>
                Fila de Resultados (S3 / Importação)
              </Tabs.Tab>
            </Tabs.List>
          </Tabs>

          {errorMessage && (
            <Alert icon={<IconAlertCircle size={16} />} color="red" variant="light" mb="lg">
              {errorMessage}
            </Alert>
          )}

          {/* Cards de Métricas (KPIs) */}
          <SimpleGrid cols={{ base: 1, sm: 3 }} mb="xl">
            <Paper
              withBorder
              p="lg"
              radius="md"
              style={{
                backgroundColor: '#ffffff',
                borderColor: '#e5e7eb',
                boxShadow: '0 1px 3px rgba(0, 0, 0, 0.04)',
              }}
            >
              <Group justify="space-between" mb="xs">
                <Text size="xs" fw={700} style={{ color: '#6b7280', textTransform: 'uppercase', letterSpacing: '0.5px' }}>
                  {isJobs ? 'Tarefas na Fila' : 'Resultados na Fila'}
                </Text>
                <div style={{ padding: 8, borderRadius: 8, backgroundColor: isJobs ? '#eff6ff' : '#ecfdf5' }}>
                  <IconInbox size={20} color={isJobs ? '#2563eb' : '#059669'} />
                </div>
              </Group>
              <Group align="baseline" gap="xs" mt={6}>
                <Text style={{ color: '#111827', fontSize: '2rem', fontWeight: 700, lineHeight: 1 }}>
                  {loadingStats ? <Loader size="sm" /> : stats?.approximateMessageCount ?? 0}
                </Text>
                <Text size="xs" style={{ color: '#6b7280' }}>
                  {isJobs ? 'aguardando execução' : 'disponíveis para importação'}
                </Text>
              </Group>
            </Paper>

            <Paper
              withBorder
              p="lg"
              radius="md"
              style={{
                backgroundColor: '#ffffff',
                borderColor: '#e5e7eb',
                boxShadow: '0 1px 3px rgba(0, 0, 0, 0.04)',
              }}
            >
              <Group justify="space-between" mb="xs">
                <Text size="xs" fw={700} style={{ color: '#6b7280', textTransform: 'uppercase', letterSpacing: '0.5px' }}>
                  Em Processamento
                </Text>
                <div style={{ padding: 8, borderRadius: 8, backgroundColor: '#fefce8' }}>
                  <IconLayersLinked size={20} color="#ca8a04" />
                </div>
              </Group>
              <Group align="baseline" gap="xs" mt={6}>
                <Text style={{ color: '#111827', fontSize: '2rem', fontWeight: 700, lineHeight: 1 }}>
                  {loadingStats ? <Loader size="sm" /> : stats?.approximateInFlightCount ?? 0}
                </Text>
                <Text size="xs" style={{ color: '#6b7280' }}>
                  {isJobs ? 'sendo extraídos pelos workers' : 'em andamento (in-flight)'}
                </Text>
              </Group>
            </Paper>

            <Paper
              withBorder
              p="lg"
              radius="md"
              style={{
                backgroundColor: '#ffffff',
                borderColor: '#e5e7eb',
                boxShadow: '0 1px 3px rgba(0, 0, 0, 0.04)',
              }}
            >
              <Group justify="space-between" mb="xs">
                <Text size="xs" fw={700} style={{ color: '#6b7280', textTransform: 'uppercase', letterSpacing: '0.5px' }}>
                  Fila Conectada
                </Text>
                <div style={{ padding: 8, borderRadius: 8, backgroundColor: stats?.isConfigured ? '#f0fdf4' : '#f3f4f6' }}>
                  <IconClock size={20} color={stats?.isConfigured ? '#16a34a' : '#9ca3af'} />
                </div>
              </Group>
              <Group align="baseline" gap="xs" mt={6}>
                <Text style={{ color: '#111827', fontSize: '1.25rem', fontWeight: 700, lineHeight: 1.2 }}>
                  {stats?.queueName || (stats?.isConfigured ? 'Ativa' : 'Desconectada')}
                </Text>
              </Group>
              <Text size="xs" style={{ color: '#6b7280' }} mt={4}>
                {isJobs ? 'Fila de entrada (Jobs)' : 'Fila de saída (Resultados)'}
              </Text>
            </Paper>
          </SimpleGrid>

          {/* Tabela de Mensagens Disponíveis */}
          <Card
            withBorder
            radius="md"
            p="xl"
            style={{
              backgroundColor: '#ffffff',
              borderColor: '#e5e7eb',
              boxShadow: '0 1px 3px rgba(0, 0, 0, 0.04)',
            }}
          >
            <Group justify="space-between" mb="lg">
              <div>
                <Title order={4} style={{ color: '#111827', fontWeight: 600 }}>
                  {isJobs ? 'Ordens de Scraping Pendentes' : 'Resultados Prontos no S3'}
                </Title>
                <Text size="xs" style={{ color: '#6b7280' }} mt={2}>
                  {isJobs
                    ? 'Mensagens com credenciais criptografadas aguardando consumo pelo worker Fargate.'
                    : 'Resultados de scraping prontos para download do S3 e importação no banco.'}
                </Text>
              </div>
              <Button
                size="xs"
                variant="light"
                leftSection={<IconRefresh size={14} />}
                onClick={() => fetchMessages(activeTab)}
                loading={loadingMessages}
              >
                Consultar Mensagens
              </Button>
            </Group>

            {loadingMessages ? (
              <Group justify="center" py="xl">
                <Loader size="md" color="blue" />
                <Text size="sm" style={{ color: '#6b7280' }}>
                  Buscando mensagens da fila SQS...
                </Text>
              </Group>
            ) : messages.length === 0 ? (
              <Alert icon={<IconCheck size={16} />} color="blue" variant="light">
                {isJobs
                  ? 'Nenhuma ordem de scraping pendente na fila de jobs no momento.'
                  : 'Nenhum resultado pendente na fila de resultados no momento.'}
              </Alert>
            ) : isJobs ? (
              /* TABELA DE JOBS */
              <Table
                highlightOnHover
                verticalSpacing="sm"
                horizontalSpacing="md"
                style={{ borderCollapse: 'collapse', width: '100%' }}
              >
                <Table.Thead style={{ backgroundColor: '#f8fafc' }}>
                  <Table.Tr>
                    <Table.Th style={{ color: '#475569', fontWeight: 600, fontSize: '0.85rem' }}>Matrícula</Table.Th>
                    <Table.Th style={{ color: '#475569', fontWeight: 600, fontSize: '0.85rem' }}>Tipo</Table.Th>
                    <Table.Th style={{ color: '#475569', fontWeight: 600, fontSize: '0.85rem' }}>Período / Datas</Table.Th>
                    <Table.Th style={{ color: '#475569', fontWeight: 600, fontSize: '0.85rem' }}>Loja / Unidade</Table.Th>
                    <Table.Th style={{ color: '#475569', fontWeight: 600, fontSize: '0.85rem' }}>Segurança</Table.Th>
                    <Table.Th style={{ color: '#475569', fontWeight: 600, fontSize: '0.85rem' }}>Enviado em</Table.Th>
                    <Table.Th style={{ textAlign: 'right', color: '#475569', fontWeight: 600, fontSize: '0.85rem' }}>Ações</Table.Th>
                  </Table.Tr>
                </Table.Thead>
                <Table.Tbody>
                  {messages.map((msg) => (
                    <Table.Tr key={msg.receiptHandle} style={{ borderBottom: '1px solid #f1f5f9' }}>
                      <Table.Td>
                        <Badge variant="light" color="indigo" size="sm">
                          {msg.matricula || 'N/A'}
                        </Badge>
                      </Table.Td>
                      <Table.Td>
                        <Badge
                          variant="light"
                          color={msg.scrapeType === 'consultor' ? 'violet' : 'blue'}
                          size="sm"
                        >
                          {msg.scrapeType === 'consultor' ? 'Consultor' : 'Geral'}
                        </Badge>
                      </Table.Td>
                      <Table.Td>
                        <Group gap={4}>
                          <IconCalendar size={14} color="#6b7280" />
                          <Text size="xs" style={{ color: '#374151', fontWeight: 500 }}>
                            {msg.scrapeDates && msg.scrapeDates.length > 0
                              ? msg.scrapeDates.join(', ')
                              : msg.scrapeDate || 'Atual'}
                          </Text>
                        </Group>
                      </Table.Td>
                      <Table.Td>
                        <Group gap={4}>
                          <IconBuildingStore size={14} color="#6b7280" />
                          <Text size="xs" style={{ color: '#4b5563' }}>
                            {msg.store || (msg.scrapeType === 'consultor' ? 'Consultor' : 'Automático')}
                          </Text>
                        </Group>
                      </Table.Td>
                      <Table.Td>
                        <Badge
                          variant="outline"
                          color={msg.isEncrypted ? 'teal' : 'gray'}
                          size="xs"
                          leftSection={<IconLock size={12} />}
                        >
                          {msg.isEncrypted ? 'AES-256-GCM' : 'Padrão'}
                        </Badge>
                      </Table.Td>
                      <Table.Td>
                        <Text size="xs" style={{ color: '#6b7280' }}>
                          {msg.sentAt ? new Date(msg.sentAt).toLocaleString('pt-BR') : '—'}
                        </Text>
                      </Table.Td>
                      <Table.Td style={{ textAlign: 'right' }}>
                        <Button
                          size="xs"
                          variant="subtle"
                          color="red"
                          leftSection={<IconTrash size={14} />}
                          loading={discardingReceipt === msg.receiptHandle}
                          onClick={() => handleDiscard(msg.receiptHandle)}
                        >
                          Cancelar Ordem
                        </Button>
                      </Table.Td>
                    </Table.Tr>
                  ))}
                </Table.Tbody>
              </Table>
            ) : (
              /* TABELA DE RESULTADOS */
              <Table
                highlightOnHover
                verticalSpacing="sm"
                horizontalSpacing="md"
                style={{ borderCollapse: 'collapse', width: '100%' }}
              >
                <Table.Thead style={{ backgroundColor: '#f8fafc' }}>
                  <Table.Tr>
                    <Table.Th style={{ color: '#475569', fontWeight: 600, fontSize: '0.85rem' }}>Matrícula</Table.Th>
                    <Table.Th style={{ color: '#475569', fontWeight: 600, fontSize: '0.85rem' }}>Loja / Tipo</Table.Th>
                    <Table.Th style={{ color: '#475569', fontWeight: 600, fontSize: '0.85rem' }}>Período</Table.Th>
                    <Table.Th style={{ color: '#475569', fontWeight: 600, fontSize: '0.85rem' }}>Linhas</Table.Th>
                    <Table.Th style={{ color: '#475569', fontWeight: 600, fontSize: '0.85rem' }}>Arquivo S3</Table.Th>
                    <Table.Th style={{ color: '#475569', fontWeight: 600, fontSize: '0.85rem' }}>Recebido em</Table.Th>
                    <Table.Th style={{ textAlign: 'right', color: '#475569', fontWeight: 600, fontSize: '0.85rem' }}>Ações</Table.Th>
                  </Table.Tr>
                </Table.Thead>
                <Table.Tbody>
                  {messages.map((msg) => (
                    <Table.Tr key={msg.receiptHandle} style={{ borderBottom: '1px solid #f1f5f9' }}>
                      <Table.Td>
                        <Badge variant="light" color="indigo" size="sm">
                          {msg.matricula || 'N/A'}
                        </Badge>
                      </Table.Td>
                      <Table.Td>
                        <Text size="sm" fw={500} style={{ color: '#1f2937' }}>
                          {msg.store || (msg.scrapeType === 'consultor' ? 'Consultor' : 'Geral')}
                        </Text>
                      </Table.Td>
                      <Table.Td>
                        <Text size="xs" style={{ color: '#6b7280' }}>
                          {msg.scrapeDate || 'Todos'}
                        </Text>
                      </Table.Td>
                      <Table.Td>
                        <Text size="sm" fw={600} style={{ color: '#111827' }}>
                          {msg.rowCount != null ? msg.rowCount.toLocaleString('pt-BR') : '—'}
                        </Text>
                      </Table.Td>
                      <Table.Td>
                        <Tooltip label={`s3://${msg.s3Bucket || ''}/${msg.s3Key || ''}`} withArrow>
                          <Text
                            size="xs"
                            style={{
                              color: '#6b7280',
                              maxWidth: 220,
                              overflow: 'hidden',
                              textOverflow: 'ellipsis',
                              whiteSpace: 'nowrap',
                              fontFamily: 'monospace',
                            }}
                          >
                            {msg.s3Key ? msg.s3Key.split('/').pop() : '—'}
                          </Text>
                        </Tooltip>
                      </Table.Td>
                      <Table.Td>
                        <Text size="xs" style={{ color: '#6b7280' }}>
                          {msg.sentAt ? new Date(msg.sentAt).toLocaleString('pt-BR') : '—'}
                        </Text>
                      </Table.Td>
                      <Table.Td style={{ textAlign: 'right' }}>
                        <Group gap="xs" justify="flex-end">
                          <Button
                            size="xs"
                            color="teal"
                            variant="light"
                            leftSection={<IconDownload size={14} />}
                            loading={processingReceipt === msg.receiptHandle}
                            disabled={discardingReceipt === msg.receiptHandle}
                            onClick={() => handleProcess(msg)}
                          >
                            Importar
                          </Button>
                          <Button
                            size="xs"
                            variant="subtle"
                            color="red"
                            leftSection={<IconTrash size={14} />}
                            loading={discardingReceipt === msg.receiptHandle}
                            disabled={processingReceipt === msg.receiptHandle}
                            onClick={() => handleDiscard(msg.receiptHandle)}
                          >
                            Descartar
                          </Button>
                        </Group>
                      </Table.Td>
                    </Table.Tr>
                  ))}
                </Table.Tbody>
              </Table>
            )}
          </Card>
        </div>
      </div>
    </Menu>
  )
}

export default SqsQueuePanel

