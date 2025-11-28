import React from "react"
import { render, screen, fireEvent, waitFor } from "@testing-library/react"
import "@testing-library/jest-dom"
import BulkImportModal from "./BulkImportModal"
import { apiService } from "../services/apiService"

jest.mock("../services/apiService")

const mockOnClose = jest.fn()
const mockOnSuccess = jest.fn()

describe("BulkImportModal", () => {
  beforeEach(() => {
    jest.clearAllMocks()
  })

  it("renders upload step initially", () => {
    render(<BulkImportModal onClose={mockOnClose} onSuccess={mockOnSuccess} />)
    expect(screen.getByText(/Arquivo CSV ou XLSX/i)).toBeInTheDocument()
    expect(screen.getByText(/Formato esperado/i)).toBeInTheDocument()
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

    render(<BulkImportModal onClose={mockOnClose} onSuccess={mockOnSuccess} />)

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
      expect(screen.getByText(/Mapeamento de Colunas/i)).toBeInTheDocument()
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

    render(<BulkImportModal onClose={mockOnClose} onSuccess={mockOnSuccess} />)

    const fileInput = screen.getByLabelText(/Arquivo CSV ou XLSX/i) as HTMLInputElement
    const file = new File(["name,email\nJohn,john@test.com"], "test.csv", { type: "text/csv" })

    fireEvent.change(fileInput, { target: { files: [file] } })
    fireEvent.click(screen.getByText(/Próximo/i))

    await waitFor(() => screen.getByText(/Mapeamento de Colunas/i))

    // Confirm import
    const confirmBtn = screen.getByText(/Confirmar e Importar/i)
    fireEvent.click(confirmBtn)

    await waitFor(() => expect(apiService.configureImportMappings).toHaveBeenCalled())
    await waitFor(() => expect(apiService.confirmImport).toHaveBeenCalled())
    await waitFor(() => expect(mockOnSuccess).toHaveBeenCalled())
  })

  it("shows error when upload fails", async () => {
    ;(apiService.uploadImportFile as jest.Mock).mockRejectedValue(
      new Error("Failed to upload file")
    )

    render(<BulkImportModal onClose={mockOnClose} onSuccess={mockOnSuccess} />)

    const fileInput = screen.getByLabelText(/Arquivo CSV ou XLSX/i) as HTMLInputElement
    const file = new File(["test"], "test.csv", { type: "text/csv" })

    fireEvent.change(fileInput, { target: { files: [file] } })
    fireEvent.click(screen.getByText(/Próximo/i))

    await waitFor(() =>
      expect(screen.getByText(/Failed to upload file/i)).toBeInTheDocument()
    )
  })
})

export {}
