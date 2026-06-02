import config from '../config';
import { authenticatedFetch, getAuthHeaders } from '../utils/httpInterceptor';

const API_BASE_URL = config.apiUrl;

// Types
export interface ViewColumn {
  reportFilterId?: string;
}

export interface ViewRow {
  columns: ViewColumn[];
}

export interface ReportView {
  viewId: string;
  userId: string;
  name: string;
  description?: string;
  scope: 'private' | 'shared';
  rows: ViewRow[];
  allowedTeamIds?: number[];
  allowedRoles?: string[];
  createdAt: string;
  updatedAt: string;
}

export interface CreateReportViewRequest {
  name: string;
  description?: string;
  scope: 'private' | 'shared';
  rows: ViewRow[];
  allowedTeamIds?: number[];
  allowedRoles?: string[];
}

export interface UpdateReportViewRequest {
  name: string;
  description?: string;
  scope: 'private' | 'shared';
  rows: ViewRow[];
  allowedTeamIds?: number[];
  allowedRoles?: string[];
}

interface ApiResponse<T> {
  success: boolean;
  data: T;
  message?: string;
}

// API Functions
export const getReportViews = async (): Promise<ReportView[]> => {
  const response = await authenticatedFetch(`${API_BASE_URL}/report-views`, {
    method: 'GET',
    headers: getAuthHeaders(),
  });

  if (!response.ok) {
    throw new Error('Failed to fetch views');
  }

  const result: ApiResponse<ReportView[]> = await response.json();
  return result.data;
};

export const getReportView = async (id: string): Promise<ReportView> => {
  const response = await authenticatedFetch(`${API_BASE_URL}/report-views/${id}`, {
    method: 'GET',
    headers: getAuthHeaders(),
  });

  if (!response.ok) {
    throw new Error('Failed to fetch view');
  }

  const result: ApiResponse<ReportView> = await response.json();
  return result.data;
};

export const createReportView = async (data: CreateReportViewRequest): Promise<ReportView> => {
  const response = await authenticatedFetch(`${API_BASE_URL}/report-views`, {
    method: 'POST',
    headers: getAuthHeaders(),
    body: JSON.stringify(data),
  });

  if (!response.ok) {
    const error = await response.json();
    const message = error.errors && error.errors.length > 0 ? error.errors[0].message : 'Failed to create view';
    throw new Error(message);
  }

  const result: ApiResponse<ReportView> = await response.json();
  return result.data;
};

export const updateReportView = async (id: string, data: UpdateReportViewRequest): Promise<ReportView> => {
  const response = await authenticatedFetch(`${API_BASE_URL}/report-views/${id}`, {
    method: 'PUT',
    headers: getAuthHeaders(),
    body: JSON.stringify(data),
  });

  if (!response.ok) {
    const error = await response.json();
    const message = error.errors && error.errors.length > 0 ? error.errors[0].message : 'Failed to update view';
    throw new Error(message);
  }

  const result: ApiResponse<ReportView> = await response.json();
  return result.data;
};

export const deleteReportView = async (id: string): Promise<void> => {
  const response = await authenticatedFetch(`${API_BASE_URL}/report-views/${id}`, {
    method: 'DELETE',
    headers: getAuthHeaders(),
  });

  if (!response.ok) {
    const error = await response.json();
    throw new Error(error.message || 'Failed to delete view');
  }
};
