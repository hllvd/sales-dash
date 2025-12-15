const API_BASE_URL = process.env.REACT_APP_API_URL || 'http://localhost:5017/api';

// TypeScript Interfaces
export interface Contract {
  id: number;
  contractNumber: string;
  userId?: string | null;
  userName?: string | null;
  totalAmount: number;
  groupId: number;
  groupName: string;
  pvId?: number;
  pvName?: string;
  status: 'Active' | 'Late1' | 'Late2' | 'Late3' | 'Defaulted';
  contractStartDate: string;
  contractEndDate: string | null;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
  contractType?: number;
  quota?: number;
  customerName?: string;
}

export interface CreateContractRequest {
  contractNumber: string;
  userId?: string | null;
  totalAmount: number;
  groupId: number;
  pvId?: number;
  status: 'Active' | 'Late1' | 'Late2' | 'Late3' | 'Defaulted';
  contractStartDate: string;
  contractEndDate?: string | null;
  contractType?: number;
  quota?: number;
  customerName?: string;
}

export interface UpdateContractRequest {
  contractNumber?: string;
  userId?: string;
  totalAmount?: number;
  groupId?: number;
  pvId?: number;
  status?: 'Active' | 'Late1' | 'Late2' | 'Late3' | 'Defaulted';
  contractStartDate?: string;
  contractEndDate?: string | null;
  isActive?: boolean;
  contractType?: number;
  quota?: number;
  customerName?: string;
}

export interface User {
  id: string;
  name: string;
  email: string;
  role: string;
  isActive: boolean;
  matricula?: string;
  isMatriculaOwner: boolean;
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
}

interface ApiResponse<T> {
  success: boolean;
  data: T;
  message: string;
  aggregation?: ContractAggregation;
}

// Helper function to get auth token
const getAuthToken = (): string | null => {
  return localStorage.getItem('token');
};

// Helper function to create headers
const getHeaders = (): HeadersInit => {
  const token = getAuthToken();
  return {
    'Content-Type': 'application/json',
    ...(token && { 'Authorization': `Bearer ${token}` }),
  };
};

// Contract CRUD Operations
export const getContracts = async (
  userId?: string,
  groupId?: number,
  startDate?: string,
  endDate?: string
): Promise<{ contracts: Contract[]; aggregation?: ContractAggregation }> => {
  const params = new URLSearchParams();
  if (userId) params.append('userId', userId);
  if (groupId) params.append('groupId', groupId.toString());
  if (startDate) params.append('startDate', startDate);
  if (endDate) params.append('endDate', endDate);

  const queryString = params.toString();
  const url = `${API_BASE_URL}/contracts${queryString ? `?${queryString}` : ''}`;

  const response = await fetch(url, {
    method: 'GET',
    headers: getHeaders(),
  });

  if (!response.ok) {
    throw new Error('Failed to fetch contracts');
  }

  const result: ApiResponse<Contract[]> = await response.json();
  return { contracts: result.data, aggregation: result.aggregation };
};

export const getContract = async (id: number): Promise<Contract> => {
  const response = await fetch(`${API_BASE_URL}/contracts/${id}`, {
    method: 'GET',
    headers: getHeaders(),
  });

  if (!response.ok) {
    throw new Error('Failed to fetch contract');
  }

  const result: ApiResponse<Contract> = await response.json();
  return result.data;
};

export const createContract = async (data: CreateContractRequest): Promise<Contract> => {
  const response = await fetch(`${API_BASE_URL}/contracts`, {
    method: 'POST',
    headers: getHeaders(),
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
  const response = await fetch(`${API_BASE_URL}/contracts/${id}`, {
    method: 'PUT',
    headers: getHeaders(),
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
  const response = await fetch(`${API_BASE_URL}/contracts/${id}`, {
    method: 'DELETE',
    headers: getHeaders(),
  });

  if (!response.ok) {
    throw new Error('Failed to delete contract');
  }
};

// Helper functions to fetch users and groups for dropdowns
export const getUsers = async (): Promise<User[]> => {
  const response = await fetch(`${API_BASE_URL}/users?page=1&pageSize=1000`, {
    method: 'GET',
    headers: getHeaders(),
  });

  if (!response.ok) {
    throw new Error('Failed to fetch users');
  }

  const result: ApiResponse<{ items: User[]; totalCount: number }> = await response.json();
  return result.data.items.filter(user => user.isActive);
};

export const getGroups = async (): Promise<Group[]> => {
  const response = await fetch(`${API_BASE_URL}/groups`, {
    method: 'GET',
    headers: getHeaders(),
  });

  if (!response.ok) {
    throw new Error('Failed to fetch groups');
  }

  const result: ApiResponse<Group[]> = await response.json();
  return result.data.filter(group => group.isActive);
};

// Get contract by contract number
export const getContractByNumber = async (contractNumber: string): Promise<Contract> => {
  const response = await fetch(`${API_BASE_URL}/contracts/number/${contractNumber}`, {
    method: 'GET',
    headers: getHeaders(),
  });

  if (!response.ok) {
    const error = await response.json();
    throw new Error(error.message || 'Failed to fetch contract');
  }

  const result: ApiResponse<Contract> = await response.json();
  return result.data;
};

// Assign contract to current user
export const assignContract = async (contractNumber: string): Promise<Contract> => {
  const response = await fetch(`${API_BASE_URL}/users/assign-contract/${contractNumber}`, {
    method: 'POST',
    headers: getHeaders(),
  });

  if (!response.ok) {
    const error = await response.json();
    throw new Error(error.message || 'Failed to assign contract');
  }

  const result: ApiResponse<Contract> = await response.json();
  return result.data;
};
