import { authenticatedFetch, getAuthHeaders } from '../utils/httpInterceptor'

const API_BASE_URL =
  process.env.REACT_APP_API_URL || "http://localhost:5017/api"

interface ApiResponse<T> {
  success: boolean
  data?: T
  message: string
}

interface PagedResponse<T> {
  items: T[]
  totalCount: number
  page: number
  pageSize: number
}

export interface UserMatriculaInfo {
  id: number
  matriculaNumber: string
  isOwner: boolean
  status: string
  startDate: string
  endDate: string | null
}

export interface User {
  id: string
  name: string
  email: string
  role: string
  parentUserId?: string
  parentUserName?: string
  isActive: boolean
  matricula?: string
  isMatriculaOwner: boolean
  createdAt: string
  updatedAt: string
  activeMatriculas?: UserMatriculaInfo[]
}

export interface UserLookupByMatricula {
  id: string
  name: string
  email: string
  matriculaId: number
  matriculaNumber: string
  isOwner: boolean
}

export interface CreateUserRequest {
  name: string
  email: string
  password: string
  role: string
  parentUserId?: string
}

export interface UpdateUserRequest {
  name?: string
  email?: string
  password?: string
  role?: string
  parentUserId?: string
  isActive?: boolean
}

export const apiService = {
  async getUsers(
    page: number = 1,
    pageSize: number = 10,
    search?: string,
    contractNumber?: string
  ): Promise<ApiResponse<PagedResponse<User>>> {
    const params = new URLSearchParams({
      page: page.toString(),
      pageSize: pageSize.toString(),
    })

    if (search) {
      params.append("search", search)
    }

    if (contractNumber) {
      params.append("contractNumber", contractNumber)
    }

    const response = await authenticatedFetch(`${API_BASE_URL}/users?${params}`, {
      headers: getAuthHeaders(),
    })

    if (!response.ok) {
      throw new Error("Failed to fetch users")
    }

    return response.json()
  },

  async getUser(id: string): Promise<ApiResponse<User>> {
    const response = await authenticatedFetch(`${API_BASE_URL}/users/${id}`, {
      headers: getAuthHeaders(),
    })

    if (!response.ok) {
      throw new Error("Failed to fetch user")
    }

    return response.json()
  },

  async getCurrentUser(): Promise<ApiResponse<User>> {
    const response = await authenticatedFetch(`${API_BASE_URL}/users/me`, {
      headers: getAuthHeaders(),
    })

    if (!response.ok) {
      throw new Error("Failed to fetch current user")
    }

    return response.json()
  },

  async requestMatricula(matriculaNumber: string): Promise<ApiResponse<any>> {
    const response = await authenticatedFetch(`${API_BASE_URL}/users/me/request-matricula`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify({ matriculaNumber })
    })

    if (!response.ok) {
      const error = await response.json()
      throw new Error(error.message || "Failed to request matricula")
    }

    return response.json()
  },

  async createUser(userData: CreateUserRequest): Promise<ApiResponse<User>> {
    const response = await authenticatedFetch(`${API_BASE_URL}/users/register`, {
      method: "POST",
      headers: getAuthHeaders(),
      body: JSON.stringify(userData),
    })

    if (!response.ok) {
      const error = await response.json()
      throw new Error(error.message || "Failed to create user")
    }

    return response.json()
  },

  async updateUser(
    id: string,
    userData: UpdateUserRequest
  ): Promise<ApiResponse<User>> {
    const response = await authenticatedFetch(`${API_BASE_URL}/users/${id}`, {
      method: "PUT",
      headers: getAuthHeaders(),
      body: JSON.stringify(userData),
    })

    if (!response.ok) {
      const error = await response.json()
      throw new Error(error.message || "Failed to update user")
    }

    return response.json()
  },

  async deleteUser(id: string): Promise<ApiResponse<object>> {
    const response = await authenticatedFetch(`${API_BASE_URL}/users/${id}`, {
      method: "DELETE",
      headers: getAuthHeaders(),
    })

    if (!response.ok) {
      throw new Error("Failed to delete user")
    }

    return response.json()
  },

  async importUsers(
    file: File
  ): Promise<ApiResponse<{ imported: number; errors?: any[] }>> {
    const formData = new FormData()
    formData.append("file", file)

    const token = localStorage.getItem("token")

    const response = await authenticatedFetch(`${API_BASE_URL}/users/import`, {
      method: "POST",
      headers: {
        Authorization: `Bearer ${token}`,
      },
      body: formData,
    })

    if (!response.ok) {
      const error = await response
        .json()
        .catch(() => ({ message: "Failed to import users" }))
      throw new Error(error.message || "Failed to import users")
    }

    return response.json()
  },

  async getImportTemplates(entityType?: string): Promise<ApiResponse<any[]>> {
    const params = entityType ? `?entityType=${entityType}` : "";
    const response = await authenticatedFetch(`${API_BASE_URL}/imports/templates${params}`, {
      headers: getAuthHeaders(),
    });

    if (!response.ok) {
      throw new Error("Failed to fetch import templates");
    }

    return response.json();
  },

  async uploadImportFile(
    file: File,
    templateId: number
  ): Promise<
    ApiResponse<{
      uploadId: string
      sessionId: string
      templateId: number
      templateName: string
      entityType: string
      fileName: string
      detectedColumns: string[]
      sampleRows: Record<string, string>[]
      totalRows: number
      suggestedMappings: Record<string, string>
      requiredFields: string[]
      optionalFields: string[]
      isTemplateMatch?: boolean
      matchMessage?: string
    }>
  > {
    const formData = new FormData()
    formData.append("file", file)

    const token = localStorage.getItem("token")

    const response = await authenticatedFetch(
      `${API_BASE_URL}/imports/upload?templateId=${templateId}`,
      {
        method: "POST",
        headers: {
          Authorization: `Bearer ${token}`,
        },
        body: formData,
      }
    )

    if (!response.ok) {
      const error = await response
        .json()
        .catch(() => ({ message: "Failed to upload file" }))
      throw new Error(error.message || "Failed to upload file")
    }

    return response.json()
  },

  async configureImportMappings(
    uploadId: string,
    mappings: Record<string, string>,
    allowAutoCreateGroups: boolean = false,
    allowAutoCreatePVs: boolean = false,
    skipMissingContractNumber: boolean = false
  ): Promise<
    ApiResponse<{
      uploadId: string
      status: string
      totalRows: number
      processedRows: number
      failedRows: number
      errors: string[]
    }>
  > {
    const response = await authenticatedFetch(`${API_BASE_URL}/imports/${uploadId}/mappings`, {
      method: "POST",
      headers: getAuthHeaders(),
      body: JSON.stringify({ 
        mappings, 
        allowAutoCreateGroups,
        allowAutoCreatePVs,
        skipMissingContractNumber
      }),
    })

    if (!response.ok) {
      const error = await response
        .json()
        .catch(() => ({ message: "Failed to configure mappings" }))
      throw new Error(error.message || "Failed to configure mappings")
    }

    return response.json()
  },

  async confirmImport(
    uploadId: string,
    dateFormat: string = "MM/DD/YYYY",
    skipMissingContractNumber: boolean = false,
    allowAutoCreateGroups: boolean = false,
    allowAutoCreatePVs: boolean = false
  ): Promise<
    ApiResponse<{
      uploadId: string
      status: string
      totalRows: number
      processedRows: number
      failedRows: number
      errors: string[]
      createdGroups: string[]
      createdPVs: string[]
    }>
  > {
    const response = await authenticatedFetch(`${API_BASE_URL}/imports/${uploadId}/confirm`, {
      method: "POST",
      headers: getAuthHeaders(),
      body: JSON.stringify({ 
        dateFormat, 
        skipMissingContractNumber, 
        allowAutoCreateGroups,
        allowAutoCreatePVs
      }),
    })

    if (!response.ok) {
      const error = await response
        .json()
        .catch(() => ({ message: "Failed to confirm import" }))
      throw new Error(error.message || "Failed to confirm import")
    }

    return response.json()
  },

  async getUsersByMatriculaNumber(matriculaNumber: string): Promise<ApiResponse<UserLookupByMatricula[]>> {
    const response = await authenticatedFetch(`${API_BASE_URL}/usermatriculas/by-number/${matriculaNumber}`, {
      headers: getAuthHeaders(),
    })

    if (!response.ok) {
      throw new Error("Failed to fetch users by matricula number")
    }

    return response.json()
  },

  async getUsersByMatriculaId(matriculaId: number): Promise<ApiResponse<UserLookupByMatricula[]>> {
    const response = await authenticatedFetch(`${API_BASE_URL}/usermatriculas/${matriculaId}/users`, {
      headers: getAuthHeaders(),
    })

    if (!response.ok) {
      throw new Error("Failed to fetch users by matricula ID")
    }

    return response.json()
  },

  // PV (Point of Sale) methods
  async getPVs(): Promise<ApiResponse<PV[]>> {
    const response = await authenticatedFetch(`${API_BASE_URL}/point-of-sale`, {
      headers: getAuthHeaders(),
    })

    if (!response.ok) {
      throw new Error("Failed to fetch PVs")
    }

    return response.json()
  },

  async getPV(id: number): Promise<ApiResponse<PV>> {
    const response = await authenticatedFetch(`${API_BASE_URL}/point-of-sale/${id}`, {
      headers: getAuthHeaders(),
    })

    if (!response.ok) {
      throw new Error("Failed to fetch PV")
    }

    return response.json()
  },

  async createPV(pv: PVRequest): Promise<ApiResponse<PV>> {
    const response = await authenticatedFetch(`${API_BASE_URL}/point-of-sale`, {
      method: "POST",
      headers: getAuthHeaders(),
      body: JSON.stringify(pv),
    })

    if (!response.ok) {
      const error = await response
        .json()
        .catch(() => ({ message: "Failed to create PV" }))
      throw new Error(error.message || "Failed to create PV")
    }

    return response.json()
  },

  async updatePV(id: number, pv: PVRequest): Promise<ApiResponse<PV>> {
    const response = await authenticatedFetch(`${API_BASE_URL}/point-of-sale/${id}`, {
      method: "PUT",
      headers: getAuthHeaders(),
      body: JSON.stringify(pv),
    })

    if (!response.ok) {
      const error = await response
        .json()
        .catch(() => ({ message: "Failed to update PV" }))
      throw new Error(error.message || "Failed to update PV")
    }

    return response.json()
  },

  async deletePV(id: number): Promise<ApiResponse<void>> {
    const response = await authenticatedFetch(`${API_BASE_URL}/point-of-sale/${id}`, {
      method: "DELETE",
      headers: getAuthHeaders(),
    })

    if (!response.ok) {
      const error = await response
        .json()
        .catch(() => ({ message: "Failed to delete PV" }))
      throw new Error(error.message || "Failed to delete PV")
    }

    return response.json()
  },

  // Matricula endpoints
  async getMatriculas(
    page: number = 1,
    pageSize: number = 10,
    search?: string
  ): Promise<ApiResponse<PagedResponse<UserMatricula>>> {
    const params = new URLSearchParams({
      page: page.toString(),
      pageSize: pageSize.toString(),
    })

    if (search) {
      params.append("search", search)
    }

    const response = await authenticatedFetch(`${API_BASE_URL}/usermatriculas?${params}`, {
      headers: getAuthHeaders(),
    })

    if (!response.ok) {
      throw new Error("Failed to fetch matriculas")
    }

    return response.json()
  },

  async getAllMatriculas(): Promise<ApiResponse<UserMatricula[]>> {
    const response = await authenticatedFetch(`${API_BASE_URL}/usermatriculas`, {
      headers: getAuthHeaders(),
    })

    if (!response.ok) {
      throw new Error("Failed to fetch matriculas")
    }

    return response.json()
  },

  async getMatriculaById(id: number): Promise<ApiResponse<UserMatricula>> {
    const response = await authenticatedFetch(`${API_BASE_URL}/usermatriculas/${id}`, {
      headers: getAuthHeaders(),
    })

    if (!response.ok) {
      throw new Error("Failed to fetch matricula")
    }

    return response.json()
  },

  async getUserMatriculas(userId: string): Promise<ApiResponse<UserMatricula[]>> {
    const response = await authenticatedFetch(`${API_BASE_URL}/usermatriculas/user/${userId}`, {
      headers: getAuthHeaders(),
    })

    if (!response.ok) {
      throw new Error("Failed to fetch user matriculas")
    }

    return response.json()
  },

  async createMatricula(data: CreateMatriculaRequest): Promise<ApiResponse<UserMatricula>> {
    const response = await authenticatedFetch(`${API_BASE_URL}/usermatriculas`, {
      method: "POST",
      headers: getAuthHeaders(),
      body: JSON.stringify(data),
    })

    if (!response.ok) {
      const error = await response
        .json()
        .catch(() => ({ message: "Failed to create matricula" }))
      throw new Error(error.message || "Failed to create matricula")
    }

    return response.json()
  },

  async updateMatricula(id: number, data: UpdateMatriculaRequest): Promise<ApiResponse<UserMatricula>> {
    const response = await authenticatedFetch(`${API_BASE_URL}/usermatriculas/${id}`, {
      method: "PUT",
      headers: getAuthHeaders(),
      body: JSON.stringify(data),
    })

    if (!response.ok) {
      const error = await response
        .json()
        .catch(() => ({ message: "Failed to update matricula" }))
      throw new Error(error.message || "Failed to update matricula")
    }

    return response.json()
  },

  async deleteMatricula(id: number): Promise<ApiResponse<void>> {
    const response = await authenticatedFetch(`${API_BASE_URL}/usermatriculas/${id}`, {
      method: "DELETE",
      headers: getAuthHeaders(),
    })

    if (!response.ok) {
      const error = await response
        .json()
        .catch(() => ({ message: "Failed to delete matricula" }))
      throw new Error(error.message || "Failed to delete matricula")
    }

    return response.json()
  },

  async bulkCreateMatriculas(data: CreateMatriculaRequest[]): Promise<ApiResponse<BulkCreateMatriculaResponse>> {
    const response = await authenticatedFetch(`${API_BASE_URL}/usermatriculas/bulk`, {
      method: "POST",
      headers: getAuthHeaders(),
      body: JSON.stringify({ matriculas: data }),
    })

    if (!response.ok) {
      const error = await response
        .json()
        .catch(() => ({ message: "Failed to bulk create matriculas" }))
      throw new Error(error.message || "Failed to bulk create matriculas")
    }

    return response.json()
  },
}

export interface PV {
  id: number
  name: string
  matriculaId?: number
  createdAt: string
  updatedAt: string
}

export interface PVRequest {
  id: number
  name: string
  matriculaId?: number
}

export interface UserMatricula {
  id: number
  userId: string
  userName: string
  matriculaNumber: string
  startDate: string
  endDate?: string
  isActive: boolean
  isOwner: boolean
  status: string
  createdAt: string
}

export interface CreateMatriculaRequest {
  userId?: string
  userEmail?: string
  matriculaNumber: string
  startDate: string
  endDate?: string
  isOwner?: boolean
}

export interface UpdateMatriculaRequest {
  matriculaNumber?: string
  startDate?: string
  endDate?: string
  isActive?: boolean
  isOwner?: boolean
  status?: string
}

export interface BulkCreateMatriculaResponse {
  totalProcessed: number
  successCount: number
  errorCount: number
  createdMatriculas: UserMatricula[]
  errors: Array<{
    rowNumber: number
    matriculaNumber: string
    userEmail: string
    error: string
  }>
}
