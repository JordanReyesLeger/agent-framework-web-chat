# SE-SemanticKernel Development Guidelines

Auto-generated from all feature plans. Last updated: 2026-02-17

## Active Technologies
- C# / .NET 9.0 (SkWebChat) + Microsoft.SemanticKernel (agents/orchestration packages already present), ASP.NET Core host, existing Azure Search & Blob deps (no nuevas) (003-groupchat-agents)
- In-memory session state for group chat; reutilizar almacenamiento existente solo si ya disponible (no añadir nuevas stores). (003-groupchat-agents)

- C# .NET 9.0 (backend), JavaScript ES6 (frontend) + ASP.NET Core MVC, Microsoft Semantic Kernel, Bootstrap 5, Bootstrap Icons (002-chatgpt-style-ui)

## Project Structure

```text
backend/
frontend/
tests/
```

## Commands

npm test; npm run lint

## Code Style

C# .NET 9.0 (backend), JavaScript ES6 (frontend): Follow standard conventions

## Recent Changes
- 003-groupchat-agents: Added C# / .NET 9.0 (SkWebChat) + Microsoft.SemanticKernel (agents/orchestration packages already present), ASP.NET Core host, existing Azure Search & Blob deps (no nuevas)

- 002-chatgpt-style-ui: Added C# .NET 9.0 (backend), JavaScript ES6 (frontend) + ASP.NET Core MVC, Microsoft Semantic Kernel, Bootstrap 5, Bootstrap Icons

<!-- MANUAL ADDITIONS START -->
<!-- MANUAL ADDITIONS END -->
