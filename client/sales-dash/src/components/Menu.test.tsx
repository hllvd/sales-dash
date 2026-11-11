import React from 'react';
import { render, screen } from '@testing-library/react';
import '@testing-library/jest-dom';
import { MantineProvider } from '@mantine/core';
import Menu from './Menu';
import { BuildInfoProvider } from '../contexts/BuildInfoContext';
import { apiService, helpdeskApiService } from '../services/apiService';
import { surveyPollingService } from '../services/surveyPollingService';

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
  });

  it('shows Grupos link for superadmin', () => {
    localStorage.setItem('token', makeJwt({ perm: ['system:superadmin', 'groups:read'] }));
    localStorage.setItem('user', JSON.stringify({ role: 'superadmin', email: 'sa@test.com' }));

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
  });

  it('hides Grupos link for admin even with groups:read permission', () => {
    localStorage.setItem('token', makeJwt({ perm: ['system:admin', 'groups:read', 'groups:write'] }));
    localStorage.setItem('user', JSON.stringify({ role: 'admin', email: 'admin@test.com' }));

    render(
      <MantineProvider>
        <BuildInfoProvider>
          <Menu><div>content</div></Menu>
        </BuildInfoProvider>
      </MantineProvider>
    );

    expect(screen.queryByTestId('nav-groups')).not.toBeInTheDocument();
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
  });
});
