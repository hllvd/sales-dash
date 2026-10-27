import React, { useEffect, useState } from 'react';
import ReactMarkdown from 'react-markdown';
import remarkGfm from 'remark-gfm';
import rehypeRaw from 'rehype-raw';
import { Button, Loader, Alert, Breadcrumbs, Anchor, Text, Group, Badge } from '@mantine/core';
import { IconPrinter, IconAlertCircle, IconArrowLeft, IconSparkles } from '@tabler/icons-react';
import Menu from '../Menu';
import { helpdeskApiService, KnowledgeBaseArticleDto } from '../../services/apiService';
import './Document.css';

interface DocumentPageProps {
  docPath: string; // Ex: "como-fazer/como-ajustar-membros-equipe-calendario"
}

export const DocumentPage: React.FC<DocumentPageProps> = ({ docPath }) => {
  const [content, setContent] = useState<string>('');
  const [loading, setLoading] = useState<boolean>(true);
  const [error, setError] = useState<string | null>(null);
  const [kbArticle, setKbArticle] = useState<KnowledgeBaseArticleDto | null>(null);

  useEffect(() => {
    let isMounted = true;
    setLoading(true);
    setError(null);
    setKbArticle(null);

    const cleanDocPath = docPath.replace(/^\/+|\/+$/g, '');
    const pathParts = cleanDocPath.split('/');
    const slug = pathParts[pathParts.length - 1];
    const url = `${process.env.PUBLIC_URL || ''}/docs/${cleanDocPath}.md`;

    // 1. Try fetching static markdown file
    fetch(url)
      .then(async (res) => {
        if (!res.ok) {
          throw new Error(`Static file not found (${res.status})`);
        }
        return res.text();
      })
      .then((data) => {
        if (isMounted) {
          setContent(data);
          setLoading(false);
        }
      })
      .catch(async () => {
        // 2. Fallback: Check DynamoDB Knowledge Base API
        try {
          const apiRes = await helpdeskApiService.getKnowledgeBaseArticle(slug);
          if (isMounted) {
            if (apiRes.success && apiRes.data) {
              setKbArticle(apiRes.data);
              setContent(`# ${apiRes.data.title}\n\n${apiRes.data.summaryContent}`);
              setLoading(false);
            } else {
              setError(`Documento "${cleanDocPath}" não encontrado na base de documentação.`);
              setLoading(false);
            }
          }
        } catch (apiErr: any) {
          if (isMounted) {
            setError(apiErr.message || 'Falha ao carregar o documento');
            setLoading(false);
          }
        }
      });

    return () => {
      isMounted = false;
    };
  }, [docPath]);

  const handlePrintPdf = () => {
    window.print();
  };

  const pathParts = docPath.split('/');
  const section = pathParts[0] === 'como-fazer' ? 'Como Fazer' : 'Documentação';
  const topicName = pathParts.slice(1).join('/');

  return (
    <Menu>
      <div className="doc-page-wrapper">
        <div className="doc-inner-container">
          <div className="doc-header-actions">
            <div>
              <Breadcrumbs separator="→" mb="xs">
                <Anchor href="#/help" size="sm">
                  Documentos
                </Anchor>
                <Text size="sm" c="dimmed">
                  {section}
                </Text>
                {topicName && (
                  <Text size="sm" fw={500} c="dark">
                    {kbArticle ? kbArticle.title : topicName}
                  </Text>
                )}
                {kbArticle && (
                  <Badge size="xs" variant="light" color={kbArticle.category === 'FAQ' ? 'blue' : 'teal'} leftSection={<IconSparkles size={12} />}>
                    Gerado por IA (Helpdesk)
                  </Badge>
                )}
              </Breadcrumbs>
              <Group gap="xs">
                <Button
                  variant="subtle"
                  size="xs"
                  leftSection={<IconArrowLeft size={16} />}
                  component="a"
                  href="#/help"
                >
                  Voltar ao Índice
                </Button>
              </Group>
            </div>

            <Button
              variant="default"
              leftSection={<IconPrinter size={18} />}
              onClick={handlePrintPdf}
            >
              Exportar PDF
            </Button>
          </div>

          {loading && (
            <div style={{ display: 'flex', justifyContent: 'center', padding: '5rem 0' }}>
              <Loader size="lg" color="blue" />
            </div>
          )}

          {error && (
            <Alert icon={<IconAlertCircle size={16} />} title="Erro ao carregar documento" color="red">
              {error}. Verifique se o arquivo existe em <code>public/docs/{docPath}.md</code>.
            </Alert>
          )}

          {!loading && !error && (
            <div className="doc-github-card">
              <div className="markdown-body">
                <ReactMarkdown remarkPlugins={[remarkGfm]} rehypePlugins={[rehypeRaw]}>
                  {content}
                </ReactMarkdown>
              </div>
            </div>
          )}
        </div>
      </div>
    </Menu>
  );
};

export default DocumentPage;
