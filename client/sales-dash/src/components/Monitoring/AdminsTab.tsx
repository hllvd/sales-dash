import React, { useState } from 'react';
import { 
  Table, 
  Badge, 
  Text, 
  TextInput, 
  Tooltip,
  NumberFormatter
} from '@mantine/core';
import { IconSearch, IconFileSpreadsheet } from '@tabler/icons-react';
import { AdminImportStats } from '../../services/contractService';
import dayjs from 'dayjs';

interface AdminsTabProps {
  data: AdminImportStats[];
}

const AdminsTab: React.FC<AdminsTabProps> = ({ data }) => {
  const [search, setSearch] = useState('');

  const filteredAdmins = data.filter(admin => 
    admin.userName.toLowerCase().includes(search.toLowerCase()) ||
    admin.userEmail.toLowerCase().includes(search.toLowerCase())
  );

  const rows = filteredAdmins.map((item) => (
    <Table.Tr key={item.userId}>
      <Table.Td>
        <Text fw={600} size="sm">{item.userName}</Text>
        <Text size="xs" c="dimmed">ID Interno: {item.userInternalId}</Text>
      </Table.Td>
      <Table.Td>
        <Text size="sm">{item.userEmail}</Text>
      </Table.Td>
      <Table.Td>
        <Text fw={600} size="sm" style={{ display: 'flex', alignItems: 'center', gap: '4px' }}>
          <IconFileSpreadsheet size={16} color="#40c057" />
          <NumberFormatter value={item.totalImports} thousandSeparator="." decimalSeparator="," />
        </Text>
      </Table.Td>
      <Table.Td>
        {item.lastImportAt ? (
          <Tooltip label={dayjs(item.lastImportAt).format('dddd, D [de] MMMM [de] YYYY [às] HH:mm')}>
            <div>
              <Text size="sm" fw={500}>{dayjs(item.lastImportAt).format('DD/MM/YYYY HH:mm')}</Text>
              <Text size="xs" c="dimmed">{dayjs(item.lastImportAt).fromNow()}</Text>
            </div>
          </Tooltip>
        ) : (
          <Badge color="gray" variant="light">Nunca importou</Badge>
        )}
      </Table.Td>
    </Table.Tr>
  ));

  return (
    <div>
      <div className="search-container" style={{ marginBottom: '1rem' }}>
        <TextInput
          placeholder="Pesquisar admin..."
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
                <Table.Th>Admin</Table.Th>
                <Table.Th>E-mail</Table.Th>
                <Table.Th>Total de Imports</Table.Th>
                <Table.Th>Último Import</Table.Th>
              </Table.Tr>
            </Table.Thead>
            <Table.Tbody>
              {rows.length > 0 ? rows : (
                <Table.Tr>
                  <Table.Td colSpan={4}>
                    <Text c="dimmed" ta="center" py="xl">Nenhum usuário admin encontrado</Text>
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

export default AdminsTab;
