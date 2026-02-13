import React, { useState, useEffect } from 'react';
import { AppShell, NavLink, Text, Group, Button } from '@mantine/core';
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
} from '@tabler/icons-react';

interface MenuProps {
  children?: React.ReactNode;
}

const Menu: React.FC<MenuProps> = ({ children }) => {
  const [userRole, setUserRole] = useState('');
  const [currentPath, setCurrentPath] = useState(window.location.hash || '#/home');

  useEffect(() => {
    const user = JSON.parse(localStorage.getItem('user') || '{}');
    setUserRole(user.role || '');

    const handleHashChange = () => {
      setCurrentPath(window.location.hash || '#/my-contracts');
    };

    window.addEventListener('hashchange', handleHashChange);
    return () => window.removeEventListener('hashchange', handleHashChange);
  }, []);

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
            <Text size="xl" fw={700} c="white">
              Painel de Vendas
            </Text>
          </Group>
        </AppShell.Section>

        <AppShell.Section grow>


          {userRole === 'superadmin' && (
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

          {userRole === 'superadmin' && (
            <NavLink
              href="#/contracts"
              label="Contratos"
              leftSection={<IconFileText size={20} />}
              active={isActive('#/contracts')}
              variant="filled"
              color="red"
              styles={navLinkStyles('#/contracts')}
            />
          )}

          {(userRole === 'admin' || userRole === 'superadmin') && (
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

          {userRole === 'superadmin' && (
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

          {userRole === 'superadmin' && (
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

          {(userRole === 'admin' || userRole === 'superadmin') && (
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

          {(userRole === 'admin' || userRole === 'superadmin') && (
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
          />


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