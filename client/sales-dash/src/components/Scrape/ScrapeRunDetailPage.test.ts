import { formatMonthYear, formatSingleMonth } from './ScrapeRunDetailPage';

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
});
