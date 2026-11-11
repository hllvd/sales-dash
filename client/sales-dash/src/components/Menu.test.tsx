import React from 'react';
import { render, screen, waitFor } from '@testing-library/react';
import '@testing-library/jest-dom';
import { MantineProvider } from '@mantine/core';
import Menu from './Menu';
import { BuildInfoProvider } from '../contexts/BuildInfoContext';
import { apiService, helpdeskApiService } from '../services/apiService';
import { surveyPollingService } from '../services/surveyPollingService';
import { getGroups } from '../services/contractService';

jest.mock('../services/apiService', () => ({
  apiService: {
    getPendingApprovalRequests: jest.fn(),
  },
  helpdeskApiService: {
    getUnreadCount: jest.fn(),
  },
}));

jest.mock('../services/surveyPollingService', () => ({
  surveyPollingService: {
    getPendingList: jest.fn(),
  },
}));

jest.mock('../services/contractService', () => {
  const actual = jest.requireActual('../services/contractService');
  return {
    ...actual,
    getGroups: jest.fn(),
  };
});

jest.mock('../contexts/BuildInfoContext', () => ({
  BuildInfoProvider: ({ children }: { children: React.ReactNode }) => <>{children}</>,
  useBuildInfo: () => ({ buildInfo: { buildId: 'test-build' } }),
}));

function makeJwt(payload: object): string {
  const header = btoa(JSON.stringify({ alg: 'HS256', typ: 'JWT' }));
  const body = btoa(JSON.stringify(payload));
  return `${header}.${body}.sig`;
}

describe('Menu Grupos visibility', () => {
  beforeEach(() => {
    localStorage.clear();
    (apiService.getPendingApprovalRequests as jest.Mock).mockResolvedValue({ success: true, data: [] });
    (helpdeskApiService.getUnreadCount as jest.Mock).mockResolvedValue({ success: true, data: { unreadCount: 0 } });
    (surveyPollingService.getPendingList as jest.Mock).mockReturnValue([]);
    (getGroups as jest.Mock).mockResolvedValue([]);
  });

  it('shows Grupos link and unassigned segment badge for superadmin', async () => {
    localStorage.setItem('token', makeJwt({ perm: ['system:superadmin', 'groups:read'] }));
    localStorage.setItem('user', JSON.stringify({ role: 'superadmin', email: 'sa@test.com' }));
    (getGroups as jest.Mock).mockResolvedValue([
      { id: 1, name: 'G1', description: '', commission: 0, segment: null, isActive: true },
      { id: 2, name: 'G2', description: '', commission: 0, segment: null, isActive: true },
      { id: 3, name: 'G3', description: '', commission: 0, segment: 1, isActive: true },
      { id: 4, name: 'G4', description: '', commission: 0, segment: null, isActive: false },
    ]);

    render(
      <MantineProvider>
        <BuildInfoProvider>
          <Menu><div>content</div></Menu>
        </BuildInfoProvider>
      </MantineProvider>
    );

    const groupsLink = screen.getByTestId('nav-groups');
    expect(groupsLink).toBeInTheDocument();
    expect(groupsLink).toHaveAttribute('href', '#/groups');

    await waitFor(() => {
      expect(groupsLink).toHaveTextContent('2');
    });
  });

  it('hides badge on Grupos link when all active groups have segments', async () => {
    localStorage.setItem('token', makeJwt({ perm: ['system:superadmin', 'groups:read'] }));
    localStorage.setItem('user', JSON.stringify({ role: 'superadmin', email: 'sa@test.com' }));
    (getGroups as jest.Mock).mockResolvedValue([
      { id: 1, name: 'G1', description: '', commission: 0, segment: 1, isActive: true },
      { id: 2, name: 'G2', description: '', commission: 0, segment: null, isActive: false },
    ]);

    render(
      <MantineProvider>
        <BuildInfoProvider>
          <Menu><div>content</div></Menu>
        </BuildInfoProvider>
      </MantineProvider>
    );

    const groupsLink = screen.getByTestId('nav-groups');
    await waitFor(() => {
      expect(getGroups).toHaveBeenCalledWith(false);
    });
    expect(groupsLink).not.toHaveTextContent('0');
  });

  it('hides Grupos link and skips fetching groups for admin even with groups:read permission', async () => {
    localStorage.setItem('token', makeJwt({ perm: ['system:admin', 'groups:read', 'groups:write'] }));
    localStorage.setItem('user', JSON.stringify({ role: 'admin', email: 'admin@test.com' }));

    render(
      <MantineProvider>
        <BuildInfoProvider>
          <Menu><div>content</div></Menu>
        </BuildInfoProvider>
      </MantineProvider>
    );

    await waitFor(() => {
      expect(apiService.getPendingApprovalRequests).toHaveBeenCalled();
    });
    expect(screen.queryByTestId('nav-groups')).not.toBeInTheDocument();
    expect(getGroups).not.toHaveBeenCalled();
  });

  it('hides Grupos link for regular user', () => {
    localStorage.setItem('token', makeJwt({ perm: ['contracts:read'] }));
    localStorage.setItem('user', JSON.stringify({ role: 'user', email: 'user@test.com' }));

    render(
      <MantineProvider>
        <BuildInfoProvider>
          <Menu><div>content</div></Menu>
        </BuildInfoProvider>
      </MantineProvider>
    );

    expect(screen.queryByTestId('nav-groups')).not.toBeInTheDocument();
    expect(getGroups).not.toHaveBeenCalled();
  });
});
