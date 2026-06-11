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

## 🔲 Modals & Dialogs (`StyledModal` & `StandardModal`)

Modals overlay existing UI to edit records, configure options, delete entities, or show details. Always use `<StyledModal>` (for forms) or `<StandardModal>` (for confirmations and custom layouts) to enforce a uniform layout:
* **Light Theme**: Always use a clean white background (`#ffffff`) for content and headers to prevent low-contrast or invisible text.
* **Centered headers & Premium Titles**: Modal titles must use the `<Title>` component with color `#1c1c1e` and weight 700.
* **Mantine Buttons**: Always use Mantine `<Button>` components in footers and actions instead of raw HTML buttons.
* Rounded corners and sleek scrollable dynamic bounds.
* Subtle grey/dark backdrop overlays.

---

## 🔍 Searchable Dropdowns (`SearchableDropdown`)

Never use default native browser select boxes for lists with more than 5 options. 
* Replace with Mantine's custom `Select` or `MultiSelect` with `searchable` prop enabled.
* Provide clean, clear placeholders (e.g. `Buscar por equipe...` or `Selecione um e-mail...`).

---

## 👥 Dual-Column Member Management Modal (`TeamMembersModal`)

For rich, highly interactive member assignment, use a **Dual-Column Light Modal layout**. This provides a massive, high-efficiency drag-and-drop-style assignment feel instead of standard checklists.

### 🎨 Visual & Styling Guidelines
* **Light Contrast**: Always use a clean **light-themed modal background** (`#f8f9fa`) with **pure white card components** (`#ffffff`). Never use black or very dark backgrounds here.
* **Dimensions**: Enforce a large modal viewport (`size="75%"` or `size="xl"`).
* **Dynamic Header Bar**: Place a white card at the top with inline team name modification and dynamic validation status.

### 🌳 BFS Hierarchical Sorting Order (Left Column)
The available members column must follow a Level-Order Tree Traversal (BFS) based on the Team Owner:
1. **Direct Supervisor Link**: Active users whose direct supervisor (`parentUserId`) is the current Team Owner.
2. **Subsequent Levels**: Subordinate levels sorted by creation date (`createdAt`).
3. **BFS Re-activity**: Clicking the crown icon next to any member in the right column sets them as Owner, instantly re-indexing the left column's BFS ordering.

### ⚡ One-Click Event Handling
* Move actions must trigger immediately upon clicking any region of the available user card.
* Instantly commit the changes to the API rather than buffering state with a final form button.

---

## 👤 Reusable User Profile & Performance Metrics Component (`UserProfile`)

The `UserProfile` component provides a unified, highly aesthetic view for displaying and managing a user's details, classification levels, matriculas, and lazy-loaded performance metrics.

### 🎨 Visual & Styling Guidelines
* **Dual Contexts**: Functions seamlessly as a full-page view (`mode="page"`) or a wide modal overlay (`mode="modal"`).
* **Initials Avatar**: A circular avatar displaying the user's initials using a premium gradient (`linear-gradient(135deg, #6366f1 0%, #4f46e5 100%)`).
* **Interactive Accents**: Actionable fields feature subtle highlight animations, smooth transitions, and distinct HSL theme border highlights for individual statistics cards.
* **Lazy Stats Section**: Employs an `IntersectionObserver` to defer fetching performance metrics (`GET /api/users/{id}/stats`) until the stats container scrolls into viewport, rendering animated skeleton pulse cards while loading.

### 🔌 Code Integration Examples

#### Full Page Shell View:
```tsx
import { UserProfile } from './components/UserProfile';

const MyProfilePage = () => {
  return (
    <Menu>
      <div className="my-profile-page" style={{ padding: '24px', maxWidth: '1200px' }}>
        <UserProfile userId={currentUser.id} mode="page" />
      </div>
    </Menu>
  );
};
```

#### Wide Sheet Modal View:
```tsx
import { UserProfileModal } from './components/UserProfile';

const UsersPage = () => {
  const [profileUserId, setProfileUserId] = useState<string | null>(null);
  
  return (
    <>
      {/* Name click triggers profile view */}
      <span onClick={() => setProfileUserId(user.id)}>{user.name}</span>

      <UserProfileModal
        userId={profileUserId}
        opened={profileUserId !== null}
        onClose={() => setProfileUserId(null)}
      />
    </>
  );
};
```

---

## 📝 Reusable Form Inputs & Modal Fields (`FormField`)

To ensure flawless readability on both light and dark backgrounds, form fields and modals must use high-contrast text configurations.

### 🎨 Color & Label Contrast Rules
* **Modals & Light Backgrounds**: 
  - Default input labels must utilize `#1c1c1e` (Primary Charcoal Text) to guarantee accessibility compliance against white (`#ffffff`) background layers.
  - Subtexts or input descriptions must use `#6b7280` (Secondary Text).
* **High Contrast Checks**: When designing custom layout shells, verify that no hard-coded light colors (like `#e9ecef`) are applied to labels inside white modals.

---

## 🧭 Navigation Submenus

To keep the main application shell structured and neat, group related pages using collapsible navigation submenus.
* **Pattern**: Parent `<NavLink>` component containing child `<NavLink>` items.
* **Visual styling**: Offset children using `childrenOffset={28}` and apply custom active root logic to highlight the parent whenever a sub-page route is active.
* **Consistency**: Do not mix flat link items with submenus without assigning clear semantic parent icons (e.g. `IconUsers` or `IconActivity`).

```typescript
<NavLink
  label="Usuários"
  leftSection={<IconUsers size={20} />}
  childrenOffset={28}
  styles={navLinkStyles('users-parent')}
  active={currentPath === '#/users' || currentPath === '#/users/tree'}
  color="red"
  variant="filled"
  defaultOpened={currentPath === '#/users' || currentPath === '#/users/tree'}
>
  <NavLink
    href="#/users"
    label="Lista"
    active={isActive('#/users')}
    styles={navLinkStyles('#/users')}
  />
  <NavLink
    href="#/users/tree"
    label="Árvore"
    leftSection={<IconSitemap size={16} />}
    active={isActive('#/users/tree')}
    styles={navLinkStyles('#/users/tree')}
  />
</NavLink>
```

---

## 🛡️ Anti-Patterns (Forbid These)

1.  **❌ Raw CSS Styling Override Banners**: Avoid styling alerts with absolute black/dark badge blocks or hard borders (`border: '1px solid rgba(253, 224, 71, 0.4)'`). Rely on Mantine's native theme engine.
2.  **❌ raw HTML inputs**: Use Mantine's `<TextInput>`, `<Select>`, or `<FileInput>` instead of browser defaults to maintain border-radius and focus-ring consistency.
3.  **❌ Hard-coded Card Layouts for Tabular Lists**: Do not display tables/lists as a vertical list of disjointed cards inside dynamic modals; use a clean, scoped standard `<table>` instead.
