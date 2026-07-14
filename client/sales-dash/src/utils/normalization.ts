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

export const FIELD_TRANSLATIONS: Record<string, string> = {
  // Contracts Template Fields
  ContractNumber: "Número do Contrato",
  TotalAmount: "Valor Total",
  SaleStartDate: "Data de Venda",
  MatriculaNumber: "Matrícula",
  UserEmail: "Email do Usuário",
  Status: "Status",
  PvId: "ID do Ponto de Venda",
  PvName: "Nome do Ponto de Venda",
  Version: "Versão",
  Category: "Categoria",
  PlanoVenda: "Plano de Venda",

  // Users Template Fields
  Name: "Nome",
  Email: "Email",
  Role: "Função",
  ParentEmail: "Email do Pai",
  Matricula: "Matrícula",
  Owner_Matricula: "Proprietário da Matrícula",
  Password: "Senha"
};

export const getFriendlyFieldName = (field: string | null | undefined): string => {
  if (!field) return '';
  const normalizedKey = Object.keys(FIELD_TRANSLATIONS).find(
    key => key.toLowerCase() === field.toLowerCase()
  );
  return normalizedKey ? FIELD_TRANSLATIONS[normalizedKey] : field;
};

