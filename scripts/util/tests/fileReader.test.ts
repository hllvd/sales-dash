import { readInputFile } from '../src/utils/fileReader';
import * as fs from 'fs';
import * as path from 'path';
import * as XLSX from 'xlsx';

// Mock fs and XLSX
jest.mock('fs');
jest.mock('xlsx');
// Mock csv-parser (more complex, might use integration test or different mocking strategy)
// For unit test, we can mock the stream behavior or just test the XLSX part which is synchronous

const mockedFs = fs as jest.Mocked<typeof fs>;
const mockedXLSX = XLSX as jest.Mocked<typeof XLSX>;

describe('fileReader', () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  describe('readInputFile', () => {
    it('should read XLSX file correctly', async () => {
      const mockWorkbook = {
        SheetNames: ['Sheet1'],
        Sheets: {
          'Sheet1': {}
        }
      };
      
      const mockData = [{ valid: 'data' }];
      
      mockedXLSX.readFile.mockReturnValue(mockWorkbook as any);
      (mockedXLSX.utils as any).sheet_to_json = jest.fn().mockReturnValue(mockData);
      
      const result = await readInputFile('test.xlsx');
      
      expect(mockedXLSX.readFile).toHaveBeenCalledWith('test.xlsx');
      expect(result).toEqual(mockData);
    });

    it('should throw error if XLSX has no sheets', async () => {
      const mockWorkbook = {
        SheetNames: [],
        Sheets: {}
      };
      
      mockedXLSX.readFile.mockReturnValue(mockWorkbook as any);
      
      await expect(readInputFile('test.xlsx')).rejects.toThrow('XLSX file has no sheets');
    });

    // CSV testing requires mocking streams which is verbose.
    // Given the previous tests covered CSV reading via csv-parser, 
    // and we kept the logic similar, we can rely on integration tests for CSV.
  });
});
