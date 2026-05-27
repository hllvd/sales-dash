import config from '../config';
import { authenticatedFetch, getAuthHeaders } from '../utils/httpInterceptor';

const API_BASE_URL = config.apiUrl;

// Types
export interface FilterConfig {
  matriculas?: string[];
  startDate?: string;
  endDate?: string;
  relativeStartDate?: string;
  relativeEndDate?: string;
  currentUserAsParent?: boolean;
  emails?: string[];
  groups?: number[];
  pvs?: number[];
  statuses?: string[];
  statusOperator?: 'or' | 'and';
}

export interface OutputColumn {
  source: string;
  field: string;
  label: string;
  order: number;
  format?: string;
}

export interface ReportFilter {
  filterId: string;
  userId: string;
  name: string;
  description?: string;
  scope: 'private' | 'shared';
  filterConfig: FilterConfig;
  outputColumns: OutputColumn[];
  groupByEmail: boolean;
  groupByTeam: boolean;
  orderByField?: string;
  orderByDirection?: 'asc' | 'desc';
  createdAt: string;
  updatedAt: string;
}

export interface CreateReportFilterRequest {
  name: string;
  description?: string;
  scope: 'private' | 'shared';
  filterConfig: FilterConfig;
  outputColumns: OutputColumn[];
  groupByEmail: boolean;
  groupByTeam: boolean;
  orderByField?: string;
  orderByDirection?: 'asc' | 'desc';
}

export interface UpdateReportFilterRequest {
  name: string;
  description?: string;
  scope: 'private' | 'shared';
  filterConfig: FilterConfig;
  outputColumns: OutputColumn[];
  groupByEmail: boolean;
  groupByTeam: boolean;
  orderByField?: string;
  orderByDirection?: 'asc' | 'desc';
}

export interface SourceColumns {
  source: string;
  fields: string[];
}

export interface AvailableColumnsResponse {
  sources: SourceColumns[];
}

export interface ReportResultsResponse {
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  columns: OutputColumn[];
  rows: Record<string, any>[];
}

interface ApiResponse<T> {
  success: boolean;
  data: T;
  message?: string;
}

// API Functions
export const getReportFilters = async (): Promise<ReportFilter[]> => {
  const response = await authenticatedFetch(`${API_BASE_URL}/report-filters`, {
    method: 'GET',
    headers: getAuthHeaders(),
  });

  if (!response.ok) {
    throw new Error('Failed to fetch report filters');
  }

  const result: ApiResponse<ReportFilter[]> = await response.json();
  return result.data;
};

export const getAvailableColumns = async (): Promise<AvailableColumnsResponse> => {
  const response = await authenticatedFetch(`${API_BASE_URL}/report-filters/columns/available`, {
    method: 'GET',
    headers: getAuthHeaders(),
  });

  if (!response.ok) {
    throw new Error('Failed to fetch available columns');
  }

  const result: ApiResponse<AvailableColumnsResponse> = await response.json();
  return result.data;
};

export const getReportFilter = async (id: string): Promise<ReportFilter> => {
  const response = await authenticatedFetch(`${API_BASE_URL}/report-filters/${id}`, {
    method: 'GET',
    headers: getAuthHeaders(),
  });

  if (!response.ok) {
    throw new Error('Failed to fetch report filter');
  }

  const result: ApiResponse<ReportFilter> = await response.json();
  return result.data;
};

export const createReportFilter = async (data: CreateReportFilterRequest): Promise<ReportFilter> => {
  const response = await authenticatedFetch(`${API_BASE_URL}/report-filters`, {
    method: 'POST',
    headers: getAuthHeaders(),
    body: JSON.stringify(data),
  });

  if (!response.ok) {
    const error = await response.json();
    throw new Error(error.message || 'Failed to create report filter');
  }

  const result: ApiResponse<ReportFilter> = await response.json();
  return result.data;
};

export const updateReportFilter = async (id: string, data: UpdateReportFilterRequest): Promise<ReportFilter> => {
  const response = await authenticatedFetch(`${API_BASE_URL}/report-filters/${id}`, {
    method: 'PUT',
    headers: getAuthHeaders(),
    body: JSON.stringify(data),
  });

  if (!response.ok) {
    const error = await response.json();
    throw new Error(error.message || 'Failed to update report filter');
  }

  const result: ApiResponse<ReportFilter> = await response.json();
  return result.data;
};

export const deleteReportFilter = async (id: string): Promise<void> => {
  const response = await authenticatedFetch(`${API_BASE_URL}/report-filters/${id}`, {
    method: 'DELETE',
    headers: getAuthHeaders(),
  });

  if (!response.ok) {
    const error = await response.json();
    throw new Error(error.message || 'Failed to delete report filter');
  }
};

export const getReportResults = async (
  id: string,
  page: number = 1,
  pageSize: number = 25
): Promise<ReportResultsResponse> => {
  const params = new URLSearchParams({
    page: page.toString(),
    pageSize: pageSize.toString()
  });

  const response = await authenticatedFetch(`${API_BASE_URL}/report-filters/${id}/results?${params.toString()}`, {
    method: 'GET',
    headers: getAuthHeaders(),
  });

  if (!response.ok) {
    throw new Error('Failed to fetch report results');
  }

  const result: ApiResponse<ReportResultsResponse> = await response.json();
  return result.data;
};

export const startReportExport = async (filterId: string): Promise<any> => {
  const response = await authenticatedFetch(`${API_BASE_URL}/report-filters/${filterId}/export`, {
    method: 'POST',
    headers: getAuthHeaders(),
  });

  if (!response.ok) {
    throw new Error('Failed to start report export');
  }

  const result = await response.json();
  return result.data;
};

export const getReportExportStatus = async (jobId: string): Promise<any> => {
  const response = await authenticatedFetch(`${API_BASE_URL}/report-filters/export/${jobId}/status`, {
    method: 'GET',
    headers: getAuthHeaders(),
  });

  if (!response.ok) {
    throw new Error('Failed to get export status');
  }

  const result = await response.json();
  return result.data;
};

export const getReportExportStatusUrl = (jobId: string): string => {
  return `${API_BASE_URL}/report-filters/export/${jobId}/status`;
};

export const getReportExportDownloadUrl = (jobId: string): string => {
  return `${API_BASE_URL}/report-filters/export/${jobId}/download`;
};
