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
} from '@tabler/icons-react'
import Menu from '../Menu'
import {
  apiService,
  SqsQueueStats,
  SqsMessageItem,
} from '../../services/apiService'
import { toast } from '../../utils/toast'

export const SqsQueuePanel: React.FC = () => {
  const [stats, setStats] = useState<SqsQueueStats | null>(null)
  const [messages, setMessages] = useState<SqsMessageItem[]>([])
  const [loadingStats, setLoadingStats] = useState(false)
  const [loadingMessages, setLoadingMessages] = useState(false)
  const [processingReceipt, setProcessingReceipt] = useState<string | null>(null)
  const [discardingReceipt, setDiscardingReceipt] = useState<string | null>(null)
  const [errorMessage, setErrorMessage] = useState<string | null>(null)

  const fetchStats = useCallback(async () => {
    setLoadingStats(true)
    setErrorMessage(null)
    try {
      const res = await apiService.getSqsQueueStats()
      setStats(res)
    } catch (err: any) {
      setErrorMessage(err.message || 'Falha ao buscar estatísticas da fila.')
    } finally {
      setLoadingStats(false)
    }
  }, [])

  const fetchMessages = useCallback(async () => {
    setLoadingMessages(true)
    try {
      const res = await apiService.peekSqsMessages(10)
      setMessages(res || [])
    } catch (err: any) {
      toast.error(err.message || 'Falha ao consultar mensagens da fila.')
    } finally {
      setLoadingMessages(false)
    }
  }, [])

  const handleRefreshAll = useCallback(() => {
    fetchStats()
    fetchMessages()
  }, [fetchStats, fetchMessages])

  useEffect(() => {
    handleRefreshAll()
    const interval = setInterval(fetchStats, 30000)
    return () => clearInterval(interval)
  }, [handleRefreshAll, fetchStats])

  const handleProcess = async (msg: SqsMessageItem) => {
    setProcessingReceipt(msg.receiptHandle)
    try {
      const res = await apiService.processSqsMessage(msg.receiptHandle, JSON.stringify(msg))
      toast.success(res.message || 'Mensagem importada com sucesso!')
      setMessages((prev) => prev.filter((m) => m.receiptHandle !== msg.receiptHandle))
      fetchStats()
    } catch (err: any) {
      toast.error(err.message || 'Falha ao importar mensagem.')
    } finally {
      setProcessingReceipt(null)
    }
  }

  const handleDiscard = async (receiptHandle: string) => {
    if (!window.confirm('Tem certeza que deseja descartar esta mensagem da fila sem importar?')) {
      return
    }
    setDiscardingReceipt(receiptHandle)
    try {
      await apiService.discardSqsMessage(receiptHandle)
      toast.success('Mensagem removida da fila.')
      setMessages((prev) => prev.filter((m) => m.receiptHandle !== receiptHandle))
      fetchStats()
    } catch (err: any) {
      toast.error(err.message || 'Falha ao descartar mensagem.')
    } finally {
      setDiscardingReceipt(null)
    }
  }

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
          <Group justify="space-between" align="flex-start" mb="xl">
            <div>
              <Group gap="xs" align="center" mb={6}>
                <IconCloud size={30} color="#2563eb" />
                <Title order={2} style={{ color: '#111827', fontWeight: 700, margin: 0 }}>
                  Fila de Scraping AWS (SQS / S3)
                </Title>
                {stats?.isConfigured ? (
                  <Badge color="green" variant="light" size="md">
                    SQS Conectado
                  </Badge>
                ) : (
                  <Badge color="gray" variant="light" size="md">
                    Não Configurado
                  </Badge>
                )}
              </Group>
              <Text style={{ color: '#6b7280' }} size="sm">
                Monitore os resultados de scraping enviados para a Amazon Queue (SQS) e importe os arquivos CSV do S3 sob demanda.
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
                  Mensagens na Fila
                </Text>
                <div style={{ padding: 8, borderRadius: 8, backgroundColor: '#eff6ff' }}>
                  <IconInbox size={20} color="#2563eb" />
                </div>
              </Group>
              <Group align="baseline" gap="xs" mt={6}>
                <Text style={{ color: '#111827', fontSize: '2rem', fontWeight: 700, lineHeight: 1 }}>
                  {loadingStats ? <Loader size="sm" /> : stats?.approximateMessageCount ?? 0}
                </Text>
                <Text size="xs" style={{ color: '#6b7280' }}>
                  disponíveis para importação
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
                  em andamento (in-flight)
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
                  Estado da Conexão
                </Text>
                <div style={{ padding: 8, borderRadius: 8, backgroundColor: stats?.isConfigured ? '#f0fdf4' : '#f3f4f6' }}>
                  <IconClock size={20} color={stats?.isConfigured ? '#16a34a' : '#9ca3af'} />
                </div>
              </Group>
              <Group align="baseline" gap="xs" mt={6}>
                <Text style={{ color: '#111827', fontSize: '1.5rem', fontWeight: 700, lineHeight: 1.2 }}>
                  {stats?.isConfigured ? 'Ativa' : 'Desconectada'}
                </Text>
                <Text size="xs" style={{ color: '#6b7280' }}>
                  polling 20s
                </Text>
              </Group>
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
                  Mensagens Pendentes na Fila
                </Title>
                <Text size="xs" style={{ color: '#6b7280' }} mt={2}>
                  Listagem das mensagens aguardando processamento pelo worker local ou por importação manual.
                </Text>
              </div>
              <Button
                size="xs"
                variant="light"
                leftSection={<IconRefresh size={14} />}
                onClick={fetchMessages}
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
                Nenhuma mensagem pendente na fila no momento.
              </Alert>
            ) : (
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
                          {msg.store || 'Geral'}
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
