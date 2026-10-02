import React, { useEffect, useState } from 'react';
import ReactMarkdown from 'react-markdown';
import remarkGfm from 'remark-gfm';
import rehypeRaw from 'rehype-raw';
import { Button, Loader, Alert, Breadcrumbs, Anchor, Text, Group } from '@mantine/core';
import { IconPrinter, IconAlertCircle, IconArrowLeft } from '@tabler/icons-react';
import Menu from '../Menu';
import './Document.css';

interface DocumentPageProps {
  docPath: string; // Ex: "como-fazer/como-ajustar-membros-equipe-calendario"
}

export const DocumentPage: React.FC<DocumentPageProps> = ({ docPath }) => {
  const [content, setContent] = useState<string>('');
  const [loading, setLoading] = useState<boolean>(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let isMounted = true;
    setLoading(true);
    setError(null);

    const cleanDocPath = docPath.replace(/^\/+|\/+$/g, '');
    const url = `${process.env.PUBLIC_URL || ''}/docs/${cleanDocPath}.md`;

    fetch(url)
      .then((res) => {
        if (!res.ok) {
          throw new Error(`Documento não encontrado (status HTTP ${res.status})`);
        }
        return res.text();
      })
      .then((data) => {
        if (isMounted) {
          setContent(data);
          setLoading(false);
        }
      })
      .catch((err) => {
        if (isMounted) {
          setError(err.message || 'Falha ao carregar o documento');
          setLoading(false);
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
                <Anchor href="#/document" size="sm">
                  Documentos
                </Anchor>
                <Text size="sm" c="dimmed">
                  {section}
                </Text>
                {topicName && (
                  <Text size="sm" fw={500} c="dark">
                    {topicName}
                  </Text>
                )}
              </Breadcrumbs>
              <Group gap="xs">
                <Button
                  variant="subtle"
                  size="xs"
                  leftSection={<IconArrowLeft size={16} />}
                  component="a"
                  href="#/document"
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
