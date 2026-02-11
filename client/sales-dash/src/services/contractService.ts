import config from '../config';
import { authenticatedFetch, getAuthHeaders } from '../utils/httpInterceptor';

const API_BASE_URL = config.apiUrl;

// Contract Status Enum
export enum ContractStatus {
  Active = 'Active',
  Late1 = 'Late1',
  Late2 = 'Late2',
  Late3 = 'Late3',
  Defaulted = 'Defaulted'
}

// TypeScript Interfaces
export interface Contract {
  id: number;
  contractNumber: string;
  userId?: string | null;
  userName?: string | null;
  totalAmount: number;
  groupId?: number | null;
  groupName: string;
  pvId?: number;
  pvName?: string;
  status: ContractStatus;
  contractStartDate: string;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
  contractType?: string;
  quota?: number;
  customerName?: string;
  matriculaNumber?: string;
  userMatriculaId?: number | null;
}

export interface CreateContractRequest {
  contractNumber: string;
  userId?: string;
  totalAmount: number;
  groupId?: number;
  status: ContractStatus;
  contractStartDate: string;
  pvId?: number;
  contractType?: string;
  quota?: number;
  customerName?: string;
  matriculaNumber?: string;
  userMatriculaId?: number;
}

export interface UpdateContractRequest {
  contractNumber?: string;
  userId?: string;
  totalAmount?: number;
  groupId?: number;
  pvId?: number;
  status?: ContractStatus;
  contractStartDate?: string;
  isActive?: boolean;
  contractType?: string;
  quota?: number;
  customerName?: string;
  matriculaNumber?: string;
  userMatriculaId?: number;
}

export interface UserMatriculaInfo {
  id: number;
  matriculaNumber: string;
  isOwner: boolean;
  status: string;
  startDate: string;
  endDate: string | null;
}

export interface User {
  id: string;
  name: string;
  email: string;
  role: string;
  isActive: boolean;
  matriculaId?: number;
  matriculaNumber?: string;
  isMatriculaOwner: boolean;
  activeMatriculas?: UserMatriculaInfo[];
}

export interface Group {
  id: number;
  name: string;
  description: string;
  commission: number;
  isActive: boolean;
}

export interface ContractAggregation {
  total: number;
  totalCancel: number;
  totalActive: number;
  totalLate: number;
  retention: number;
}

export interface MonthlyProduction {
  period: string; // "YYYY-MM"
  totalProduction: number;
  contractCount: number;
}

export interface HistoricProductionResponse {
  monthlyData: MonthlyProduction[];
  totalProduction: number;
  totalContracts: number;
}


interface ApiResponse<T> {
  success: boolean;
  data: T;
  message: string;
  aggregation?: ContractAggregation;
}

// Contract CRUD Operations
export const getContracts = async (
  userId?: string,
  groupId?: number,
  startDate?: string,
  endDate?: string,
  contractNumber?: string,
  showUnassigned?: boolean,
  matricula?: string
): Promise<{ contracts: Contract[]; aggregation?: ContractAggregation }> => {
  const params = new URLSearchParams();
  if (userId) params.append('userId', userId);
  if (groupId) params.append('groupId', groupId.toString());
  if (startDate) params.append('startDate', startDate);
  if (endDate) params.append('endDate', endDate);
  if (contractNumber) params.append('contractNumber', contractNumber);
  if (showUnassigned !== undefined) params.append('showUnassigned', showUnassigned.toString());
  if (matricula) params.append('matricula', matricula);

  const queryString = params.toString();
  const url = `${API_BASE_URL}/contracts${queryString ? `?${queryString}` : ''}`;

  const response = await authenticatedFetch(url, {
    method: 'GET',
    headers: getAuthHeaders(),
  });

  if (!response.ok) {
    throw new Error('Failed to fetch contracts');
  }

  const result: ApiResponse<Contract[]> = await response.json();
  return { contracts: result.data, aggregation: result.aggregation };
};

export const getUserContracts = async (
  userId: string,
  startDate?: string,
  endDate?: string
): Promise<{ contracts: Contract[]; aggregation?: ContractAggregation }> => {
  const params = new URLSearchParams();
  if (startDate) params.append('startDate', startDate);
  if (endDate) params.append('endDate', endDate);

  const queryString = params.toString();
  const url = `${API_BASE_URL}/contracts/user/${userId}${queryString ? `?${queryString}` : ''}`;

  const response = await authenticatedFetch(url, {
    method: 'GET',
    headers: getAuthHeaders(),
  });

  if (!response.ok) {
    throw new Error('Failed to fetch user contracts');
  }

  const data: ApiResponse<Contract[]> = await response.json();
  return { contracts: data.data, aggregation: data.aggregation };
};


export const getContract = async (id: number): Promise<Contract> => {
  const response = await authenticatedFetch(`${API_BASE_URL}/contracts/${id}`, {
    method: 'GET',
    headers: getAuthHeaders(),
  });

  if (!response.ok) {
    throw new Error('Failed to fetch contract');
  }

  const result: ApiResponse<Contract> = await response.json();
  return result.data;
};

export const createContract = async (data: CreateContractRequest): Promise<Contract> => {
  const response = await authenticatedFetch(`${API_BASE_URL}/contracts`, {
    method: 'POST',
    headers: getAuthHeaders(),
    body: JSON.stringify(data),
  });

  if (!response.ok) {
    const error = await response.json();
    throw new Error(error.message || 'Failed to create contract');
  }

  const result: ApiResponse<Contract> = await response.json();
  return result.data;
};

export const updateContract = async (id: number, data: UpdateContractRequest): Promise<Contract> => {
  const response = await authenticatedFetch(`${API_BASE_URL}/contracts/${id}`, {
    method: 'PUT',
    headers: getAuthHeaders(),
    body: JSON.stringify(data),
  });

  if (!response.ok) {
    const error = await response.json();
    throw new Error(error.message || 'Failed to update contract');
  }

  const result: ApiResponse<Contract> = await response.json();
  return result.data;
};

export const deleteContract = async (id: number): Promise<void> => {
  const response = await authenticatedFetch(`${API_BASE_URL}/contracts/${id}`, {
    method: 'DELETE',
    headers: getAuthHeaders(),
  });

  if (!response.ok) {
    throw new Error('Failed to delete contract');
  }
};

// Helper functions to fetch users and groups for dropdowns
export const getUsers = async (): Promise<User[]> => {
  const response = await authenticatedFetch(`${API_BASE_URL}/users?page=1&pageSize=1000`, {
    method: 'GET',
    headers: getAuthHeaders(),
  });

  if (!response.ok) {
    throw new Error('Failed to fetch users');
  }

  const result: ApiResponse<{ items: User[]; totalCount: number }> = await response.json();
  return result.data.items.filter(user => user.isActive);
};

export const getGroups = async (): Promise<Group[]> => {
  const response = await authenticatedFetch(`${API_BASE_URL}/groups`, {
    method: 'GET',
    headers: getAuthHeaders(),
  });

  if (!response.ok) {
    throw new Error('Failed to fetch groups');
  }

  const result: ApiResponse<Group[]> = await response.json();
  return result.data.filter(group => group.isActive);
};

// Get contract by contract number
export const getContractByNumber = async (contractNumber: string): Promise<Contract> => {
  const response = await authenticatedFetch(`${API_BASE_URL}/contracts/number/${contractNumber}`, {
    method: 'GET',
    headers: getAuthHeaders(),
  });

  if (!response.ok) {
    const error = await response.json();
    throw new Error(error.message || 'Failed to fetch contract');
  }

  const result: ApiResponse<Contract> = await response.json();
  return result.data;
};

// Assign contract to current user
export const assignContract = async (contractNumber: string, matriculaNumber?: string, userMatriculaId?: number): Promise<Contract> => {
  const params = new URLSearchParams();
  if (matriculaNumber) params.append('matriculaNumber', matriculaNumber);
  if (userMatriculaId) params.append('userMatriculaId', userMatriculaId.toString());
  
  const queryString = params.toString();
  const url = `${API_BASE_URL}/users/assign-contract/${contractNumber}${queryString ? `?${queryString}` : ''}`;
  
  const response = await authenticatedFetch(url, {
    method: 'POST',
    headers: getAuthHeaders(),
  });

  if (!response.ok) {
    const error = await response.json();
    throw new Error(error.message || 'Failed to assign contract');
  }

  const result: ApiResponse<Contract> = await response.json();
  return result.data;
};

// Get historic production data
export const getHistoricProduction = async (
  startDate?: string,
  endDate?: string,
  userId?: string,
  showUnassigned?: boolean
): Promise<HistoricProductionResponse> => {
  const params = new URLSearchParams();
  if (startDate) params.append('startDate', startDate);
  if (endDate) params.append('endDate', endDate);
  if (userId) params.append('userId', userId);
  if (showUnassigned !== undefined) params.append('showUnassigned', showUnassigned.toString());

  const queryString = params.toString();
  const url = `${API_BASE_URL}/contracts/aggregation/historic-production${queryString ? `?${queryString}` : ''}`;

  const response = await authenticatedFetch(url, {
    method: 'GET',
    headers: getAuthHeaders(),
  });

  if (!response.ok) {
    throw new Error('Failed to fetch historic production');
  }

  const result: ApiResponse<HistoricProductionResponse> = await response.json();
  return result.data;
};
