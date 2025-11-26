const API_BASE_URL = process.env.REACT_APP_API_URL || 'http://localhost:5017/api';


interface ApiResponse<T> {
  success: boolean;
  data?: T;
  message: string;
}

interface PagedResponse<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface User {
  id: string;
  name: string;
  email: string;
  role: string;
  parentUserId?: string;
  parentUserName?: string;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface CreateUserRequest {
  name: string;
  email: string;
  password: string;
  role: string;
  parentUserId?: string;
}

export interface UpdateUserRequest {
  name?: string;
  email?: string;
  password?: string;
  role?: string;
  parentUserId?: string;
  isActive?: boolean;
}

const getAuthHeaders = (): HeadersInit => {
  const token = localStorage.getItem('token');
  return {
    'Content-Type': 'application/json',
    'Authorization': `Bearer ${token}`
  };
};

export const apiService = {
  async getUsers(page: number = 1, pageSize: number = 10, search?: string): Promise<ApiResponse<PagedResponse<User>>> {
    const params = new URLSearchParams({
      page: page.toString(),
      pageSize: pageSize.toString(),
    });
    
    if (search) {
      params.append('search', search);
    }

    const response = await fetch(`${API_BASE_URL}/users?${params}`, {
      headers: getAuthHeaders(),
    });

    if (!response.ok) {
      throw new Error('Failed to fetch users');
    }

    return response.json();
  },

  async getUser(id: string): Promise<ApiResponse<User>> {
    const response = await fetch(`${API_BASE_URL}/users/${id}`, {
      headers: getAuthHeaders(),
    });

    if (!response.ok) {
      throw new Error('Failed to fetch user');
    }

    return response.json();
  },

  async createUser(userData: CreateUserRequest): Promise<ApiResponse<User>> {
    const response = await fetch(`${API_BASE_URL}/users/register`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify(userData),
    });

    if (!response.ok) {
      const error = await response.json();
      throw new Error(error.message || 'Failed to create user');
    }

    return response.json();
  },

  async updateUser(id: string, userData: UpdateUserRequest): Promise<ApiResponse<User>> {
    const response = await fetch(`${API_BASE_URL}/users/${id}`, {
      method: 'PUT',
      headers: getAuthHeaders(),
      body: JSON.stringify(userData),
    });

    if (!response.ok) {
      const error = await response.json();
      throw new Error(error.message || 'Failed to update user');
    }

    return response.json();
  },

  async deleteUser(id: string): Promise<ApiResponse<object>> {
    const response = await fetch(`${API_BASE_URL}/users/${id}`, {
      method: 'DELETE',
      headers: getAuthHeaders(),
    });

    if (!response.ok) {
      throw new Error('Failed to delete user');
    }

    return response.json();
  },
};
