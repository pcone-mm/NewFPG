# Unity MCP Setup

This project is wired to use [`MCP for Unity`](https://github.com/CoplayDev/unity-mcp) through Unity Package Manager.

## Added package

`Packages/manifest.json` includes:

```json
"com.coplaydev.unity-mcp": "https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity#v9.7.1"
```

The package is pinned to `v9.7.1` for reproducible installs. If you want to follow upstream continuously, switch the suffix to `#main`.

## Local prerequisites

- Unity `2021.3+` is required. This project currently uses `6000.3.15f1`.
- Python `3.10+`
- `uv`

This machine already has:

- `Python 3.12.10`
- `uv 0.10.9`

## Finish setup in Unity

1. Open the project in Unity.
2. Let Package Manager resolve the new dependency.
3. Open `Window > MCP for Unity`.
4. Click `Start Server`.
5. Use `Configure` for your MCP client, or manually point your client to:

```json
{
  "mcpServers": {
    "unityMCP": {
      "url": "http://localhost:8080/mcp"
    }
  }
}
```

When the window shows `Connected`, the project is ready for MCP-driven editing and automation.

## Notes

- `Packages/packages-lock.json` was already dirty before this MCP change, so it was left untouched to avoid overwriting unrelated local work.
- On the next Unity open/resolution pass, Unity may update `packages-lock.json` automatically.
