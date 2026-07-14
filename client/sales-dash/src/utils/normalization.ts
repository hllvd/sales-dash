/**
 * Normalizes a number string by removing leading zeros.
 * "012345" -> "12345"
 * "000" -> "0"
 * "" -> ""
 */
export const normalizeNumber = (value: string | null | undefined): string => {
  if (!value) return '';
  
  const trimmed = value.trim();
  if (!trimmed) return '';
  
  // Remove leading zeros
  const normalized = trimmed.replace(/^0+/, '');
  
  // If it was all zeros, return "0"
  if (normalized.length === 0 && trimmed.length > 0) {
    return '0';
  }
  
  return normalized;
};

/**
 * Normalizes a team name:
 * - Removes the word "Equipe" (case-insensitive) as a whole word
 * - Converts everything to lowercase
 * - Trims whitespace
 */
export const normalizeTeamName = (name: string | null | undefined): string => {
  if (!name) return '';
  return name
    .replace(/\bequipe\b/gi, '')
    .replace(/\s+/g, ' ') // Collapse multiple spaces
    .trim()
    .toLowerCase();
};

