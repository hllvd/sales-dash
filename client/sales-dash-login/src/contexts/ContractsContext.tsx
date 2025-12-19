import React, { createContext, useContext, useState, useCallback, ReactNode } from 'react';
import { Contract, User, Group as ContractGroup } from '../services/contractService';

interface ContractsContextType {
  // Cached data
  contracts: Contract[];
  users: User[];
  groups: ContractGroup[];
  
  // Setters
  setContracts: (contracts: Contract[]) => void;
  setUsers: (users: User[]) => void;
  setGroups: (groups: ContractGroup[]) => void;
  
  // Helper to get contract by ID from cache
  getContractById: (id: number) => Contract | undefined;
}

const ContractsContext = createContext<ContractsContextType | undefined>(undefined);

interface ContractsProviderProps {
  children: ReactNode;
}

export const ContractsProvider: React.FC<ContractsProviderProps> = ({ children }) => {
  const [contracts, setContracts] = useState<Contract[]>([]);
  const [users, setUsers] = useState<User[]>([]);
  const [groups, setGroups] = useState<ContractGroup[]>([]);

  const getContractById = useCallback((id: number) => {
    return contracts.find(contract => contract.id === id);
  }, [contracts]);

  const value: ContractsContextType = {
    contracts,
    users,
    groups,
    setContracts,
    setUsers,
    setGroups,
    getContractById,
  };

  return (
    <ContractsContext.Provider value={value}>
      {children}
    </ContractsContext.Provider>
  );
};

export const useContractsContext = () => {
  const context = useContext(ContractsContext);
  if (context === undefined) {
    throw new Error('useContractsContext must be used within a ContractsProvider');
  }
  return context;
};
