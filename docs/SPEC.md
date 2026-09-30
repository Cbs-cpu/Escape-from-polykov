# SPEC — Escape from Polykov (Low-Poly Extraction Shooter)

> Estado: **Fase 1 — Game Feel / Movimiento base** (activa)
> Motor: Unity 6.6 (6000.6.3f1), URP · Herramientas: Claude Code + Unity MCP + Blender MCP (Blender 5.2)

---

## Problem Statement

Quiero un extraction shooter FPS multijugador con la tensión, profundidad y riesgo/recompensa de Escape from Tarkov, pero más accesible, fluido y mejor optimizado, con estética low-poly táctica. Hoy el proyecto es una plantilla URP vacía. Si se construyen los sistemas en el orden "todo singleplayer y multiplayer al final", habrá refactorizaciones masivas; y si se añaden mecánicas antes de que el movimiento se sienta bien, el juego nunca se sentirá bien.

## Solution

Desarrollo por fases estrictas:

1. **Fase 1:** el mejor movimiento FPS posible (personaje con cuerpo completo, cámara, locomoción, animación con IK de pies) en una Movement Test Arena. No se avanza hasta aprobar el Game Feel.
2. **Fase 2:** infraestructura multiplayer (servidor dedicado, autoridad de servidor, predicción + reconciliación del movimiento, interpolación de remotos).
3. **Fase 3+:** cada sistema nuevo (arma, disparo, recarga, aiming, manipulación, movimiento avanzado, combate, inventario, loot, extracción, mapa, audio, pulido) nace con estado local, estado replicado, autoridad y validación.

La Fase 1 se diseña ya para no bloquear la Fase 2: la lógica de movimiento es una simulación determinista separada de la presentación, reutilizable en cliente (predicción) y servidor (validación).

Referencias visuales: hoja del personaje táctico (gorra, gafas, barba, uniforme verde oliva, botas; esqueleto humanoide con IK/FK) y hoja de la M1911 modular (slide, frame, barrel, grips, magazine, controles, variantes). El personaje entra en Fase 1; la M1911 en Fase 3.

## User Stories

### Movimiento (Fase 1)
1. Como jugador, quiero caminar con WASD, para moverme con precisión.
2. Como jugador, quiero que el personaje acelere de forma progresiva pero rápida, para sentir peso sin perder respuesta.
3. Como jugador, quiero que el personaje frene con una desaceleración corta y natural, para detenerme donde quiero.
4. Como jugador, quiero un estado Run por defecto y Walk con una tecla modificadora, para controlar mi ritmo y sigilo.
5. Como jugador, quiero esprintar manteniendo Shift solo hacia delante, para cubrir distancia con compromiso táctico.
6. Como jugador, quiero que el sprint se cancele al moverme hacia atrás o de lado, para que sea coherente y creíble.
7. Como jugador, quiero que el strafe y el movimiento hacia atrás sean más lentos que hacia delante, para sentir un cuerpo real.
8. Como jugador, quiero que cambiar de dirección tenga una inercia breve, para sentir masa sin sentirme torpe.
9. Como jugador, quiero moverme sobre rampas y escalones pequeños sin saltos ni temblores, para que el terreno no rompa la inmersión.
10. Como jugador, quiero que la gravedad me mantenga pegado al suelo en bajadas, para no "flotar" al descender rampas.
11. Como jugador, quiero que el input diagonal no sea más rápido que el recto, para que el movimiento sea justo.
12. Como jugador con mando, quiero que la magnitud del stick module la velocidad, para tener control analógico.

### Cámara (Fase 1)
13. Como jugador, quiero mirar con el ratón sin aceleración ni suavizado que añada latencia, para apuntar con precisión.
14. Como jugador, quiero limitar la mirada vertical a un rango natural, para no dar la vuelta a la cámara.
15. Como jugador, quiero un FOV configurable, para ajustar comodidad y claridad.
16. Como jugador, quiero un head movement sutil ligado a la locomoción, para sentir el cuerpo sin marearme.
17. Como jugador, quiero que la cámara esté anclada a la cabeza del cuerpo, para que la perspectiva sea física.
18. Como jugador, quiero poder desactivar/reducir el head bob, para accesibilidad.

### Cuerpo y animación (Fase 1)
19. Como jugador, quiero ver mi cuerpo, piernas y pies al mirar abajo, para sentir presencia.
20. Como jugador, quiero que las animaciones mezclen suavemente entre idle, walk, run y sprint, para que no haya saltos.
21. Como jugador, quiero que las animaciones direccionales (strafe, backward) coincidan con mi dirección de movimiento, para que no haya pies patinando.
22. Como jugador, quiero que los pies se apoyen correctamente en rampas y escalones (IK de pies), para que el cuerpo parezca físico.
23. Como jugador, quiero que la cadencia de los pasos coincida con la velocidad real, para evitar foot sliding.
24. Como jugador, quiero que mi personaje proyecte sombra de cuerpo completo, para mayor inmersión.
25. Como artista, quiero que el personaje low-poly siga la hoja de referencia (proporciones, gorra, gafas, barba, uniforme, botas, paleta), para mantener la identidad visual.
26. Como artista, quiero un esqueleto humanoide compatible con Mecanim, para reutilizar animaciones y retargeting.

### Input (Fase 1)
27. Como jugador, quiero acciones independientes (Move, Look, Sprint, Walk, Crouch, Jump, Interact, Aim, Fire, Reload, WeaponSwitch, Lean, Inventory) en Unity Input System, para rebinding futuro.
28. Como jugador, quiero soporte teclado+ratón y mando desde el principio, para jugar como prefiera.

### Debug y testeo (Fase 1)
29. Como desarrollador, quiero un overlay MOVEMENT DEBUG (speed, velocity, state, grounded, slope), para ajustar el Game Feel con datos.
30. Como desarrollador, quiero ajustar todos los parámetros de movimiento desde un ScriptableObject en caliente, para iterar rápido.
31. Como desarrollador, quiero una Movement Test Arena con suelo, rampas, obstáculos, pasillos, zonas abiertas y cambios de altura, para probar cada caso.
32. Como desarrollador, quiero tests automáticos del motor de movimiento, para que el tuning no rompa comportamientos.

### Multiplayer (Fase 2)
33. Como jugador, quiero conectarme a un servidor dedicado, para jugar con otros.
34. Como jugador, quiero que mi movimiento responda al instante aunque haya latencia (predicción), para no sentir lag.
35. Como jugador, quiero que las correcciones del servidor sean suaves, para no ver teletransportes.
36. Como jugador, quiero ver a otros jugadores moverse de forma fluida (interpolación), para leer sus acciones.
37. Como servidor, quiero ser autoridad sobre la posición, para impedir speed hacks.
38. Como desarrollador, quiero un overlay MULTIPLAYER DEBUG (role, tick, ping, position error, prediction, interpolation), para diagnosticar la red.
39. Como jugador, quiero reconectarme tras una desconexión breve, para no perder la partida.

### Armas y combate (Fases 3–9)
40. Como jugador, quiero equipar una M1911 low-poly fiel a la hoja de referencia, visible también para otros jugadores.
41. Como jugador, quiero disparar con recoil basado en perfiles (kick, recovery, return), con munición validada por servidor.
42. Como jugador, quiero recargas tácticas y en vacío con manos que interactúan con el cargador por IK.
43. Como jugador, quiero hip, low ready y ADS alineados con cámara, manos y hombro.
44. Como jugador, quiero inspeccionar, revisar recámara y usar el seguro.
45. Como jugador, quiero agacharme, inclinarme, saltar y saltar obstáculos con soporte multiplayer.
46. Como jugador, quiero que el daño, la salud y la muerte los decida el servidor.
47. Como jugador, quiero personalizar la M1911 sustituyendo slide, barrel, grips, magazine y muzzle.

### Inventario, loot, extracción (Fases 10–12)
48. Como jugador, quiero un inventario por slots con peso, validado por servidor.
49. Como jugador, quiero registrar contenedores y cadáveres con loot generado en servidor y sin duplicación.
50. Como jugador, quiero extraerme en zonas con condiciones y temporizador validados por servidor, conservando o perdiendo mis objetos.

## Implementation Decisions

### Principios
- **Separación simulación / presentación.** La lógica de gameplay es C# plano y determinista; los MonoBehaviours solo recogen input, llaman a la simulación y presentan.
- **ScriptableObject = datos estáticos; runtime state = clases/structs.** Nunca estado de partida en un SO.
- **Sin PlayerController monolítico.** Componentes con responsabilidad única.
- **Performance first:** cero allocations por frame en la ruta de movimiento, referencias cacheadas, sin GetComponent en Update.

### Módulos de Fase 1
- **Input:** asset de Input System propio del proyecto con el mapa `Player` (acciones de §13 + Walk). Un componente adaptador convierte input a un struct `MovementInput` (moveAxis, lookDelta, sprint, walk) — el mismo struct que viajará por red en Fase 2.
- **Movement Motor (simulación pura):** función `Step(MovementState, MovementInput, MovementSettings, deltaTime) → MovementState`. Contiene aceleración/desaceleración, velocidades por dirección (forward/strafe/backward), reglas de sprint, estados Idle/Walk/Run/Sprint, gravedad y snap al suelo. La información del suelo (grounded, normal) entra como dato, no como consulta de física, para que siga siendo determinista y testeable.
- **Movement State:** struct con velocity, locomotionState, grounded, groundNormal, yaw.
- **Movement Settings:** ScriptableObject con todas las velocidades, aceleraciones, multiplicadores direccionales y parámetros de gravedad. Valores iniciales orientativos: walk 1.8 m/s, run 3.6 m/s, sprint 5.8 m/s, strafe ×0.85, backward ×0.7, tiempo de aceleración ~0.18 s, frenada ~0.12 s.
- **Player Motor (adaptador):** MonoBehaviour que ejecuta el motor en tick fijo, resuelve colisiones con CharacterController y hace ground probing.
- **Player Look:** yaw aplicado al cuerpo, pitch a la cámara; sin suavizado de entrada.
- **Camera Rig:** cámara anclada al hueso de la cabeza con estabilización; head movement procedural sutil y FOV configurable. Local only.
- **Player Animator:** traduce el estado de movimiento a parámetros del Animator (velocidad local X/Z, estado). Blend Tree 2D direccional + capas; Animation Rigging (Two Bone IK) para pies con raycast al suelo y ajuste de pelvis.
- **Personaje:** modelo low-poly en Blender vía MCP siguiendo la hoja de referencia; rig humanoide (nombres compatibles con Mecanim), skinning, animaciones de locomoción propias (idle, walk/run/sprint en 8 direcciones) hechas en Blender; export FBX a escala 1 = 1 m, Y-up en Unity.
- **Primera persona:** un solo cuerpo completo; cabeza oculta al render de la cámara del jugador (shadows only) para ver cuerpo sin clipping; misma malla sirve como tercera persona para remotos.
- **Debug:** overlay de movimiento y Gizmos (probe de suelo, IK de pies).
- **Arena:** escena `MovementTestArena` con blockout low-poly (rampas de 10°/20°/35°/50°, escalones de 10–40 cm, pasillo estrecho, zona abierta, plataformas a varias alturas).

### Decisiones de proyecto
- Estructura bajo `Assets/_Project` según §53; assembly definitions por módulo (Core, Input, Movement, Animation, Camera, Debug, Tests).
- Paquetes a añadir en Fase 1: **Animation Rigging**. Se eliminan los assets de tutorial de la plantilla.
- Tick de simulación fijo (p. ej. 60 Hz) desde Fase 1, con interpolación visual, para que el modelo de red de Fase 2 encaje sin cambios.
- **Networking (Fase 2):** candidato Netcode for GameObjects + Unity Transport con servidor dedicado; se confirmará al empezar la Fase 2. La predicción de movimiento reutilizará el Movement Motor tal cual.
- Versión de Unity: 6.6 no es LTS; se acepta por ahora y se re-evaluará antes de la Fase 2.
- Git + LFS: se recomienda inicializar antes de empezar a producir assets.

### Clasificación de red (preparada ya)
- LOCAL ONLY: cámara, head bob, FOV, IK de pies, parámetros visuales.
- REPLICATED: posición, yaw, pitch (para remotos), locomotionState, velocidad local para animación.
- SERVER ONLY (Fase 2+): validación de posición.

## Testing Decisions

- Un buen test comprueba **comportamiento externo** del motor (dado este input durante N ticks, la velocidad/estado resultante es X), no detalles internos.
- **Seam único:** el Movement Motor puro, con tests EditMode de Unity Test Framework. Casos: aceleración alcanza velocidad objetivo en el tiempo configurado; frenada; diagonal no más rápida; sprint solo hacia delante y cancelación; multiplicadores strafe/backward; transiciones de estado; gravedad y snap; determinismo (mismo input → mismo resultado).
- Colisiones, cámara, animación e IK se validan manualmente en la arena con el overlay de debug (criterio de aprobación §30).
- Prior art: ninguno (proyecto nuevo); estos tests establecen el patrón.
- En Fase 2 el mismo seam sirve para tests de reconciliación.

## Out of Scope (Fase 1)

Armas, disparo, recoil, inventario, loot, enemigos, economía, quests, sonido, extracción, mapa grande, networking, crouch, jump, lean, vault, stamina, heridas. La M1911 se modela en Fase 3.

## Further Notes

- Fidelidad visual: los modelos serán una interpretación **low-poly** fiel en silueta, proporciones, prendas y paleta a las hojas de referencia, no una copia fotorrealista.
- Metodología: cada paso se muestra al usuario antes de pasar al siguiente. La Fase 1 no se cierra hasta que el usuario apruebe el Game Feel.
- Orden de trabajo Fase 1: (1) estructura + paquetes → (2) Input → (3) Movement Motor + tests → (4) arena blockout → (5) cápsula jugable + cámara + debug → (6) tuning con usuario → (7) personaje en Blender → (8) rig + animaciones → (9) Animator + IK de pies → (10) validación final.
