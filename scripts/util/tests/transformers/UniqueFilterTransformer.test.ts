import { UniqueFilterTransformer } from '../../src/transformers/UniqueFilterTransformer';

describe('UniqueFilterTransformer', () => {
  it('should remove duplicates based on field name', () => {
    const data = [
      { id: 1, name: 'Alice' },
      { id: 2, name: 'Bob' },
      { id: 3, name: 'Alice' } // Duplicate name
    ];

    const transformer = new UniqueFilterTransformer('name');
    const result = transformer.transform(data);

    expect(result).toEqual([
      { id: 1, name: 'Alice' },
      { id: 2, name: 'Bob' }
    ]);
  });

  it('should be case-insensitive', () => {
    const data = [
      { id: 1, name: 'Alice' },
      { id: 2, name: 'ALICE' } // Duplicate name (different case)
    ];

    const transformer = new UniqueFilterTransformer('name');
    const result = transformer.transform(data);

    expect(result).toEqual([
      { id: 1, name: 'Alice' }
    ]);
  });

  it('should handle trimming', () => {
    const data = [
      { id: 1, name: 'Alice' },
      { id: 2, name: ' Alice ' } // Duplicate name (with spaces)
    ];

    const transformer = new UniqueFilterTransformer('name');
    const result = transformer.transform(data);

    expect(result).toEqual([
      { id: 1, name: 'Alice' }
    ]);
  });

  it('should handle empty input', () => {
    const transformer = new UniqueFilterTransformer('name');
    expect(transformer.transform([])).toEqual([]);
  });
});
