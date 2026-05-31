---
description: UI - Premium UI Design System and Component Guidelines
---

# 🎨 UI Design System & Component Consistency Workflow

This workflow ensures that all user interface, layout, and styling changes made to the **SalesApp** client codebase remain strictly consistent, premium, and unified.

## Context
SalesApp's UI is built on top of **Mantine UI (v7)** and styled with premium vanilla CSS tokens. To prevent disjointed, raw, or outlier layouts (e.g., custom filled-variant warning banners with heavy black badges or hard border lines), every developer or agent must adhere to the design system catalog.

---

## 🛠️ Mandatory Workflow Steps

### 1. Consult UI Library First
Before creating a new UI view, modal, warning banner, table, or form field, you **MUST** read and consult the central UI design library:
* **File Location**: [UI-ELEMENTS.md](file:///Users/hudson/development/sales-dash/UI-ELEMENTS.md)
* **Goal**: Look up the approved Mantine components, semantic color mappings, vanilla CSS table definitions, and layout configurations.

### 2. Follow Color & Component Standards

#### 🚨 Warning & Alert Banners (`<Alert>`)
* **Default Variant**: Use the modern **light/subtle** variant (do not set `variant="filled"` or add raw custom borders).
* **Color Semantics**: 
  - `orange`: Warnings / Conflicts resolved
  - `red`: Fatal/validation errors
  - `green`: Success messages
  - `blue`: Dynamic information & user guides
* **Warning Lists**: When listing multiple items inside an Alert block, use a standard nested Mantine `<List withPadding>` instead of dark high-contrast `<Badge>` blocks.

#### 🗂️ Scoped Tables (`<table>`)
* Render standard, accessible HTML tables.
* Highlight special roles (e.g., team owners) using a sophisticated **light amber background tint (`rgba(254, 243, 199, 0.5)`)** and gold-accent borders rather than stark colors.
* Wrap action buttons cleanly within headers or aligned column headers.

#### 👥 Dual-Column Member Management Modal (`TeamMembersModal` & `ClassificationsPage`)
* **Light Palette**: Always style assignment modals with a **light theme background (`#ffffff` / `#f8f9fa`)** and white cards (`#ffffff`).
* **Breadth-First Sorting**: Implement Level-Order (BFS) hierarchical user lists starting from the Team Owner.
* **One-Click Realtime Actions / Instant Multi-Select**: Either trigger immediate single-member API additions, or provide a spacious split grid (like `cls-modal-grid`) where users can search, check multiple user cards (`ScrollArea` height `320px` for optimal viewing), and assign in bulk.

---

## 🎨 Central UI Design Color Palette
To maintain high contrast and unified styling across all pages, refer to these approved color variables:

| Intent | Code / Hex | Background Tint | Use Case |
|---|---|---|---|
| **Primary Text / Headings** | `#1c1c1e` | — | Explicit Modal titles, main labels, high contrast readability. |
| **Secondary Text** | `#6b7280` | — | Descriptions, secondary meta-information. |
| **Brand Primary** | `#6366f1` | `#ede9fe` / `#f5f3ff` | Indigo accents, active selections, primary indicators. |
| **Action / Info** | `#228be6` | `#e7f5ff` | Blue accents, default prompts, navigation. |
| **Success** | `#10b981` | `#e6fffa` | Active states, successful validations, active level markers. |
| **Warning / Conflict** | `#f59e0b` | `#fffbeb` | Warning banners, resolved association alerts, temporary status. |
| **Error / Deletion** | `#ef4444` | `#fef2f2` | High-importance delete confirmations, validation errors. |
| **Subtle Borders** | `#e9ecef` | — | Divider lines, card outlines, table row separations. |

> **Modal Header Title & Dialogue Visibility Rule**:
> When using standard light-themed modals (`StyledModal` or `StandardModal`), **never** pass raw strings to the `title` attribute of the `<Modal>`. The global CSS theme overrides default title elements to white, rendering them invisible.
> **Always** wrap the title content in an explicit element with our premium dark color token:
> ```tsx
> title={<Title order={3} style={{ color: '#1c1c1e', fontWeight: 700 }}>Title Text</Title>}
> ```
> Keep confirmation dialogue elements fully light-themed, using standard `StandardModal` and Mantine `<Button>` components for cancels (`variant="default"`) and destructive actions (`color="red"`).

---

## ❌ strictly Forbiden Patterns (Do Not Implement)
1. **Raw CSS Banner Colors**: Do not override warning boxes with solid, saturated blocks or thick borders.
2. **Raw HTML Inputs**: Do not introduce native `<select>` or `<input>` fields that clash with Mantine's focus borders and rounding.
3. **Disjointed Modals**: Never stack vertical, loose cards inside standard modal popups where a structured `<table>` provides a clean grid.
4. **Duplicate UI Code**: Do not write ad-hoc utility styling if an element can be governed by a shared CSS class in `index.css` or component-specific CSS (like `TeamsPage.css`).
5. **Invisible Modal Headers**: Do not omit `style={{ color: '#1c1c1e' }}` on custom `<Modal>` header titles.

