# 🎨 SalesApp UI Elements Library & Design Tokens

This document defines the unified, premium UI components and design guidelines for the **SalesApp** client. Every component is built on top of **Mantine UI (v7)** and styled with premium vanilla CSS variables. 

To maintain visual excellence, consistency, and a premium UX, **developers and AI agents must strictly adhere to these components and patterns**. Do not introduce ad-hoc utility styling, raw/harsh colors, or custom layouts that deviate from this system.

---

## 🚨 Banners & Notifications (`<Alert>`)

Use alerts for warnings, errors, success notifications, and user guidance. All alerts must use Mantine’s default modern **light/subtle** styling. Do not use high-contrast filled blocks (`variant="filled"`) or heavy, custom, dark badges inside warning messages.

### Standard Theme Semantics

| Type | Mantine Color | Purpose | Code Example |
| :--- | :--- | :--- | :--- |
| **Warning** | `orange` | Resolved conflicts, data warnings | `<Alert color="orange" title="Atenção" icon={<IconAlertTriangle size={16} />} />` |
| **Error** | `red` | Critical system errors, field validation | `<Alert color="red" title="Erro" icon={<IconLock size={16} />} />` |
| **Success** | `green` | Import completion, successful updates | `<Alert color="green" title="Concluído" icon={<IconCheck size={16} />} />` |
| **Info / Guide** | `blue` | Dynamic instructions, helper notes | `<Alert color="blue" title="Instruções" icon={<IconAlertCircle size={16} />} />` |

### Premium Design Pattern: Warning Banners with Bullet Lists

When presenting multiple warning items or details (e.g., date conflicts or resolved rules), use a nested Mantine `<List>` instead of badges or raw lists.

```typescript
import { Alert, Stack, Text, List } from '@mantine/core';
import { IconAlertTriangle } from '@tabler/icons-react';

<Alert 
  icon={<IconAlertTriangle size={16} />} 
  title="Conflitos de Associação Resolvidos!" 
  color="orange" 
  withCloseButton 
  onClose={onCloseHandler}
  mb="lg"
>
  <Stack gap="xs">
    <Text size="sm" style={{ color: '#4b5563' }}>
      Os seguintes usuários foram removidos de suas equipes anteriores porque foram associados a um novo período sobreposto:
    </Text>
    <List size="sm" withPadding spacing="xs" style={{ color: '#4b5563' }}>
      {warnings.map((warn, i) => (
        <List.Item key={i}>
          {warn}
        </List.Item>
      ))}
    </List>
  </Stack>
</Alert>
```

---

## 🗂️ Tables (`<Table>`)

Tabular data must look premium, modern, and have high visual accessibility. When editing member details, hierarchies, or contracts, render standard Mantine tables styled with high-contrast, scoped CSS.

### Design Guidelines
* **Headers**: Distinct borders and bold lettering.
* **Row Hover Effects**: A subtle grey scale transition on row focus (`#f9fafb` or `#f3f4f6`).
* **Highlights (Owners/Admins)**: Distinct, beautiful background indicators (e.g., a warm, sophisticated amber tint `#fffbeb` with dark golden borders for owners).

```css
/* Premium Table Styling (e.g., inside Modals) */
.modal-members-table {
  width: 100%;
  border-collapse: collapse;
  margin-top: 15px;
}

.modal-members-table th {
  text-align: left;
  padding: 10px 12px;
  border-bottom: 2px solid #e5e7eb;
  color: #374151;
  font-weight: 600;
  font-size: 0.85rem;
}

.modal-members-table td {
  padding: 10px 12px;
  border-bottom: 1px solid #f3f4f6;
  vertical-align: middle;
  font-size: 0.9rem;
  color: #4b5563;
}

/* Owner Row Highlighting */
.modal-members-table tr.is-owner-row {
  background-color: rgba(254, 243, 199, 0.5) !important; /* Soft Amber Tint */
}
```

---

## 🔲 Modals & Dialogs (`StyledModal`)

Modals overlay existing UI to edit records, configure options, or show details. Always use `<StyledModal>` to enforce a uniform layout:
* Centered headers.
* Rounded corners and sleek scrollable dynamic bounds.
* Subtle grey/dark backdrop overlays.

---

## 🔍 Searchable Dropdowns (`SearchableDropdown`)

Never use default native browser select boxes for lists with more than 5 options. 
* Replace with Mantine's custom `Select` or `MultiSelect` with `searchable` prop enabled.
* Provide clean, clear placeholders (e.g. `Buscar por equipe...` or `Selecione um e-mail...`).

---

## 🛡️ Anti-Patterns (Forbid These)

1. **❌ Raw CSS Styling Override Banners**: Avoid styling alerts with absolute black/dark badge blocks or hard borders (`border: '1px solid rgba(253, 224, 71, 0.4)'`). Rely on Mantine's native theme engine.
2. **❌ raw HTML inputs**: Use Mantine's `<TextInput>`, `<Select>`, or `<FileInput>` instead of browser defaults to maintain border-radius and focus-ring consistency.
3. **❌ Hard-coded Card Layouts for Tabular Lists**: Do not display tables/lists as a vertical list of disjointed cards inside dynamic modals; use a clean, scoped standard `<table>` instead.
