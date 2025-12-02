import React from "react"
import { render, screen, fireEvent, waitFor } from "@testing-library/react"
import "@testing-library/jest-dom"
import UsersMappingPage from "./UsersMappingPage"
import { apiService } from "../services/apiService"

// Mock the Menu component
jest.mock("./Menu", () => {
  return function Menu() {
    return <div data-testid="menu">Menu</div>
  }
})

jest.mock("../services/apiService")

// Helper function to get file input
const getFileInput = () => {
  return screen.getByRole('textbox', { hidden: true }) || 
         document.querySelector('input[type="file"]') as HTMLInputElement
}

describe("UsersMappingPage", () => {
  beforeEach(() => {
    jest.clearAllMocks()
  })

  it("renders the page with upload section", () => {
    render(<UsersMappingPage />)
    expect(screen.getByText(/Mapeamento de Usuários/i)).toBeInTheDocument()
    expect(screen.getByText(/Processar Arquivo/i)).toBeInTheDocument()
  })

  it("enables process button when file is selected", () => {
    render(<UsersMappingPage />)
    
    const fileInput = document.querySelector('input[type="file"]') as HTMLInputElement
    const processButton = screen.getByText(/Processar Arquivo/i) as HTMLButtonElement
    
    expect(processButton).toBeDisabled()
    
    const file = new File(["matricula,name\n123,user1"], "test.csv", { type: "text/csv" })
    fireEvent.change(fileInput, { target: { files: [file] } })
    
    expect(processButton).not.toBeDisabled()
  })

  it("shows error when CSV has no matricula column", async () => {
    render(<UsersMappingPage />)
    
    const fileInput = document.querySelector('input[type="file"]') as HTMLInputElement
    const file = new File(["name,email\nuser1,user1@test.com"], "test.csv", { type: "text/csv" })
    
    fireEvent.change(fileInput, { target: { files: [file] } })
    fireEvent.click(screen.getByText(/Processar Arquivo/i))
    
    await waitFor(() => {
      expect(screen.getByText(/Coluna "matricula" não encontrada/i)).toBeInTheDocument()
    })
  })

  it("processes CSV and shows preview with matched users", async () => {
    (apiService.getUsersByMatricula as jest.Mock).mockResolvedValue({
      success: true,
      data: [
        { id: "1", name: "user1", email: "user1@test.com", matricula: "123" }
      ]
    })

    render(<UsersMappingPage />)
    
    const fileInput = document.querySelector('input[type="file"]') as HTMLInputElement
    const csv = "matricula,name\n123,user1"
    const file = new File([csv], "test.csv", { type: "text/csv" })
    
    fireEvent.change(fileInput, { target: { files: [file] } })
    fireEvent.click(screen.getByText(/Processar Arquivo/i))
    
    await waitFor(() => {
      expect(screen.getByText(/Revisão dos Dados Processados/i)).toBeInTheDocument()
    })
    
    expect(screen.getByText("user1")).toBeInTheDocument()
    expect(screen.getByText("user1@test.com")).toBeInTheDocument()
    expect(screen.getByText(/✓ Encontrado/i)).toBeInTheDocument()
  })

  it("filters users by name (case-insensitive)", async () => {
    (apiService.getUsersByMatricula as jest.Mock).mockResolvedValue({
      success: true,
      data: [
        { id: "1", name: "user1", email: "user1@test.com", matricula: "123" },
        { id: "2", name: "user2", email: "user2@test.com", matricula: "123" }
      ]
    })

    render(<UsersMappingPage />)
    
    const fileInput = document.querySelector('input[type="file"]') as HTMLInputElement
    const csv = "matricula,name\n123,USER1"
    const file = new File([csv], "test.csv", { type: "text/csv" })
    
    fireEvent.change(fileInput, { target: { files: [file] } })
    fireEvent.click(screen.getByText(/Processar Arquivo/i))
    
    await waitFor(() => {
      expect(screen.getByText(/Revisão dos Dados Processados/i)).toBeInTheDocument()
    })
    
    // Should match user1 (case-insensitive)
    expect(screen.getByText("user1@test.com")).toBeInTheDocument()
  })

  it("detects and highlights duplicate users", async () => {
    (apiService.getUsersByMatricula as jest.Mock).mockResolvedValue({
      success: true,
      data: [
        { id: "1", name: "user1", email: "user1@test.com", matricula: "123" },
        { id: "2", name: "user1", email: "user1-dup@test.com", matricula: "123" }
      ]
    })

    render(<UsersMappingPage />)
    
    const fileInput = document.querySelector('input[type="file"]') as HTMLInputElement
    const csv = "matricula,name\n123,user1"
    const file = new File([csv], "test.csv", { type: "text/csv" })
    
    fireEvent.change(fileInput, { target: { files: [file] } })
    fireEvent.click(screen.getByText(/Processar Arquivo/i))
    
    await waitFor(() => {
      expect(screen.getByText(/2 duplicados/i)).toBeInTheDocument()
    })
    
    expect(screen.getByText(/Duplicados: 1/i)).toBeInTheDocument()
  })

  it("shows not found status when user doesn't exist", async () => {
    (apiService.getUsersByMatricula as jest.Mock).mockResolvedValue({
      success: true,
      data: []
    })

    render(<UsersMappingPage />)
    
    const fileInput = document.querySelector('input[type="file"]') as HTMLInputElement
    const csv = "matricula,name\n123,nonexistent"
    const file = new File([csv], "test.csv", { type: "text/csv" })
    
    fireEvent.change(fileInput, { target: { files: [file] } })
    fireEvent.click(screen.getByText(/Processar Arquivo/i))
    
    await waitFor(() => {
      expect(screen.getByText(/✗ Não encontrado/i)).toBeInTheDocument()
    })
    
    expect(screen.getByText(/Não encontrados: 1/i)).toBeInTheDocument()
  })

  it("shows not found when name doesn't match", async () => {
    (apiService.getUsersByMatricula as jest.Mock).mockResolvedValue({
      success: true,
      data: [
        { id: "1", name: "user1", email: "user1@test.com", matricula: "123" }
      ]
    })

    render(<UsersMappingPage />)
    
    const fileInput = document.querySelector('input[type="file"]') as HTMLInputElement
    const csv = "matricula,name\n123,user2"
    const file = new File([csv], "test.csv", { type: "text/csv" })
    
    fireEvent.change(fileInput, { target: { files: [file] } })
    fireEvent.click(screen.getByText(/Processar Arquivo/i))
    
    await waitFor(() => {
      expect(screen.getByText(/✗ Não encontrado/i)).toBeInTheDocument()
    })
  })

  it("preserves original name from CSV", async () => {
    (apiService.getUsersByMatricula as jest.Mock).mockResolvedValue({
      success: true,
      data: [
        { id: "1", name: "user1", email: "user1@test.com", matricula: "123" }
      ]
    })

    render(<UsersMappingPage />)
    
    const fileInput = document.querySelector('input[type="file"]') as HTMLInputElement
    const csv = "matricula,name\n123,user1"
    const file = new File([csv], "test.csv", { type: "text/csv" })
    
    fireEvent.change(fileInput, { target: { files: [file] } })
    fireEvent.click(screen.getByText(/Processar Arquivo/i))
    
    await waitFor(() => {
      expect(screen.getByText(/Revisão dos Dados Processados/i)).toBeInTheDocument()
    })
    
    // Original name should be preserved
    const nameCell = screen.getAllByText("user1")[0]
    expect(nameCell).toBeInTheDocument()
  })

  it("allows downloading CSV after processing", async () => {
    (apiService.getUsersByMatricula as jest.Mock).mockResolvedValue({
      success: true,
      data: [
        { id: "1", name: "user1", email: "user1@test.com", matricula: "123" }
      ]
    })

    // Mock URL.createObjectURL
    global.URL.createObjectURL = jest.fn(() => "blob:mock-url")
    global.URL.revokeObjectURL = jest.fn()

    render(<UsersMappingPage />)
    
    const fileInput = document.querySelector('input[type="file"]') as HTMLInputElement
    const csv = "matricula,name\n123,user1"
    const file = new File([csv], "test.csv", { type: "text/csv" })
    
    fireEvent.change(fileInput, { target: { files: [file] } })
    fireEvent.click(screen.getByText(/Processar Arquivo/i))
    
    await waitFor(() => {
      expect(screen.getByText(/Confirmar e Baixar CSV/i)).toBeInTheDocument()
    })
    
    const downloadButton = screen.getByText(/Confirmar e Baixar CSV/i)
    fireEvent.click(downloadButton)
    
    expect(global.URL.createObjectURL).toHaveBeenCalled()
  })

  it("allows processing a new file after completion", async () => {
    (apiService.getUsersByMatricula as jest.Mock).mockResolvedValue({
      success: true,
      data: [
        { id: "1", name: "user1", email: "user1@test.com", matricula: "123" }
      ]
    })

    render(<UsersMappingPage />)
    
    const fileInput = document.querySelector('input[type="file"]') as HTMLInputElement
    const csv = "matricula,name\n123,user1"
    const file = new File([csv], "test.csv", { type: "text/csv" })
    
    fireEvent.change(fileInput, { target: { files: [file] } })
    fireEvent.click(screen.getByText(/Processar Arquivo/i))
    
    await waitFor(() => {
      expect(screen.getByText(/Processar Novamente/i)).toBeInTheDocument()
    })
    
    const resetButton = screen.getByText(/Processar Novamente/i)
    fireEvent.click(resetButton)
    
    // Should show upload section again
    expect(screen.getByText(/Processar Arquivo/i)).toBeInTheDocument()
  })

  it("handles API errors gracefully", async () => {
    (apiService.getUsersByMatricula as jest.Mock).mockRejectedValue(
      new Error("API Error")
    )

    render(<UsersMappingPage />)
    
    const fileInput = document.querySelector('input[type="file"]') as HTMLInputElement
    const csv = "matricula,name\n123,user1"
    const file = new File([csv], "test.csv", { type: "text/csv" })
    
    fireEvent.change(fileInput, { target: { files: [file] } })
    fireEvent.click(screen.getByText(/Processar Arquivo/i))
    
    await waitFor(() => {
      expect(screen.getByText(/Revisão dos Dados Processados/i)).toBeInTheDocument()
    })
    
    // Should show not found since API failed
    expect(screen.getByText(/✗ Não encontrado/i)).toBeInTheDocument()
  })

  it("processes multiple rows correctly", async () => {
    (apiService.getUsersByMatricula as jest.Mock)
      .mockResolvedValueOnce({
        success: true,
        data: [{ id: "1", name: "user1", email: "user1@test.com", matricula: "123" }]
      })
      .mockResolvedValueOnce({
        success: true,
        data: [{ id: "2", name: "user2", email: "user2@test.com", matricula: "456" }]
      })

    render(<UsersMappingPage />)
    
    const fileInput = document.querySelector('input[type="file"]') as HTMLInputElement
    const csv = "matricula,name\n123,user1\n456,user2"
    const file = new File([csv], "test.csv", { type: "text/csv" })
    
    fireEvent.change(fileInput, { target: { files: [file] } })
    fireEvent.click(screen.getByText(/Processar Arquivo/i))
    
    await waitFor(() => {
      expect(screen.getByText(/Total de linhas: 2/i)).toBeInTheDocument()
    })
    
    expect(screen.getByText("user1@test.com")).toBeInTheDocument()
    expect(screen.getByText("user2@test.com")).toBeInTheDocument()
  })

  it("allows selecting between duplicate users", async () => {
    (apiService.getUsersByMatricula as jest.Mock).mockResolvedValue({
      success: true,
      data: [
        { id: "1", name: "user1", email: "user1-v1@test.com", matricula: "123" },
        { id: "2", name: "user1", email: "user1-v2@test.com", matricula: "123" }
      ]
    })

    render(<UsersMappingPage />)
    
    const fileInput = document.querySelector('input[type="file"]') as HTMLInputElement
    const csv = "matricula,name\n123,user1"
    const file = new File([csv], "test.csv", { type: "text/csv" })
    
    fireEvent.change(fileInput, { target: { files: [file] } })
    fireEvent.click(screen.getByText(/Processar Arquivo/i))
    
    await waitFor(() => {
      expect(screen.getByText(/2 duplicados/i)).toBeInTheDocument()
    })
    
    // Should show dropdown with both options
    const dropdown = screen.getByRole('combobox') as HTMLSelectElement
    expect(dropdown).toBeInTheDocument()
    expect(dropdown.options.length).toBe(2)
    expect(dropdown.options[0].text).toContain("user1-v1@test.com")
    expect(dropdown.options[1].text).toContain("user1-v2@test.com")
    
    // Change selection to second user
    fireEvent.change(dropdown, { target: { value: "1" } })
    
    // Verify the selection changed
    expect(dropdown.value).toBe("1")
  })
})

export {}
