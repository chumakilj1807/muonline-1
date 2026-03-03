# PrebuiltContent

This folder stores prebuilt MGCB outputs (`.xnb`) used as a fallback when shader/content compilation cannot run locally (for example, macOS without `wine`).

## Layout

- `DesktopGL/Content/*.xnb`

## MuMac behavior

`MuMac.csproj` supports two modes:

- Default mode: build content from `Content.mgcb` (normal MGCB flow).
- Fallback mode: use prebuilt `.xnb` files from this folder.

Fallback is enabled when:

- `wine` is not available on non-Windows, or
- build is forced with `-p:UsePrebuiltContent=true`.

## Example

```bash
dotnet build ./MuMac/MuMac.csproj -c Debug -p:UsePrebuiltContent=true
```

Keep this folder synchronized with `Client.Main/MGContent/Content.mgcb`.

Expected files in `DesktopGL/Content`:

- `AlphaRGB.xnb`
- `Arial.xnb`
- `Background.xnb`
- `Bubbles.xnb`
- `DynamicLighting.xnb`
- `FXAA.xnb`
- `GammaCorrection.xnb`
- `Grass.xnb`
- `ItemMaterial.xnb`
- `MonsterMaterial.xnb`
- `NotoKR.xnb`
- `Shadow.xnb`
- `WaterSplashParticle.xnb`
