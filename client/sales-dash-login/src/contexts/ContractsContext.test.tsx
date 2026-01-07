import { renderHook, act } from '@testing-library/react';
import { ContractsProvider, useContractsContext } from './ContractsContext';
import { Contract, ContractStatus, User, Group as ContractGroup } from '../services/contractService';

describe('ContractsContext', () => {
  it('should provide initial empty state', () => {
    const wrapper = ({ children }: { children: React.ReactNode }) => (
      <ContractsProvider>{children}</ContractsProvider>
    );

    const { result } = renderHook(() => useContractsContext(), { wrapper });

    expect(result.current.contracts).toEqual([]);
    expect(result.current.users).toEqual([]);
    expect(result.current.groups).toEqual([]);
  });

  it('should update contracts', () => {
    const wrapper = ({ children }: { children: React.ReactNode }) => (
      <ContractsProvider>{children}</ContractsProvider>
    );

    const { result } = renderHook(() => useContractsContext(), { wrapper });

    const mockContracts: Contract[] = [
      {
        id: 1,
        contractNumber: 'C001',
        totalAmount: 1000,
        status: ContractStatus.Active,
        contractStartDate: '2024-01-01',
        isActive: true,
        createdAt: '2024-01-01',
        updatedAt: '2024-01-01',
        groupName: 'Test Group',
      } as Contract,
    ];

    act(() => {
      result.current.setContracts(mockContracts);
    });

    expect(result.current.contracts).toEqual(mockContracts);
  });

  it('should update users', () => {
    const wrapper = ({ children }: { children: React.ReactNode }) => (
      <ContractsProvider>{children}</ContractsProvider>
    );

    const { result } = renderHook(() => useContractsContext(), { wrapper });

    const mockUsers: User[] = [
      {
        id: '1',
        name: 'Test User',
        email: 'test@example.com',
        role: 'User',
        isActive: true,
        isMatriculaOwner: false,
      },
    ];

    act(() => {
      result.current.setUsers(mockUsers);
    });

    expect(result.current.users).toEqual(mockUsers);
  });

  it('should update groups', () => {
    const wrapper = ({ children }: { children: React.ReactNode }) => (
      <ContractsProvider>{children}</ContractsProvider>
    );

    const { result } = renderHook(() => useContractsContext(), { wrapper });

    const mockGroups: ContractGroup[] = [
      {
        id: 1,
        name: 'Test Group',
        description: 'Test Description',
        commission: 10,
        isActive: true,
      },
    ];

    act(() => {
      result.current.setGroups(mockGroups);
    });

    expect(result.current.groups).toEqual(mockGroups);
  });

  it('should get contract by ID', () => {
    const wrapper = ({ children }: { children: React.ReactNode }) => (
      <ContractsProvider>{children}</ContractsProvider>
    );

    const { result } = renderHook(() => useContractsContext(), { wrapper });

    const mockContracts: Contract[] = [
      {
        id: 1,
        contractNumber: 'C001',
        totalAmount: 1000,
        status: ContractStatus.Active,
        contractStartDate: '2024-01-01',
        isActive: true,
        createdAt: '2024-01-01',
        updatedAt: '2024-01-01',
        groupName: 'Test Group',
      } as Contract,
      {
        id: 2,
        contractNumber: 'C002',
        totalAmount: 2000,
        status: ContractStatus.Active,
        contractStartDate: '2024-01-01',
        isActive: true,
        createdAt: '2024-01-01',
        updatedAt: '2024-01-01',
        groupName: 'Test Group',
      } as Contract,
    ];

    act(() => {
      result.current.setContracts(mockContracts);
    });

    const contract = result.current.getContractById(1);
    expect(contract).toEqual(mockContracts[0]);

    const nonExistent = result.current.getContractById(999);
    expect(nonExistent).toBeUndefined();
  });

  it('should throw error when used outside provider', () => {
    // Suppress console.error for this test
    const originalError = console.error;
    console.error = jest.fn();

    expect(() => {
      renderHook(() => useContractsContext());
    }).toThrow('useContractsContext must be used within a ContractsProvider');

    console.error = originalError;
  });
});
