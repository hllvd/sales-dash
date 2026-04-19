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
        return apiService.get(`${ENDPOINT_PREFIX}/configs/me`);
    },

    saveConfig: async (data: Partial<ScrapeConfig>): Promise<ScrapeConfig> => {
        return apiService.post(`${ENDPOINT_PREFIX}/configs`, data);
    },

    getJobs: async (): Promise<ScrapeJob[]> => {
        return apiService.get(`${ENDPOINT_PREFIX}/jobs/me`);
    },

    triggerScrape: async (configId: number): Promise<{ jobId: string }> => {
        return apiService.post(`${ENDPOINT_PREFIX}/jobs/${configId}`, {});
    }
};
