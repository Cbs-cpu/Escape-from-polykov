# Red (Fase 2) — diseño y estado

> Estado: **lógica de netcode pura hecha y testeada** (`Assets/_Project/Scripts/Netcode`, 18 tests).
> Pendiente en local: integrar el transporte (Netcode for GameObjects + Unity Transport) — ver `docs/TODO_LOCAL.md`.

## Modelo

- **Servidor dedicado autoritativo** a tick fijo (60 Hz, el mismo `PlayerMotor.TickInterval`).
- **Clientes envían comandos, no posiciones.** El servidor simula a cada jugador con `MovementMotor` + `WeaponMotor`
  (los mismos que en el cliente) y es el único que decide posición, munición, disparos, daño.
- **Predicción en el cliente** para el jugador local: simula al instante con su propio input; cuando llega el estado
  autoritativo de un tick, lo compara y si difiere **rebobina y re-simula** (reconciliación).
- **Interpolación** para los jugadores remotos: se dibujan ~2–3 ticks en el pasado, mezclando entre dos snapshots.
- **Compensación de lag** para disparos: el servidor rebobina las posiciones de los objetivos al tick que veía el tirador.

Por qué funciona sin correcciones en condiciones normales: la simulación es **determinista** y el input se
**cuantiza igual en ambos lados** (`PlayerCommand.Quantized()` antes de simular, también en el cliente). El test
`LaggyLossyLink_WithRedundancy_NeedsNoCorrections` lo demuestra: 100 ms de latencia y 20 % de pérdida → 0 correcciones.

## Piezas (C# puro, sin transporte)

| Tipo | Lado | Qué hace |
|---|---|---|
| `PlayerCommand` | C→S | Input de un tick: movimiento, arma, pitch. 13 bytes: tick u32, move 2×s8, yaw u16, pitch s16 (0.01°), lean s8, flags u8, acciones u8 (seguro, inspeccionar, recámara). `AsRepeat()` para huecos. |
| `ByteWriter/ByteReader` | ambos | Serialización little-endian sin allocations. |
| `NetPlayerState` | S→C | Estado autoritativo: posición, yaw, pitch, `MovementState`. |
| `IPlayerSimulator` | ambos | Un tick de simulación. La implementación Unity usará `CharacterController` (colisiones); los tests, suelo plano. |
| `ClientPrediction` | C | Historial (256 ticks) de comando + estado predicho. `Reconcile()` → `Confirmed` / `Corrected` (re-simula) / `NoHistory`. |
| `VisualErrorSmoother` | C | Esconde la corrección visual (decae en ~0.15 s; >1.5 m = teletransporte). |
| `ServerInputQueue` | S | Cola por cliente: 1 comando por tick en orden; si falta repite el último sin acciones puntuales; descarta duplicados y tardíos; `BufferedCount` para sincronizar. |
| `TickRateAdjuster` | C | Acelera/frena el reloj del cliente (±8 %) para mantener N comandos en el búfer del servidor. |
| `SnapshotBuffer` | C/S | Interpolación de remotos (yaw por el camino corto, huecos, extrapolación limitada) y **historial de posiciones para compensación de lag**. |

## Flujo por tick

**Cliente (jugador local)**
1. Leer input → `PlayerCommand(tick)` → `Quantized()`.
2. Simular localmente (predicción) → `ClientPrediction.Record(cmd, estado)`.
3. Enviar los últimos **N comandos** (redundancia; N=3–5) en un paquete no fiable.
4. Al recibir `(tickServidor, estado)`: `Reconcile()`. Si `Corrected`: aplicar estado, `VisualErrorSmoother.OnCorrection`.
5. `TickRateAdjuster.Update(bufferedDelServidor)` → escala del reloj de ticks.

**Servidor**
1. Recibir paquetes → `ServerInputQueue.Enqueue()` por cada comando.
2. Cada tick, por jugador: `TryDequeue()` → simular → guardar en `SnapshotBuffer` (lag comp).
3. Enviar a cada cliente: su propio estado (+ tick, + `BufferedCount`) y los de los demás (snapshots).

**Regla importante:** la redundancia solo ayuda si las copias llegan antes de que el servidor necesite el comando: el
búfer del servidor (objetivo de `TickRateAdjuster`) debe ser ≥ redundancia − 1. Recomendado: redundancia 4, búfer 3.

## Clasificación (del SPEC)

- LOCAL ONLY: cámara, head bob, FOV, IK de pies/manos, balanceo del arma, sonidos propios.
- REPLICATED: posición, yaw, pitch, `MovementState` (locomoción, crouch, lean, stamina), `WeaponState` (munición, recarga, apuntado).
- SERVER ONLY: validación, daño, loot, extracción.

## Probar sin red: simulador F6

`Scripts/Netcode/Unity/LatencySimulator.cs` ejecuta el camino real de la Fase 2 en un solo proceso: cliente con
predicción + servidor con su propio `CharacterBody` (colisiones reales) + enlace falso con latencia/jitter/pérdida.
`BodySimulator` es el `IPlayerSimulator` real (re-simula con el `CharacterController`), que NGO reutilizará tal cual.

## Siguiente (necesita Unity)

1. Añadir paquetes `com.unity.netcode.gameobjects` y `com.unity.transport`.
2. `NetworkPlayer` (NetworkBehaviour): en el propietario, `PlayerMotor` + `PlayerWeapon` generan `PlayerCommand` y se
   envían por `CustomMessagingManager` (no fiable); en el servidor, cola + simulación; en remotos, `SnapshotBuffer`
   → posición/animación (el `PlayerAnimator` ya usa solo estado replicable).
3. ~~`UnityPlayerSimulator`~~ hecho: `BodySimulator` + `CharacterBody` (re-simulación con colisiones).
4. Overlay MULTIPLAYER DEBUG (role, tick, ping, error de posición, correcciones/s, búfer, interpolación).
5. Build de servidor dedicado (Linux headless) y prueba con latencia simulada (Unity Transport Simulator).
