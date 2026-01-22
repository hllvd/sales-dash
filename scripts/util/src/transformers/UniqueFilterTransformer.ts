import { IDataTransformer } from './IDataTransformer';

/**
 * Transformer that filters out duplicates based on a specific field
 */
export class UniqueFilterTransformer implements IDataTransformer {
  private fieldName: string;

  /**
   * @param fieldName - The field name to check for uniqueness
   */
  constructor(fieldName: string) {
    this.fieldName = fieldName;
  }

  /**
   * Removes rows with duplicate values in the configured field.
   * Keeps the first occurrence.
   * @param data Array of row objects
   * @returns Transformed data array
   */
  transform(data: any[]): any[] {
    if (!data || data.length === 0) {
      return data;
    }

    const seen = new Set<string>();
    return data.filter(row => {
      const value = row[this.fieldName];
      
      // If value is missing or null, we might want to keep it or filter it?
      // Assuming we treat missing values as "undefined" string key or similar.
      // For user template, empty name might be invalid, but let's just treat it as a value.
      const key = String(value).toLowerCase().trim(); // Case-insensitive deduplication for names?
       
      if (seen.has(key)) {
        return false;
      }
      
      seen.add(key);
      return true;
    });
  }
}
