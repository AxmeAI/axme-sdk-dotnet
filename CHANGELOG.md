# Changelog

## 0.1.2 (2026-03-18)

### Features
- `SendIntentAsync()` — convenience wrapper with auto-generated correlation_id
- `ApplyScenarioAsync()` — compile and submit scenario bundle
- `ValidateScenarioAsync()` — dry-run scenario validation
- `HealthAsync()` — gateway health check
- `McpInitializeAsync()` — MCP protocol handshake
- `McpListToolsAsync()` — list available MCP tools
- `McpCallToolAsync()` — invoke MCP tool

## 0.1.1 (2026-03-13)

- Initial alpha release with full AXME API coverage (82 async methods)
- Intent lifecycle, inbox, webhooks, admin APIs
- Zero external dependencies (System.Net.Http + System.Text.Json)
