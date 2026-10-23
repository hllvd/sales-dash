import React, { useState } from 'react'
import {
  Title,
  Button,
  Group,
  Text,
  Textarea,
  Alert,
  Stack,
  Table,
  Paper,
  Badge,
  Card,
  SimpleGrid,
} from '@mantine/core'
import {
  IconSearch,
  IconAlertTriangle,
  IconFileSpreadsheet,
  IconTrash,
  IconCheck,
  IconX,
} from '@tabler/icons-react'
import Menu from '../Menu'
import {
  apiService,
  AdminCheckContractsResponse,
} from '../../services/apiService'
import { toast } from '../../utils/toast'

function formatDate(dateStr?: string | null): string {
  if (!dateStr) return '—'
  try {
    const d = new Date(dateStr)
    if (isNaN(d.getTime())) return dateStr
    return d.toLocaleDateString('pt-BR', { timeZone: 'UTC' })
  } catch {
    return dateStr
  }
}

function formatCurrency(amount?: number | null): string {
  if (amount === undefined || amount === null) return '—'
  return amount.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })
}

export const ContractComparisonTool: React.FC = () => {
  const [inputText, setInputText] = useState('')
  const [loading, setLoading] = useState(false)
  const [exporting, setExporting] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [data, setData] = useState<AdminCheckContractsResponse | null>(null)
  const [lastSubmittedNumbers, setLastSubmittedNumbers] = useState<string[]>([])

  const parseInputNumbers = (rawText: string): string[] => {
    return rawText
      .split(/[,\n\r]+/)
      .map((item) => item.trim())
      .filter((item) => item.length > 0)
  }

  const handleCompare = async () => {
    setError(null)
    const numbers = parseInputNumbers(inputText)

    if (numbers.length === 0) {
      setError('Por favor, informe ao menos um número de contrato.')
      return
    }

    try {
      setLoading(true)
      const res = await apiService.checkContracts(numbers)
      if (res.success && res.data) {
        setData(res.data)
        setLastSubmittedNumbers(numbers)
        toast.success(`Conferência concluída: ${res.data.uniqueInput} contratos únicos analisados.`)
      } else {
        setError(res.message || 'Falha ao processar a conferência dos contratos.')
      }
    } catch (err: any) {
      const msg = err.message || 'Erro inesperado ao conferir contratos.'
      setError(msg)
      toast.error(msg)
    } finally {
      setLoading(false)
    }
  }

  const handleClear = () => {
    setInputText('')
    setData(null)
    setError(null)
    setLastSubmittedNumbers([])
  }

  const handleExportXlsx = async () => {
    if (lastSubmittedNumbers.length === 0) {
      toast.warning('Nenhum resultado para exportar.')
      return
    }

    try {
      setExporting(true)
      const blob = await apiService.exportCheckContractsXlsx(lastSubmittedNumbers)
      const url = window.URL.createObjectURL(blob)
      const a = document.createElement('a')
      a.href = url
      a.download = `conferencia_contratos_${new Date().toISOString().slice(0, 10)}.xlsx`
      document.body.appendChild(a)
      a.click()
      a.remove()
      window.URL.revokeObjectURL(url)
      toast.success('Arquivo Excel (.xlsx) exportado com sucesso!')
    } catch (err: any) {
      toast.error(err.message || 'Erro ao exportar arquivo XLSX')
    } finally {
      setExporting(false)
    }
  }

  return (
    <Menu>
      <div style={{ maxWidth: '1300px', margin: '0 auto', padding: '16px' }}>
        <Stack gap="lg">
          {/* Header */}
          <div>
            <Group justify="space-between" align="flex-start">
              <div>
                <Group gap="xs" align="center">
                  <Title order={2}>Conferência de Contratos</Title>
                  <Badge color="red" variant="light" size="lg">
                    SuperAdmin
                  </Badge>
                </Group>
                <Text c="dimmed" size="sm" mt="xs">
                  Cole uma lista de contratos para buscar no sistema todos os registros (ativos e excluídos),
                  verificar atribuição, valores, datas e identificar duplicidades na entrada.
                </Text>
              </div>

              {data && (
                <Button
                  leftSection={<IconFileSpreadsheet size={18} />}
                  color="teal"
                  onClick={handleExportXlsx}
                  loading={exporting}
                >
                  Exportar XLSX
                </Button>
              )}
            </Group>
          </div>

          {error && (
            <Alert icon={<IconAlertTriangle size={16} />} title="Atenção" color="red">
              {error}
            </Alert>
          )}

          {/* Input Form */}
          <Paper withBorder p="md" radius="md">
            <Stack gap="md">
              <Textarea
                label="Lista de Números de Contratos"
                description="Cole os números de contratos separados por vírgula ou por linha."
                placeholder={'Ex:\n100234\n100235, 100236\n100237'}
                value={inputText}
                onChange={(e) => setInputText(e.currentTarget.value)}
                minRows={5}
                maxRows={12}
                autosize
              />

              <Group justify="space-between" align="center">
                <Text size="xs" c="dimmed">
                  Dica: Números repetidos serão detectados automaticamente e apresentados em uma tabela separada.
                </Text>

                <Group gap="xs">
                  <Button
                    variant="default"
                    leftSection={<IconTrash size={16} />}
                    onClick={handleClear}
                    disabled={loading || (!inputText && !data)}
                  >
                    Limpar
                  </Button>

                  <Button
                    leftSection={<IconSearch size={16} />}
                    color="red"
                    onClick={handleCompare}
                    loading={loading}
                    disabled={!inputText.trim()}
                  >
                    Comparar Contratos
                  </Button>
                </Group>
              </Group>
            </Stack>
          </Paper>

          {/* Results Area */}
          {data && (
            <Stack gap="lg">
              {/* Summary Cards */}
              <SimpleGrid cols={{ base: 2, sm: 3, md: 6 }} spacing="sm">
                <Card withBorder padding="sm" radius="md">
                  <Text size="xs" c="dimmed" tt="uppercase" fw={700}>
                    Informados
                  </Text>
                  <Text size="xl" fw={700}>
                    {data.totalInput}
                  </Text>
                </Card>

                <Card withBorder padding="sm" radius="md">
                  <Text size="xs" c="dimmed" tt="uppercase" fw={700}>
                    Únicos
                  </Text>
                  <Text size="xl" fw={700} c="blue">
                    {data.uniqueInput}
                  </Text>
                </Card>

                <Card withBorder padding="sm" radius="md">
                  <Text size="xs" c="dimmed" tt="uppercase" fw={700}>
                    No Sistema
                  </Text>
                  <Text size="xl" fw={700} c="teal">
                    {data.foundCount}
                  </Text>
                </Card>

                <Card withBorder padding="sm" radius="md">
                  <Text size="xs" c="dimmed" tt="uppercase" fw={700}>
                    Não Encontrados
                  </Text>
                  <Text size="xl" fw={700} c="red">
                    {data.notFoundCount}
                  </Text>
                </Card>

                <Card withBorder padding="sm" radius="md">
                  <Text size="xs" c="dimmed" tt="uppercase" fw={700}>
                    Excluídos
                  </Text>
                  <Text size="xl" fw={700} c="orange">
                    {data.deletedCount}
                  </Text>
                </Card>

                <Card withBorder padding="sm" radius="md">
                  <Text size="xs" c="dimmed" tt="uppercase" fw={700}>
                    Duplicados na Lista
                  </Text>
                  <Text size="xl" fw={700} c={data.duplicatesInList.length > 0 ? 'yellow' : 'gray'}>
                    {data.duplicatesInList.length}
                  </Text>
                </Card>
              </SimpleGrid>

              {/* Table of Duplicates (if any) */}
              {data.duplicatesInList.length > 0 && (
                <Paper withBorder p="md" radius="md" style={{ borderColor: '#fcc419' }}>
                  <Group justify="space-between" mb="xs">
                    <div>
                      <Group gap="xs">
                        <Title order={4} c="yellow.9">
                          Contratos duplicados na lista
                        </Title>
                        <Badge color="yellow" variant="light">
                          {data.duplicatesInList.length} duplicado(s)
                        </Badge>
                      </Group>
                      <Text size="xs" c="dimmed" mt={4}>
                        Estes contratos foram informados mais de uma vez no campo de texto de entrada:
                      </Text>
                    </div>
                  </Group>

                  <Table striped highlightOnHover withTableBorder withColumnBorders>
                    <Table.Thead>
                      <Table.Tr>
                        <Table.Th style={{ width: '60px' }}>#</Table.Th>
                        <Table.Th>Número do Contrato Duplicado</Table.Th>
                      </Table.Tr>
                    </Table.Thead>
                    <Table.Tbody>
                      {data.duplicatesInList.map((num, idx) => (
                        <Table.Tr key={`dup-${num}-${idx}`}>
                          <Table.Td style={{ color: '#868e96' }}>{idx + 1}</Table.Td>
                          <Table.Td>
                            <Text fw={600}>{num}</Text>
                          </Table.Td>
                        </Table.Tr>
                      ))}
                    </Table.Tbody>
                  </Table>
                </Paper>
              )}

              {/* Main Comparison Table */}
              <Paper withBorder p="md" radius="md">
                <Group justify="space-between" mb="sm">
                  <div>
                    <Title order={4}>Resultado da Conferência</Title>
                    <Text size="xs" c="dimmed">
                      Exibindo cada número único informado e seu respectivo status no sistema.
                    </Text>
                  </div>
                  <Button
                    variant="light"
                    color="teal"
                    size="xs"
                    leftSection={<IconFileSpreadsheet size={16} />}
                    onClick={handleExportXlsx}
                    loading={exporting}
                  >
                    Exportar XLSX
                  </Button>
                </Group>

                <div style={{ overflowX: 'auto' }}>
                  <Table striped highlightOnHover withTableBorder withColumnBorders>
                    <Table.Thead>
                      <Table.Tr>
                        <Table.Th>Número do Contrato</Table.Th>
                        <Table.Th>Pra quem está atribuído</Table.Th>
                        <Table.Th>Valor</Table.Th>
                        <Table.Th>Data</Table.Th>
                        <Table.Th style={{ textAlign: 'center' }}>Está no sistema</Table.Th>
                        <Table.Th style={{ textAlign: 'center' }}>Contrato excluído</Table.Th>
                      </Table.Tr>
                    </Table.Thead>
                    <Table.Tbody>
                      {data.results.map((item, idx) => (
                        <Table.Tr key={`res-${item.contractNumber}-${idx}`}>
                          <Table.Td>
                            <Text fw={600}>{item.contractNumber}</Text>
                          </Table.Td>

                          <Table.Td>
                            {item.existsInSystem ? (
                              <Text size="sm">{item.assignedTo || 'Não atribuído'}</Text>
                            ) : (
                              <Text size="sm" c="dimmed">
                                —
                              </Text>
                            )}
                          </Table.Td>

                          <Table.Td>
                            {item.existsInSystem && item.totalAmount != null ? (
                              <Text size="sm">{formatCurrency(item.totalAmount)}</Text>
                            ) : (
                              <Text size="sm" c="dimmed">
                                —
                              </Text>
                            )}
                          </Table.Td>

                          <Table.Td>
                            {item.existsInSystem && item.saleStartDate ? (
                              <Text size="sm">{formatDate(item.saleStartDate)}</Text>
                            ) : (
                              <Text size="sm" c="dimmed">
                                —
                              </Text>
                            )}
                          </Table.Td>

                          <Table.Td style={{ textAlign: 'center' }}>
                            {item.existsInSystem ? (
                              <Badge color="green" variant="light" leftSection={<IconCheck size={12} />}>
                                Sim
                              </Badge>
                            ) : (
                              <Badge color="red" variant="light" leftSection={<IconX size={12} />}>
                                Não
                              </Badge>
                            )}
                          </Table.Td>

                          <Table.Td style={{ textAlign: 'center' }}>
                            {!item.existsInSystem ? (
                              <Text size="sm" c="dimmed">
                                —
                              </Text>
                            ) : item.isDeleted ? (
                              <Badge color="red" variant="filled">
                                Sim
                              </Badge>
                            ) : (
                              <Badge color="gray" variant="light">
                                Não
                              </Badge>
                            )}
                          </Table.Td>
                        </Table.Tr>
                      ))}
                    </Table.Tbody>
                  </Table>
                </div>
              </Paper>
            </Stack>
          )}
        </Stack>
      </div>
    </Menu>
  )
}

export default ContractComparisonTool
