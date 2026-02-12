import config from '../config'
import { authenticatedFetch, getAuthHeaders } from '../utils/httpInterceptor'

const API_BASE_URL = config.apiUrl

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

/**
 * Parses and extracts a user-friendly error message from an API response.
 * Handles both custom ApiResponse structure and standard ASP.NET Core ProblemDetails.
 */
async function extractErrorMessage(response: Response, defaultMessage: string): Promise<string> {
  try {
    const errorData = await response.json();
    
    // 1. Try our custom ApiResponse structure
    if (errorData.message && !errorData.errors) {
      return errorData.message;
    }

    // 2. Try ASP.NET Core validation errors (ProblemDetails)
    if (errorData.errors) {
      const errorEntries = Object.entries(errorData.errors);
      if (errorEntries.length > 0) {
        const [field, messages] = errorEntries[0];
        if (Array.isArray(messages) && messages.length > 0) {
          return messages[0];
        }
      }
    }

    // 3. Fallback to title if available
    if (errorData.title) {
      return errorData.title;
    }
  } catch (e) {
    // Parsing failed, ignore
  }
  
  return defaultMessage;
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
      throw new Error(await extractErrorMessage(response, "Failed to request matricula"))
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
      throw new Error(await extractErrorMessage(response, "Failed to create user"))
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
      throw new Error(await extractErrorMessage(response, "Failed to update user"))
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

  async getImportHistory(): Promise<ApiResponse<ImportSession[]>> {
    const response = await authenticatedFetch(`${API_BASE_URL}/imports/history`, {
      headers: getAuthHeaders(),
    });

    if (!response.ok) {
      throw new Error("Failed to fetch import history");
    }

    return response.json();
  },

  async undoImport(id: number): Promise<ApiResponse<string>> {
    const response = await authenticatedFetch(`${API_BASE_URL}/imports/${id}/undo`, {
      method: "DELETE",
      headers: getAuthHeaders(),
    });

    if (!response.ok) {
      const error = await response.json();
      throw new Error(error.message || "Failed to undo import");
    }

    return response.json();
  },

  async uploadWizardStep1(file: File): Promise<ApiResponse<any>> {
    const formData = new FormData()
    formData.append("file", file)

    const token = localStorage.getItem("token")

    const response = await authenticatedFetch(`${API_BASE_URL}/wizard/step1-upload`, {
      method: "POST",
      headers: {
        Authorization: `Bearer ${token}`,
      },
      body: formData,
    })

    if (!response.ok) {
      const error = await response
        .json()
        .catch(() => ({ message: "Failed to upload file" }))
      throw new Error(error.message || "Failed to upload file")
    }

    return response.json()
  },

  async downloadWizardTemplate(uploadId: string): Promise<Blob> {
    const response = await authenticatedFetch(`${API_BASE_URL}/wizard/step1-template/${uploadId}`, {
      headers: getAuthHeaders(),
    })

    if (!response.ok) {
      throw new Error("Failed to download template")
    }

    return response.blob()
  },

  async runWizardStep2(uploadId: string, usersFile: File): Promise<ApiResponse<ImportStatusResponse>> {
    const formData = new FormData();
    formData.append('uploadId', uploadId);
    formData.append('usersFile', usersFile);

    const token = localStorage.getItem("token")
    const response = await authenticatedFetch(`${API_BASE_URL}/wizard/step2-import`, {
      method: "POST",
      headers: {
        Authorization: `Bearer ${token}`,
      },
      body: formData,
    })

    if (!response.ok) {
      const error = await response
        .json()
        .catch(() => ({ message: "Failed to run step 2" }))
      throw new Error(error.message || "Failed to run step 2")
    }

    return response.json()
  },

  async downloadWizardContracts(uploadId: string): Promise<void> {
    const response = await authenticatedFetch(`${API_BASE_URL}/wizard/step3-contracts/${uploadId}`, {
      method: 'GET',
      headers: getAuthHeaders(),
    });

    if (!response.ok) throw new Error('Failed to download contracts');

    const blob = await response.blob();
    const url = window.URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = 'contracts.csv';
    document.body.appendChild(a);
    a.click();
    window.URL.revokeObjectURL(url);
    document.body.removeChild(a);
  },

  // Generic methods
  async get<T = any>(endpoint: string): Promise<T> {
    const response = await authenticatedFetch(`${API_BASE_URL}${endpoint}`, {
      headers: getAuthHeaders(),
    });
    if (!response.ok) {
      const error = await response.json().catch(() => ({ message: `Failed to fetch ${endpoint}` }));
      throw new Error(error.message || `Failed to fetch ${endpoint}`);
    }
    return response.json();
  },

  async post<T = any>(endpoint: string, data: any): Promise<T> {
    const response = await authenticatedFetch(`${API_BASE_URL}${endpoint}`, {
      method: "POST",
      headers: getAuthHeaders(),
      body: JSON.stringify(data),
    });
    if (!response.ok) {
      const error = await response.json().catch(() => ({ message: `Failed to post to ${endpoint}` }));
      throw new Error(error.message || `Failed to post to ${endpoint}`);
    }
    return response.json();
  },

  async put<T = any>(endpoint: string, data: any): Promise<T> {
    const response = await authenticatedFetch(`${API_BASE_URL}${endpoint}`, {
      method: "PUT",
      headers: getAuthHeaders(),
      body: JSON.stringify(data),
    });
    if (!response.ok) {
      const error = await response.json().catch(() => ({ message: `Failed to put to ${endpoint}` }));
      throw new Error(error.message || `Failed to put to ${endpoint}`);
    }
    return response.json();
  },

  async delete<T = any>(endpoint: string): Promise<T> {
    const response = await authenticatedFetch(`${API_BASE_URL}${endpoint}`, {
      method: "DELETE",
      headers: getAuthHeaders(),
    });
    if (!response.ok) {
      const error = await response.json().catch(() => ({ message: `Failed to delete ${endpoint}` }));
      throw new Error(error.message || `Failed to delete ${endpoint}`);
    }
    return response.json();
  },
}

export interface ImportStatusResponse {
  uploadId: string;
  status: string;
  totalRows: number;
  processedRows: number;
  failedRows: number;
  errors?: string[];
  createdGroups?: string[];
  createdPVs?: string[];
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

export interface ImportSession {
  id: number;
  uploadId: string;
  templateId?: number;
  templateName?: string;
  fileName: string;
  status: string;
  totalRows: number;
  processedRows: number;
  failedRows: number;
  uploadedByUserId: string;
  uploadedBy?: { name: string };
  createdAt: string;
  completedAt?: string;
}
