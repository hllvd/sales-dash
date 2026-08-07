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
  id: number          // UserMatriculas join-table PK
  matriculaId: number // Matriculas table PK
  matriculaNumber: string
  isOwner: boolean
  status: string
  startDate: string
  endDate: string | null
}

export interface UserMetadataFieldValue {
  fieldId: number
  key: string
  label: string
  fieldType: string
  dropdownOptions?: string
  isRequired: boolean
  value?: string
}

export interface UserMetadataGroup {
  groupLabel: string | null
  fields: UserMetadataFieldValue[]
}

export interface UserMetadataFieldDef {
  id: number
  key: string
  label: string
  groupLabel?: string | null
  fieldType: string
  dropdownOptions?: string | null
  displayOrder: number
  isRequired: boolean
  isActive: boolean
}

export interface User {
  id: string
  name: string
  email: string
  role: string
  parentUserId?: string
  parentUserName?: string
  parentEmail?: string
  currentTeamName?: string
  currentLevelName?: string
  isActive: boolean
  matricula?: string
  isMatriculaOwner: boolean
  createdAt: string
  updatedAt: string
  activeMatriculas?: UserMatriculaInfo[]
  powerBiUsername?: string
  hasPowerBiCredentials?: boolean
  metadataGroups?: UserMetadataGroup[]
}

export interface ContractMigrationPreviewItem {
  contractId: number
  contractNumber: string
  totalAmount: number
  status: string
  currentMatriculaId?: number
  currentMatriculaNumber: string
  targetMatriculaId: number
  targetMatriculaNumber: string
  isAutoSelected: boolean
}

export interface ContractMigrationResult {
  migratedCount: number
}


export interface UserStats {
  pendingContractsCount: number
  totalProduction: number
  totalRetention: number
  strictRetention: number
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
  powerBiUsername?: string
  powerBiPassword?: string
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
        const [, messages] = errorEntries[0];
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
    contractNumber?: string,
    scopeToDescendants: boolean = false,
    activeOnly: boolean = false,
    status: string = "active"
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

    if (scopeToDescendants) {
      params.append("scopeToDescendants", "true")
    }

    if (activeOnly) {
      params.append("activeOnly", "true")
    } else if (status) {
      params.append("status", status)
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

  async getUserStats(id: string): Promise<ApiResponse<UserStats>> {
    const response = await authenticatedFetch(`${API_BASE_URL}/users/${id}/stats`, {
      headers: getAuthHeaders(),
    })

    if (!response.ok) {
      throw new Error("Failed to fetch user stats")
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

  async getMigrationPreview(userId: string): Promise<ApiResponse<ContractMigrationPreviewItem[]>> {
    const response = await authenticatedFetch(`${API_BASE_URL}/contracts/user/${userId}/migrate-preview`, {
      method: "GET",
      headers: getAuthHeaders(),
    })

    if (!response.ok) {
      throw new Error(await extractErrorMessage(response, "Failed to get migration preview"))
    }

    return response.json()
  },

  async migrateContracts(
    userId: string,
    mappings?: { contractId: number; targetMatriculaId: number }[]
  ): Promise<ApiResponse<ContractMigrationResult>> {
    const response = await authenticatedFetch(`${API_BASE_URL}/contracts/user/${userId}/migrate`, {
      method: "POST",
      headers: getAuthHeaders(),
      body: JSON.stringify({ mappings: mappings || null }),
    })

    if (!response.ok) {
      throw new Error(await extractErrorMessage(response, "Failed to migrate contracts"))
    }

    return response.json()
  },


  async batchUpdateParent(
    requestData: BatchUpdateParentRequest
  ): Promise<ApiResponse<BatchUpdateParentResult>> {
    const response = await authenticatedFetch(`${API_BASE_URL}/batch/users/parent`, {
      method: "POST",
      headers: getAuthHeaders(),
      body: JSON.stringify(requestData),
    })

    if (!response.ok) {
      throw new Error(await extractErrorMessage(response, "Failed to perform batch update"))
    }

    return response.json()
  },

  async batchAssignTeam(
    requestData: BatchAssignTeamRequest
  ): Promise<ApiResponse<BatchAssignTeamResult>> {
    const response = await authenticatedFetch(`${API_BASE_URL}/batch/team/assign`, {
      method: "POST",
      headers: getAuthHeaders(),
      body: JSON.stringify(requestData),
    })

    if (!response.ok) {
      throw new Error(await extractErrorMessage(response, "Failed to perform batch team assignment"))
    }

    return response.json()
  },

  async batchMergeUsers(
    requestData: MergeUsersRequest
  ): Promise<ApiResponse<MergeUsersResult>> {
    const response = await authenticatedFetch(`${API_BASE_URL}/batch/users/merge`, {
      method: "POST",
      headers: getAuthHeaders(),
      body: JSON.stringify(requestData),
    })

    if (!response.ok) {
      throw new Error(await extractErrorMessage(response, "Failed to perform batch user merge"))
    }

    return response.json()
  },

  async batchMergeMatriculas(
    requestData: MergeMatriculasRequest
  ): Promise<ApiResponse<MergeMatriculasResult>> {
    const response = await authenticatedFetch(`${API_BASE_URL}/batch/matriculas/merge`, {
      method: "POST",
      headers: getAuthHeaders(),
      body: JSON.stringify(requestData),
    })

    if (!response.ok) {
      throw new Error(await extractErrorMessage(response, "Failed to perform batch matricula merge"))
    }

    return response.json()
  },


  async savePowerBiCredentials(username: string, password?: string): Promise<ApiResponse<User>> {

    const response = await authenticatedFetch(`${API_BASE_URL}/users/me/powerbi-credentials`, {
      method: "PUT",
      headers: getAuthHeaders(),
      body: JSON.stringify({ username, password }),
    })

    if (!response.ok) {
      throw new Error(await extractErrorMessage(response, "Failed to save PowerBI credentials"))
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
    
    // Resolve specific endpoint if possible
    let endpoint = "upload"
    if (templateId === 1) endpoint = "users/upload"
    else if (templateId === 2) endpoint = "contracts/upload"
    else if (templateId === 3) endpoint = "dashboard/upload"

    const response = await authenticatedFetch(
      `${API_BASE_URL}/imports/${endpoint}${endpoint === "upload" ? `?templateId=${templateId}` : ""}`,
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
    skipMissingContractNumber: boolean = false,
    templateName?: string,
    updateMatriculaOnExisting: boolean = false,
    updateTotalAmountOnExisting: boolean = true
  ): Promise<ApiResponse<ImportStatusResponse>> {
    let prefix = "imports"
    if (templateName === "Contracts") prefix = "imports/contracts"
    else if (templateName === "contractDashboard") prefix = "imports/dashboard"
    else if (templateName === "Users") prefix = "imports/users"

    const response = await authenticatedFetch(`${API_BASE_URL}/${prefix}/${uploadId}/mappings`, {
      method: "POST",
      headers: getAuthHeaders(),
      body: JSON.stringify({ 
        mappings, 
        allowAutoCreateGroups,
        allowAutoCreatePVs,
        skipMissingContractNumber,
        updateMatriculaOnExisting,
        updateTotalAmountOnExisting
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
    allowAutoCreatePVs: boolean = false,
    templateName?: string,
    updateMatriculaOnExisting: boolean = false,
    updateTotalAmountOnExisting: boolean = true
  ): Promise<ApiResponse<ImportStatusResponse>> {
    let prefix = "imports"
    if (templateName === "Contracts") prefix = "imports/contracts"
    else if (templateName === "contractDashboard") prefix = "imports/dashboard"
    else if (templateName === "Users") prefix = "imports/users"

    const response = await authenticatedFetch(`${API_BASE_URL}/${prefix}/${uploadId}/confirm`, {
      method: "POST",
      headers: getAuthHeaders(),
      body: JSON.stringify({ 
        dateFormat, 
        skipMissingContractNumber, 
        allowAutoCreateGroups,
        allowAutoCreatePVs,
        updateMatriculaOnExisting,
        updateTotalAmountOnExisting
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

  async validateStatusColumn(
    uploadId: string,
    columnName: string
  ): Promise<ApiResponse<{
    isValid: boolean
    invalidValues: string[]
    unrecognizedValues: string[]
    sampleValues: string[]
    validCount: number
    totalChecked: number
  }>> {
    const response = await authenticatedFetch(
      `${API_BASE_URL}/imports/${uploadId}/validate-status`,
      {
        method: 'POST',
        headers: getAuthHeaders(),
        body: JSON.stringify({ columnName }),
      }
    )
    if (!response.ok) {
      const error = await response.json().catch(() => ({ message: 'Failed to validate status column' }))
      throw new Error(error.message || 'Failed to validate status column')
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
    a.download = 'contracts.xlsx';
    document.body.appendChild(a);
    a.click();
    window.URL.revokeObjectURL(url);
    document.body.removeChild(a);
  },

  async runWizardStep3Import(
    uploadId: string,
    options: {
      skipMissingContractNumber: boolean;
      allowAutoCreateGroups: boolean;
      allowAutoCreatePVs: boolean;
      dateFormat: string;
    }
  ): Promise<ApiResponse<ImportStatusResponse>> {
    const response = await authenticatedFetch(`${API_BASE_URL}/wizard/step3-import/${uploadId}`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify({
        skipMissingContractNumber: options.skipMissingContractNumber,
        allowAutoCreateGroups: options.allowAutoCreateGroups,
        allowAutoCreatePVs: options.allowAutoCreatePVs,
        dateFormat: options.dateFormat,
      }),
    });

    if (!response.ok) {
      const error = await response.json().catch(() => ({ message: 'Falha ao importar contratos' }));
      throw new Error(error.message || 'Falha ao importar contratos');
    }

    return response.json();
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
    if (response.status === 204) {
      return null as any;
    }
    
    const text = await response.text();
    return text ? JSON.parse(text) : null;
  },

  // ── Contract Export ──────────────────────────────────────────────────────


  async startContractExport(filters: {
    userId?: string;
    groupId?: number;
    startDate?: string;
    endDate?: string;
    contractNumber?: string;
    showUnassigned?: boolean;
    matricula?: string;
    matriculas?: string[];
    userEmail?: string;
    teamIds?: number[];
    userIds?: string[];
  }): Promise<{ jobId: string; status: string; totalRows: number; processedRows: number }> {
    const response = await authenticatedFetch(`${API_BASE_URL}/contracts/export`, {
      method: 'POST',
      headers: { ...getAuthHeaders(), 'Content-Type': 'application/json' },
      body: JSON.stringify(filters),
    });
    if (!response.ok) throw new Error('Falha ao iniciar exportação');
    const json = await response.json();
    return json.data;
  },

  async getContractExportStatus(jobId: string): Promise<{ jobId: string; status: string; totalRows: number; processedRows: number; errorMessage?: string }> {
    const response = await authenticatedFetch(`${API_BASE_URL}/contracts/export/${jobId}`, {
      headers: getAuthHeaders(),
    });
    if (!response.ok) throw new Error('Job não encontrado ou expirado');
    const json = await response.json();
    return json.data;
  },

  contractExportDownloadUrl(jobId: string): string {
    return `${API_BASE_URL}/contracts/export/${jobId}/download`;
  },

  contractExportPollUrl(jobId: string): string {
    return `${API_BASE_URL}/contracts/export/${jobId}`;
  },

  async startMyContractExport(filters: {
    startDate?: string;
    endDate?: string;
  }): Promise<{ jobId: string; status: string; totalRows: number; processedRows: number }> {
    const response = await authenticatedFetch(`${API_BASE_URL}/users/me/contracts/export`, {
      method: 'POST',
      headers: { ...getAuthHeaders(), 'Content-Type': 'application/json' },
      body: JSON.stringify(filters),
    });
    if (!response.ok) throw new Error('Falha ao iniciar exportação');
    const json = await response.json();
    return json.data;
  },

  myContractExportDownloadUrl(jobId: string): string {
    return `${API_BASE_URL}/users/me/contracts/export/${jobId}/download`;
  },

  myContractExportPollUrl(jobId: string): string {
    return `${API_BASE_URL}/users/me/contracts/export/${jobId}`;
  },

  // Teams Management methods
  async getTeams(): Promise<ApiResponse<Team[]>> {
    const response = await authenticatedFetch(`${API_BASE_URL}/teams`, {
      headers: getAuthHeaders(),
    })
    if (!response.ok) throw new Error("Failed to fetch teams")
    return response.json()
  },

  async getTeam(id: number): Promise<ApiResponse<Team>> {
    const response = await authenticatedFetch(`${API_BASE_URL}/teams/${id}`, {
      headers: getAuthHeaders(),
    })
    if (!response.ok) throw new Error("Failed to fetch team")
    return response.json()
  },

  async createTeam(name: string, members: Array<{ userId: string; startDate?: string }>, storeId?: number): Promise<ApiResponse<Team>> {
    const response = await authenticatedFetch(`${API_BASE_URL}/teams`, {
      method: "POST",
      headers: getAuthHeaders(),
      body: JSON.stringify({ name, members, storeId }),
    })
    if (!response.ok) {
      throw new Error(await extractErrorMessage(response, "Failed to create team"))
    }
    return response.json()
  },

  async updateTeam(id: number, data: { name?: string; ownerUserId?: string; storeId?: number; clearStore?: boolean }): Promise<ApiResponse<Team>> {
    const response = await authenticatedFetch(`${API_BASE_URL}/teams/${id}`, {
      method: "PUT",
      headers: getAuthHeaders(),
      body: JSON.stringify(data),
    })
    if (!response.ok) {
      throw new Error(await extractErrorMessage(response, "Failed to update team"))
    }
    return response.json()
  },

  async deleteTeam(id: number): Promise<ApiResponse<any>> {
    const response = await authenticatedFetch(`${API_BASE_URL}/teams/${id}`, {
      method: "DELETE",
      headers: getAuthHeaders(),
    })
    if (!response.ok) throw new Error("Failed to delete team")
    return response.json()
  },

  // Stores Management methods
  async getStores(): Promise<ApiResponse<Store[]>> {
    const response = await authenticatedFetch(`${API_BASE_URL}/stores`, {
      headers: getAuthHeaders(),
    })
    if (!response.ok) throw new Error("Failed to fetch stores")
    return response.json()
  },

  async getAllActiveStores(): Promise<ApiResponse<Store[]>> {
    const response = await authenticatedFetch(`${API_BASE_URL}/stores/all`, {
      headers: getAuthHeaders(),
    })
    if (!response.ok) throw new Error("Failed to fetch active stores")
    return response.json()
  },

  async getStore(id: number): Promise<ApiResponse<Store>> {
    const response = await authenticatedFetch(`${API_BASE_URL}/stores/${id}`, {
      headers: getAuthHeaders(),
    })
    if (!response.ok) throw new Error("Failed to fetch store")
    return response.json()
  },

  async createStore(data: CreateStoreRequest): Promise<ApiResponse<Store>> {
    const response = await authenticatedFetch(`${API_BASE_URL}/stores`, {
      method: "POST",
      headers: getAuthHeaders(),
      body: JSON.stringify(data),
    })
    if (!response.ok) {
      throw new Error(await extractErrorMessage(response, "Failed to create store"))
    }
    return response.json()
  },

  async updateStore(id: number, data: UpdateStoreRequest): Promise<ApiResponse<Store>> {
    const response = await authenticatedFetch(`${API_BASE_URL}/stores/${id}`, {
      method: "PUT",
      headers: getAuthHeaders(),
      body: JSON.stringify(data),
    })
    if (!response.ok) {
      throw new Error(await extractErrorMessage(response, "Failed to update store"))
    }
    return response.json()
  },

  async deleteStore(id: number): Promise<ApiResponse<any>> {
    const response = await authenticatedFetch(`${API_BASE_URL}/stores/${id}`, {
      method: "DELETE",
      headers: getAuthHeaders(),
    })
    if (!response.ok) throw new Error("Failed to delete store")
    return response.json()
  },

  async addTeamMembers(id: number, members: Array<{ userId: string; startDate?: string }>): Promise<ApiResponse<Team>> {
    const response = await authenticatedFetch(`${API_BASE_URL}/teams/${id}/members`, {
      method: "POST",
      headers: getAuthHeaders(),
      body: JSON.stringify({ members }),
    })
    if (!response.ok) {
      throw new Error(await extractErrorMessage(response, "Failed to add team members"))
    }
    return response.json()
  },

  async removeTeamMember(id: number, userId: string): Promise<ApiResponse<Team>> {
    const response = await authenticatedFetch(`${API_BASE_URL}/teams/${id}/members/${userId}`, {
      method: "DELETE",
      headers: getAuthHeaders(),
    })
    if (!response.ok) throw new Error("Failed to remove team member")
    return response.json()
  },

  async updateTeamMemberDates(id: number, userId: string, startDate: string, endDate: string | null): Promise<ApiResponse<Team>> {
    const response = await authenticatedFetch(`${API_BASE_URL}/teams/${id}/members/${userId}`, {
      method: "PUT",
      headers: getAuthHeaders(),
      body: JSON.stringify({ startDate, endDate }),
    })
    if (!response.ok) {
      throw new Error(await extractErrorMessage(response, "Failed to update team member dates"))
    }
    return response.json()
  },

  async setTeamOwner(id: number, ownerUserId: string): Promise<ApiResponse<Team>> {
    const response = await authenticatedFetch(`${API_BASE_URL}/teams/${id}/owner`, {
      method: "POST",
      headers: {
        ...getAuthHeaders(),
        "Content-Type": "application/json"
      },
      body: JSON.stringify(ownerUserId),
    })
    if (!response.ok) {
      throw new Error(await extractErrorMessage(response, "Failed to set team owner"))
    }
    return response.json()
  },

  // ── Classification Levels ────────────────────────────────────────────────────

  async getClassificationLevels(): Promise<ApiResponse<ClassificationLevel[]>> {
    const response = await authenticatedFetch(`${API_BASE_URL}/classifications/levels`, {
      headers: getAuthHeaders(),
    })
    if (!response.ok) throw new Error(await extractErrorMessage(response, "Failed to fetch levels"))
    return response.json()
  },

  async createClassificationLevel(data: CreateClassificationLevelRequest): Promise<ApiResponse<ClassificationLevel>> {
    const response = await authenticatedFetch(`${API_BASE_URL}/classifications/levels`, {
      method: "POST",
      headers: { ...getAuthHeaders(), "Content-Type": "application/json" },
      body: JSON.stringify(data),
    })
    if (!response.ok) throw new Error(await extractErrorMessage(response, "Failed to create level"))
    return response.json()
  },

  async updateClassificationLevel(id: number, data: Partial<CreateClassificationLevelRequest>): Promise<ApiResponse<ClassificationLevel>> {
    const response = await authenticatedFetch(`${API_BASE_URL}/classifications/levels/${id}`, {
      method: "PUT",
      headers: { ...getAuthHeaders(), "Content-Type": "application/json" },
      body: JSON.stringify(data),
    })
    if (!response.ok) throw new Error(await extractErrorMessage(response, "Failed to update level"))
    return response.json()
  },

  async deleteClassificationLevel(id: number): Promise<ApiResponse<object>> {
    const response = await authenticatedFetch(`${API_BASE_URL}/classifications/levels/${id}`, {
      method: "DELETE",
      headers: getAuthHeaders(),
    })
    if (!response.ok) throw new Error(await extractErrorMessage(response, "Failed to delete level"))
    return response.json()
  },

  async assignUserLevel(data: AssignUserLevelRequest): Promise<ApiResponse<UserClassification>> {
    const response = await authenticatedFetch(`${API_BASE_URL}/classifications/assign`, {
      method: "POST",
      headers: { ...getAuthHeaders(), "Content-Type": "application/json" },
      body: JSON.stringify(data),
    })
    if (!response.ok) throw new Error(await extractErrorMessage(response, "Failed to assign level"))
    return response.json()
  },

  async getUserClassificationHistory(userId: string): Promise<ApiResponse<UserClassification[]>> {
    const response = await authenticatedFetch(`${API_BASE_URL}/classifications/users/${userId}/history`, {
      headers: getAuthHeaders(),
    })
    if (!response.ok) throw new Error(await extractErrorMessage(response, "Failed to fetch classification history"))
    return response.json()
  },

  async getUserActiveClassification(userId: string): Promise<ApiResponse<UserClassification | null>> {
    const response = await authenticatedFetch(`${API_BASE_URL}/classifications/users/${userId}/active`, {
      headers: getAuthHeaders(),
    })
    if (!response.ok) throw new Error(await extractErrorMessage(response, "Failed to fetch active classification"))
    return response.json()
  },

  async removeUserClassification(assignmentId: number): Promise<ApiResponse<object>> {
    const response = await authenticatedFetch(`${API_BASE_URL}/classifications/assignments/${assignmentId}`, {
      method: "DELETE",
      headers: getAuthHeaders(),
    })
    if (!response.ok) throw new Error(await extractErrorMessage(response, "Failed to remove classification"))
    return response.json()
  },

  async getLevelMembers(levelId: number): Promise<ApiResponse<UserClassification[]>> {
    const response = await authenticatedFetch(`${API_BASE_URL}/classifications/levels/${levelId}/members`, {
      headers: getAuthHeaders(),
    })
    if (!response.ok) throw new Error(await extractErrorMessage(response, "Failed to fetch level members"))
    return response.json()
  },

  // ── User Metadata Fields & Values ──────────────────────────────────────────────

  async getUserMetadataFields(): Promise<ApiResponse<UserMetadataFieldDef[]>> {
    const response = await authenticatedFetch(`${API_BASE_URL}/usermetadata/fields`, {
      headers: getAuthHeaders(),
    })
    if (!response.ok) throw new Error(await extractErrorMessage(response, "Failed to fetch metadata fields"))
    return response.json()
  },

  async createMetadataField(req: Omit<UserMetadataFieldDef, 'id' | 'isActive'>): Promise<ApiResponse<UserMetadataFieldDef>> {
    const response = await authenticatedFetch(`${API_BASE_URL}/usermetadata/fields`, {
      method: "POST",
      headers: {
        ...getAuthHeaders(),
        "Content-Type": "application/json",
      },
      body: JSON.stringify(req),
    })
    if (!response.ok) throw new Error(await extractErrorMessage(response, "Failed to create metadata field"))
    return response.json()
  },

  async updateMetadataField(id: number, req: Omit<UserMetadataFieldDef, 'id' | 'isActive'>): Promise<ApiResponse<UserMetadataFieldDef>> {
    const response = await authenticatedFetch(`${API_BASE_URL}/usermetadata/fields/${id}`, {
      method: "PUT",
      headers: {
        ...getAuthHeaders(),
        "Content-Type": "application/json",
      },
      body: JSON.stringify(req),
    })
    if (!response.ok) throw new Error(await extractErrorMessage(response, "Failed to update metadata field"))
    return response.json()
  },

  async deleteMetadataField(id: number): Promise<ApiResponse<object>> {
    const response = await authenticatedFetch(`${API_BASE_URL}/usermetadata/fields/${id}`, {
      method: "DELETE",
      headers: getAuthHeaders(),
    })
    if (!response.ok) throw new Error(await extractErrorMessage(response, "Failed to delete metadata field"))
    return response.json()
  },

  async getUserMetadataValues(userId: string): Promise<ApiResponse<UserMetadataGroup[]>> {
    const response = await authenticatedFetch(`${API_BASE_URL}/usermetadata/${userId}/values`, {
      headers: getAuthHeaders(),
    })
    if (!response.ok) throw new Error(await extractErrorMessage(response, "Failed to fetch metadata values"))
    return response.json()
  },

  async upsertUserMetadataValues(userId: string, values: { fieldId: number; value?: string }[]): Promise<ApiResponse<object>> {
    const response = await authenticatedFetch(`${API_BASE_URL}/usermetadata/${userId}/values`, {
      method: "PUT",
      headers: {
        ...getAuthHeaders(),
        "Content-Type": "application/json",
      },
      body: JSON.stringify({ values }),
    })
    if (!response.ok) throw new Error(await extractErrorMessage(response, "Failed to save metadata values"))
    return response.json()
  },

  async getCurrentUserTree(depth: number = 10): Promise<ApiResponse<UserTreeResponse>> {
    const response = await authenticatedFetch(`${API_BASE_URL}/users/me/tree?depth=${depth}`, {
      headers: getAuthHeaders(),
    })
    if (!response.ok) {
      throw new Error(await extractErrorMessage(response, "Failed to fetch user tree"))
    }
    return response.json()
  },

  async getUserTree(id: string, depth: number = 10): Promise<ApiResponse<UserTreeResponse>> {
    const response = await authenticatedFetch(`${API_BASE_URL}/users/${id}/tree?depth=${depth}`, {
      headers: getAuthHeaders(),
    })
    if (!response.ok) {
      throw new Error(await extractErrorMessage(response, "Failed to fetch user tree"))
    }
    return response.json()
  },

  async checkEmail(email: string): Promise<ApiResponse<EmailCheckResponse>> {
    const response = await fetch(`${API_BASE_URL}/users/check-email?email=${encodeURIComponent(email)}`)
    if (!response.ok) throw new Error(await extractErrorMessage(response, "Failed to check email"))
    return response.json()
  },

  async autocompleteParents(search: string): Promise<ApiResponse<ParentAutocompleteResponse[]>> {
    const response = await fetch(`${API_BASE_URL}/users/autocomplete-parents?search=${encodeURIComponent(search)}`)
    if (!response.ok) throw new Error(await extractErrorMessage(response, "Failed to fetch parents for autocomplete"))
    return response.json()
  },

  async adminRegister(payload: AdminRegistrationRequest): Promise<ApiResponse<object>> {
    const response = await fetch(`${API_BASE_URL}/users/admin-register`, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify(payload),
    })
    if (!response.ok) throw new Error(await extractErrorMessage(response, "Failed to register user"))
    return response.json()
  },

  async createApprovalRequest(payload: CreateApprovalRequestPayload): Promise<ApiResponse<ApprovalRequestItem>> {
    const response = await authenticatedFetch(`${API_BASE_URL}/approval-requests`, {
      method: "POST",
      headers: getAuthHeaders(),
      body: JSON.stringify(payload),
    })
    if (!response.ok) throw new Error(await extractErrorMessage(response, "Failed to create approval request"))
    return response.json()
  },

  async getPendingApprovalRequests(): Promise<ApiResponse<ApprovalRequestItem[]>> {
    const response = await authenticatedFetch(`${API_BASE_URL}/approval-requests/pending`, {
      method: "GET",
      headers: getAuthHeaders(),
    })
    if (!response.ok) throw new Error(await extractErrorMessage(response, "Failed to fetch pending approval requests"))
    return response.json()
  },

  async getMyApprovalRequests(): Promise<ApiResponse<ApprovalRequestItem[]>> {
    const response = await authenticatedFetch(`${API_BASE_URL}/approval-requests/mine`, {
      method: "GET",
      headers: getAuthHeaders(),
    })
    if (!response.ok) throw new Error(await extractErrorMessage(response, "Failed to fetch user approval requests"))
    return response.json()
  },

  async resolveApprovalRequest(id: number, payload: ResolveApprovalPayload): Promise<ApiResponse<ApprovalRequestItem>> {
    const response = await authenticatedFetch(`${API_BASE_URL}/approval-requests/${id}/resolve`, {
      method: "POST",
      headers: getAuthHeaders(),
      body: JSON.stringify(payload),
    })
    if (!response.ok) throw new Error(await extractErrorMessage(response, "Failed to resolve approval request"))
    return response.json()
  },
}

export interface UserHierarchyNode {
  id: string
  name: string
  email: string
  role: string
  parentUserId?: string | null
  parentUserName?: string | null
  level: number
  isActive: boolean
  createdAt: string
  updatedAt: string
  ownedTeamId?: number | null
  ownedTeamName?: string | null
}

export interface UserTreeResponse {
  users: UserHierarchyNode[]
  totalUsers: number
  maxDepth: number
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
  warnings?: string[];
  desistenteContractNumbers?: string[];
  failedRowsDetails?: Record<string, string>[];
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
  lastUpdate?: string
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

export interface Store {
  id: number
  name: string
  state: string
  isActive: boolean
  createdAt: string
  updatedAt: string
}

export interface CreateStoreRequest {
  name: string
  state: string
}

export interface UpdateStoreRequest {
  name?: string
  state?: string
  isActive?: boolean
}

export interface TeamMember {
  userId: string
  userInternalId: number
  userName: string
  userEmail: string
  startDate: string
  endDate: string | null
  isActive: boolean
  isOwner: boolean
}

export interface Team {
  id: number
  name: string
  storeId?: number
  storeName?: string
  storeState?: string
  owner: TeamMember | null
  members: TeamMember[]
  warnings?: string[]
  createdAt: string
  updatedAt: string
}

// ── Classification Levels ──────────────────────────────────────────────────────

export interface ClassificationLevel {
  id: number
  name: string
  description?: string
  prize?: string
  salesGoal?: number
  retention?: number
  nextLevelId?: number
  nextLevelName?: string
  minimumDirect1LevelId?: number
  minimumDirect1LevelName?: string
  minimumDirect1MinCount?: number
  minimumDirect2LevelId?: number
  minimumDirect2LevelName?: string
  minimumDirect2MinCount?: number
  activeUsersCount: number
  createdAt: string
  updatedAt: string
}

export interface UserClassification {
  id: number
  userId: string
  userInternalId: number
  userName: string
  userEmail: string
  levelId: number
  levelName: string
  levelDescription?: string
  levelPrize?: string
  levelSalesGoal?: number
  startDate: string
  endDate: string | null
  isActive: boolean
  createdAt: string
}

export interface CreateClassificationLevelRequest {
  name: string
  description?: string
  prize?: string
  salesGoal?: number
  retention?: number
  nextLevelId?: number
  clearNextLevel?: boolean
  minimumDirect1LevelId?: number
  minimumDirect1MinCount?: number
  clearMinimumDirect1?: boolean
  minimumDirect2LevelId?: number
  minimumDirect2MinCount?: number
  clearMinimumDirect2?: boolean
}

export interface AssignUserLevelRequest {
  userId: string
  levelId: number
  startDate: string
  endDate?: string | null
}

export interface EmailCheckResponse {
  exists: boolean
  contactPhone?: string
}

export interface ParentAutocompleteResponse {
  name: string
  email: string
}

export interface AdminRegistrationRequest {
  email: string
  name: string
  password: string
  teamName: string
  classificationLevelId: number
  classificationStartDate?: string
  role: 'manager' | 'secretary'
  secretaryName?: string
  secretaryEmail?: string
  secretaryWhatsapp?: string
  parentEmail?: string
}

export interface BatchUpdateParentRequest {
  parentEmail: string
  overrideExisting: boolean
  teamId?: number
  matricula?: string
}

export interface ModifiedUserSummary {
  id: string
  name: string
  email: string
  oldParentEmail?: string
  newParentEmail: string
}

export interface SkippedUserSummary {
  id: string
  name: string
  email: string
  currentParentEmail?: string
  reason: string
}

export interface BatchUpdateParentResult {
  modified: ModifiedUserSummary[]
  skipped: SkippedUserSummary[]
}

export interface BatchAssignTeamRequest {
  parentEmail?: string
  matricula?: string
  teamId: number
  startDate?: string
  overrideExisting: boolean
}

export interface AddedMemberSummary {
  id: string
  name: string
  email: string
}

export interface BatchAssignTeamResult {
  added: AddedMemberSummary[]
  skipped: SkippedUserSummary[]
}

export interface MergeUserPair {
  mainEmail: string
  duplicateEmail: string
}

export interface MergeUsersRequest {
  pairs: MergeUserPair[]
  deactivateDuplicate: boolean
  dryRun: boolean
}

export interface MergeUserPairResult {
  mainEmail: string
  duplicateEmail: string
  error?: string | null
  contractsMigrated: number
  matriculasMigrated: number
  childUsersMigrated: number
  teamMembershipsMigrated: number
  duplicateDeactivated: boolean
}

export interface MergeUsersResult {
  isDryRun: boolean
  pairs: MergeUserPairResult[]
}

export interface MergeMatriculaPair {
  mainMatricula: string
  duplicateMatricula: string
}

export interface MergeMatriculasRequest {
  pairs: MergeMatriculaPair[]
  deleteDuplicate: boolean
  dryRun: boolean
}

export interface MergeMatriculaPairResult {
  mainMatricula: string
  duplicateMatricula: string
  error?: string | null
  userLinksMigrated: number
  contractsMigrated: number
  duplicateDeleted: boolean
}

export interface MergeMatriculasResult {
  isDryRun: boolean
  pairs: MergeMatriculaPairResult[]
}


export interface ApprovalRequestItem {

  id: number
  requestType: string
  requesterId: string
  requesterName: string
  requesterEmail: string
  approverId?: string | null
  approverName?: string | null
  status: string
  payloadJson: string
  approverComment?: string | null
  createdAt: string
  updatedAt: string
}

export interface CreateApprovalRequestPayload {
  requestType: string
  payloadJson: string
}

export interface ResolveApprovalPayload {
  action: string
  comment?: string
}
