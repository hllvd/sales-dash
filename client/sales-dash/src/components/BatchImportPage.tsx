import React, { useState, useRef, useEffect } from 'react'
import {
  Title,
  Text,
  Paper,
  Stack,
  Group,
  Button,
  Checkbox,
  Progress,
  Badge,
  Alert,
  List,
  Accordion,
  Table,
  ActionIcon,
  Box,
  SimpleGrid
} from '@mantine/core'
import {
  IconUpload,
  IconFileSpreadsheet,
  IconTrash,
  IconCheck,
  IconX,
  IconAlertTriangle,
  IconDownload,
  IconRefresh,
  IconPlayerPlay,
  IconLock
} from '@tabler/icons-react'
import Menu from './Menu'
import { apiService } from '../services/apiService'
import { UserRole } from '../types/UserRole'
import { downloadFailedRowsCsv } from '../utils/csvDownloader'
import { getFriendlyFieldName } from '../utils/normalization'

export interface BatchFileResult {
  fileName: string
  success: boolean
  totalRows: number
  processedRows: number
  failedRows: number
  errors: string[]
  warnings: string[]
  desistenteContractNumbers: string[]
  createdGroups: string[]
  createdPVs: string[]
  failedRowsDetails?: Record<string, string>[]
}

export interface BatchFileItem {
  id: string
  file: File
  status: 'idle' | 'uploading' | 'mapping' | 'confirming' | 'success' | 'warning' | 'error'
  statusMessage?: string
  result?: BatchFileResult
}

export const formatFileSize = (bytes: number): string => {
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
}

export const isSupportedFile = (file: File): boolean => {
  const name = file.name.toLowerCase()
  return name.endsWith('.csv') || name.endsWith('.xlsx') || name.endsWith('.xls')
}

const BatchImportPage: React.FC = () => {
  const [isSuperAdmin, setIsSuperAdmin] = useState<boolean | null>(null)
  const [files, setFiles] = useState<BatchFileItem[]>([])
  const [isDragging, setIsDragging] = useState(false)
  const [isProcessing, setIsProcessing] = useState(false)
  const [currentIndex, setCurrentIndex] = useState<number>(-1)
  const [isFinished, setIsFinished] = useState(false)
  const fileInputRef = useRef<HTMLInputElement>(null)

  useEffect(() => {
    let superAdmin = false
    try {
      const userJson = localStorage.getItem('user')
      if (userJson) {
        const u = JSON.parse(userJson)
        if (u.role === UserRole.SUPERADMIN || u.role === 'superadmin') {
          superAdmin = true
        }
      }
      const token = localStorage.getItem('token')
      if (token) {
        const payload = token.split('.')[1]
        const base64 = payload.replace(/-/g, '+').replace(/_/g, '/')
        const jsonPayload = decodeURIComponent(
          atob(base64)
            .split('')
            .map((c) => '%' + ('00' + c.charCodeAt(0).toString(16)).slice(-2))
            .join('')
        )
        const decoded = JSON.parse(jsonPayload)
        if (
          decoded.role === 'superadmin' ||
          decoded.role === UserRole.SUPERADMIN ||
          (decoded.perm &&
            (Array.isArray(decoded.perm)
              ? decoded.perm.includes('system:superadmin')
              : decoded.perm === 'system:superadmin'))
        ) {
          superAdmin = true
        }
      }
    } catch {
      // ignore parsing error
    }
    setIsSuperAdmin(superAdmin)
  }, [])

  // Import options with exact defaults from Contracts
  const [skipMissingContractNumber, setSkipMissingContractNumber] = useState(true)
  const [allowAutoCreateGroups, setAllowAutoCreateGroups] = useState(true)
  const [allowAutoCreatePVs, setAllowAutoCreatePVs] = useState(true)
  const [updateMatriculaOnExisting, setUpdateMatriculaOnExisting] = useState(false)
  const [updateTotalAmountOnExisting, setUpdateTotalAmountOnExisting] = useState(true)
  const [updateStartDateOnExisting, setUpdateStartDateOnExisting] = useState(true)

  const addFiles = (newFiles: FileList | File[]) => {
    if (isProcessing) return
    const validFiles: BatchFileItem[] = []
    const existingKeys = new Set(files.map(f => `${f.file.name}-${f.file.size}-${f.file.lastModified}`))

    Array.from(newFiles).forEach(file => {
      if (isSupportedFile(file)) {
        const key = `${file.name}-${file.size}-${file.lastModified}`
        if (!existingKeys.has(key)) {
          existingKeys.add(key)
          validFiles.push({
            id: `${Date.now()}-${Math.random().toString(36).substring(2, 9)}`,
            file,
            status: 'idle'
          })
        }
      }
    })

    if (validFiles.length > 0) {
      setFiles(prev => [...prev, ...validFiles])
      setIsFinished(false)
    }
  }

  const handleDragOver = (e: React.DragEvent) => {
    e.preventDefault()
    e.stopPropagation()
    if (!isProcessing) setIsDragging(true)
  }

  const handleDragLeave = (e: React.DragEvent) => {
    e.preventDefault()
    e.stopPropagation()
    setIsDragging(false)
  }

  const handleDrop = (e: React.DragEvent) => {
    e.preventDefault()
    e.stopPropagation()
    setIsDragging(false)
    if (!isProcessing && e.dataTransfer.files && e.dataTransfer.files.length > 0) {
      addFiles(e.dataTransfer.files)
    }
  }

  const handleRemoveFile = (id: string) => {
    if (isProcessing) return
    setFiles(prev => prev.filter(f => f.id !== id))
  }

  const handleClearAll = () => {
    if (isProcessing) return
    setFiles([])
    setIsFinished(false)
    setCurrentIndex(-1)
  }

  const processSingleFile = async (
    item: BatchFileItem,
    updateItem: (status: BatchFileItem['status'], message?: string, result?: BatchFileResult) => void
  ): Promise<BatchFileResult> => {
    try {
      // 1. Upload & template auto-detection
      updateItem('uploading', 'Enviando arquivo e detectando cabeçalhos...')
      const uploadResp = await apiService.uploadImportFile(item.file, 3)

      if (!uploadResp.success || !uploadResp.data) {
        const errMsg = uploadResp.message || 'Falha no upload do arquivo'
        const res: BatchFileResult = {
          fileName: item.file.name,
          success: false,
          totalRows: 0,
          processedRows: 0,
          failedRows: 0,
          errors: [errMsg],
          warnings: [],
          desistenteContractNumbers: [],
          createdGroups: [],
          createdPVs: []
        }
        updateItem('error', errMsg, res)
        return res
      }

      const { templateName, suggestedMappings, requiredFields, totalRows } = uploadResp.data
      const uploadId = uploadResp.data.uploadId || uploadResp.data.sessionId || ''

      // Check required fields
      let missingRequired = (requiredFields || []).filter(
        (rf: string) => !Object.values(suggestedMappings || {}).includes(rf)
      )
      if (templateName === 'contractDashboard' || templateName === 'consultorDashboard') {
        missingRequired = missingRequired.filter((rf: string) => rf !== 'TotalAmount')
      }

      if (missingRequired.length > 0) {
        const friendlyMissing = missingRequired.map(getFriendlyFieldName).join(', ')
        const errMsg = `Campos obrigatórios não encontrados no arquivo: ${friendlyMissing}`
        const res: BatchFileResult = {
          fileName: item.file.name,
          success: false,
          totalRows: totalRows || 0,
          processedRows: 0,
          failedRows: totalRows || 0,
          errors: [errMsg],
          warnings: [],
          desistenteContractNumbers: [],
          createdGroups: [],
          createdPVs: []
        }
        updateItem('error', errMsg, res)
        return res
      }

      // 2. Configure mappings
      updateItem('mapping', 'Configurando mapeamentos automáticos...')
      const explicitlyMapped = Object.fromEntries(
        Object.entries(suggestedMappings || {}).filter(([_, targetField]) => targetField !== '')
      )

      const mappingResp = await apiService.configureImportMappings(
        uploadId,
        explicitlyMapped,
        allowAutoCreateGroups,
        allowAutoCreatePVs,
        skipMissingContractNumber,
        templateName,
        updateMatriculaOnExisting,
        updateTotalAmountOnExisting,
        updateStartDateOnExisting
      )

      if (!mappingResp.success) {
        const errMsg = mappingResp.message || 'Falha ao configurar mapeamentos'
        const res: BatchFileResult = {
          fileName: item.file.name,
          success: false,
          totalRows: totalRows || 0,
          processedRows: 0,
          failedRows: totalRows || 0,
          errors: [errMsg],
          warnings: [],
          desistenteContractNumbers: [],
          createdGroups: [],
          createdPVs: []
        }
        updateItem('error', errMsg, res)
        return res
      }

      // 3. Confirm & execute import
      updateItem('confirming', 'Processando e gravando contratos...')
      const confirmResp = await apiService.confirmImport(
        uploadId,
        'MM/DD/YYYY',
        skipMissingContractNumber,
        allowAutoCreateGroups,
        allowAutoCreatePVs,
        templateName,
        updateMatriculaOnExisting,
        updateTotalAmountOnExisting,
        updateStartDateOnExisting,
        (pollData) => {
          updateItem(
            'confirming',
            `Processando no servidor... (${pollData.processedRows || 0} de ${pollData.totalRows || totalRows || 0})`
          )
        }
      )

      if (confirmResp.success && confirmResp.data) {
        const data = confirmResp.data
        const hasErrors = (data.errors && data.errors.length > 0) || (data.failedRows && data.failedRows > 0)
        const hasWarnings = (data.warnings && data.warnings.length > 0) || (data.desistenteContractNumbers && data.desistenteContractNumbers.length > 0)
        const finalStatus = data.processedRows === 0 && hasErrors ? 'error' : hasWarnings || hasErrors ? 'warning' : 'success'

        const res: BatchFileResult = {
          fileName: item.file.name,
          success: data.processedRows > 0,
          totalRows: data.totalRows || totalRows || 0,
          processedRows: data.processedRows || 0,
          failedRows: data.failedRows || 0,
          errors: data.errors || [],
          warnings: data.warnings || [],
          desistenteContractNumbers: data.desistenteContractNumbers || [],
          createdGroups: data.createdGroups || [],
          createdPVs: data.createdPVs || [],
          failedRowsDetails: data.failedRowsDetails
        }

        const statusMsg = `Concluído: ${data.processedRows} processados${data.failedRows > 0 ? `, ${data.failedRows} falhas` : ''}`
        updateItem(finalStatus, statusMsg, res)
        return res
      } else {
        const errMsg = confirmResp.message || 'Falha ao confirmar importação'
        const res: BatchFileResult = {
          fileName: item.file.name,
          success: false,
          totalRows: totalRows || 0,
          processedRows: 0,
          failedRows: totalRows || 0,
          errors: [errMsg],
          warnings: [],
          desistenteContractNumbers: [],
          createdGroups: [],
          createdPVs: []
        }
        updateItem('error', errMsg, res)
        return res
      }
    } catch (err: any) {
      const errMsg = err.message || 'Erro inesperado durante a importação'
      const res: BatchFileResult = {
        fileName: item.file.name,
        success: false,
        totalRows: 0,
        processedRows: 0,
        failedRows: 0,
        errors: [errMsg],
        warnings: [],
        desistenteContractNumbers: [],
        createdGroups: [],
        createdPVs: []
      }
      updateItem('error', errMsg, res)
      return res
    }
  }

  const handleStartBatchImport = async () => {
    if (files.length === 0 || isProcessing) return

    setIsProcessing(true)
    setIsFinished(false)

    for (let i = 0; i < files.length; i++) {
      setCurrentIndex(i)
      const currentItem = files[i]

      await processSingleFile(currentItem, (status, statusMessage, result) => {
        setFiles(prev =>
          prev.map((f, idx) => (idx === i ? { ...f, status, statusMessage, result } : f))
        )
      })
    }

    setIsProcessing(false)
    setIsFinished(true)
    setCurrentIndex(-1)
  }

  // Summary calculations
  const totalFiles = files.length
  const completedFiles = files.filter(f => f.status === 'success' || f.status === 'warning' || f.status === 'error').length
  const successFiles = files.filter(f => f.status === 'success' || (f.status === 'warning' && (f.result?.processedRows ?? 0) > 0)).length
  const errorFiles = files.filter(f => f.status === 'error' || (f.result?.processedRows === 0 && (f.result?.errors?.length ?? 0) > 0)).length
  const totalProcessedRows = files.reduce((acc, f) => acc + (f.result?.processedRows || 0), 0)
  const totalFailedRows = files.reduce((acc, f) => acc + (f.result?.failedRows || 0), 0)
  const progressPercent = totalFiles > 0 ? Math.round((completedFiles / totalFiles) * 100) : 0

  if (isSuperAdmin === false) {
    return (
      <Menu>
        <Box p="md" maw={600} mx="auto" mt={50} data-testid="batch-import-unauthorized">
          <Alert
            icon={<IconLock size={20} />}
            title="Acesso Restrito"
            color="red"
            variant="light"
            radius="md"
          >
            Esta página é restrita apenas a usuários com perfil de Superadmin.
          </Alert>
        </Box>
      </Menu>
    )
  }

  return (
    <Menu>
      <Box
        translate="no"
        pos="relative"
        style={{ maxWidth: '1000px', margin: '0 auto', padding: '24px 20px' }}
      >
        <Group justify="space-between" align="center" mb="lg">
          <div>
            <Title order={2} style={{ color: '#1c1c1e', fontWeight: 700 }}>
              Importação de Arquivos em Lote
            </Title>
            <Text size="sm" c="dimmed" mt={4}>
              Carregue múltiplos arquivos CSV ou Excel. O sistema processará cada um em série com detecção automática de cabeçalho.
            </Text>
          </div>
        </Group>

        {/* 1. Drag and Drop Zone */}
        <Paper
          withBorder
          p="xl"
          radius="md"
          mb="lg"
          style={{
            borderStyle: 'dashed',
            borderWidth: 2,
            borderColor: isDragging ? '#228be6' : '#d1d5db',
            backgroundColor: isDragging ? '#f0f9ff' : '#fafafa',
            cursor: isProcessing ? 'not-allowed' : 'pointer',
            textAlign: 'center',
            transition: 'border-color 0.2s, background-color 0.2s'
          }}
          onDragOver={handleDragOver}
          onDragLeave={handleDragLeave}
          onDrop={handleDrop}
          onClick={() => {
            if (!isProcessing && fileInputRef.current) {
              fileInputRef.current.click()
            }
          }}
          data-testid="batch-dropzone"
        >
          <input
            type="file"
            ref={fileInputRef}
            style={{ display: 'none' }}
            multiple
            accept=".csv,.xlsx,.xls"
            onChange={e => {
              if (e.target.files) addFiles(e.target.files)
            }}
            disabled={isProcessing}
            data-testid="batch-file-input"
          />
          <Stack align="center" gap="xs">
            <IconUpload size={40} color={isDragging ? '#228be6' : '#6b7280'} />
            <Text fw={600} size="md" c="#1f2937">
              Arraste e solte seus arquivos aqui ou clique para selecionar
            </Text>
            <Text size="xs" c="dimmed">
              Suporta múltiplos arquivos CSV ou XLSX/XLS (Dashboard de Contratos e Relatórios do Consultor)
            </Text>
          </Stack>
        </Paper>

        {/* 2. Selected Files Queue */}
        {files.length > 0 && (
          <Paper withBorder p="md" radius="md" mb="lg">
            <Group justify="space-between" align="center" mb="sm">
              <Text fw={600} size="sm" c="#374151">
                Fila de Arquivos ({files.length})
              </Text>
              {!isProcessing && (
                <Button
                  variant="subtle"
                  color="red"
                  size="xs"
                  onClick={handleClearAll}
                  leftSection={<IconTrash size={14} />}
                >
                  Limpar Todos
                </Button>
              )}
            </Group>

            <Table striped highlightOnHover verticalSpacing="xs">
              <Table.Thead>
                <Table.Tr>
                  <Table.Th style={{ width: '40px' }}>#</Table.Th>
                  <Table.Th>Arquivo</Table.Th>
                  <Table.Th style={{ width: '120px' }}>Tamanho</Table.Th>
                  <Table.Th style={{ width: '220px' }}>Status</Table.Th>
                  <Table.Th style={{ width: '60px' }}></Table.Th>
                </Table.Tr>
              </Table.Thead>
              <Table.Tbody>
                {files.map((item, idx) => (
                  <Table.Tr key={item.id} data-testid={`batch-file-row-${idx}`}>
                    <Table.Td>{idx + 1}</Table.Td>
                    <Table.Td>
                      <Group gap="xs" wrap="nowrap">
                        <IconFileSpreadsheet size={18} color="#2563eb" />
                        <Text size="sm" fw={500} truncate="end">
                          {item.file.name}
                        </Text>
                      </Group>
                    </Table.Td>
                    <Table.Td>
                      <Text size="xs" c="dimmed">
                        {formatFileSize(item.file.size)}
                      </Text>
                    </Table.Td>
                    <Table.Td>
                      {item.status === 'idle' && <Badge color="gray" variant="light">Na Fila</Badge>}
                      {item.status === 'uploading' && <Badge color="blue" variant="filled">Enviando...</Badge>}
                      {item.status === 'mapping' && <Badge color="blue" variant="filled">Mapeando...</Badge>}
                      {item.status === 'confirming' && <Badge color="blue" variant="filled">Importando...</Badge>}
                      {item.status === 'success' && <Badge color="green" variant="light" leftSection={<IconCheck size={12} />}>Sucesso</Badge>}
                      {item.status === 'warning' && <Badge color="orange" variant="light" leftSection={<IconAlertTriangle size={12} />}>Com Avisos</Badge>}
                      {item.status === 'error' && <Badge color="red" variant="light" leftSection={<IconX size={12} />}>Erro</Badge>}
                    </Table.Td>
                    <Table.Td>
                      {!isProcessing && (
                        <ActionIcon
                          variant="subtle"
                          color="red"
                          size="sm"
                          onClick={() => handleRemoveFile(item.id)}
                          aria-label={`Remover ${item.file.name}`}
                          data-testid={`batch-remove-file-${idx}`}
                        >
                          <IconTrash size={16} />
                        </ActionIcon>
                      )}
                    </Table.Td>
                  </Table.Tr>
                ))}
              </Table.Tbody>
            </Table>
          </Paper>
        )}

        {/* 3. Progress Bar & Realtime Tracker */}
        {(isProcessing || isFinished) && files.length > 0 && (
          <Paper withBorder p="md" radius="md" mb="lg" style={{ backgroundColor: '#f8fafc' }} data-testid="batch-progress-section">
            <Group justify="space-between" mb="xs">
              <Text fw={600} size="sm" c="#1e293b">
                Progresso Geral da Importação:
              </Text>
              <Text fw={700} size="sm" c="#2563eb">
                {completedFiles} de {totalFiles} arquivos ({progressPercent}%)
              </Text>
            </Group>

            <Progress
              value={progressPercent}
              size="lg"
              radius="xl"
              animated={isProcessing}
              color={isFinished && errorFiles > 0 ? (successFiles > 0 ? 'orange' : 'red') : 'blue'}
              mb="xs"
              data-testid="batch-progress-bar"
            />

            {isProcessing && currentIndex >= 0 && (
              <Text size="xs" c="#64748b" style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
                <span className="btn-spinner" style={{ width: 12, height: 12, borderWidth: 2 }} />
                Processando arquivo {currentIndex + 1} de {totalFiles}: <strong>{files[currentIndex]?.file.name}</strong> — {files[currentIndex]?.statusMessage}
              </Text>
            )}

            {isFinished && (
              <Text size="xs" c={errorFiles === 0 ? '#15803d' : '#b45309'} fw={500}>
                {errorFiles === 0
                  ? 'Todos os arquivos foram processados com sucesso!'
                  : `Processamento do lote finalizado: ${successFiles} com sucesso, ${errorFiles} com erro.`}
              </Text>
            )}
          </Paper>
        )}

        {/* 4. Import Options Card (Matching BulkImportModal) */}
        <Paper withBorder p="md" radius="md" mb="lg" style={{ backgroundColor: '#f9fafb' }}>
          <Title order={4} size="sm" c="#374151" mb="xs">
            Opções de Importação (Aplicadas a todos os arquivos):
          </Title>

          <Stack gap="xs">
            <Checkbox
              id="batch-skipMissingContractNumber"
              data-testid="batch-opt-skip-missing"
              label="Pular linhas sem número de contrato (útil para arquivos com subtotais ou lixo)"
              checked={skipMissingContractNumber}
              onChange={e => setSkipMissingContractNumber(e.currentTarget.checked)}
              disabled={isProcessing}
            />

            <Checkbox
              id="batch-allowAutoCreateGroups"
              data-testid="batch-opt-auto-groups"
              label="Permitir criação automática de grupos"
              checked={allowAutoCreateGroups}
              onChange={e => setAllowAutoCreateGroups(e.currentTarget.checked)}
              disabled={isProcessing}
            />

            <Checkbox
              id="batch-allowAutoCreatePVs"
              data-testid="batch-opt-auto-pvs"
              label="Permitir criação automática de PV"
              checked={allowAutoCreatePVs}
              onChange={e => setAllowAutoCreatePVs(e.currentTarget.checked)}
              disabled={isProcessing}
            />

            <Checkbox
              id="batch-updateMatriculaOnExisting"
              data-testid="batch-opt-update-matricula"
              label="Atualizar matrícula em contratos existentes"
              checked={updateMatriculaOnExisting}
              onChange={e => setUpdateMatriculaOnExisting(e.currentTarget.checked)}
              disabled={isProcessing}
            />

            <Checkbox
              id="batch-updateTotalAmountOnExisting"
              data-testid="batch-opt-update-total"
              label="Atualizar valor total em contratos existentes"
              checked={updateTotalAmountOnExisting}
              onChange={e => setUpdateTotalAmountOnExisting(e.currentTarget.checked)}
              disabled={isProcessing}
            />

            <Checkbox
              id="batch-updateStartDateOnExisting"
              data-testid="batch-opt-update-start-date"
              label="Atualizar data do contrato"
              checked={updateStartDateOnExisting}
              onChange={e => setUpdateStartDateOnExisting(e.currentTarget.checked)}
              disabled={isProcessing}
            />
          </Stack>
        </Paper>

        {/* 5. Action Button */}
        <Group justify="flex-end" mb="xl">
          {isFinished ? (
            <Button
              variant="outline"
              color="blue"
              onClick={handleClearAll}
              leftSection={<IconRefresh size={18} />}
              data-testid="batch-reset-btn"
            >
              Importar Novo Lote
            </Button>
          ) : (
            <Button
              size="md"
              color="blue"
              disabled={files.length === 0 || isProcessing}
              loading={isProcessing}
              onClick={handleStartBatchImport}
              leftSection={<IconPlayerPlay size={18} />}
              data-testid="batch-start-btn"
            >
              {isProcessing ? 'Importando Lote em Série...' : `Iniciar Importação em Lote (${files.length})`}
            </Button>
          )}
        </Group>

        {/* 6. Results Summary (Rendered after processing) */}
        {isFinished && (
          <Stack gap="lg" data-testid="batch-results-summary">
            <Title order={3} size="h4" c="#1c1c1e">
              Resumo da Importação em Lote
            </Title>

            <SimpleGrid cols={{ base: 2, sm: 4 }} spacing="md">
              <Paper withBorder p="md" radius="md" style={{ textAlign: 'center' }}>
                <Text size="xs" c="dimmed" fw={600}>TOTAL ARQUIVOS</Text>
                <Text size="xl" fw={700} c="#1e293b" data-testid="metric-total-files">{totalFiles}</Text>
              </Paper>

              <Paper withBorder p="md" radius="md" style={{ textAlign: 'center', backgroundColor: '#f0fdf4' }}>
                <Text size="xs" c="#166534" fw={600}>SUCESSO</Text>
                <Text size="xl" fw={700} c="#15803d" data-testid="metric-success-files">{successFiles}</Text>
              </Paper>

              <Paper withBorder p="md" radius="md" style={{ textAlign: 'center', backgroundColor: errorFiles > 0 ? '#fef2f2' : '#fafafa' }}>
                <Text size="xs" c={errorFiles > 0 ? '#991b1b' : 'dimmed'} fw={600}>FALHAS</Text>
                <Text size="xl" fw={700} c={errorFiles > 0 ? '#dc2626' : '#6b7280'} data-testid="metric-error-files">{errorFiles}</Text>
              </Paper>

              <Paper withBorder p="md" radius="md" style={{ textAlign: 'center' }}>
                <Text size="xs" c="dimmed" fw={600}>CONTRATOS IMPORTADOS</Text>
                <Text size="xl" fw={700} c="#2563eb" data-testid="metric-total-processed">{totalProcessedRows}</Text>
                {totalFailedRows > 0 && (
                  <Text size="xs" c="red" data-testid="metric-total-failed">({totalFailedRows} linhas com erro)</Text>
                )}
              </Paper>
            </SimpleGrid>

            {/* Per-File Breakdown Accordion */}
            <Accordion variant="separated" radius="md">
              {files.map((item, idx) => {
                const res = item.result

                return (
                  <Accordion.Item key={item.id} value={item.id} data-testid={`batch-result-item-${idx}`}>
                    <Accordion.Control>
                      <Group justify="space-between" wrap="nowrap" pr="xs">
                        <Group gap="xs" wrap="nowrap">
                          <IconFileSpreadsheet size={18} color="#2563eb" />
                          <Text fw={600} size="sm">
                            {item.file.name}
                          </Text>
                        </Group>

                        <Group gap="sm" wrap="nowrap">
                          {res && (
                            <Text size="xs" c="dimmed">
                              {res.processedRows} importado(s){res.failedRows > 0 ? `, ${res.failedRows} erro(s)` : ''}
                            </Text>
                          )}
                          {item.status === 'success' && <Badge color="green">Sucesso</Badge>}
                          {item.status === 'warning' && <Badge color="orange">Avisos</Badge>}
                          {item.status === 'error' && <Badge color="red">Erro</Badge>}
                        </Group>
                      </Group>
                    </Accordion.Control>

                    <Accordion.Panel>
                      <Stack gap="sm">
                        {/* Errors */}
                        {res && res.errors.length > 0 && (
                          <Alert color="red" title="Erros na Importação" icon={<IconX size={16} />}>
                            <List size="sm" withPadding spacing="xs">
                              {res.errors.map((err, errIdx) => (
                                <List.Item key={errIdx}>{err}</List.Item>
                              ))}
                            </List>
                          </Alert>
                        )}

                        {/* Warnings */}
                        {res && res.warnings.length > 0 && (
                          <Alert color="orange" title="Avisos do Processamento" icon={<IconAlertTriangle size={16} />}>
                            <List size="sm" withPadding spacing="xs">
                              {res.warnings.map((warn, warnIdx) => (
                                <List.Item key={warnIdx}>{warn}</List.Item>
                              ))}
                            </List>
                          </Alert>
                        )}

                        {/* Desistentes */}
                        {res && res.desistenteContractNumbers.length > 0 && (
                          <Alert color="orange" title="Contratos Desistentes Ignorados" icon={<IconAlertTriangle size={16} />}>
                            <Text size="xs" mb="xs">
                              {res.desistenteContractNumbers.length} contrato(s) ignorados por cancelamento anterior à assembleia:
                            </Text>
                            <List size="xs" withPadding spacing="xs">
                              {res.desistenteContractNumbers.slice(0, 5).map(cn => (
                                <List.Item key={cn}><strong>{cn}</strong></List.Item>
                              ))}
                              {res.desistenteContractNumbers.length > 5 && (
                                <List.Item>... e mais {res.desistenteContractNumbers.length - 5} contratos.</List.Item>
                              )}
                            </List>
                          </Alert>
                        )}

                        {/* Created Groups */}
                        {res && res.createdGroups.length > 0 && (
                          <Alert color="green" title="Grupos Criados Automaticamente" icon={<IconCheck size={16} />}>
                            <Text size="xs">{res.createdGroups.join(', ')}</Text>
                          </Alert>
                        )}

                        {/* Created PVs */}
                        {res && res.createdPVs.length > 0 && (
                          <Alert color="green" title="Pontos de Venda Criados Automaticamente" icon={<IconCheck size={16} />}>
                            <Text size="xs">{res.createdPVs.join(', ')}</Text>
                          </Alert>
                        )}

                        {/* Failed Rows Download */}
                        {res && res.failedRowsDetails && res.failedRowsDetails.length > 0 && (
                          <Group justify="flex-start" mt="xs">
                            <Button
                              size="xs"
                              variant="light"
                              color="red"
                              leftSection={<IconDownload size={14} />}
                              onClick={() => downloadFailedRowsCsv(res.failedRowsDetails!, `erros_${item.file.name.replace(/\.[^.]+$/, '')}`)}
                            >
                              Baixar Linhas com Erro (CSV)
                            </Button>
                          </Group>
                        )}
                      </Stack>
                    </Accordion.Panel>
                  </Accordion.Item>
                )
              })}
            </Accordion>
          </Stack>
        )}
      </Box>
    </Menu>
  )
}

export default BatchImportPage
