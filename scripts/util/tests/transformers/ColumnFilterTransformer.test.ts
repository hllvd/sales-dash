import { ColumnFilterTransformer } from '../../src/transformers/ColumnFilterTransformer';

describe('ColumnFilterTransformer', () => {
  it('should remove specified columns', () => {
    const data = [
      { id: 1, name: 'Alice', Total: 100, Numeric: 50 },
      { id: 2, name: 'Bob', Total: 200, Numeric: 60 }
    ];

    const transformer = new ColumnFilterTransformer(['Total', 'Numeric']);
    const result = transformer.transform(data);

    expect(result).toEqual([
      { id: 1, name: 'Alice' },
      { id: 2, name: 'Bob' }
    ]);
  });

  it('should be case-insensitive by default', () => {
    const data = [
      { id: 1, TOTAL: 100, numeric: 50 }
    ];

    const transformer = new ColumnFilterTransformer(['Total', 'Numeric']);
    const result = transformer.transform(data);

    expect(result).toEqual([
      { id: 1 }
    ]);
  });

  it('should handle data without the specified columns gracefully', () => {
    const data = [
      { id: 1, name: 'Alice' }
    ];

    const transformer = new ColumnFilterTransformer(['Total']);
    const result = transformer.transform(data);

    expect(result).toEqual([
      { id: 1, name: 'Alice' }
    ]);
  });

  it('should handle empty input', () => {
    const transformer = new ColumnFilterTransformer(['Total']);
    expect(transformer.transform([])).toEqual([]);
    // @ts-ignore
    expect(transformer.transform(null)).toBeNull();
  });
});
