import React, { useState, useEffect } from 'react';
import { Title, Table, Checkbox, TextInput, Loader, Alert, Group, Button, Badge, ScrollArea } from '@mantine/core';
import { IconSearch, IconLock, IconCheck, IconRefresh } from '@tabler/icons-react';
import Menu from './Menu';
import { apiService } from '../services/apiService';

interface Role {
  id: number;
  name: string;
}

interface Endpoint {
  controller: string;
  action: string;
  httpMethod: string;
  route: string;
}

interface Permission {
  roleId: number;
  controllerName: string;
  actionName: string;
}

interface MatrixData {
  roles: Role[];
  endpoints: Endpoint[];
  permissions: Permission[];
}

const AccessControlPage: React.FC = () => {
  const [data, setData] = useState<MatrixData | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [searchTerm, setSearchTerm] = useState('');
  const [saving, setSaving] = useState<string | null>(null);

  useEffect(() => {
    fetchMatrix();
  }, []);

  const fetchMatrix = async () => {
    setLoading(true);
    try {
      const response = await apiService.get('/permissions/matrix');
      setData(response);
    } catch (err: any) {
      setError(err.message || 'Falha ao carregar matriz de permissões');
    } finally {
      setLoading(false);
    }
  };

  const isPermissionEnabled = (roleId: number, controller: string, action: string) => {
    if (!data) return false;
    // SuperAdmin always enabled (but might not be in DB explicitly)
    const role = data.roles.find(r => r.id === roleId);
    if (role?.name === 'SuperAdmin') return true;

    return data.permissions.some(
      p => p.roleId === roleId && p.controllerName === controller && p.actionName === action
    );
  };

  const handleToggle = async (roleId: number, controller: string, action: string, currentlyEnabled: boolean) => {
    const key = `${roleId}-${controller}-${action}`;
    setSaving(key);
    try {
      await apiService.post('/permissions/assign', {
        roleId,
        controllerName: controller,
        actionName: action,
        isEnabled: !currentlyEnabled
      });
      
      // Update local state
      if (data) {
        const newPermissions = !currentlyEnabled
          ? [...data.permissions, { roleId, controllerName: controller, actionName: action }]
          : data.permissions.filter(p => !(p.roleId === roleId && p.controllerName === controller && p.actionName === action));
        
        setData({ ...data, permissions: newPermissions });
      }
    } catch (err: any) {
      alert('Erro ao salvar permissão: ' + err.message);
    } finally {
      setSaving(null);
    }
  };

  const filteredEndpoints = data?.endpoints.filter(e => 
    e.controller.toLowerCase().includes(searchTerm.toLowerCase()) ||
    e.action.toLowerCase().includes(searchTerm.toLowerCase()) ||
    e.route.toLowerCase().includes(searchTerm.toLowerCase())
  ) || [];

  if (loading) {
    return (
      <Menu>
        <div style={{ display: 'flex', justifyContent: 'center', alignItems: 'center', height: '100%' }}>
          <Loader size="xl" />
        </div>
      </Menu>
    );
  }

  return (
    <Menu>
      <div style={{ padding: '20px', maxWidth: '1400px', margin: '0 auto' }}>
        <Group justify="space-between" mb="xl">
          <Title order={2}>Controle de Acesso (RBAC)</Title>
          <Button variant="light" leftSection={<IconRefresh size={16} />} onClick={fetchMatrix}>
            Atualizar
          </Button>
        </Group>

        {error && (
          <Alert color="red" title="Erro" icon={<IconLock size={16} />} mb="md">
            {error}
          </Alert>
        )}

        <TextInput
          placeholder="Pesquisar endpoints..."
          mb="md"
          leftSection={<IconSearch size={16} />}
          value={searchTerm}
          onChange={(e) => setSearchTerm(e.currentTarget.value)}
        />

        <ScrollArea h={700}>
          <Table stickyHeader striped highlightOnHover withTableBorder withColumnBorders>
            <Table.Thead>
              <Table.Tr>
                <Table.Th>Controller / Action</Table.Th>
                <Table.Th>Método / Rota</Table.Th>
                {data?.roles.map(role => (
                  <Table.Th key={role.id} style={{ textAlign: 'center' }}>
                    {role.name}
                  </Table.Th>
                ))}
              </Table.Tr>
            </Table.Thead>
            <Table.Tbody>
              {filteredEndpoints.map((e, idx) => (
                <Table.Tr key={`${e.controller}-${e.action}-${idx}`}>
                  <Table.Td>
                    <div style={{ fontWeight: 600 }}>{e.controller}</div>
                    <div style={{ fontSize: '12px', color: '#666' }}>{e.action}</div>
                  </Table.Td>
                  <Table.Td>
                    <Badge color={e.httpMethod === 'GET' ? 'blue' : e.httpMethod === 'POST' ? 'green' : 'orange'} size="xs" mr="xs">
                      {e.httpMethod}
                    </Badge>
                    <code style={{ fontSize: '11px', backgroundColor: '#f0f0f0', padding: '2px 4px', borderRadius: '4px' }}>
                      {e.route}
                    </code>
                  </Table.Td>
                  {data?.roles.map(role => {
                    const isEnabled = isPermissionEnabled(role.id, e.controller, e.action);
                    const isSuperAdmin = role.name === 'SuperAdmin';
                    const key = `${role.id}-${e.controller}-${e.action}`;
                    
                    return (
                      <Table.Td key={role.id} style={{ textAlign: 'center' }}>
                        {isSuperAdmin ? (
                          <div style={{ color: '#aaa' }} title="SuperAdmin sempre tem acesso">
                            <IconCheck size={20} stroke={3} />
                          </div>
                        ) : (
                          <div style={{ display: 'flex', justifyContent: 'center' }}>
                            {saving === key ? (
                              <Loader size="xs" />
                            ) : (
                              <Checkbox
                                checked={isEnabled}
                                onChange={() => handleToggle(role.id, e.controller, e.action, isEnabled)}
                              />
                            )}
                          </div>
                        )}
                      </Table.Td>
                    );
                  })}
                </Table.Tr>
              ))}
            </Table.Tbody>
          </Table>
        </ScrollArea>
      </div>
    </Menu>
  );
};

export default AccessControlPage;
