import React, { useState } from 'react';
import { 
  Table, 
  Badge, 
  Text, 
  TextInput, 
  NumberFormatter
} from '@mantine/core';
import { IconSearch, IconCheck, IconAlertCircle } from '@tabler/icons-react';
import { MatriculaHealth } from '../../services/contractService';
import dayjs from 'dayjs';

interface MatriculasTabProps {
  data: MatriculaHealth[];
}

const MatriculasTab: React.FC<MatriculasTabProps> = ({ data }) => {
  const [search, setSearch] = useState('');

  const filteredData = data.filter(item => 
    item.matricula.toLowerCase().includes(search.toLowerCase())
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

  const rows = filteredData.map((item) => (
    <Table.Tr key={item.matricula}>
      <Table.Td>
        <Text fw={600}>{item.matricula}</Text>
      </Table.Td>
      <Table.Td>
        <Text fw={600} size="sm">
          <NumberFormatter value={item.contractCount} thousandSeparator="." decimalSeparator="," />
        </Text>
      </Table.Td>
      <Table.Td>
        <Text size="sm">{dayjs(item.lastUpdate).format('DD/MM/YYYY HH:mm')}</Text>
        <Text size="xs" c="dimmed">{dayjs(item.lastUpdate).fromNow()}</Text>
      </Table.Td>
      <Table.Td>{getStatusBadge(item.status)}</Table.Td>
    </Table.Tr>
  ));

  return (
    <div className="matriculas-tab-container">
      <div className="search-container" style={{ marginBottom: '1rem' }}>
        <TextInput
          placeholder="Pesquisar matrícula..."
          leftSection={<IconSearch size={16} />}
          value={search}
          onChange={(event) => setSearch(event.currentTarget.value)}
          style={{ maxWidth: 400 }}
        />
      </div>

      <div className="monitoring-table-container">
        <Table.ScrollContainer minWidth={600}>
          <Table striped highlightOnHover>
            <Table.Thead>
              <Table.Tr>
                <Table.Th>Matrícula</Table.Th>
                <Table.Th>Qtd. Contratos</Table.Th>
                <Table.Th>Última Atualização</Table.Th>
                <Table.Th>Status</Table.Th>
              </Table.Tr>
            </Table.Thead>
            <Table.Tbody>
              {rows.length > 0 ? rows : (
                <Table.Tr>
                  <Table.Td colSpan={4}>
                    <Text c="dimmed" ta="center" py="xl">Nenhuma matrícula encontrada</Text>
                  </Table.Td>
                </Table.Tr>
              )}
            </Table.Tbody>
          </Table>
        </Table.ScrollContainer>
      </div>
    </div>
  );
};

export default MatriculasTab;
