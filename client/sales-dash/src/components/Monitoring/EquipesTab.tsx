import React, { useState } from 'react';
import { 
  Accordion, 
  Table, 
  Badge, 
  Text, 
  TextInput, 
  Group, 
  NumberFormatter
} from '@mantine/core';
import { IconSearch, IconUsers, IconCheck, IconAlertCircle } from '@tabler/icons-react';
import { TeamMatriculaHealth } from '../../services/contractService';
import dayjs from 'dayjs';

interface EquipesTabProps {
  data: TeamMatriculaHealth[];
}

const EquipesTab: React.FC<EquipesTabProps> = ({ data }) => {
  const [search, setSearch] = useState('');

  const searchLower = search.trim().toLowerCase();
  const filteredTeams = data.filter(team => 
    !searchLower ||
    team.teamName.toLowerCase().includes(searchLower)
  );

  const getStatusBadge = (status: string) => {
    switch (status) {
      case 'Healthy':
        return <Badge color="green" leftSection={<IconCheck size={12} />}>Normal</Badge>;
      case 'Warning':
        return <Badge color="yellow" leftSection={<IconAlertCircle size={12} />}>Requer Atenção</Badge>;
      case 'OutOfDate':
        return <Badge color="orange" leftSection={<IconAlertCircle size={12} />}>Atenção</Badge>;
      case 'Danger':
        return <Badge color="red" leftSection={<IconAlertCircle size={12} />}>Muito Importante</Badge>;
      default:
        return <Badge color="gray">{status}</Badge>;
    }
  };

  const getWorstStatusBadge = (status: string) => {
    switch (status) {
      case 'Healthy':
        return <Badge color="green" variant="light">Tudo Normal</Badge>;
      case 'Warning':
        return <Badge color="yellow" variant="light">Alerta (Atenção)</Badge>;
      case 'OutOfDate':
        return <Badge color="orange" variant="light">Alerta Médio</Badge>;
      case 'Danger':
        return <Badge color="red" variant="filled">Crítico (Danger)</Badge>;
      default:
        return <Badge color="gray" variant="light">{status}</Badge>;
    }
  };

  return (
    <div>
      <div className="search-container" style={{ marginBottom: '1.5rem' }}>
        <TextInput
          placeholder="Pesquisar equipe..."
          leftSection={<IconSearch size={16} />}
          value={search}
          onChange={(event) => setSearch(event.currentTarget.value)}
          style={{ maxWidth: 400 }}
        />
      </div>

      {filteredTeams.length > 0 ? (
        <Accordion variant="separated" radius="md">
          {filteredTeams.map((team) => (
            <Accordion.Item key={team.teamId} value={team.teamId.toString()}>
              <Accordion.Control>
                <Group justify="space-between" wrap="nowrap" style={{ paddingRight: '1rem' }}>
                  <Group gap="sm">
                    <IconUsers size={20} color="#228be6" />
                    <div>
                      <Text fw={600} size="sm">{team.teamName}</Text>
                      <Text size="xs" c="dimmed">ID da Equipe: {team.teamId}</Text>
                    </div>
                  </Group>
                  <Group gap="xs">
                    <Badge color="blue" variant="outline">
                      {team.totalMatriculas} {team.totalMatriculas === 1 ? 'Matrícula' : 'Matrículas'}
                    </Badge>
                    {getWorstStatusBadge(team.worstStatus)}
                  </Group>
                </Group>
              </Accordion.Control>
              <Accordion.Panel>
                <Table striped highlightOnHover verticalSpacing="xs">
                  <Table.Thead>
                    <Table.Tr>
                      <Table.Th style={{ width: '30%' }}>Matrícula</Table.Th>
                      <Table.Th style={{ width: '20%' }}>Qtd. Contratos</Table.Th>
                      <Table.Th style={{ width: '30%' }}>Última Atualização</Table.Th>
                      <Table.Th style={{ width: '20%' }}>Status</Table.Th>
                    </Table.Tr>
                  </Table.Thead>
                  <Table.Tbody>
                    {team.matriculas.map((mat) => (
                      <Table.Tr key={mat.matricula}>
                        <Table.Td>
                          <Text fw={600} size="sm">{mat.matricula}</Text>
                        </Table.Td>
                        <Table.Td>
                          <Text size="sm">
                            <NumberFormatter value={mat.contractCount} thousandSeparator="." decimalSeparator="," />
                          </Text>
                        </Table.Td>
                        <Table.Td>
                          <Text size="sm">{dayjs(mat.lastUpdate).format('DD/MM/YYYY HH:mm')}</Text>
                          <Text size="xs" c="dimmed">{dayjs(mat.lastUpdate).fromNow()}</Text>
                        </Table.Td>
                        <Table.Td>{getStatusBadge(mat.status)}</Table.Td>
                      </Table.Tr>
                    ))}
                  </Table.Tbody>
                </Table>
              </Accordion.Panel>
            </Accordion.Item>
          ))}
        </Accordion>
      ) : (
        <Text c="dimmed" ta="center" py="xl">Nenhuma equipe encontrada</Text>
      )}
    </div>
  );
};

export default EquipesTab;
