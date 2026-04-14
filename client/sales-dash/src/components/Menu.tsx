import React, { useState, useEffect } from 'react';
import { AppShell, NavLink, Text, Group, Button, Tooltip } from '@mantine/core';
import { useBuildInfo } from '../contexts/BuildInfoContext';
import { UserRole } from '../types/UserRole';
import {
  IconUsers,
  IconFileText,
  IconBuilding,
  IconClipboardList,
  IconLogout,
  IconId,
  IconHistory,
  IconWand,
  IconLock,
  IconRefresh,
} from '@tabler/icons-react';

interface MenuProps {
  children?: React.ReactNode;
}

const Menu: React.FC<MenuProps> = ({ children }) => {
  const [userPermissions, setUserPermissions] = useState<string[]>([]);
  const [currentPath, setCurrentPath] = useState(window.location.hash || '#/home');
  const { buildInfo } = useBuildInfo();

  useEffect(() => {
    // Extract dynamic PBAC Permissions from JWT
    const token = localStorage.getItem('token');
    if (token) {
      try {
        const payload = token.split('.')[1];
        const base64 = payload.replace(/-/g, '+').replace(/_/g, '/');
        const jsonPayload = decodeURIComponent(atob(base64).split('').map(c => 
          '%' + ('00' + c.charCodeAt(0).toString(16)).slice(-2)
        ).join(''));
        
        const decoded = JSON.parse(jsonPayload);
        if (decoded.perm) {
          setUserPermissions(Array.isArray(decoded.perm) ? decoded.perm : [decoded.perm]);
        }
      } catch (e) {
        console.error('Failed to parse token permissions', e);
      }
    }

    const handleHashChange = () => {
      setCurrentPath(window.location.hash || '#/my-contracts');
    };

    window.addEventListener('hashchange', handleHashChange);
    return () => window.removeEventListener('hashchange', handleHashChange);
  }, []);

  const hasPermission = (permission: string) => {
    const isSuperAdmin = userPermissions.includes('system:superadmin');
    
    if (isSuperAdmin) return true;
    return userPermissions.includes(permission);
  };

  const handleLogout = () => {
    localStorage.removeItem('token');
    localStorage.removeItem('user');
    window.location.href = '/';
  };

  const isActive = (path: string) => currentPath === path;

  const navLinkStyles = (path: string) => ({
    root: {
      color: '#d1d5db',
      borderRadius: '8px',
      marginBottom: '4px',
      '&:hover': {
        backgroundColor: '#374151',
        color: 'white',
      },
      backgroundColor: isActive(path) ? undefined : 'transparent',
    },
    label: { color: isActive(path) ? 'white' : 'inherit' },
  });

  return (
    <AppShell
      navbar={{
        width: 280,
        breakpoint: 'sm',
      }}
      padding="md"
    >
      <AppShell.Navbar p="md" style={{ backgroundColor: '#1f2937' }}>
        <AppShell.Section>
          <Group mb="lg">
            <Tooltip
              label={buildInfo ? `Build: ${buildInfo.buildId}` : 'Carregando build info…'}
              position="right"
              withArrow
              arrowSize={6}
              styles={{
                tooltip: {
                  fontSize: '0.75rem',
                  fontFamily: 'monospace',
                  background: 'rgba(0,0,0,0.85)',
                  color: '#a5f3a0',
                },
              }}
            >
              <Text size="xl" fw={700} c="white" style={{ cursor: 'default' }}>
                Painel de Vendas
              </Text>
            </Tooltip>
          </Group>
        </AppShell.Section>

        <AppShell.Section grow>


          {hasPermission('users:read') && (
            <NavLink
              href="#/users"
              label="Usuários"
              leftSection={<IconUsers size={20} />}
              active={isActive('#/users')}
              variant="filled"
              color="red"
              styles={navLinkStyles('#/users')}
            />
          )}

          {hasPermission('system:admin') && (
            <NavLink
              href="#/contracts"
              label="Contratos"
              leftSection={<IconFileText size={20} />}
              active={isActive('#/contracts')}
              variant="filled"
              color="red"
              styles={navLinkStyles('#/contracts')}
              data-testid="nav-contracts"
            />
          )}

          {hasPermission('pvs:read') && (
            <NavLink
              href="#/point-of-sale"
              label="Pontos de Venda"
              leftSection={<IconBuilding size={20} />}
              active={isActive('#/point-of-sale')}
              variant="filled"
              color="red"
              styles={navLinkStyles('#/point-of-sale')}
            />
          )}

          {(hasPermission('matriculas:read') && hasPermission('system:superadmin')) && (
            <NavLink
              href="#/matriculas"
              label="Matrículas"
              leftSection={<IconId size={20} />}
              active={isActive('#/matriculas')}
              variant="filled"
              color="red"
              styles={navLinkStyles('#/matriculas')}
            />
          )}

          {hasPermission('roles:read') && (
            <NavLink
              href="#/access-control"
              label="Controle de Acesso"
              leftSection={<IconLock size={20} />}
              active={isActive('#/access-control')}
              variant="filled"
              color="red"
              styles={navLinkStyles('#/access-control')}
            />
          )}

          {hasPermission('imports:history') && (
            <NavLink
              href="#/import-history"
              label="Histórico de Importação"
              leftSection={<IconHistory size={20} />}
              active={isActive('#/import-history')}
              variant="filled"
              color="red"
              styles={navLinkStyles('#/import-history')}
            />
          )}

          {hasPermission('imports:execute') && (
            <NavLink
              href="#/import-wizard"
              label="Assistente de Importação"
              leftSection={<IconWand size={20} />}
              active={isActive('#/import-wizard')}
              variant="filled"
              color="red"
              styles={navLinkStyles('#/import-wizard')}
            />
          )}

          <NavLink
            href="#/my-contracts"
            label="Meus Contratos"
            leftSection={<IconClipboardList size={20} />}
            active={isActive('#/my-contracts')}
            variant="filled"
            color="red"
            styles={navLinkStyles('#/my-contracts')}
            data-testid="nav-my-contracts"
          />
          
          {hasPermission('system:admin') && (
            <NavLink
              href="#/scrapes"
              label="Extração PowerBI"
              leftSection={<IconRefresh size={20} />}
              active={isActive('#/scrapes')}
              variant="filled"
              color="red"
              styles={navLinkStyles('#/scrapes')}
            />
          )}


        </AppShell.Section>

        <AppShell.Section style={{ border: '0px', borderTop: '1px solid #374151', borderRadius: '8px', paddingTop: '16px' }}>
          <NavLink
            href="#/my-profile"
            label="Meu Usuário"
            leftSection={<IconUsers size={20} />}
            active={isActive('#/my-profile')}
            variant="filled"
            color="red"
            styles={navLinkStyles('#/my-profile')}
            mb="xs"
          />
          <Button
            fullWidth
            leftSection={<IconLogout size={20} />}
            variant="subtle"
            color="red"
            className="no-border"
            onClick={handleLogout}
          >
            Logout
          </Button>
        </AppShell.Section>
      </AppShell.Navbar>

      <AppShell.Main style={{ backgroundColor: '#f5f5f5' }}>{children}</AppShell.Main>
    </AppShell>
  );
};

export default Menu;