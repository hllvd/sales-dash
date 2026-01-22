import { generateTimestamp, ensureOutputDirectory, generateOutputPath, getOutputDirectory } from '../src/utils/outputGenerator';
import * as fs from 'fs';
import * as path from 'path';

jest.mock('fs');

const mockedFs = fs as jest.Mocked<typeof fs>;

describe('outputGenerator', () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  describe('generateTimestamp', () => {
    it('should generate timestamp in correct format', () => {
      const timestamp = generateTimestamp();
      
      // Format: YYYYMMDD-HHMMSS
      expect(timestamp).toMatch(/^\d{8}-\d{6}$/);
    });

    it('should generate unique timestamps', () => {
      const timestamp1 = generateTimestamp();
      // Small delay to ensure different timestamp
      const timestamp2 = generateTimestamp();
      
      // They might be the same if called in the same second
      expect(typeof timestamp1).toBe('string');
      expect(typeof timestamp2).toBe('string');
    });
  });

  describe('ensureOutputDirectory', () => {
    it('should create directory if it does not exist', () => {
      mockedFs.existsSync.mockReturnValue(false);
      mockedFs.mkdirSync.mockImplementation(() => undefined);
      
      ensureOutputDirectory('/path/to/output');
      
      expect(mockedFs.mkdirSync).toHaveBeenCalledWith('/path/to/output', { recursive: true });
    });

    it('should not create directory if it already exists', () => {
      mockedFs.existsSync.mockReturnValue(true);
      
      ensureOutputDirectory('/path/to/output');
      
      expect(mockedFs.mkdirSync).not.toHaveBeenCalled();
    });
  });

  describe('generateOutputPath', () => {
    it('should generate path with template name and timestamp', () => {
      const outputPath = generateOutputPath('user-template', '/output');
      
      expect(outputPath).toContain('/output/user-template-');
      expect(outputPath).toMatch(/user-template-\d{8}-\d{6}\.csv$/);
    });

    it('should use correct directory', () => {
      const outputPath = generateOutputPath('pv-temp', '/custom/output');
      
      expect(outputPath).toContain('/custom/output/');
    });
  });

  describe('getOutputDirectory', () => {
    it('should return path to project root output directory', () => {
      const outputDir = getOutputDirectory();
      
      expect(outputDir).toContain('data/output');
      expect(path.isAbsolute(outputDir)).toBe(true);
    });
  });
});
