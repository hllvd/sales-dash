import React, { useState, useEffect } from 'react';
import { Accordion, TextInput, Select, Grid, Text, Paper } from '@mantine/core';
import { IconFolder, IconInfoCircle } from '@tabler/icons-react';
import { UserMetadataGroup } from '../../services/apiService';

interface UserMetadataSectionProps {
  groups: UserMetadataGroup[];
  isEditing: boolean;
  values: Record<number, string>;
  onChange: (fieldId: number, value: string) => void;
  canEdit: boolean;
}

export const UserMetadataSection: React.FC<UserMetadataSectionProps> = ({
  groups,
  isEditing,
  values,
  onChange,
  canEdit,
}) => {
  const [accordionValue, setAccordionValue] = useState<string | null>(null);

  // Auto-expand in edit mode
  useEffect(() => {
    if (isEditing) {
      setAccordionValue('metadata');
    }
  }, [isEditing]);

  if (!groups || groups.length === 0) {
    return null;
  }

  // Helper to parse dropdown options
  const parseDropdownOptions = (optionsStr?: string | null): { value: string; label: string }[] => {
    if (!optionsStr) return [];
    try {
      const parsed = JSON.parse(optionsStr);
      if (Array.isArray(parsed)) {
        return parsed.map((opt: any) => ({
          value: String(opt),
          label: String(opt),
        }));
      }
    } catch (e) {
      console.error('Error parsing dropdown options', e);
    }
    return [];
  };

  return (
    <Paper withBorder p="md" radius="md" mt="md">
      <Accordion
        value={accordionValue}
        onChange={setAccordionValue}
        variant="separated"
        styles={{
          item: {
            border: 'none',
            backgroundColor: 'transparent',
          },
          control: {
            padding: 0,
            '&:hover': {
              backgroundColor: 'transparent',
            },
          },
          content: {
            padding: '12px 0 0 0',
          },
        }}
      >
        <Accordion.Item value="metadata">
          <Accordion.Control>
            <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
              <IconInfoCircle size={20} style={{ color: '#228be6' }} />
              <Text fw={600} size="lg">Informações Adicionais</Text>
            </div>
          </Accordion.Control>
          <Accordion.Panel>
            {groups.map((group, groupIdx) => {
              const hasGroupLabel = !!group.groupLabel;

              return (
                <div key={groupIdx} style={{ marginBottom: groupIdx < groups.length - 1 ? '20px' : '0' }}>
                  {hasGroupLabel && (
                    <div
                      style={{
                        display: 'flex',
                        alignItems: 'center',
                        gap: '6px',
                        marginBottom: '12px',
                        borderBottom: '1px solid #e9ecef',
                        paddingBottom: '4px',
                      }}
                    >
                      <IconFolder size={16} style={{ color: '#868e96' }} />
                      <Text fw={600} size="sm" c="dimmed">
                        {group.groupLabel}
                      </Text>
                    </div>
                  )}

                  <Grid gutter="md">
                    {group.fields.map((field) => {
                      const currentValue =
                        values[field.fieldId] !== undefined
                          ? values[field.fieldId]
                          : (field.value || '');

                      if (isEditing && canEdit) {
                        return (
                          <Grid.Col key={field.fieldId} span={{ base: 12, md: 6 }}>
                            {field.fieldType === 'dropdown' ? (
                              <Select
                                label={field.label}
                                placeholder="Selecione uma opção..."
                                required={field.isRequired}
                                data={parseDropdownOptions(field.dropdownOptions)}
                                value={currentValue || null}
                                onChange={(val) => onChange(field.fieldId, val || '')}
                                clearable={!field.isRequired}
                              />
                            ) : (
                              <TextInput
                                label={field.label}
                                placeholder={`Digite ${field.label.toLowerCase()}...`}
                                required={field.isRequired}
                                value={currentValue}
                                onChange={(e) => onChange(field.fieldId, e.currentTarget.value)}
                              />
                            )}
                          </Grid.Col>
                        );
                      } else {
                        // View Mode
                        return (
                          <Grid.Col key={field.fieldId} span={{ base: 12, md: 6 }}>
                            <div style={{ display: 'flex', flexDirection: 'column' }}>
                              <Text size="xs" c="dimmed" fw={500}>
                                {field.label} {field.isRequired && <span style={{ color: 'red' }}>*</span>}
                              </Text>
                              <Text size="sm" fw={500} style={{ minHeight: '20px' }}>
                                {field.value || '-'}
                              </Text>
                            </div>
                          </Grid.Col>
                        );
                      }
                    })}
                  </Grid>
                </div>
              );
            })}
          </Accordion.Panel>
        </Accordion.Item>
      </Accordion>
    </Paper>
  );
};
