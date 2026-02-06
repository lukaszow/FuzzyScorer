# Development Notes & AI Guidelines

This document provides guidelines for developers and AI agents working on WordsCloud project.

## 🤖 AI Session Bootstrap

To maintain perfect context and architectural consistency while saving LLM token limits, always start a new AI session (or project-wide task) by pasting the following prompt:

> **Bootstrap Prompt:**
> "Start session. I have created the configuration files (`AI_RULES.md`, `STRUCTURE.md`). Please analyze them first to understand the project architecture, tech stack.
> 
> Then, based on `STRUCTURE.md`, verify or maintain the directory tree."

## Project Configuration Files

The following files define the core project governance:

1.  **[AI_RULES.md](AI_RULES.md)**: Defines the tech stack (React 19+, Vite, Tailwind), coding standards, and language rules.
2.  **[STRUCTURE.md](STRUCTURE.md)**: Defines the strict directory hierarchy and data flow (Processing -> State -> Display).

## Universal Compatibility

These instructions are intended to be universal. Whether you are using:
- **VS Code with Copilot/Claude Dev/Roo Code**
- **Cursor**
- **Antigravity**
- **Web-based LLMs (ChatGPT, Claude.ai)**

Simply mentioning or pasting the bootstrap prompt ensures the agent is fully aligned with the project's "Business Logic" and "Type Safety" goals.