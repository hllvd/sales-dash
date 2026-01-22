import * as fs from 'fs';
import * as path from 'path';
import { validateFileExists, validateCsvOrXlsxFile, validateInputFile } from '../src/utils/fileValidator';

// Mock fs module
jest.mock('fs');

const mockedFs = fs as jest.Mocked<typeof fs>;

describe('fileValidator', () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  describe('validateFileExists', () => {
    it('should throw error when file path is empty', () => {
      expect(() => validateFileExists('')).toThrow('Input file path is required');
    });

    it('should throw error when file does not exist', () => {
      mockedFs.existsSync.mockReturnValue(false);
      
      expect(() => validateFileExists('/path/to/nonexistent.csv')).toThrow('Input file not found');
    });

    it('should throw error when path is not a file', () => {
      mockedFs.existsSync.mockReturnValue(true);
      mockedFs.statSync.mockReturnValue({
        isFile: () => false
      } as fs.Stats);
      
      expect(() => validateFileExists('/path/to/directory')).toThrow('Path is not a file');
    });

    it('should not throw error for valid file', () => {
      mockedFs.existsSync.mockReturnValue(true);
      mockedFs.statSync.mockReturnValue({
        isFile: () => true
      } as fs.Stats);
      
      expect(() => validateFileExists('/path/to/valid.csv')).not.toThrow();
    });
  });

  describe('validateCsvOrXlsxFile', () => {
    it('should throw error for non-CSV/XLSX file', () => {
      expect(() => validateCsvOrXlsxFile('/path/to/file.txt')).toThrow('Input file must be a CSV or XLSX file');
    });

    it('should not throw error for CSV file', () => {
      expect(() => validateCsvOrXlsxFile('/path/to/file.csv')).not.toThrow();
    });

    it('should not throw error for XLSX file', () => {
      expect(() => validateCsvOrXlsxFile('/path/to/file.xlsx')).not.toThrow();
    });

    it('should handle uppercase extensions', () => {
      expect(() => validateCsvOrXlsxFile('/path/to/file.CSV')).not.toThrow();
      expect(() => validateCsvOrXlsxFile('/path/to/file.XLSX')).not.toThrow();
    });
  });

  describe('validateInputFile', () => {
    it('should validate both file existence and format', () => {
      mockedFs.existsSync.mockReturnValue(true);
      mockedFs.statSync.mockReturnValue({
        isFile: () => true
      } as fs.Stats);
      
      expect(() => validateInputFile('/path/to/valid.csv')).not.toThrow();
    });

    it('should throw error for invalid format', () => {
      mockedFs.existsSync.mockReturnValue(true);
      mockedFs.statSync.mockReturnValue({
        isFile: () => true
      } as fs.Stats);
      
      expect(() => validateInputFile('/path/to/file.txt')).toThrow('Input file must be a CSV or XLSX file');
    });
  });
});
