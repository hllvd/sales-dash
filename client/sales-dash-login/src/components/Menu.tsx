import React, { useState, useEffect } from 'react';
import { AppShell, NavLink, Text, Group, Button } from '@mantine/core';
import {
  IconHome,
  IconChartBar,
  IconUsers,
  IconFileText,
  IconMap,
  IconBuilding,
  IconClipboardList,
  IconUsersGroup,
  IconLogout,
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
              color="blue"
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
              color="blue"
              styles={navLinkStyles('#/contracts')}
            />
          )}

          {userRole === 'superadmin' && (
            <NavLink
              href="#/users-mapping"
              label="Mapeamento"
              leftSection={<IconMap size={20} />}
              active={isActive('#/users-mapping')}
              variant="filled"
              color="blue"
              styles={navLinkStyles('#/users-mapping')}
            />
          )}

          {(userRole === 'admin' || userRole === 'superadmin') && (
            <NavLink
              href="#/point-of-sale"
              label="Pontos de Venda"
              leftSection={<IconBuilding size={20} />}
              active={isActive('#/point-of-sale')}
              variant="filled"
              color="blue"
              styles={navLinkStyles('#/point-of-sale')}
            />
          )}

          <NavLink
            href="#/my-contracts"
            label="Meus Contratos"
            leftSection={<IconClipboardList size={20} />}
            active={isActive('#/my-contracts')}
            variant="filled"
            color="blue"
            styles={navLinkStyles('#/my-contracts')}
          />


        </AppShell.Section>

        <AppShell.Section>
          <Button
            fullWidth
            leftSection={<IconLogout size={20} />}
            variant="subtle"
            color="red"
            onClick={handleLogout}
            styles={{
              root: {
                borderTop: '1px solid #374151',
                borderRadius: '8px',
                marginTop: '8px',
                paddingTop: '16px',
                '&:hover': {
                  backgroundColor: 'rgba(220, 38, 38, 0.1)',
                },
              },
            }}
          >
            Logout
          </Button>
        </AppShell.Section>
      </AppShell.Navbar>

      <AppShell.Main>{children}</AppShell.Main>
    </AppShell>
  );
};

export default Menu;