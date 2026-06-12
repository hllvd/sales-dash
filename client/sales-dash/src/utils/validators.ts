/**
 * Validates a password against current security policy.
 * Requirement: At least 6 characters, one letter, and one number.
 * 
 * @param password The password to validate
 * @returns An object with isValid boolean and an error message if invalid
 */
export const validatePassword = (password: string): { isValid: boolean; message: string } => {
  if (!password) {
    return { isValid: false, message: 'A senha é obrigatória' };
  }

  const minLength = 6;
  if (password.length < minLength) {
    return { 
      isValid: false, 
      message: `A senha deve ter pelo menos ${minLength} caracteres` 
    };
  }

  return { isValid: true, message: '' };
};
