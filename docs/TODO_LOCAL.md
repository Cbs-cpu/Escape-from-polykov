# TODO local (necesita Unity / Blender en tu ordenador)

Cosas que Claude en la nube **no puede hacer** (no hay editor de Unity ni Blender en el contenedor). Cuando abras una
sesión de Claude Code en tu PC (con Unity MCP y Blender MCP), pídele: *"haz lo pendiente de docs/TODO_LOCAL.md"*.
Marca cada punto con `[x]` al terminarlo.

## Siempre, tras cada `git pull`
- [ ] Abrir Unity, esperar a que compile y revisar la consola (0 errores / 0 warnings nuevos).
- [ ] Ejecutar los tests EditMode (Window > General > Test Runner) — todos en verde.
- [ ] Jugar `Assets/_Project/Scenes/MovementTestArena.unity` y revisar lo indicado en `docs/CHANGELOG_PLAYTEST.md`.

## Pendiente
- [ ] **Crouch**: verificar en la arena que existe algún hueco bajo (~1.4 m) para probar el bloqueo por techo; si no, añadir
      uno al blockout (`MovementTestArena`, zona de pasillos).
- [ ] **Crouch (arte)**: clips propios `Crouch_Idle`, `Crouch_Walk_F/B/L/R` en `ArtSource/Tools/build_animations.py` y añadir
      una capa/blend tree de crouch en `OperatorAnimatorBuilder` (parámetro `Crouch`). Hasta entonces la pose es procedural.
- [ ] **Arma — ajuste visual de manos** (necesita ver el editor): en Play Mode ajustar `WD_M1911` (Hands) y `HandIK` hasta que
      ambas manos agarren bien la M1911 en cadera y apuntando; copiar los valores al asset (Play Mode los pierde en componentes,
      no en el asset) y hacer commit. Revisar también en tercera persona desde la Scene view.
- [ ] **Arma — modelo M1911 en Blender** (hoja de referencia): script `ArtSource/Tools/build_m1911.py` que genere el modelo
      low-poly modular (slide, frame, barrel, grips, magazine, hammer, trigger, safety, slide stop) con los **mismos nombres**
      que `WeaponModel` (`Slide`, `Magazine`, `Hammer`, `Trigger`, `Muzzle`, `SightLine`, `GripCenter`, `MagazineGrab`),
      +Z hacia la boca, origen sobre el guardamonte. Exportar FBX a `Assets/_Project/Art/Weapons/M1911/` y hacer que
      `PlayerWeapon` instancie ese prefab en lugar de `M1911Builder` cuando esté asignado.
- [ ] **Arma — sonido**: disparo, clic en vacío, sacar/meter cargador, soltar corredera (eventos ya expuestos en `PlayerWeapon`).
