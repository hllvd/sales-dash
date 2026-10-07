import React from 'react';
import { Title, Text, SimpleGrid, Card, Group, Badge, Stack, ThemeIcon, Anchor } from '@mantine/core';
import { IconBook, IconHelp, IconArrowRight, IconFileText } from '@tabler/icons-react';
import Menu from '../Menu';
import './Document.css';

export const DocumentIndex: React.FC = () => {
  return (
    <Menu>
      <div className="doc-page-wrapper">
        <div className="doc-inner-container">
          <Stack gap="xs" mb="xl">
            <Title order={1} style={{ color: '#1f2328' }}>
              Central de Documentação
            </Title>
            <Text c="dimmed">
              Consulte guias práticos, tutoriais de uso e a documentação completa do sistema.
            </Text>
          </Stack>

          <SimpleGrid cols={{ base: 1, md: 2 }} spacing="lg">
            {/* Card Como Fazer */}
            <Card shadow="sm" padding="lg" radius="md" withBorder style={{ backgroundColor: '#ffffff', borderColor: '#d0d7de' }}>
              <Group justify="space-between" mb="xs">
                <ThemeIcon color="teal" size="lg" radius="md">
                  <IconHelp size={22} />
                </ThemeIcon>
                <Badge color="teal" variant="light">
                  Tutoriais
                </Badge>
              </Group>

              <Text fw={600} size="lg" mt="sm" c="#1f2328">
                Como Fazer
              </Text>
              <Text size="sm" c="dimmed" mt="xs" mb="md">
                Guias operacionais passo a passo para as atividades e fluxos comuns da rotina de vendas.
              </Text>

              <Stack gap="sm">
                <Anchor
                  href="#/document/como-fazer/como-importar-contratos-powerbi"
                  underline="hover"
                  style={{ display: 'flex', alignItems: 'center', gap: '8px', color: '#0969da' }}
                >
                  <IconFileText size={16} />
                  <span>Como importar contratos do Power BI</span>
                  <IconArrowRight size={14} style={{ marginLeft: 'auto' }} />
                </Anchor>

                <Anchor
                  href="#/document/como-fazer/como-ajustar-membros-equipe-calendario"
                  underline="hover"
                  style={{ display: 'flex', alignItems: 'center', gap: '8px', color: '#0969da' }}
                >
                  <IconFileText size={16} />
                  <span>Como ajustar membros de equipe usando o calendário</span>
                  <IconArrowRight size={14} style={{ marginLeft: 'auto' }} />
                </Anchor>

                <Anchor
                  href="#/document/como-fazer/como-usar-solicitacoes"
                  underline="hover"
                  style={{ display: 'flex', alignItems: 'center', gap: '8px', color: '#0969da' }}
                >
                  <IconFileText size={16} />
                  <span>Como usar solicitações</span>
                  <IconArrowRight size={14} style={{ marginLeft: 'auto' }} />
                </Anchor>
              </Stack>
            </Card>

            {/* Card Documentação Técnica */}
            <Card shadow="sm" padding="lg" radius="md" withBorder style={{ backgroundColor: '#ffffff', borderColor: '#d0d7de' }}>
              <Group justify="space-between" mb="xs">
                <ThemeIcon color="blue" size="lg" radius="md">
                  <IconBook size={22} />
                </ThemeIcon>
                <Badge color="blue" variant="light">
                  Referência
                </Badge>
              </Group>

              <Text fw={600} size="lg" mt="sm" c="#1f2328">
                Documentação
              </Text>
              <Text size="sm" c="dimmed" mt="xs" mb="md">
                Documentação do sistema, regras de negócio e referências sobre as funcionalidades.
              </Text>

              <Stack gap="sm">
                <Anchor
                  href="#/document/documentacao/introducao"
                  underline="hover"
                  style={{ display: 'flex', alignItems: 'center', gap: '8px', color: '#0969da' }}
                >
                  <IconFileText size={16} />
                  <span>Introdução ao Sistema</span>
                  <IconArrowRight size={14} style={{ marginLeft: 'auto' }} />
                </Anchor>

                <Anchor
                  href="#/document/documentacao/perguntas-frequentes"
                  underline="hover"
                  style={{ display: 'flex', alignItems: 'center', gap: '8px', color: '#0969da' }}
                >
                  <IconFileText size={16} />
                  <span>Perguntas Frequentes (FAQ)</span>
                  <IconArrowRight size={14} style={{ marginLeft: 'auto' }} />
                </Anchor>
              </Stack>
            </Card>
          </SimpleGrid>
        </div>
      </div>
    </Menu>
  );
};

export default DocumentIndex;
