import {
  getContracts,
  getContract,
  createContract,
  updateContract,
  deleteContract,
  getUsers,
  getGroups,
  CreateContractRequest,
  UpdateContractRequest,
  Contract,
} from './contractService';

// Mock fetch globally
global.fetch = jest.fn();

describe('contractService', () => {
  beforeEach(() => {
    // Clear all mocks before each test
    jest.clearAllMocks();
    // Set up localStorage mock
    Storage.prototype.getItem = jest.fn(() => 'mock-token');
  });

  describe('getContracts', () => {
    it('should fetch contracts without filters', async () => {
      const mockContracts = [
        {
          id: 1,
          contractNumber: 'C001',
          userId: 'user-1',
          userName: 'John Doe',
          totalAmount: 1000,
          groupId: 1,
          groupName: 'Group A',
          status: 'Active' as const,
          contractStartDate: '2024-01-01',
          contractEndDate: null,
          isActive: true,
          createdAt: '2024-01-01',
          updatedAt: '2024-01-01',
        },
      ];

      (global.fetch as jest.Mock).mockResolvedValueOnce({
        ok: true,
        json: async () => ({ success: true, data: mockContracts, message: 'Success' }),
      });

      const result = await getContracts();

      expect(global.fetch).toHaveBeenCalledWith(
        'http://localhost:5017/api/contracts',
        expect.objectContaining({
          method: 'GET',
          headers: expect.objectContaining({
            'Content-Type': 'application/json',
            Authorization: 'Bearer mock-token',
          }),
        })
      );
      expect(result).toEqual(mockContracts);
    });

    it('should fetch contracts with filters', async () => {
      const mockContracts: Contract[] = [];

      (global.fetch as jest.Mock).mockResolvedValueOnce({
        ok: true,
        json: async () => ({ success: true, data: mockContracts, message: 'Success' }),
      });

      await getContracts('user-1', 1, '2024-01-01', '2024-12-31');

      expect(global.fetch).toHaveBeenCalledWith(
        'http://localhost:5017/api/contracts?userId=user-1&groupId=1&startDate=2024-01-01&endDate=2024-12-31',
        expect.any(Object)
      );
    });

    it('should throw error when fetch fails', async () => {
      (global.fetch as jest.Mock).mockResolvedValueOnce({
        ok: false,
      });

      await expect(getContracts()).rejects.toThrow('Failed to fetch contracts');
    });
  });

  describe('getContract', () => {
    it('should fetch a single contract by id', async () => {
      const mockContract = {
        id: 1,
        contractNumber: 'C001',
        userId: 'user-1',
        userName: 'John Doe',
        totalAmount: 1000,
        groupId: 1,
        groupName: 'Group A',
        status: 'Active' as const,
        contractStartDate: '2024-01-01',
        contractEndDate: null,
        isActive: true,
        createdAt: '2024-01-01',
        updatedAt: '2024-01-01',
      };

      (global.fetch as jest.Mock).mockResolvedValueOnce({
        ok: true,
        json: async () => ({ success: true, data: mockContract, message: 'Success' }),
      });

      const result = await getContract(1);

      expect(global.fetch).toHaveBeenCalledWith(
        'http://localhost:5017/api/contracts/1',
        expect.any(Object)
      );
      expect(result).toEqual(mockContract);
    });
  });

  describe('createContract', () => {
    it('should create a new contract', async () => {
      const createData: CreateContractRequest = {
        contractNumber: 'C001',
        userId: 'user-1',
        totalAmount: 1000,
        groupId: 1,
        status: 'Active',
        contractStartDate: '2024-01-01',
      };

      const mockResponse = {
        id: 1,
        ...createData,
        userName: 'John Doe',
        groupName: 'Group A',
        contractEndDate: null,
        isActive: true,
        createdAt: '2024-01-01',
        updatedAt: '2024-01-01',
      };

      (global.fetch as jest.Mock).mockResolvedValueOnce({
        ok: true,
        json: async () => ({ success: true, data: mockResponse, message: 'Created' }),
      });

      const result = await createContract(createData);

      expect(global.fetch).toHaveBeenCalledWith(
        'http://localhost:5017/api/contracts',
        expect.objectContaining({
          method: 'POST',
          body: JSON.stringify(createData),
        })
      );
      expect(result).toEqual(mockResponse);
    });

    it('should throw error with message when creation fails', async () => {
      (global.fetch as jest.Mock).mockResolvedValueOnce({
        ok: false,
        json: async () => ({ message: 'Contract number already exists' }),
      });

      await expect(
        createContract({
          contractNumber: 'C001',
          userId: 'user-1',
          totalAmount: 1000,
          groupId: 1,
          status: 'Active',
          contractStartDate: '2024-01-01',
        })
      ).rejects.toThrow('Contract number already exists');
    });
  });

  describe('updateContract', () => {
    it('should update an existing contract', async () => {
      const updateData: UpdateContractRequest = {
        totalAmount: 1500,
        status: 'Defaulted',
      };

      const mockResponse = {
        id: 1,
        contractNumber: 'C001',
        userId: 'user-1',
        userName: 'John Doe',
        totalAmount: 1500,
        groupId: 1,
        groupName: 'Group A',
        status: 'Defaulted' as const,
        contractStartDate: '2024-01-01',
        contractEndDate: null,
        isActive: true,
        createdAt: '2024-01-01',
        updatedAt: '2024-01-02',
      };

      (global.fetch as jest.Mock).mockResolvedValueOnce({
        ok: true,
        json: async () => ({ success: true, data: mockResponse, message: 'Updated' }),
      });

      const result = await updateContract(1, updateData);

      expect(global.fetch).toHaveBeenCalledWith(
        'http://localhost:5017/api/contracts/1',
        expect.objectContaining({
          method: 'PUT',
          body: JSON.stringify(updateData),
        })
      );
      expect(result).toEqual(mockResponse);
    });
  });

  describe('deleteContract', () => {
    it('should delete a contract', async () => {
      (global.fetch as jest.Mock).mockResolvedValueOnce({
        ok: true,
      });

      await deleteContract(1);

      expect(global.fetch).toHaveBeenCalledWith(
        'http://localhost:5017/api/contracts/1',
        expect.objectContaining({
          method: 'DELETE',
        })
      );
    });

    it('should throw error when deletion fails', async () => {
      (global.fetch as jest.Mock).mockResolvedValueOnce({
        ok: false,
      });

      await expect(deleteContract(1)).rejects.toThrow('Failed to delete contract');
    });
  });

  describe('getUsers', () => {
    it('should fetch active users', async () => {
      const mockUsers = [
        { id: 'user-1', name: 'John Doe', email: 'john@example.com', role: 'user', isActive: true },
        { id: 'user-2', name: 'Jane Smith', email: 'jane@example.com', role: 'admin', isActive: true },
        { id: 'user-3', name: 'Inactive User', email: 'inactive@example.com', role: 'user', isActive: false },
      ];

      (global.fetch as jest.Mock).mockResolvedValueOnce({
        ok: true,
        json: async () => ({
          success: true,
          data: { items: mockUsers, totalCount: 3 },
          message: 'Success',
        }),
      });

      const result = await getUsers();

      expect(result).toHaveLength(2);
      expect(result).toEqual([mockUsers[0], mockUsers[1]]);
    });
  });

  describe('getGroups', () => {
    it('should fetch active groups', async () => {
      const mockGroups = [
        { id: 1, name: 'Group A', description: 'Test', commission: 10, isActive: true },
        { id: 2, name: 'Group B', description: 'Test', commission: 15, isActive: true },
        { id: 3, name: 'Inactive Group', description: 'Test', commission: 5, isActive: false },
      ];

      (global.fetch as jest.Mock).mockResolvedValueOnce({
        ok: true,
        json: async () => ({ success: true, data: mockGroups, message: 'Success' }),
      });

      const result = await getGroups();

      expect(result).toHaveLength(2);
      expect(result).toEqual([mockGroups[0], mockGroups[1]]);
    });
  });
});
