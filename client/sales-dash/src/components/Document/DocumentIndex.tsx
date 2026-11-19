import React, { useState, useEffect, useMemo } from 'react';
import { Title, Text, SimpleGrid, Card, Group, Badge, Stack, ThemeIcon, Anchor, TextInput, ActionIcon, Box } from '@mantine/core';
import {
  IconBook,
  IconHelp,
  IconArrowRight,
  IconFileText,
  IconSparkles,
  IconSearch,
  IconX,
  IconListSearch,
  IconFolder,
} from '@tabler/icons-react';
import Menu from '../Menu';
import { HelpdeskSection } from './HelpdeskSection';
import { helpdeskApiService, KnowledgeBaseArticleDto } from '../../services/apiService';
import './Document.css';

interface IndexTopic {
  title: string;
  description: string;
  href: string;
  category: 'Vendas & Atribuição' | 'Importações & Power BI' | 'Equipes & Calendário' | 'Solicitações' | 'Dúvidas & Regras';
  tags: string[];
}

const TOPICS_INDEX: IndexTopic[] = [
  {
    title: 'Como atribuir e solicitar contratos',
    description: 'Atribuição automática de contratos do Ava Pro ao titular e solicitação de transferência pelo consultor.',
    href: '#/help/como-fazer/como-atribuir-e-solicitar-contratos',
    category: 'Vendas & Atribuição',
    tags: ['contrato', 'atribuição', 'ava pro', 'gestor', 'venda', 'solicitação', 'transferência', 'matrícula'],
  },
  {
    title: 'Guia do Consultor: Dominando Suas Vendas',
    description: 'Acompanhamento de carteira, totais ativos, atrasados, retenção de clientes e estimativa de comissão.',
    href: '#/help/documentacao/guia-do-consultor',
    category: 'Vendas & Atribuição',
    tags: ['consultor', 'comissão', 'meus contratos', 'vendas', 'produção', 'carteira', 'retenção', 'gráfico'],
  },
  {
    title: 'Como usar solicitações',
    description: 'Guia completo para pedir alteração de superior, matrícula, criação de equipe (Guimel), nível e perfil.',
    href: '#/help/como-fazer/como-usar-solicitacoes',
    category: 'Solicitações',
    tags: ['solicitação', 'pedidos', 'aprovação', 'superior', 'guimel', 'equipe', 'promoção', 'perfil'],
  },
  {
    title: 'Como importar contratos do Power BI',
    description: 'Instruções para subir planilhas XLSX e atualizar o banco de contratos e cotas.',
    href: '#/help/como-fazer/como-importar-contratos-powerbi',
    category: 'Importações & Power BI',
    tags: ['importação', 'power bi', 'planilha', 'upload', 'contratos', 'lote'],
  },
  {
    title: 'Como atualizar senha no robô PowerBI',
    description: 'Passo a passo para atualizar credenciais de acesso do scraper de extração automatizada.',
    href: '#/help/como-fazer/como-atualizar-senha-powerbi-scrape',
    category: 'Importações & Power BI',
    tags: ['robô', 'powerbi', 'senha', 'credenciais', 'scraper', 'automação'],
  },
  {
    title: 'Como ajustar membros de equipe usando o calendário',
    description: 'Gestão de membros, movimentação de consultores e histórico de vigências nas equipes.',
    href: '#/help/como-fazer/como-ajustar-membros-equipe-calendario',
    category: 'Equipes & Calendário',
    tags: ['equipe', 'calendário', 'membros', 'gestor', 'consultores', 'histórico'],
  },
  {
    title: 'Perguntas Frequentes (FAQ) & Regras do Sistema',
    description: 'Tire dúvidas sobre atualização de status, matrículas na importação, QA e regras de comissionamento.',
    href: '#/help/documentacao/perguntas-frequentes',
    category: 'Dúvidas & Regras',
    tags: ['faq', 'perguntas', 'dúvidas', 'regras', 'status', 'matrícula', 'qa', 'comissão'],
  },
  {
    title: 'Introdução ao Sistema',
    description: 'Conceitos fundamentais, visão geral e objetivos do Sales Dashboard.',
    href: '#/help/documentacao/introducao',
    category: 'Dúvidas & Regras',
    tags: ['introdução', 'sobre', 'módulos', 'visão geral', 'dashboard'],
  },
];

export const DocumentIndex: React.FC = () => {
  const [searchQuery, setSearchQuery] = useState('');
  const [kbArticles, setKbArticles] = useState<KnowledgeBaseArticleDto[]>([]);

  useEffect(() => {
    helpdeskApiService.listKnowledgeBaseArticles()
      .then((res) => {
        if (res.success && res.data) {
          setKbArticles(res.data);
        }
      })
      .catch((err) => {
        console.error('Error fetching knowledge base articles:', err);
      });
  }, []);

  const comoFazerKb = kbArticles.filter(a => a.category === 'COMO_FAZER');
  const faqKb = kbArticles.filter(a => a.category === 'FAQ');

  const filteredTopics = useMemo(() => {
    const q = searchQuery.trim().toLowerCase();
    if (!q) return TOPICS_INDEX;
    return TOPICS_INDEX.filter((t) =>
      t.title.toLowerCase().includes(q) ||
      t.description.toLowerCase().includes(q) ||
      t.category.toLowerCase().includes(q) ||
      t.tags.some(tag => tag.toLowerCase().includes(q))
    );
  }, [searchQuery]);

  const categories = useMemo(() => {
    const map = new Map<string, IndexTopic[]>();
    filteredTopics.forEach((t) => {
      const list = map.get(t.category) || [];
      list.push(t);
      map.set(t.category, list);
    });
    return Array.from(map.entries());
  }, [filteredTopics]);

  return (
    <Menu>
      <div className="doc-page-wrapper">
        <div className="doc-inner-container">
          <Stack gap="xs" mb="lg">
            <Title order={1} style={{ color: '#1f2328' }}>
              Central de Documentação e Ajuda
            </Title>
            <Text c="dimmed">
              Consulte guias práticos, tutoriais passo a passo, perguntas frequentes e o índice completo de tópicos do sistema.
            </Text>
          </Stack>

          {/* Barra de Pesquisa de Tópicos */}
          <Box mb="xl">
            <TextInput
              size="md"
              placeholder="Pesquisar por assunto, guia, regra ou palavra-chave (ex: matrícula, transferência, importação)..."
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              leftSection={<IconSearch size={18} color="#6e7781" />}
              rightSection={
                searchQuery ? (
                  <ActionIcon variant="subtle" color="gray" onClick={() => setSearchQuery('')}>
                    <IconX size={16} />
                  </ActionIcon>
                ) : null
              }
              styles={{
                input: {
                  backgroundColor: '#ffffff',
                  borderColor: '#d0d7de',
                  borderRadius: '8px',
                },
              }}
            />
          </Box>

          {/* Seção Índice Geral de Tópicos */}
          <Card shadow="sm" padding="lg" radius="md" withBorder mb="xl" style={{ backgroundColor: '#ffffff', borderColor: '#d0d7de' }}>
            <Group justify="space-between" mb="md">
              <Group gap="xs">
                <ThemeIcon color="indigo" size="md" radius="md">
                  <IconListSearch size={18} />
                </ThemeIcon>
                <Text fw={600} size="md" c="#1f2328">
                  Índice Geral de Tópicos
                </Text>
              </Group>
              <Badge color={searchQuery ? 'orange' : 'gray'} variant="light">
                {filteredTopics.length} {filteredTopics.length === 1 ? 'tópico' : 'tópicos'}
              </Badge>
            </Group>

            {filteredTopics.length === 0 ? (
              <Text size="sm" c="dimmed" ta="center" py="md">
                Nenhum tópico encontrado para "{searchQuery}". Tente outros termos como "contratos", "equipes", "solicitações" ou "importação".
              </Text>
            ) : (
              <SimpleGrid cols={{ base: 1, sm: 2, lg: 3 }} spacing="md">
                {categories.map(([categoryName, topics]) => (
                  <Box
                    key={categoryName}
                    p="sm"
                    style={{
                      backgroundColor: '#f6f8fa',
                      borderRadius: '6px',
                      border: '1px solid #e1e4e8',
                    }}
                  >
                    <Group gap="xs" mb="xs">
                      <IconFolder size={16} color="#4b5563" />
                      <Text size="xs" fw={700} c="#4b5563" style={{ textTransform: 'uppercase', letterSpacing: '0.5px' }}>
                        {categoryName}
                      </Text>
                    </Group>
                    <Stack gap="xs">
                      {topics.map((t) => (
                        <Box key={t.href}>
                          <Anchor
                            href={t.href}
                            underline="hover"
                            style={{
                              display: 'block',
                              color: '#0969da',
                              fontSize: '13px',
                              fontWeight: 600,
                              lineHeight: 1.4,
                            }}
                          >
                            {t.title}
                          </Anchor>
                          <Text size="xs" c="dimmed" lineClamp={2} style={{ marginTop: '2px' }}>
                            {t.description}
                          </Text>
                        </Box>
                      ))}
                    </Stack>
                  </Box>
                ))}
              </SimpleGrid>
            )}
          </Card>

          {/* Cards Detalhados: Como Fazer e Documentação Técnica */}
          <SimpleGrid cols={{ base: 1, md: 2 }} spacing="lg">
            {/* Card Como Fazer */}
            <Card shadow="sm" padding="lg" radius="md" withBorder style={{ backgroundColor: '#ffffff', borderColor: '#d0d7de' }}>
              <Group justify="space-between" mb="xs">
                <ThemeIcon color="teal" size="lg" radius="md">
                  <IconHelp size={22} />
                </ThemeIcon>
                <Badge color="teal" variant="light">
                  Tutoriais Passo a Passo
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
                  href="#/help/como-fazer/como-atribuir-e-solicitar-contratos"
                  underline="hover"
                  style={{ display: 'flex', alignItems: 'center', gap: '8px', color: '#0969da' }}
                >
                  <IconFileText size={16} />
                  <span>Como atribuir e solicitar contratos</span>
                  <IconArrowRight size={14} style={{ marginLeft: 'auto' }} />
                </Anchor>

                <Anchor
                  href="#/help/como-fazer/como-importar-contratos-powerbi"
                  underline="hover"
                  style={{ display: 'flex', alignItems: 'center', gap: '8px', color: '#0969da' }}
                >
                  <IconFileText size={16} />
                  <span>Como importar contratos do Power BI</span>
                  <IconArrowRight size={14} style={{ marginLeft: 'auto' }} />
                </Anchor>

                <Anchor
                  href="#/help/como-fazer/como-ajustar-membros-equipe-calendario"
                  underline="hover"
                  style={{ display: 'flex', alignItems: 'center', gap: '8px', color: '#0969da' }}
                >
                  <IconFileText size={16} />
                  <span>Como ajustar membros de equipe usando o calendário</span>
                  <IconArrowRight size={14} style={{ marginLeft: 'auto' }} />
                </Anchor>

                <Anchor
                  href="#/help/como-fazer/como-usar-solicitacoes"
                  underline="hover"
                  style={{ display: 'flex', alignItems: 'center', gap: '8px', color: '#0969da' }}
                >
                  <IconFileText size={16} />
                  <span>Como usar solicitações</span>
                  <IconArrowRight size={14} style={{ marginLeft: 'auto' }} />
                </Anchor>

                <Anchor
                  href="#/help/como-fazer/como-atualizar-senha-powerbi-scrape"
                  underline="hover"
                  style={{ display: 'flex', alignItems: 'center', gap: '8px', color: '#0969da' }}
                >
                  <IconFileText size={16} />
                  <span>Como atualizar senha no robô PowerBI</span>
                  <IconArrowRight size={14} style={{ marginLeft: 'auto' }} />
                </Anchor>

                {/* Dynamic articles generated by AI */}
                {comoFazerKb.map((article) => (
                  <Anchor
                    key={article.articleId}
                    href={`#/document/como-fazer/${article.slug}`}
                    underline="hover"
                    style={{ display: 'flex', alignItems: 'center', gap: '8px', color: '#0969da' }}
                  >
                    <IconSparkles size={16} color="#0d9488" />
                    <span>{article.title}</span>
                    <Badge size="xs" variant="outline" color="teal">IA</Badge>
                    <IconArrowRight size={14} style={{ marginLeft: 'auto' }} />
                  </Anchor>
                ))}
              </Stack>
            </Card>

            {/* Card Documentação Técnica & FAQ */}
            <Card shadow="sm" padding="lg" radius="md" withBorder style={{ backgroundColor: '#ffffff', borderColor: '#d0d7de' }}>
              <Group justify="space-between" mb="xs">
                <ThemeIcon color="blue" size="lg" radius="md">
                  <IconBook size={22} />
                </ThemeIcon>
                <Badge color="blue" variant="light">
                  Referência & FAQ
                </Badge>
              </Group>

              <Text fw={600} size="lg" mt="sm" c="#1f2328">
                Documentação & Perguntas Frequentes
              </Text>
              <Text size="sm" c="dimmed" mt="xs" mb="md">
                Documentação do sistema, regras de negócio e respostas para dúvidas frequentes.
              </Text>

              <Stack gap="sm">
                <Anchor
                  href="#/help/documentacao/guia-do-consultor"
                  underline="hover"
                  style={{ display: 'flex', alignItems: 'center', gap: '8px', color: '#0969da' }}
                >
                  <IconFileText size={16} />
                  <span>Guia do Consultor (Vendas & Comissões)</span>
                  <IconArrowRight size={14} style={{ marginLeft: 'auto' }} />
                </Anchor>

                <Anchor
                  href="#/help/documentacao/perguntas-frequentes"
                  underline="hover"
                  style={{ display: 'flex', alignItems: 'center', gap: '8px', color: '#0969da' }}
                >
                  <IconFileText size={16} />
                  <span>Perguntas Frequentes (FAQ) & Regras</span>
                  <IconArrowRight size={14} style={{ marginLeft: 'auto' }} />
                </Anchor>

                <Anchor
                  href="#/help/documentacao/introducao"
                  underline="hover"
                  style={{ display: 'flex', alignItems: 'center', gap: '8px', color: '#0969da' }}
                >
                  <IconFileText size={16} />
                  <span>Introdução ao Sistema</span>
                  <IconArrowRight size={14} style={{ marginLeft: 'auto' }} />
                </Anchor>

                {/* Dynamic FAQ articles generated by AI */}
                {faqKb.map((article) => (
                  <Anchor
                    key={article.articleId}
                    href={`#/document/documentacao/${article.slug}`}
                    underline="hover"
                    style={{ display: 'flex', alignItems: 'center', gap: '8px', color: '#0969da' }}
                  >
                    <IconSparkles size={16} color="#2563eb" />
                    <span>{article.title}</span>
                    <Badge size="xs" variant="outline" color="blue">IA</Badge>
                    <IconArrowRight size={14} style={{ marginLeft: 'auto' }} />
                  </Anchor>
                ))}
              </Stack>
            </Card>
          </SimpleGrid>

          {/* Helpdesk FAQ / Q&A Section */}
          <HelpdeskSection />
        </div>
      </div>
    </Menu>
  );
};

export default DocumentIndex;

