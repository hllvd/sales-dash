import { IDataTransformer } from './IDataTransformer';

/**
 * Transformer that filters out specific columns by name
 */
export class ColumnFilterTransformer implements IDataTransformer {
  private columnsToRemove: Set<string>;

  /**
   * @param columnsToRemove - List of column names to remove (case-insensitive)
   */
  constructor(columnsToRemove: string[]) {
    this.columnsToRemove = new Set(columnsToRemove.map(col => col.toLowerCase()));
  }

  /**
   * Removes configured columns from each row
   * @param data Array of row objects
   * @returns Transformed data array
   */
  transform(data: any[]): any[] {
    if (!data || data.length === 0) {
      return data;
    }

    return data.map(row => {
      const newRow: any = {};
      
      Object.keys(row).forEach(key => {
        // Only keep column if its lowercased name is NOT in the removal set
        if (!this.columnsToRemove.has(key.toLowerCase())) {
          newRow[key] = row[key];
        }
      });

      return newRow;
    });
  }
}
