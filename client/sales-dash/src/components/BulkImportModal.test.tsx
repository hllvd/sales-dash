import React from "react"
import { render, screen, fireEvent, waitFor } from "@testing-library/react"
import "@testing-library/jest-dom"
import { MantineProvider } from "@mantine/core"
import BulkImportModal from "./BulkImportModal"
import { apiService } from "../services/apiService"

jest.mock("../services/apiService")

const mockOnClose = jest.fn()
const mockOnSuccess = jest.fn()

const renderWithProviders = (ui: React.ReactElement) => {
  return render(<MantineProvider>{ui}</MantineProvider>)
}

describe("BulkImportModal", () => {
  beforeEach(() => {
    jest.clearAllMocks()
    localStorage.setItem("user", JSON.stringify({ role: "superadmin" }))
    ;(apiService.getImportTemplates as jest.Mock).mockResolvedValue({
      success: true,
      data: [
        { id: 1, name: "Users", entityType: "User" },
        { id: 2, name: "Contracts", entityType: "Contract" },
        { id: 3, name: "contractDashboard", entityType: "Contract" },
        { id: 4, name: "consultorDashboard", entityType: "Contract" },
      ],
    })
  })

  it("renders upload step initially and does not render templateSelection dropdown", () => {
    renderWithProviders(<BulkImportModal onClose={mockOnClose} onSuccess={mockOnSuccess} templateId={3} title="Importar Contratos em Lote" />)
    expect(screen.getByText(/Importar Contratos em Lote/i)).toBeInTheDocument()
    expect(screen.getByText(/Arquivo CSV ou XLSX/i)).toBeInTheDocument()
    expect(screen.getByText(/Formato esperado/i)).toBeInTheDocument()
    expect(screen.queryByLabelText(/Selecione o Modelo de Importação/i)).not.toBeInTheDocument()
    expect(screen.queryByRole("combobox")).not.toBeInTheDocument()
  })

  it("uploads file and shows mapping screen with auto-mapped columns", async () => {
    ;(apiService.uploadImportFile as jest.Mock).mockResolvedValue({
      success: true,
      data: {
        uploadId: "upload-123",
        sessionId: "upload-123",
        templateId: 1,
        templateName: "Users",
        entityType: "User",
        fileName: "users.csv",
        detectedColumns: ["name", "email", "role"],
        sampleRows: [
          { name: "John Doe", email: "john@example.com", role: "user" },
          { name: "Jane Smith", email: "jane@example.com", role: "admin" },
        ],
        totalRows: 2,
        suggestedMappings: {
          name: "Name",
          email: "Email",
          role: "Role",
        },
        requiredFields: ["Name", "Email"],
        optionalFields: ["Surname", "Role", "ParentEmail"],
      },
      message: "ok",
    })

    renderWithProviders(<BulkImportModal onClose={mockOnClose} onSuccess={mockOnSuccess} templateId={1} title="Importar Usuários em Lote" />)

    const fileInput = screen.getByLabelText(/Arquivo CSV ou XLSX/i) as HTMLInputElement
    const csv = "name,email,role\nJohn Doe,john@example.com,user\nJane Smith,jane@example.com,admin"
    const file = new File([csv], "users.csv", { type: "text/csv" })

    // Select file
    fireEvent.change(fileInput, { target: { files: [file] } })

    // Click upload button
    const uploadBtn = screen.getByText(/Próximo/i)
    fireEvent.click(uploadBtn)

    // Wait for mapping screen
    await waitFor(() =>
      expect(screen.getByText(/Mapeamentos:/i)).toBeInTheDocument()
    )

    // Check sample data is displayed
    expect(screen.getByText("John Doe")).toBeInTheDocument()
    expect(screen.getByText("john@example.com")).toBeInTheDocument()

    // Check that uploadImportFile was called with correct params
    expect(apiService.uploadImportFile).toHaveBeenCalledWith(file, 1)
  })

  it("completes import workflow successfully", async () => {
    ;(apiService.uploadImportFile as jest.Mock).mockResolvedValue({
      success: true,
      data: {
        uploadId: "upload-123",
        sessionId: "upload-123",
        detectedColumns: ["name", "email"],
        sampleRows: [{ name: "John", email: "john@test.com" }],
        suggestedMappings: { name: "Name", email: "Email" },
        requiredFields: ["Name", "Email"],
        optionalFields: [],
      },
      message: "ok",
    })

    ;(apiService.configureImportMappings as jest.Mock).mockResolvedValue({
      success: true,
      data: {
        uploadId: "upload-123",
        status: "ready",
        totalRows: 1,
        processedRows: 0,
        failedRows: 0,
        errors: [],
      },
      message: "ok",
    })

    ;(apiService.confirmImport as jest.Mock).mockResolvedValue({
      success: true,
      data: {
        uploadId: "upload-123",
        status: "completed",
        totalRows: 1,
        processedRows: 1,
        failedRows: 0,
        errors: [],
      },
      message: "ok",
    })

    renderWithProviders(<BulkImportModal onClose={mockOnClose} onSuccess={mockOnSuccess} templateId={1} title="Importar Usuários em Lote" />)

    const fileInput = screen.getByLabelText(/Arquivo CSV ou XLSX/i) as HTMLInputElement
    const file = new File(["name,email\nJohn,john@test.com"], "test.csv", { type: "text/csv" })

    fireEvent.change(fileInput, { target: { files: [file] } })
    fireEvent.click(screen.getByText(/Próximo/i))

    await waitFor(() => screen.getByText(/Mapeamentos:/i))

    // Confirm import
    const confirmBtn = screen.getByText(/Confirmar e Importar/i)
    fireEvent.click(confirmBtn)

    await waitFor(() => expect(apiService.configureImportMappings).toHaveBeenCalled())
    await waitFor(() => expect(apiService.confirmImport).toHaveBeenCalled())
    const closeBtn = await screen.findByText("Fechar")
    fireEvent.click(closeBtn)
    expect(mockOnSuccess).toHaveBeenCalled()
  })

  it("shows error when upload fails", async () => {
    ;(apiService.uploadImportFile as jest.Mock).mockRejectedValue(
      new Error("Failed to upload file")
    )

    renderWithProviders(<BulkImportModal onClose={mockOnClose} onSuccess={mockOnSuccess} templateId={1} title="Importar Usuários em Lote" />)

    const fileInput = screen.getByLabelText(/Arquivo CSV ou XLSX/i) as HTMLInputElement
    const file = new File(["test"], "test.csv", { type: "text/csv" })

    fireEvent.change(fileInput, { target: { files: [file] } })
    fireEvent.click(screen.getByText(/Próximo/i))

    await waitFor(() =>
      expect(screen.getByText(/Failed to upload file/i)).toBeInTheDocument()
    )
  })

  it("updates template to backend detected template (e.g. consultorDashboard) and shows contract options", async () => {
    ;(apiService.uploadImportFile as jest.Mock).mockResolvedValue({
      success: true,
      data: {
        uploadId: "upload-consultor-999",
        sessionId: "upload-consultor-999",
        templateId: 4,
        templateName: "consultorDashboard",
        entityType: "Contract",
        fileName: "consultor.xlsx",
        detectedColumns: ["Nº Contrato", "Cod.Comissionado", "Data.Venda", "Grupo", "Cota", "Consorciado", "R$ Bem Venda"],
        sampleRows: [
          { "Nº Contrato": "CTR-1", "Cod.Comissionado": "M1", "Data.Venda": "2024-01-01", "Grupo": "100", "Cota": "01", "Consorciado": "Cliente 1", "R$ Bem Venda": "10000" }
        ],
        totalRows: 1,
        suggestedMappings: {
          "Nº Contrato": "ContractNumber",
          "Cod.Comissionado": "MatriculaNumber",
          "Data.Venda": "SaleStartDate",
          "Grupo": "GroupId",
          "Cota": "Quota",
          "Consorciado": "CustomerName",
          "R$ Bem Venda": "TotalAmount",
        },
        requiredFields: ["ContractNumber", "TotalAmount", "SaleStartDate", "GroupId", "Quota", "CustomerName", "MatriculaNumber"],
        optionalFields: ["Status", "PvId", "PvName"],
      },
      message: "ok",
    })

    renderWithProviders(<BulkImportModal onClose={mockOnClose} onSuccess={mockOnSuccess} templateId={3} title="Importar Contratos em Lote" />)

    const fileInput = screen.getByLabelText(/Arquivo CSV ou XLSX/i) as HTMLInputElement
    const file = new File(["test"], "consultor.xlsx", { type: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" })

    fireEvent.change(fileInput, { target: { files: [file] } })
    fireEvent.click(screen.getByText(/Próximo/i))

    await waitFor(() => screen.getByText(/Mapeamentos:/i))

    // Verify contract dashboard options are rendered
    expect(screen.getByText(/Opções de Importação:/i)).toBeInTheDocument()
    expect(screen.getByText(/Pular linhas sem número de contrato/i)).toBeInTheDocument()
    expect(screen.getByText(/Permitir criação automática de grupos/i)).toBeInTheDocument()
    expect(screen.getByText(/Permitir criação automática de PV/i)).toBeInTheDocument()
  })
})

export {}
