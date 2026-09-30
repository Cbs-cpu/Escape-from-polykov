# Tools — comprobaciones sin Unity

Sirven para validar código en máquinas sin Unity (CI, sesiones de Claude en la nube). **No sustituyen**
a probar en el editor: solo detectan errores de compilación y regresiones del motor de simulación.

| Herramienta | Comando | Qué hace |
|---|---|---|
| SimTests | `dotnet test Tools/SimTests/SimTests.csproj` | Ejecuta los tests EditMode de la simulación pura (Movement, Weapons/Simulation) con un *shim* de `UnityEngine` en C# puro (`UnityShim/`). |
| CompileCheck | `python3 Tools/CompileCheck/check.py` | Genera un `.csproj` por cada `.asmdef` y compila contra las DLL de referencia de Unity (NuGet). Detecta errores de tipos y referencias entre asmdefs que faltan. |

Requisitos: .NET SDK 8 y acceso a nuget.org la primera vez.

Límites conocidos:
- Las DLL de referencia son de Unity 2021.3: una API solo de Unity 6 puede dar un falso error
  (añádela a `KNOWN_FALSE_POSITIVES` en `check.py`) y el Input System son *stubs* de firmas (`Stubs/`).
- El shim de SimTests reproduce la semántica de `Vector3`/`Quaternion`/`Mathf` de Unity, pero el
  resultado de referencia siempre es el Test Runner de Unity.
- El código de simulación que quieras testear aquí no puede usar física, MonoBehaviours ni assets.
