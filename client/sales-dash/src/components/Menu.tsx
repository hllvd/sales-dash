import React, { useState, useEffect } from 'react';
import { AppShell, NavLink, Text, Group, Button, Tooltip, Burger, Badge } from '@mantine/core';
import { useDisclosure, useMediaQuery } from '@mantine/hooks';
import { useBuildInfo } from '../contexts/BuildInfoContext';
import { UserRole } from '../types/UserRole';
import { apiService } from '../services/apiService';
import './Menu.css';
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
  IconChartBar,
  IconActivity,
  IconMedal,
  IconLayoutDashboard,
  IconSitemap,
  IconChevronDown,
  IconChevronRight,
  IconTools,
  IconReceipt2,
  IconMailForward,
  IconBuildingStore,
  IconCalendar,
  IconHelp,
  IconDatabase,
  IconFileImport,
} from '@tabler/icons-react';
import { surveyPollingService } from '../services/surveyPollingService';

interface MenuProps {
  children?: React.ReactNode;
}

const Menu: React.FC<MenuProps> = ({ children }) => {
  const [userPermissions, setUserPermissions] = useState<string[]>([]);
  const [userRole, setUserRole] = useState<string>('');
  const [userEmail, setUserEmail] = useState<string>('');
  const [currentPath, setCurrentPath] = useState(window.location.hash || '#/home');
  const [opened, { toggle, close }] = useDisclosure();
  const isMobile = useMediaQuery('(max-width: 62em)', false);
  const { buildInfo } = useBuildInfo();
  const [usersMenuOpened, setUsersMenuOpened] = useState(
    window.location.hash === '#/users' || window.location.hash === '#/users/tree'
  );
  const [teamsMenuOpened, setTeamsMenuOpened] = useState(
    window.location.hash === '#/teams' || window.location.hash === '#/teams/calendar'
  );
  const [dadosMenuOpened, setDadosMenuOpened] = useState(
    window.location.hash.startsWith('#/reports') ||
    window.location.hash.startsWith('#/views') ||
    window.location.hash.startsWith('#/scrapes')
  );
  const [importacaoMenuOpened, setImportacaoMenuOpened] = useState(
    window.location.hash === '#/import-wizard' || window.location.hash === '#/import-history'
  );
  const [pendingCount, setPendingCount] = useState<number>(0);
  const [pendingSurveyCount, setPendingSurveyCount] = useState<number>(0);

  useEffect(() => {
    const updateSurveyCount = () => {
      const list = surveyPollingService.getPendingList();
      setPendingSurveyCount(list.length);
    };

    updateSurveyCount();
    window.addEventListener('survey:updated', updateSurveyCount);
    return () => window.removeEventListener('survey:updated', updateSurveyCount);
  }, []);

  useEffect(() => {
    if (userRole === 'admin' || userRole === 'superadmin') {
      apiService.getPendingApprovalRequests()
        .then((res) => {
          if (res.success && res.data) {
            setPendingCount(res.data.length);
          }
        })
        .catch(() => {});
    }
  }, [userRole]);

  useEffect(() => {
    if (currentPath === '#/users' || currentPath === '#/users/tree') {
      setUsersMenuOpened(true);
    }
    if (currentPath === '#/teams' || currentPath === '#/teams/calendar') {
      setTeamsMenuOpened(true);
    }
    if (currentPath.startsWith('#/reports') || currentPath.startsWith('#/views') || currentPath.startsWith('#/scrapes')) {
      setDadosMenuOpened(true);
    }
    if (currentPath === '#/import-wizard' || currentPath === '#/import-history') {
      setImportacaoMenuOpened(true);
    }
  }, [currentPath]);

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

        // Get user role from localStorage
        const userJson = localStorage.getItem('user');
        if (userJson) {
          const user = JSON.parse(userJson);
          if (user.role) {
            setUserRole(user.role);
          }
          if (user.email) {
            setUserEmail(user.email);
          }
        }
      } catch (e) {
        console.error('Failed to parse token or user data', e);
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

  const isActive = (path: string) => {
    if (path === '#/scrapes' && currentPath.startsWith('#/scrapes')) return true;
    return currentPath === path;
  };

  const navLinkStyles = (path: string) => {
    const isNodeActive = isActive(path) || 
      (path === 'users-parent' && (currentPath === '#/users' || currentPath === '#/users/tree')) ||
      (path === 'teams-parent' && (currentPath === '#/teams' || currentPath === '#/teams/calendar'));
    return {
      root: {
        color: '#d1d5db',
        borderRadius: '8px',
        marginBottom: '4px',
        '&:hover': {
          backgroundColor: '#374151',
          color: 'white',
        },
        backgroundColor: isNodeActive ? undefined : 'transparent',
      },
      label: { color: isNodeActive ? 'white' : 'inherit' },
    };
  };

  return (
    <AppShell
      header={{ height: 60, collapsed: !isMobile }}
      navbar={{
        width: 280,
        breakpoint: 'md',
        collapsed: { mobile: !opened },
      }}
      padding="md"
    >
      <AppShell.Header 
        style={{ 
          backgroundColor: '#111827', 
          borderBottom: '1px solid #374151', 
          display: isMobile ? 'flex' : 'none',
          alignItems: 'center' 
        }}
      >
        <Group h="100%" px="md" style={{ width: '100%', justifyContent: 'space-between' }}>
          <Group>
            <Burger
              opened={opened}
              onClick={toggle}
              hiddenFrom="md"
              size="sm"
              color="#d1d5db"
            />
            <Text size="lg" fw={700} hiddenFrom="md" c="white">
              Painel de Vendas
            </Text>
          </Group>
          {/* Header can contain profile or other info later */}
        </Group>
      </AppShell.Header>
      <AppShell.Navbar 
        p="md" 
        data-collapsed={!opened}
        style={{ 
          backgroundColor: '#1f2937',
          display: isMobile ? (opened ? 'flex' : 'none') : 'flex',
          flexDirection: 'column'
        }}
      >
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

        <AppShell.Section grow className="menu-nav-scrollable" style={{ overflowY: 'auto' }}>


          {hasPermission('users:read') && (
            <>
              <NavLink
                component="a"
                href="#/users"
                role="link"
                label="Usuários"
                leftSection={<IconUsers size={20} />}
                styles={navLinkStyles('users-parent')}
                active={currentPath === '#/users' || currentPath === '#/users/tree'}
                color="red"
                variant="filled"
                rightSection={usersMenuOpened ? <IconChevronDown size={16} /> : <IconChevronRight size={16} />}
                onClick={() => {
                  setUsersMenuOpened(!usersMenuOpened);
                  if (opened) close();
                }}
              />
              {usersMenuOpened && (
                <>
                  <NavLink
                    href="#/users"
                    label="Lista"
                    active={isActive('#/users')}
                    styles={navLinkStyles('#/users')}
                    style={{ paddingLeft: 28 }}
                    onClick={() => { if (opened) close(); }}
                  />
                  <NavLink
                    href="#/users/tree"
                    label="Árvore"
                    leftSection={<IconSitemap size={16} />}
                    active={isActive('#/users/tree')}
                    styles={navLinkStyles('#/users/tree')}
                    style={{ paddingLeft: 28 }}
                    onClick={() => { if (opened) close(); }}
                  />
                </>
              )}
            </>
          )}

          {hasPermission('system:admin') || userRole === UserRole.ADMIN ? (
            <NavLink
              href="#/contracts"
              label="Contratos"
              leftSection={<IconFileText size={20} />}
              active={isActive('#/contracts')}
              variant="filled"
              color="red"
              styles={navLinkStyles('#/contracts')}
              data-testid="nav-contracts"
              onClick={() => { if (opened) close(); }}
            />
          ) : null}

          <NavLink
            href="#/my-contracts"
            label="Meus Contratos"
            leftSection={<IconClipboardList size={20} />}
            active={isActive('#/my-contracts')}
            variant="filled"
            color="red"
            styles={navLinkStyles('#/my-contracts')}
            data-testid="nav-my-contracts"
            onClick={() => { if (opened) close(); }}
          />

          {hasPermission('requests:read') && (
            <NavLink
              href="#/requests"
              label="Solicitações"
              leftSection={<IconMailForward size={20} />}
              rightSection={pendingCount > 0 ? <Badge size="xs" circle color="red">{pendingCount}</Badge> : undefined}
              active={isActive('#/requests')}
              variant="filled"
              color="red"
              styles={navLinkStyles('#/requests')}
              onClick={() => { if (opened) close(); }}
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
              onClick={() => { if (opened) close(); }}
            />
          )}

          {hasPermission('matriculas:read') && (
            <NavLink
              href="#/matriculas"
              label="Matrículas"
              leftSection={<IconId size={20} />}
              active={isActive('#/matriculas')}
              variant="filled"
              color="red"
              styles={navLinkStyles('#/matriculas')}
              onClick={() => { if (opened) close(); }}
            />
          )}

          {hasPermission('teams:manage') && (
            <>
              <NavLink
                component="a"
                href="#/teams"
                role="link"
                label="Equipes"
                leftSection={<IconUsers size={20} />}
                styles={navLinkStyles('teams-parent')}
                active={currentPath === '#/teams' || currentPath === '#/teams/calendar'}
                color="red"
                variant="filled"
                rightSection={teamsMenuOpened ? <IconChevronDown size={16} /> : <IconChevronRight size={16} />}
                onClick={() => {
                  setTeamsMenuOpened(!teamsMenuOpened);
                  if (opened) close();
                }}
              />
              {teamsMenuOpened && (
                <>
                  <NavLink
                    href="#/teams"
                    label="Lista"
                    active={isActive('#/teams')}
                    styles={navLinkStyles('#/teams')}
                    style={{ paddingLeft: 28 }}
                    onClick={() => { if (opened) close(); }}
                  />
                  <NavLink
                    href="#/teams/calendar"
                    label="Calendário"
                    leftSection={<IconCalendar size={16} />}
                    active={isActive('#/teams/calendar')}
                    styles={navLinkStyles('#/teams/calendar')}
                    style={{ paddingLeft: 28 }}
                    onClick={() => { if (opened) close(); }}
                  />
                </>
              )}
            </>
          )}

          {hasPermission('system:superadmin') && (
            <NavLink
              href="#/stores"
              label="Lojas"
              leftSection={<IconBuildingStore size={20} />}
              active={isActive('#/stores')}
              variant="filled"
              color="red"
              styles={navLinkStyles('#/stores')}
              onClick={() => { if (opened) close(); }}
            />
          )}

          {hasPermission('system:superadmin') && (
            <NavLink
              href="#/surveys"
              label="Perguntas"
              leftSection={<IconHelp size={20} />}
              active={isActive('#/surveys')}
              variant="filled"
              color="red"
              styles={navLinkStyles('#/surveys')}
              onClick={() => { if (opened) close(); }}
            />
          )}

          {(userRole === 'superadmin' || userRole === 'admin' || userRole === UserRole.SUPERADMIN || userRole === UserRole.ADMIN || hasPermission('teams:manage') || hasPermission('system:admin')) && (
            <NavLink
              href="#/classifications"
              label="Níveis de Classificação"
              leftSection={<IconMedal size={20} />}
              active={isActive('#/classifications')}
              variant="filled"
              color="red"
              styles={navLinkStyles('#/classifications')}
              onClick={() => { if (opened) close(); }}
            />
          )}

          {userRole === 'superadmin' && (
            <NavLink
              href="#/user-metadata-fields"
              label="Campos de Metadados"
              leftSection={<IconClipboardList size={20} />}
              active={isActive('#/user-metadata-fields')}
              variant="filled"
              color="red"
              styles={navLinkStyles('#/user-metadata-fields')}
              onClick={() => { if (opened) close(); }}
            />
          )}

          {(hasPermission('roles:read') && userRole !== UserRole.ADMIN) && (
            <NavLink
              href="#/access-control"
              label="Controle de Acesso"
              leftSection={<IconLock size={20} />}
              active={isActive('#/access-control')}
              variant="filled"
              color="red"
              styles={navLinkStyles('#/access-control')}
              onClick={() => { if (opened) close(); }}
            />
          )}

          <NavLink
            label="Dados & Relatórios"
            leftSection={<IconDatabase size={20} />}
            childrenOffset={28}
            styles={navLinkStyles('')}
            opened={dadosMenuOpened}
            onChange={setDadosMenuOpened}
          >
            <NavLink
              href="#/reports"
              label="Relatórios"
              leftSection={<IconChartBar size={16} />}
              active={isActive('#/reports') || currentPath.startsWith('#/reports/')}
              variant="filled"
              color="red"
              styles={navLinkStyles('#/reports')}
              data-testid="nav-reports"
              onClick={() => { if (opened) close(); }}
            />
            
            <NavLink
              href="#/views"
              label="Dashboards"
              leftSection={<IconLayoutDashboard size={16} />}
              active={isActive('#/views') || currentPath.startsWith('#/views/')}
              variant="filled"
              color="red"
              styles={navLinkStyles('#/views')}
              onClick={() => { if (opened) close(); }}
            />
            
            {hasPermission('system:admin') && (
              <NavLink
                href="#/scrapes"
                label="Extração PowerBI"
                leftSection={<IconRefresh size={16} />}
                active={isActive('#/scrapes')}
                variant="filled"
                color="red"
                styles={navLinkStyles('#/scrapes')}
                onClick={() => { if (opened) close(); }}
              />
            )}
          </NavLink>

          {(hasPermission('imports:execute') || hasPermission('imports:history')) && (
            <NavLink
              label="Importação"
              leftSection={<IconFileImport size={20} />}
              childrenOffset={28}
              styles={navLinkStyles('')}
              opened={importacaoMenuOpened}
              onChange={setImportacaoMenuOpened}
            >
              {hasPermission('imports:execute') && (
                <NavLink
                  href="#/import-wizard"
                  label="Assistente de Importação"
                  leftSection={<IconWand size={16} />}
                  active={isActive('#/import-wizard')}
                  variant="filled"
                  color="red"
                  styles={navLinkStyles('#/import-wizard')}
                  onClick={() => { if (opened) close(); }}
                />
              )}

              {hasPermission('imports:history') && (
                <NavLink
                  href="#/import-history"
                  label="Histórico de Importação"
                  leftSection={<IconHistory size={16} />}
                  active={isActive('#/import-history')}
                  variant="filled"
                  color="red"
                  styles={navLinkStyles('#/import-history')}
                  onClick={() => { if (opened) close(); }}
                />
              )}
            </NavLink>
          )}

          <NavLink
            href="#/qa"
            label="QA"
            leftSection={<IconHelp size={20} />}
            rightSection={pendingSurveyCount > 0 ? <Badge size="xs" circle color="red">{pendingSurveyCount}</Badge> : undefined}
            active={isActive('#/qa')}
            variant="filled"
            color="red"
            styles={navLinkStyles('#/qa')}
            data-testid="nav-qa"
            onClick={() => { if (opened) close(); }}
          />

          {hasPermission('system:superadmin') && (
            <NavLink
              label="Monitoramento"
              leftSection={<IconActivity size={20} />}
              childrenOffset={28}
              styles={navLinkStyles('')}
              defaultOpened={currentPath.startsWith('#/monitoring')}
            >
              <NavLink
                href="#/monitoring/matricula-health"
                label="Saúde das Matrículas"
                active={isActive('#/monitoring/matricula-health')}
                styles={navLinkStyles('#/monitoring/matricula-health')}
                onClick={() => { if (opened) close(); }}
              />
              <NavLink
                href="#/monitoring/licensing"
                label="Licenciamento"
                leftSection={<IconReceipt2 size={16} />}
                active={isActive('#/monitoring/licensing')}
                styles={navLinkStyles('#/monitoring/licensing')}
                onClick={() => { if (opened) close(); }}
              />
            </NavLink>
          )}

          {userEmail === 'superadmin@salesapp.com' && (
            <NavLink
              label="Ferramentas Admin"
              leftSection={<IconTools size={20} />}
              childrenOffset={28}
              styles={navLinkStyles('')}
              defaultOpened={currentPath === '#/tester' || currentPath === '#/batch' || currentPath === '#/contract-reconciliation' || currentPath === '#/retention-filter'}
            >
              <NavLink
                href="#/tester"
                label="Painel de Testes"
                active={isActive('#/tester')}
                styles={navLinkStyles('#/tester')}
                onClick={() => { if (opened) close(); }}
              />
              <NavLink
                href="#/batch"
                label="Modificação em Lote"
                active={isActive('#/batch')}
                styles={navLinkStyles('#/batch')}
                onClick={() => { if (opened) close(); }}
              />
              <NavLink
                href="#/contract-reconciliation"
                label="Reconciliação de Contratos"
                active={isActive('#/contract-reconciliation')}
                styles={navLinkStyles('#/contract-reconciliation')}
                onClick={() => { if (opened) close(); }}
              />
              <NavLink
                href="#/retention-filter"
                label="Filtro Modelo Retenção"
                active={isActive('#/retention-filter')}
                styles={navLinkStyles('#/retention-filter')}
                onClick={() => { if (opened) close(); }}
              />
            </NavLink>
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
            onClick={() => { if (opened) close(); }}
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