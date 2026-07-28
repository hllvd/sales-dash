import { normalizeName } from './normalization';

describe('normalizeName', () => {
  it('should normalize uppercase names to Pascal Case', () => {
    expect(normalizeName('joão SILVA')).toBe('João Silva');
    expect(normalizeName('JOAO SILVa')).toBe('Joao Silva');
  });

  it('should preserve Portuguese particles in lowercase', () => {
    expect(normalizeName('maria da SILVA')).toBe('Maria da Silva');
    expect(normalizeName('ANA DE OLIVEIRA')).toBe('Ana de Oliveira');
    expect(normalizeName('CARLOS DOS SANTOS')).toBe('Carlos dos Santos');
  });

  it('should trim and collapse multiple spaces', () => {
    expect(normalizeName('  joão   silva  ')).toBe('João Silva');
  });

  it('should handle empty, null, and undefined values', () => {
    expect(normalizeName('')).toBe('');
    expect(normalizeName(null)).toBe('');
    expect(normalizeName(undefined)).toBe('');
  });
});
