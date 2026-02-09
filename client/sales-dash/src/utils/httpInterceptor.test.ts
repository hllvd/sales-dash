/**
 * Unit tests for HTTP interceptor
 */

import { authenticatedFetch, getAuthHeaders } from './httpInterceptor'

// Mock fetch globally
global.fetch = jest.fn()

// Mock localStorage
const localStorageMock = (() => {
  let store: Record<string, string> = {}
  return {
    getItem: (key: string) => store[key] || null,
    setItem: (key: string, value: string) => {
      store[key] = value
    },
    removeItem: (key: string) => {
      delete store[key]
    },
    clear: () => {
      store = {}
    },
  }
})()

Object.defineProperty(window, 'localStorage', {
  value: localStorageMock,
})

// Mock window.location
delete (window as any).location
window.location = {
  hash: '',
  reload: jest.fn(),
} as any

describe('httpInterceptor', () => {
  beforeEach(() => {
    jest.clearAllMocks()
    localStorageMock.clear()
    window.location.hash = ''
  })

  describe('getAuthHeaders', () => {
    it('should return headers with Authorization token', () => {
      localStorageMock.setItem('token', 'test-token-123')

      const headers = getAuthHeaders()

      expect(headers).toEqual({
        'Content-Type': 'application/json',
        Authorization: 'Bearer test-token-123',
      })
    })

    it('should include additional headers', () => {
      localStorageMock.setItem('token', 'test-token-123')

      const headers = getAuthHeaders({ 'X-Custom-Header': 'custom-value' })

      expect(headers).toEqual({
        'Content-Type': 'application/json',
        Authorization: 'Bearer test-token-123',
        'X-Custom-Header': 'custom-value',
      })
    })

    it('should handle missing token', () => {
      const headers = getAuthHeaders()

      expect(headers).toEqual({
        'Content-Type': 'application/json',
        Authorization: 'Bearer null',
      })
    })
  })

  describe('authenticatedFetch', () => {
    it('should pass through successful responses', async () => {
      const mockResponse = {
        ok: true,
        status: 200,
        json: async () => ({ data: 'test' }),
      } as Response

      ;(global.fetch as jest.Mock).mockResolvedValue(mockResponse)

      const response = await authenticatedFetch('https://api.example.com/test')

      expect(response).toBe(mockResponse)
      expect(global.fetch).toHaveBeenCalledWith('https://api.example.com/test', {})
    })

    it('should redirect on 401 with TOKEN_EXPIRED message', async () => {
      localStorageMock.setItem('token', 'test-token')

      const mockResponse = {
        ok: false,
        status: 401,
        clone: () => ({
          json: async () => ({ message: 'TOKEN_EXPIRED' }),
        }),
      } as Response

      ;(global.fetch as jest.Mock).mockResolvedValue(mockResponse)

      await expect(
        authenticatedFetch('https://api.example.com/test')
      ).rejects.toThrow('Authentication failed')

      expect(localStorageMock.getItem('token')).toBeNull()
      expect(window.location.hash).toBe('')
      expect(window.location.reload).toHaveBeenCalled()
    })

    it('should redirect on 401 with INVALID_TOKEN message', async () => {
      localStorageMock.setItem('token', 'test-token')

      const mockResponse = {
        ok: false,
        status: 401,
        clone: () => ({
          json: async () => ({ message: 'INVALID_TOKEN' }),
        }),
      } as Response

      ;(global.fetch as jest.Mock).mockResolvedValue(mockResponse)

      await expect(
        authenticatedFetch('https://api.example.com/test')
      ).rejects.toThrow('Authentication failed')

      expect(localStorageMock.getItem('token')).toBeNull()
      expect(window.location.reload).toHaveBeenCalled()
    })

    it('should redirect on 401 with TOKEN_EXPIRED in error field', async () => {
      localStorageMock.setItem('token', 'test-token')

      const mockResponse = {
        ok: false,
        status: 401,
        clone: () => ({
          json: async () => ({ error: 'TOKEN_EXPIRED: Your session has expired' }),
        }),
      } as Response

      ;(global.fetch as jest.Mock).mockResolvedValue(mockResponse)

      await expect(
        authenticatedFetch('https://api.example.com/test')
      ).rejects.toThrow('Authentication failed')

      expect(localStorageMock.getItem('token')).toBeNull()
    })

    it('should NOT redirect on 401 without TOKEN_EXPIRED or INVALID_TOKEN', async () => {
      localStorageMock.setItem('token', 'test-token')

      const mockResponse = {
        ok: false,
        status: 401,
        clone: () => ({
          json: async () => ({ message: 'Insufficient permissions' }),
        }),
      } as Response

      ;(global.fetch as jest.Mock).mockResolvedValue(mockResponse)

      const response = await authenticatedFetch('https://api.example.com/test')

      expect(response).toBe(mockResponse)
      expect(localStorageMock.getItem('token')).toBe('test-token')
      expect(window.location.reload).not.toHaveBeenCalled()
    })

    it('should NOT redirect on 401 with generic unauthorized message', async () => {
      localStorageMock.setItem('token', 'test-token')

      const mockResponse = {
        ok: false,
        status: 401,
        clone: () => ({
          json: async () => ({ message: 'You do not have access to this resource' }),
        }),
      } as Response

      ;(global.fetch as jest.Mock).mockResolvedValue(mockResponse)

      const response = await authenticatedFetch('https://api.example.com/test')

      expect(response).toBe(mockResponse)
      expect(localStorageMock.getItem('token')).toBe('test-token')
      expect(window.location.reload).not.toHaveBeenCalled()
    })

    it('should handle 401 with unparseable JSON response', async () => {
      localStorageMock.setItem('token', 'test-token')

      const mockResponse = {
        ok: false,
        status: 401,
        clone: () => ({
          json: async () => {
            throw new Error('Invalid JSON')
          },
        }),
      } as unknown as Response

      ;(global.fetch as jest.Mock).mockResolvedValue(mockResponse)

      const response = await authenticatedFetch('https://api.example.com/test')

      // Should not redirect if we can't parse the error
      expect(response).toBe(mockResponse)
      expect(localStorageMock.getItem('token')).toBe('test-token')
      expect(window.location.reload).not.toHaveBeenCalled()
    })

    it('should pass through non-401 error responses', async () => {
      const mockResponse = {
        ok: false,
        status: 403,
        json: async () => ({ message: 'Forbidden' }),
      } as Response

      ;(global.fetch as jest.Mock).mockResolvedValue(mockResponse)

      const response = await authenticatedFetch('https://api.example.com/test')

      expect(response).toBe(mockResponse)
      expect(window.location.reload).not.toHaveBeenCalled()
    })

    it('should handle network errors', async () => {
      ;(global.fetch as jest.Mock).mockRejectedValue(new Error('Network error'))

      await expect(
        authenticatedFetch('https://api.example.com/test')
      ).rejects.toThrow('Network error')

      expect(window.location.reload).not.toHaveBeenCalled()
    })

    it('should forward fetch options', async () => {
      const mockResponse = {
        ok: true,
        status: 200,
      } as Response

      ;(global.fetch as jest.Mock).mockResolvedValue(mockResponse)

      await authenticatedFetch('https://api.example.com/test', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ test: 'data' }),
      })

      expect(global.fetch).toHaveBeenCalledWith('https://api.example.com/test', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ test: 'data' }),
      })
    })
  })
})
