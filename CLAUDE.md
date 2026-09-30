# Escape from Polykov — contexto para Claude

Extraction shooter FPS multijugador low-poly (Unity 6.6 `6000.6.3f1`, URP). Diseño completo: `docs/SPEC.md`.
Trabajo pendiente que requiere Unity/Blender en local: `docs/TODO_LOCAL.md`. Idioma de docs y commits al usuario: español; código y comentarios: inglés.

## Arquitectura (no romper)
- **Simulación pura vs presentación.** La lógica de gameplay (`Scripts/Movement`, `Scripts/Weapons/Simulation`) es C# determinista:
  `Step(state, input, ..., dt) -> state`, sin física, sin MonoBehaviours, sin allocations. Los MonoBehaviours solo leen input,
  llaman a la simulación y presentan. Esto se reutiliza en la Fase 2 (predicción en cliente + validación en servidor).
- **ScriptableObject = datos estáticos** (tuning); estado runtime en structs/clases. Nunca estado de partida en un SO.
- Un componente por responsabilidad; nada de PlayerController monolítico. Cero `GetComponent` en Update, cero allocs por frame.
- Tick fijo de simulación (60 Hz, `PlayerMotor`) con interpolación visual.
- Clasificación de red: LOCAL ONLY (cámara, bob, FOV, IK, sway), REPLICATED (posición, yaw/pitch, locomoción, lean, crouch, estado de arma), SERVER ONLY (validación).

## Orden de ejecución por frame (DefaultExecutionOrder)
`PlayerInputReader -200` → `PlayerLook -100` → `PlayerMotor -50` → Animator → `ProceduralBody 50` → `FirstPersonCameraRig 100` → presentación de arma / IK de brazos (>100).

## Comprobaciones sin Unity (ejecutar antes de cada push)
- `dotnet test Tools/SimTests/SimTests.csproj` — tests EditMode de la simulación pura con un shim de UnityEngine.
- `python3 Tools/CompileCheck/check.py` — compila cada asmdef contra DLLs de referencia de Unity.
Ver `Tools/README.md`. Todo test nuevo de simulación va en `Assets/_Project/Tests/EditMode` y debe pasar en ambos (Unity y shim).

## Convenciones de assets
- Scripts nuevos necesitan `.meta` con GUID único (32 hex). Para conectarlos a `Prefabs/Player.prefab` se edita el YAML:
  añadir `- component: {fileID: N}` al GameObject y el bloque `--- !u!114 &N` MonoBehaviour con `m_Script` = GUID del `.meta`.
- Binarios (fbx, blend, png) van por Git LFS.
- Arte: scripts de Blender en `ArtSource/Tools` (procedurales, se ejecutan dentro de Blender 5.2 vía MCP).
