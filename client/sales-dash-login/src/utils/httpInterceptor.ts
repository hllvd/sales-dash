/**
 * HTTP interceptor wrapper around fetch to handle authentication errors
 */

/**
 * Clear authentication data and redirect to login
 */
const handleAuthenticationFailure = (): void => {
  localStorage.removeItem('token')
  // Force a full page reload to the login page
  window.location.hash = ''
  window.location.reload()
}

interface FetchOptions extends RequestInit {
  skipAuthCheck?: boolean // Allow skipping auth check for login endpoint
}

/**
 * Wrapper around fetch that intercepts responses and handles authentication errors
 * @param url - The URL to fetch
 * @param options - Fetch options
 * @returns The fetch response
 */
export const authenticatedFetch = async (
  url: string,
  options: FetchOptions = {}
): Promise<Response> => {
  const { skipAuthCheck, ...fetchOptions } = options

  try {
    const response = await fetch(url, fetchOptions)

    // Intercept 401 responses
    if (response.status === 401) {
      // Try to parse the error response
      let errorData: any = null
      try {
        const clonedResponse = response.clone()
        errorData = await clonedResponse.json()
      } catch (e) {
        // If we can't parse JSON, continue with null errorData
      }

      // Check for specific authentication failure messages in response body
      const errorMessage = errorData?.message || errorData?.error || ''
      
      // Also check the www-authenticate header for token expiration
      const wwwAuthenticate = response.headers.get('www-authenticate') || ''
      
      const isAuthenticationFailure =
        errorMessage.includes('TOKEN_EXPIRED') ||
        errorMessage.includes('INVALID_TOKEN') ||
        wwwAuthenticate.includes('invalid_token') ||
        wwwAuthenticate.includes('expired')

      if (isAuthenticationFailure) {
        console.warn('Authentication failure detected:', errorMessage || wwwAuthenticate)
        handleAuthenticationFailure()
        return Promise.reject(new Error('Authentication failed'))
      }

      // If we reach here, it's a 401 but likely a permission issue, not session expiration
      // Let the calling code handle it normally
    }

    return response
  } catch (error) {
    // Network errors or other fetch failures
    throw error
  }
}

/**
 * Helper to create headers with authentication token
 * @param additionalHeaders - Additional headers to include
 * @returns Headers object with Authorization token
 */
export const getAuthHeaders = (
  additionalHeaders: HeadersInit = {}
): HeadersInit => {
  const token = localStorage.getItem('token')
  return {
    'Content-Type': 'application/json',
    Authorization: `Bearer ${token}`,
    ...additionalHeaders,
  }
}
