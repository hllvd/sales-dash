import { apiService } from './apiService';

export interface ScrapeConfig {
    id: number;
    userId: string;
    store: string;
    matricula: string;
    isEnabled: boolean;
    createdAt: string;
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
        const response = await apiService.get(`${ENDPOINT_PREFIX}/configs/me`);
        return response.data;
    },

    saveConfig: async (data: Partial<ScrapeConfig>): Promise<ScrapeConfig> => {
        const response = await apiService.post(`${ENDPOINT_PREFIX}/configs`, data);
        return response.data;
    },

    getJobs: async (): Promise<ScrapeJob[]> => {
        const response = await apiService.get(`${ENDPOINT_PREFIX}/jobs/me`);
        return response.data;
    },

    triggerScrape: async (configId: number): Promise<{ jobId: string }> => {
        const response = await apiService.post(`${ENDPOINT_PREFIX}/jobs/${configId}`, {});
        return response.data;
    }
};
