/**
 * JWT utility functions for token validation and expiration checking
 */

interface JwtPayload {
  exp?: number
  iat?: number
  [key: string]: any
}

/**
 * Decode a JWT token without verification (client-side only)
 * @param token - The JWT token string
 * @returns The decoded payload or null if invalid
 */
export const decodeJwt = (token: string): JwtPayload | null => {
  try {
    const parts = token.split('.')
    if (parts.length !== 3) {
      return null
    }

    const payload = parts[1]
    const decoded = atob(payload.replace(/-/g, '+').replace(/_/g, '/'))
    return JSON.parse(decoded)
  } catch (error) {
    console.error('Failed to decode JWT:', error)
    return null
  }
}

/**
 * Check if a JWT token is expired
 * @param token - The JWT token string
 * @returns true if expired, false if still valid
 */
export const isTokenExpired = (token: string): boolean => {
  const payload = decodeJwt(token)
  
  if (!payload || !payload.exp) {
    // If we can't decode or there's no expiration, consider it expired
    return true
  }

  // exp is in seconds, Date.now() is in milliseconds
  const currentTime = Math.floor(Date.now() / 1000)
  return payload.exp < currentTime
}

/**
 * Get the token from localStorage
 * @returns The token string or null if not found
 */
export const getToken = (): string | null => {
  return localStorage.getItem('token')
}

/**
 * Check if the current token is valid (exists and not expired)
 * @returns true if valid, false otherwise
 */
export const isTokenValid = (): boolean => {
  const token = getToken()
  if (!token) {
    return false
  }
  return !isTokenExpired(token)
}

/**
 * Clear authentication data and redirect to login
 */
export const handleAuthenticationFailure = (): void => {
  localStorage.removeItem('token')
  // Force a full page reload to the login page
  window.location.hash = ''
  window.location.reload()
}
