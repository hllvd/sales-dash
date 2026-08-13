import { apiService } from './apiService';

export interface ScrapeConfig {
    id: number;
    userId: string;
    store: string;
    matricula: string;
    credentialStatus?: 'ok' | 'wrong-password' | null;
    isEnabled: boolean;
    createdAt: string;
    updatedAt: string;
}

export interface ScrapeConfigRequest {
    id?: number;
    store: string;
    matricula: string;
    powerBiPassword?: string;
    testOnSave?: boolean;
}

export interface ScrapeJob {
    jobId: string;
    runId?: string;
    userId: string;
    userEmail?: string;
    status: string;
    store: string;
    matricula: string;
    rowCount: number;
    errorMessage?: string;
    fileRelativePath?: string;
    createdAt: string;
    completedAt?: string;
    authStatus?: 'success' | 'invalid-credentials' | 'timeout' | 'error' | string;
    authMessage?: string;
    powerBiLoaded?: boolean;
    authSteps?: string[];
}

export interface ScrapeRunSummary {
    runId: string;
    userId: string;
    userEmail: string;
    finalStatus: string;
    createdAt: string;
    totalJobs: number;
    succeededJobs: number;
    failedJobs: number;
    totalRowCount: number;
    stores: string[];
    matriculas: string[];
}

export interface ScrapeRunDetail {
    runId: string;
    userId: string;
    userEmail: string;
    finalStatus: string;
    createdAt: string;
    jobs: ScrapeJob[];
}

const ENDPOINT_PREFIX = '/scrape';

export const scrapeService = {
    getConfigs: async (): Promise<ScrapeConfig[]> => {
        return apiService.get(`${ENDPOINT_PREFIX}/configs/me`);
    },

    saveConfig: async (data: ScrapeConfigRequest): Promise<ScrapeConfig> => {
        return apiService.post(`${ENDPOINT_PREFIX}/configs`, data);
    },

    deleteConfig: async (id: number): Promise<void> => {
        return apiService.delete(`${ENDPOINT_PREFIX}/configs/${id}`);
    },

    testAuth: async (id: number, force: boolean = false): Promise<{ success: boolean; message: string; steps?: string[]; requiresConfirmation?: boolean }> => {
        return apiService.post(`${ENDPOINT_PREFIX}/configs/${id}/test-auth?force=${force}`, {});
    },

    getJobs: async (): Promise<ScrapeJob[]> => {
        return apiService.get(`${ENDPOINT_PREFIX}/jobs/me`);
    },

    getRuns: async (): Promise<ScrapeRunSummary[]> => {
        return apiService.get(`${ENDPOINT_PREFIX}/runs/me`);
    },

    getRunDetail: async (runId: string): Promise<ScrapeRunDetail> => {
        return apiService.get(`${ENDPOINT_PREFIX}/runs/${encodeURIComponent(runId)}`);
    },

    triggerScrape: async (configId: number): Promise<{ jobId: string; runId: string }> => {
        return apiService.post(`${ENDPOINT_PREFIX}/jobs/${configId}`, {});
    }
};
