# BONELAB DLL Builder

A small self-hosted web app: upload one or more `.cs` files and the `.dll` reference files you choose, then press **Build DLL** and download the result.

## Run it
Requires Docker. Build and run:

```bash
docker build -t bonelab-dll-builder .
docker run --rm -p 8080:8080 bonelab-dll-builder
```

Open `http://localhost:8080`.

## Important compatibility note
This tool does not discover or download references automatically. You upload the exact references yourself. The default target is `netstandard2.1`, but that alone does not guarantee Quest/MelonLoader compatibility. Use the target framework and reference assemblies required by the specific BONELAB Quest MelonLoader setup you're building for. A successful compile also does not guarantee the mod will load or work in-game.

Only upload source code and DLLs you have permission to use. The app is intended for private/local use; do not expose it publicly without adding authentication and upload limits.
