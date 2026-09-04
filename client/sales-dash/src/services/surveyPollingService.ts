import { apiService } from './apiService';
import { SurveyAssignmentDto } from '../types/Survey';

const PENDING_STORAGE_KEY = 'survey_pending_questions';
const PROMPT_LOG_STORAGE_KEY = 'survey_daily_prompt_log';
const POLL_INTERVAL_MS = 8 * 60 * 60 * 1000; // 3 times a day (every 8 hours)
const MAX_PROMPTS_PER_DAY = 3;

interface PromptRecord {
  date: string; // YYYY-MM-DD
  count: number;
}

type PromptLog = Record<number, PromptRecord>;

function getTodayString(): string {
  return new Date().toISOString().split('T')[0];
}

function loadLocalPending(): SurveyAssignmentDto[] {
  try {
    const raw = localStorage.getItem(PENDING_STORAGE_KEY);
    if (!raw) return [];
    const list: SurveyAssignmentDto[] = JSON.parse(raw);
    const now = new Date();
    // Filter out locally expired
    return list.filter((item) => new Date(item.expiresAt) > now);
  } catch {
    return [];
  }
}

function saveLocalPending(list: SurveyAssignmentDto[]): void {
  try {
    localStorage.setItem(PENDING_STORAGE_KEY, JSON.stringify(list));
  } catch (err) {
    console.error('Failed to save pending surveys in localStorage', err);
  }
}

function loadPromptLog(): PromptLog {
  try {
    const raw = localStorage.getItem(PROMPT_LOG_STORAGE_KEY);
    if (!raw) return {};
    return JSON.parse(raw);
  } catch {
    return {};
  }
}

function savePromptLog(log: PromptLog): void {
  try {
    localStorage.setItem(PROMPT_LOG_STORAGE_KEY, JSON.stringify(log));
  } catch (err) {
    console.error('Failed to save survey prompt log in localStorage', err);
  }
}

class SurveyPollingService {
  private timer: NodeJS.Timeout | null = null;
  private isRunning: boolean = false;

  public start(): void {
    if (this.isRunning) return;
    this.isRunning = true;

    // Check immediately on app startup / login
    this.refreshPending().catch((err) => {
      console.warn('Initial survey check failed:', err);
    });

    // Setup polling 3 times a day
    this.timer = setInterval(() => {
      this.refreshPending().catch((err) => {
        console.warn('Survey poll check failed:', err);
      });
    }, POLL_INTERVAL_MS);
  }

  public stop(): void {
    if (this.timer) {
      clearInterval(this.timer);
      this.timer = null;
    }
    this.isRunning = false;
  }

  public async refreshPending(): Promise<SurveyAssignmentDto[]> {
    const token = localStorage.getItem('token');
    if (!token) {
      this.clearLocal();
      return [];
    }

    try {
      const res = await apiService.getPendingSurveys();
      if (res.success && Array.isArray(res.data)) {
        const now = new Date();
        const validList = res.data.filter((item) => new Date(item.expiresAt) > now);
        saveLocalPending(validList);
        this.notifyUpdate();
        return validList;
      }
    } catch (err) {
      console.warn('Could not fetch pending surveys from API, fallback to local storage', err);
    }

    const localList = loadLocalPending();
    this.notifyUpdate();
    return localList;
  }

  public getPendingList(): SurveyAssignmentDto[] {
    return loadLocalPending();
  }

  public canPromptQuestion(assignmentId: number): boolean {
    const list = loadLocalPending();
    const item = list.find((q) => q.assignmentId === assignmentId);
    if (!item) return false;

    // Check TTL (2 days expiration)
    if (new Date(item.expiresAt) <= new Date()) {
      return false;
    }

    const log = loadPromptLog();
    const today = getTodayString();
    const record = log[assignmentId];

    if (!record || record.date !== today) {
      return true;
    }

    return record.count < MAX_PROMPTS_PER_DAY;
  }

  public recordQuestionPrompt(assignmentId: number): void {
    const log = loadPromptLog();
    const today = getTodayString();
    const record = log[assignmentId];

    if (!record || record.date !== today) {
      log[assignmentId] = { date: today, count: 1 };
    } else {
      log[assignmentId] = { date: today, count: record.count + 1 };
    }

    savePromptLog(log);
  }

  public getNextPromptableQuestion(): SurveyAssignmentDto | null {
    const list = loadLocalPending();
    for (const item of list) {
      if (this.canPromptQuestion(item.assignmentId)) {
        return item;
      }
    }
    return null;
  }

  public removePendingQuestion(assignmentId: number): void {
    const list = loadLocalPending().filter((item) => item.assignmentId !== assignmentId);
    saveLocalPending(list);

    const log = loadPromptLog();
    delete log[assignmentId];
    savePromptLog(log);

    this.notifyUpdate();
  }

  public clearLocal(): void {
    localStorage.removeItem(PENDING_STORAGE_KEY);
    localStorage.removeItem(PROMPT_LOG_STORAGE_KEY);
    this.notifyUpdate();
  }

  private notifyUpdate(): void {
    window.dispatchEvent(new CustomEvent('survey:updated'));
  }
}

export const surveyPollingService = new SurveyPollingService();
