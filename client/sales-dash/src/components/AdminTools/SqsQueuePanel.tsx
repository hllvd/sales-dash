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

  const handleRefreshAll = () => {
    fetchStats()
    fetchMessages()
  }

  useEffect(() => {
    handleRefreshAll()
    const interval = setInterval(fetchStats, 30000)
    return () => clearInterval(interval)
  }, [fetchStats, fetchMessages])

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
    <div style={{ display: 'flex' }}>
      <Menu />
      <div style={{ flex: 1, padding: '24px 32px', maxWidth: 1200 }}>
        <Group justify="space-between" mb="xl">
          <div>
            <Group gap="xs">
              <IconCloud size={28} color="#228be6" />
              <Title order={2}>Fila de Scraping AWS (SQS / S3)</Title>
              {stats?.isConfigured ? (
                <Badge color="green" variant="filled">
                  SQS Conectado
                </Badge>
              ) : (
                <Badge color="gray" variant="outline">
                  Não Configurado
                </Badge>
              )}
            </Group>
            <Text c="dimmed" size="sm" mt={4}>
              Monitore os resultados de scraping enviados para a Amazon Queue (SQS) e importe os CSVs do S3 sob demanda.
            </Text>
          </div>
          <Button
            leftSection={<IconRefresh size={16} />}
            variant="light"
            onClick={handleRefreshAll}
            loading={loadingStats || loadingMessages}
          >
            Atualizar
          </Button>
        </Group>

        {errorMessage && (
          <Alert icon={<IconAlertCircle size={16} />} color="red" mb="lg">
            {errorMessage}
          </Alert>
        )}

        {/* Métricas da Fila */}
        <SimpleGrid cols={{ base: 1, sm: 3 }} mb="xl">
          <Paper withBorder p="md" radius="md">
            <Group justify="space-between">
              <Text size="xs" c="dimmed" fw={700} tt="uppercase">
                Mensagens na Fila
              </Text>
              <IconInbox size={20} color="#228be6" />
            </Group>
            <Group align="flex-end" gap="xs" mt={10}>
              <Text size="xl" fw={700}>
                {loadingStats ? <Loader size="sm" /> : stats?.approximateMessageCount ?? 0}
              </Text>
              <Text size="xs" c="dimmed" mb={4}>
                disponíveis para importação
              </Text>
            </Group>
          </Paper>

          <Paper withBorder p="md" radius="md">
            <Group justify="space-between">
              <Text size="xs" c="dimmed" fw={700} tt="uppercase">
                Em Processamento
              </Text>
              <IconLayersLinked size={20} color="#fab005" />
            </Group>
            <Group align="flex-end" gap="xs" mt={10}>
              <Text size="xl" fw={700}>
                {loadingStats ? <Loader size="sm" /> : stats?.approximateInFlightCount ?? 0}
              </Text>
              <Text size="xs" c="dimmed" mb={4}>
                em andamento (in-flight)
              </Text>
            </Group>
          </Paper>

          <Paper withBorder p="md" radius="md">
            <Group justify="space-between">
              <Text size="xs" c="dimmed" fw={700} tt="uppercase">
                Idade Mais Antiga
              </Text>
              <IconClock size={20} color="#fa5252" />
            </Group>
            <Group align="flex-end" gap="xs" mt={10}>
              <Text size="xl" fw={700}>
                {loadingStats ? (
                  <Loader size="sm" />
                ) : stats?.oldestMessageAgeSeconds ? (
                  `${Math.round(parseInt(stats.oldestMessageAgeSeconds, 10) / 60)} min`
                ) : (
                  '0'
                )}
              </Text>
              <Text size="xs" c="dimmed" mb={4}>
                tempo de espera
              </Text>
            </Group>
          </Paper>
        </SimpleGrid>

        {/* Tabela de Mensagens Disponíveis */}
        <Card withBorder radius="md" p="lg">
          <Group justify="space-between" mb="md">
            <div>
              <Title order={4}>Mensagens Pendentes na Fila</Title>
              <Text size="xs" c="dimmed">
                Visualização das últimas mensagens aguardando processamento.
              </Text>
            </div>
            <Button
              size="xs"
              variant="default"
              leftSection={<IconRefresh size={14} />}
              onClick={fetchMessages}
              loading={loadingMessages}
            >
              Consultar Mensagens
            </Button>
          </Group>

          {loadingMessages ? (
            <Group justify="center" py="xl">
              <Loader />
              <Text size="sm" c="dimmed">
                Buscando mensagens da fila SQS...
              </Text>
            </Group>
          ) : messages.length === 0 ? (
            <Alert icon={<IconCheck size={16} />} color="blue" variant="light">
              Nenhuma mensagem pendente na fila no momento.
            </Alert>
          ) : (
            <Table highlightOnHover verticalSpacing="sm">
              <Table.Thead>
                <Table.Tr>
                  <Table.Th>Matrícula</Table.Th>
                  <Table.Th>Loja</Table.Th>
                  <Table.Th>Período</Table.Th>
                  <Table.Th>Linhas</Table.Th>
                  <Table.Th>Arquivo S3</Table.Th>
                  <Table.Th>Recebido em</Table.Th>
                  <Table.Th style={{ textAlign: 'right' }}>Ações</Table.Th>
                </Table.Tr>
              </Table.Thead>
              <Table.Tbody>
                {messages.map((msg) => (
                  <Table.Tr key={msg.receiptHandle}>
                    <Table.Td>
                      <Badge variant="outline" color="indigo">
                        {msg.matricula || 'N/A'}
                      </Badge>
                    </Table.Td>
                    <Table.Td>
                      <Text size="sm">{msg.store || 'Geral'}</Text>
                    </Table.Td>
                    <Table.Td>
                      <Text size="xs" c="dimmed">
                        {msg.scrapeDate || 'Todos'}
                      </Text>
                    </Table.Td>
                    <Table.Td>
                      <Text size="sm" fw={500}>
                        {msg.rowCount ?? '—'}
                      </Text>
                    </Table.Td>
                    <Table.Td>
                      <Tooltip label={`s3://${msg.s3Bucket || ''}/${msg.s3Key || ''}`}>
                        <Text size="xs" c="dimmed" style={{ maxWidth: 200, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                          {msg.s3Key ? msg.s3Key.split('/').pop() : '—'}
                        </Text>
                      </Tooltip>
                    </Table.Td>
                    <Table.Td>
                      <Text size="xs" c="dimmed">
                        {msg.sentAt ? new Date(msg.sentAt).toLocaleString('pt-BR') : '—'}
                      </Text>
                    </Table.Td>
                    <Table.Td style={{ textAlign: 'right' }}>
                      <Group gap="xs" justify="flex-end">
                        <Button
                          size="xs"
                          color="green"
                          leftSection={<IconDownload size={14} />}
                          loading={processingReceipt === msg.receiptHandle}
                          disabled={discardingReceipt === msg.receiptHandle}
                          onClick={() => handleProcess(msg)}
                        >
                          Importar
                        </Button>
                        <Button
                          size="xs"
                          variant="light"
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
  )
}

export default SqsQueuePanel
