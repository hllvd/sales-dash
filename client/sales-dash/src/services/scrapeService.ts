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
    userId: string;
    status: string;
    store: string;
    matricula: string;
    rowCount: number;
    errorMessage?: string;
    fileRelativePath?: string;
    createdAt: string;
    completedAt?: string;
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

    testAuth: async (id: number): Promise<{ success: boolean; message: string }> => {
        return apiService.post(`${ENDPOINT_PREFIX}/configs/${id}/test-auth`, {});
    },

    getJobs: async (): Promise<ScrapeJob[]> => {
        return apiService.get(`${ENDPOINT_PREFIX}/jobs/me`);
    },

    triggerScrape: async (configId: number): Promise<{ jobId: string }> => {
        return apiService.post(`${ENDPOINT_PREFIX}/jobs/${configId}`, {});
    }
};
