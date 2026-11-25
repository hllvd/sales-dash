import { formatMonthYear, formatSingleMonth, hasDownloadableCsv } from './ScrapeRunDetailPage';
import { ScrapeRunDetail } from '../../services/scrapeService';

describe('ScrapeRunDetailPage - Month formatting', () => {
  describe('formatSingleMonth', () => {
    it('returns "Todas as datas" when string is empty or "Padrão"', () => {
      expect(formatSingleMonth('')).toBe('Todas as datas');
      expect(formatSingleMonth('   ')).toBe('Todas as datas');
      expect(formatSingleMonth('Padrão')).toBe('Todas as datas');
    });

    it('formats YYYY-MM into MM/YYYY', () => {
      expect(formatSingleMonth('2026-07')).toBe('07/2026');
      expect(formatSingleMonth('2026-09')).toBe('09/2026');
      expect(formatSingleMonth('2025-12')).toBe('12/2025');
    });

    it('formats YYYY-MM-DD into MM/YYYY', () => {
      expect(formatSingleMonth('2026-08-15')).toBe('08/2026');
    });

    it('returns original trimmed string if no hyphens', () => {
      expect(formatSingleMonth('custom-str')).toBe('str/custom');
      expect(formatSingleMonth('unknown')).toBe('unknown');
    });
  });

  describe('formatMonthYear', () => {
    it('returns "Todas as datas" when undefined, null, empty or Padrão', () => {
      expect(formatMonthYear(undefined)).toBe('Todas as datas');
      expect(formatMonthYear('')).toBe('Todas as datas');
      expect(formatMonthYear('   ')).toBe('Todas as datas');
      expect(formatMonthYear('Padrão')).toBe('Todas as datas');
    });

    it('formats a single date correctly', () => {
      expect(formatMonthYear('2026-07')).toBe('07/2026');
    });

    it('formats multiple comma-separated dates correctly', () => {
      expect(formatMonthYear('2026-07,2026-08,2026-09')).toBe('07/2026, 08/2026, 09/2026');
      expect(formatMonthYear('2026-01, 2026-02')).toBe('01/2026, 02/2026');
    });
  });

  describe('hasDownloadableCsv', () => {
    const baseDetail: ScrapeRunDetail = {
      runId: 'run-1',
      userId: 'u1',
      userEmail: 'superadmin@salesapp.com',
      finalStatus: 'Succeeded',
      createdAt: '2026-11-24T16:00:00Z',
      jobs: [],
    };

    it('returns false when detail is null or has no Succeeded job with s3Key', () => {
      expect(hasDownloadableCsv(null)).toBe(false);
      expect(hasDownloadableCsv(baseDetail)).toBe(false);
      expect(
        hasDownloadableCsv({
          ...baseDetail,
          jobs: [
            {
              jobId: 'j1',
              userId: 'u1',
              status: 'Succeeded',
              store: 'Loja',
              matricula: '123',
              rowCount: 10,
              createdAt: '2026-11-24T16:00:00Z',
            },
          ],
        })
      ).toBe(false);
      expect(
        hasDownloadableCsv({
          ...baseDetail,
          jobs: [
            {
              jobId: 'j2',
              userId: 'u1',
              status: 'Failed',
              store: 'Loja',
              matricula: '123',
              rowCount: 0,
              s3Key: 'scrape-results/f.csv',
              createdAt: '2026-11-24T16:00:00Z',
            },
          ],
        })
      ).toBe(false);
    });

    it('returns true when at least one Succeeded job has s3Key', () => {
      expect(
        hasDownloadableCsv({
          ...baseDetail,
          jobs: [
            {
              jobId: 'j3',
              userId: 'u1',
              status: 'Succeeded',
              store: 'Loja',
              matricula: '123',
              rowCount: 10,
              s3Bucket: 'hdev-sales-dash',
              s3Key: 'scrape-results/f.csv',
              createdAt: '2026-11-24T16:00:00Z',
            },
          ],
        })
      ).toBe(true);
    });
  });
});
