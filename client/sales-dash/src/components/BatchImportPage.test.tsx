import React from 'react'
import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import '@testing-library/jest-dom'
import { MantineProvider } from '@mantine/core'
import BatchImportPage, { formatFileSize, isSupportedFile } from './BatchImportPage'
import { apiService } from '../services/apiService'

jest.mock('../services/apiService')

// Mock Menu component to simplify unit testing
jest.mock('./Menu', () => {
  return function MockMenu({ children }: { children?: React.ReactNode }) {
    return <div data-testid="mock-menu">{children}</div>
  }
})

// Mock CSV downloader
jest.mock('../utils/csvDownloader', () => ({
  downloadFailedRowsCsv: jest.fn()
}))

const renderWithProviders = (ui: React.ReactElement) => {
  return render(<MantineProvider>{ui}</MantineProvider>)
}

describe('BatchImportPage Utilities', () => {
  it('formats file sizes accurately', () => {
    expect(formatFileSize(500)).toBe('500 B')
    expect(formatFileSize(2048)).toBe('2.0 KB')
    expect(formatFileSize(1048576 * 3.5)).toBe('3.5 MB')
  })

  it('validates supported file extensions (.csv, .xlsx, .xls)', () => {
    const csvFile = new File(['content'], 'contracts.csv', { type: 'text/csv' })
    const xlsxFile = new File(['content'], 'contracts.xlsx', { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' })
    const xlsFile = new File(['content'], 'contracts.xls', { type: 'application/vnd.ms-excel' })
    const pdfFile = new File(['content'], 'doc.pdf', { type: 'application/pdf' })

    expect(isSupportedFile(csvFile)).toBe(true)
    expect(isSupportedFile(xlsxFile)).toBe(true)
    expect(isSupportedFile(xlsFile)).toBe(true)
    expect(isSupportedFile(pdfFile)).toBe(false)
  })
})

describe('BatchImportPage Authorization Guard', () => {
  beforeEach(() => {
    jest.clearAllMocks()
    localStorage.clear()
  })

  it('renders restricted access alert when user is not superadmin', () => {
    localStorage.setItem('user', JSON.stringify({ role: 'admin' }))
    renderWithProviders(<BatchImportPage />)

    expect(screen.getByTestId('batch-import-unauthorized')).toBeInTheDocument()
    expect(screen.getByText(/Acesso Restrito/i)).toBeInTheDocument()
    expect(screen.getByText(/Esta página é restrita apenas a usuários com perfil de Superadmin/i)).toBeInTheDocument()
  })

  it('renders batch import page when user is superadmin', () => {
    localStorage.setItem('user', JSON.stringify({ role: 'superadmin' }))
    renderWithProviders(<BatchImportPage />)

    expect(screen.queryByTestId('batch-import-unauthorized')).not.toBeInTheDocument()
    expect(screen.getByText(/Importação de Arquivos em Lote/i)).toBeInTheDocument()
    expect(screen.getByTestId('batch-dropzone')).toBeInTheDocument()
  })
})

describe('BatchImportPage Workflow', () => {
  beforeEach(() => {
    jest.clearAllMocks()
    localStorage.clear()
    localStorage.setItem('user', JSON.stringify({ role: 'superadmin' }))
  })

  it('renders default options matching Contracts BulkImportModal', () => {
    renderWithProviders(<BatchImportPage />)

    const skipMissing = screen.getByTestId('batch-opt-skip-missing') as HTMLInputElement
    const autoGroups = screen.getByTestId('batch-opt-auto-groups') as HTMLInputElement
    const autoPVs = screen.getByTestId('batch-opt-auto-pvs') as HTMLInputElement
    const updateMatricula = screen.getByTestId('batch-opt-update-matricula') as HTMLInputElement
    const updateTotalAmount = screen.getByTestId('batch-opt-update-total') as HTMLInputElement
    const updateStartDate = screen.getByTestId('batch-opt-update-start-date') as HTMLInputElement

    expect(skipMissing.checked).toBe(true)
    expect(autoGroups.checked).toBe(true)
    expect(autoPVs.checked).toBe(true)
    expect(updateMatricula.checked).toBe(false)
    expect(updateTotalAmount.checked).toBe(true)
    expect(updateStartDate.checked).toBe(true)
  })

  it('adds files to queue, allows removing individual files or clearing all', async () => {
    renderWithProviders(<BatchImportPage />)

    const fileInput = screen.getByTestId('batch-file-input')
    const file1 = new File(['file1 content'], 'planilha1.csv', { type: 'text/csv' })
    const file2 = new File(['file2 content'], 'planilha2.xlsx', { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' })

    fireEvent.change(fileInput, { target: { files: [file1, file2] } })

    expect(screen.getByText(/Fila de Arquivos \(2\)/i)).toBeInTheDocument()
    expect(screen.getByText('planilha1.csv')).toBeInTheDocument()
    expect(screen.getByText('planilha2.xlsx')).toBeInTheDocument()

    // Remove first file
    const removeBtn0 = screen.getByTestId('batch-remove-file-0')
    fireEvent.click(removeBtn0)

    expect(screen.getByText(/Fila de Arquivos \(1\)/i)).toBeInTheDocument()
    expect(screen.queryByText('planilha1.csv')).not.toBeInTheDocument()
    expect(screen.getByText('planilha2.xlsx')).toBeInTheDocument()

    // Clear all
    const clearButton = screen.getByText(/Limpar Todos/i)
    fireEvent.click(clearButton)

    expect(screen.queryByText(/Fila de Arquivos/i)).not.toBeInTheDocument()
  })

  it('executes serial import across multiple files and displays summary and accordion', async () => {
    const file1 = new File(['cont1'], 'contracts1.csv', { type: 'text/csv' })
    const file2 = new File(['cont2'], 'consultor.xlsx', { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' })

    // Mock API upload responses
    ;(apiService.uploadImportFile as jest.Mock)
      .mockResolvedValueOnce({
        success: true,
        data: {
          sessionId: 'session-1',
          templateId: 3,
          templateName: 'contractDashboard',
          detectedColumns: ['Contrato', 'Matricula', 'Valor']
        }
      })
      .mockResolvedValueOnce({
        success: true,
        data: {
          sessionId: 'session-2',
          templateId: 4,
          templateName: 'consultorDashboard',
          detectedColumns: ['Nº Contrato', 'Cod.Comissionado', 'Total Bem']
        }
      })

    // Mock mappings
    ;(apiService.configureImportMappings as jest.Mock).mockResolvedValue({
      success: true,
      data: { sessionId: 'configured' }
    })

    // Mock confirm
    ;(apiService.confirmImport as jest.Mock)
      .mockResolvedValueOnce({
        success: true,
        data: {
          totalRows: 10,
          processedRows: 10,
          failedRows: 0,
          createdGroups: ['Grupo Alpha'],
          createdPVs: ['PV Central'],
          errors: [],
          warnings: []
        }
      })
      .mockResolvedValueOnce({
        success: true,
        data: {
          totalRows: 5,
          processedRows: 4,
          failedRows: 1,
          createdGroups: [],
          createdPVs: [],
          desistenteContractNumbers: ['CTR-999'],
          errors: ['Linha 5 com dados inconsistentes'],
          warnings: ['Aviso na linha 2'],
          failedRowsDetails: [{ row: '5', reason: 'Erro de validação' }]
        }
      })

    renderWithProviders(<BatchImportPage />)

    const fileInput = screen.getByTestId('batch-file-input')
    fireEvent.change(fileInput, { target: { files: [file1, file2] } })

    const startButton = screen.getByTestId('batch-start-btn')
    fireEvent.click(startButton)

    // Wait for completion
    await waitFor(() => {
      expect(screen.getByTestId('batch-results-summary')).toBeInTheDocument()
    })

    expect(screen.getByText(/Resumo da Importação em Lote/i)).toBeInTheDocument()
    expect(screen.getByTestId('metric-total-processed')).toHaveTextContent('14')
    expect(screen.getByTestId('metric-total-failed')).toHaveTextContent('1')

    // Per-file breakdown
    expect(screen.getByTestId('batch-result-item-0')).toBeInTheDocument()
    expect(screen.getByTestId('batch-result-item-1')).toBeInTheDocument()

    // Check that serial calls occurred in order
    expect(apiService.uploadImportFile).toHaveBeenCalledTimes(2)
    expect(apiService.confirmImport).toHaveBeenCalledTimes(2)
  })

  it('continues serial processing if one file encounters an error', async () => {
    const file1 = new File(['broken'], 'bad_file.csv', { type: 'text/csv' })
    const file2 = new File(['valid'], 'good_file.csv', { type: 'text/csv' })

    // File 1 fails at upload
    ;(apiService.uploadImportFile as jest.Mock)
      .mockRejectedValueOnce(new Error('Formato corrompido ou inválido'))
      .mockResolvedValueOnce({
        success: true,
        data: {
          sessionId: 'session-good',
          templateId: 3,
          detectedColumns: ['Contrato']
        }
      })

    ;(apiService.configureImportMappings as jest.Mock).mockResolvedValue({
      success: true,
      data: { sessionId: 'configured' }
    })

    ;(apiService.confirmImport as jest.Mock).mockResolvedValue({
      success: true,
      data: {
        totalRows: 8,
        processedRows: 8,
        failedRows: 0,
        errors: [],
        warnings: []
      }
    })

    renderWithProviders(<BatchImportPage />)

    const fileInput = screen.getByTestId('batch-file-input')
    fireEvent.change(fileInput, { target: { files: [file1, file2] } })

    fireEvent.click(screen.getByTestId('batch-start-btn'))

    await waitFor(() => {
      expect(screen.getByTestId('batch-results-summary')).toBeInTheDocument()
    })

    // File 1 shows error, File 2 shows success
    expect(screen.getAllByText('bad_file.csv').length).toBeGreaterThanOrEqual(1)
    expect(screen.getAllByText('good_file.csv').length).toBeGreaterThanOrEqual(1)
    expect(screen.getByTestId('metric-success-files')).toHaveTextContent('1')
    expect(screen.getByTestId('metric-error-files')).toHaveTextContent('1')
  })

  it('handles background processing and polling progress updates during confirmation', async () => {
    const file = new File(['cont'], 'huge_file.csv', { type: 'text/csv' })

    ;(apiService.uploadImportFile as jest.Mock).mockResolvedValue({
      success: true,
      data: {
        sessionId: 'session-huge',
        templateId: 3,
        templateName: 'contractDashboard',
        totalRows: 10000,
        detectedColumns: ['Contrato', 'Matricula']
      }
    })

    ;(apiService.configureImportMappings as jest.Mock).mockResolvedValue({
      success: true,
      data: { sessionId: 'configured' }
    })

    ;(apiService.confirmImport as jest.Mock).mockImplementation(
      async (
        _uploadId,
        _df,
        _skip,
        _groups,
        _pvs,
        _tpl,
        _mat,
        _amt,
        _dt,
        onPollProgress?: (status: any) => void
      ) => {
        if (onPollProgress) {
          onPollProgress({
            uploadId: 'session-huge',
            status: 'processing',
            totalRows: 10000,
            processedRows: 5000,
            failedRows: 0
          })
        }
        return {
          success: true,
          data: {
            totalRows: 10000,
            processedRows: 10000,
            failedRows: 0,
            errors: [],
            warnings: []
          }
        }
      }
    )

    renderWithProviders(<BatchImportPage />)

    const fileInput = screen.getByTestId('batch-file-input')
    fireEvent.change(fileInput, { target: { files: [file] } })

    fireEvent.click(screen.getByTestId('batch-start-btn'))

    await waitFor(() => {
      expect(screen.getByTestId('batch-results-summary')).toBeInTheDocument()
    })

    expect(screen.getByTestId('metric-success-files')).toHaveTextContent('1')
    expect(screen.getByTestId('metric-total-processed')).toHaveTextContent('10000')
  })
})
