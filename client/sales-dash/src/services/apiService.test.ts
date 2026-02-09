import { apiService } from "./apiService"

// Mock fetch globally
global.fetch = jest.fn()

describe("apiService import flow", () => {
  beforeEach(() => {
    jest.clearAllMocks()
    Storage.prototype.getItem = jest.fn(() => "mock-token")
  })

  it("uploadImportFile posts file with templateId and returns preview", async () => {
    const mockResponse = {
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
          { name: "John", email: "john@example.com", role: "user" },
        ],
        totalRows: 1,
        suggestedMappings: {
          name: "Name",
          email: "Email",
          role: "Role",
        },
        requiredFields: ["Name", "Email"],
        optionalFields: ["Surname", "Role", "ParentEmail"],
      },
      message: "Uploaded",
    }

    ;(global.fetch as jest.Mock).mockResolvedValueOnce({
      ok: true,
      json: async () => mockResponse,
    })

    const file = new File(
      ["name,email,role\nJohn,john@example.com,user"],
      "users.csv",
      { type: "text/csv" }
    )

    const resp = await apiService.uploadImportFile(file, 1)

    expect(global.fetch).toHaveBeenCalledWith(
      expect.stringContaining("/imports/upload?templateId=1"),
      expect.objectContaining({
        method: "POST",
        headers: expect.objectContaining({
          Authorization: "Bearer mock-token",
        }),
        body: expect.any(FormData),
      })
    )

    expect(resp).toEqual(mockResponse)
  })

  it("configureImportMappings posts mappings", async () => {
    const mockResponse = {
      success: true,
      data: {
        uploadId: "upload-123",
        status: "ready",
        totalRows: 1,
        processedRows: 0,
        failedRows: 0,
        errors: [],
      },
      message: "Configured",
    }

    ;(global.fetch as jest.Mock).mockResolvedValueOnce({
      ok: true,
      json: async () => mockResponse,
    })

    const mappings = {
      name: "Name",
      email: "Email",
      role: "Role",
    }

    const resp = await apiService.configureImportMappings("upload-123", mappings)

    expect(global.fetch).toHaveBeenCalledWith(
      expect.stringContaining("/imports/upload-123/mappings"),
      expect.objectContaining({
        method: "POST",
        headers: expect.objectContaining({
          "Content-Type": "application/json",
          Authorization: "Bearer mock-token",
        }),
        body: JSON.stringify({ mappings }),
      })
    )

    expect(resp).toEqual(mockResponse)
  })

  it("confirmImport posts to confirm endpoint", async () => {
    const mockResponse = {
      success: true,
      data: {
        uploadId: "upload-123",
        status: "completed",
        totalRows: 1,
        processedRows: 1,
        failedRows: 0,
        errors: [],
      },
      message: "Imported",
    }

    ;(global.fetch as jest.Mock).mockResolvedValueOnce({
      ok: true,
      json: async () => mockResponse,
    })

    const resp = await apiService.confirmImport("upload-123")

    expect(global.fetch).toHaveBeenCalledWith(
      expect.stringContaining("/imports/upload-123/confirm"),
      expect.objectContaining({
        method: "POST",
        headers: expect.objectContaining({
          "Content-Type": "application/json",
          Authorization: "Bearer mock-token",
        }),
      })
    )

    expect(resp).toEqual(mockResponse)
  })
})
