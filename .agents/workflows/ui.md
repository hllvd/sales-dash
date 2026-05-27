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

#### 🔍 Interactive Selects & Inputs
* Use Mantine's search-enabled selectors: `<Select searchable>` or `<MultiSelect searchable>`.
* Never render raw browser-default HTML inputs or un-styled drop-down selects.

---

## ❌ strictly Forbiden Patterns (Do Not Implement)
1. **Raw CSS Banner Colors**: Do not override warning boxes with solid, saturated blocks or thick borders.
2. **Raw HTML Inputs**: Do not introduce native `<select>` or `<input>` fields that clash with Mantine's focus borders and rounding.
3. **Disjointed Modals**: Never stack vertical, loose cards inside standard modal popups where a structured `<table>` provides a clean grid.
4. **Duplicate UI Code**: Do not write ad-hoc utility styling if an element can be governed by a shared CSS class in `index.css` or component-specific CSS (like `TeamsPage.css`).
