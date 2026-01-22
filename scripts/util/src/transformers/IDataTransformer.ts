/**
 * Interface for data transformation components
 */
export interface IDataTransformer {
  /**
   * Transforms the input data array
   * @param data Array of row objects to transform
   * @returns Transformed data array
   */
  transform(data: any[]): any[];
}
