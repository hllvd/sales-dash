---
description: Read-only query; Answer questions about the code or features without making any file changes or running modifying commands.
---

# 🔍 Read-Only Query Workflow (readyonly)

This workflow is designed for queries, research, code walkthroughs, and conceptual analysis where **no code modification or repository state changes** should occur.

## 🚫 Critical Constraints
1. **No Writes or Edits**: Do NOT call `write_to_file`, `replace_file_content`, or `multi_replace_file_content` to modify the codebase.
2. **No Command-Line Modifications**: Do NOT use `run_command` to execute commands that modify files, database schemas, compile assets, or launch persistent external processes.
3. **No Execution Planning Artifacts**: Do NOT create a `task.md`, `implementation_plan.md`, or `walkthrough.md` since there is no implementation.

---

## 🛠️ Allowed Tools
You are permitted to use only read-only and analytical tools to understand the request:
* **`view_file`**: View code files and configurations.
* **`list_dir`**: Explore directory structure.
* **`grep_search`**: Search for code patterns, usage of functions, classes, or database tables.
* **`search_web` / `read_url_content`**: Look up documentation, APIs, and frameworks.

---

## 🧭 Step-by-Step Procedure

### 1. Codebase Exploration
* Locate the relevant components and files using `list_dir` or `grep_search`.
* Inspect file contents using `view_file` to understand logic flow and dependencies.

### 2. Feature & Architecture Analysis
* Verify design patterns, routing, database schemas, and state management rules.
* Draw connections between UI components and backend handlers.

### 3. Respond Concisely and Clearly
* Provide your final response directly in the chat.
* Highlight code locations with clickable file links in the standard format (e.g., [filename](file:///path/to/file#L10-L20)).
* Explain the concepts, rules, architecture, or behavior clearly without suggesting plans for code changes unless explicitly asked.
