# SPEC — Lobby (menú + armero) y primer enemigo "Vagabundo" con daño físico, gore y ragdoll

> Dos proyectos en una spec. Construye sobre lo existente: salud por zonas (7 partes), hitboxes humanoides por hueso,
> muñeco de entrenamiento, simulación de arma determinista, presentación de arma, efectos de impacto, esqueleto humanoide
> del Operator (52 huesos, mismos nombres), pipeline Tripo → Blender → Unity.

---

## Problem Statement

El juego arranca directamente en la arena de pruebas: no hay un "fuera de partida" donde preparar al personaje, y la M1911 es fija. Tampoco hay a quién disparar salvo un muñeco que se cae y se levanta: los disparos no se sienten sobre un cuerpo (no hay reacción, sangre, desmembramiento ni caída física), así que falta la tensión central de un extraction shooter.

## Solution

**Proyecto A — Lobby.** Una pantalla de menú al estilo del menú principal de *Escape from Tarkov* (pantalla de personaje con equipo e inventario, y pantalla de modificación de armas con el arma desmontada en slots) que por ahora ofrece dos acciones: **Entrar al juego** y **Armero**. En el armero se ve la M1911 en 3D, se puede cambiar su configuración (variante de corredera/cachas del set existente) y **montar o quitar un silenciador** generado para esta arma. La configuración elegida viaja a la partida y cambia el arma de verdad (modelo, sonido/fogonazo, estadísticas).

**Proyecto B — "Vagabundo".** Primer enemigo, un scav pobre y desaliñado con el **mismo esqueleto** que el Operator (misma malla base de proporciones, mismos huesos y animaciones). Los impactos **empujan el cuerpo** (reacción física parcial por hueso), salpican sangre y dejan heridas; una parte muy dañada puede **desmembrarse con muy baja probabilidad**; al morir cae como **ragdoll** con físicas creíbles. Tiene una IA sencilla de deambular, reaccionar y atacar.

## User Stories

### Lobby — navegación
1. Como jugador, quiero que el juego arranque en un lobby, para preparar mi partida antes de entrar.
2. Como jugador, quiero ver a mi Operator en el centro del lobby, en idle, iluminado, para sentir que es mi personaje.
3. Como jugador, quiero un botón "Entrar al juego", para cargar la arena con mi configuración.
4. Como jugador, quiero un botón "Armero", para modificar mi arma.
5. Como jugador, quiero volver del armero al lobby sin perder cambios, para decidir con calma.
6. Como jugador, quiero que el lobby se controle con ratón (hover, clic) y Esc para volver, para que sea intuitivo.
7. Como jugador, quiero una estética oscura, industrial y táctica coherente con el juego, para mantener la inmersión.
8. Como jugador, quiero ver huecos de inventario/equipo alrededor del personaje (vacíos o bloqueados por ahora), para entender que el sistema crecerá.
9. Como jugador, quiero que la carga a la partida muestre una transición breve, para que no sea un corte brusco.
10. Como jugador, quiero volver al lobby desde el menú de pausa, para cambiar el arma sin reiniciar el juego.

### Lobby — armero
11. Como jugador, quiero ver la M1911 en 3D grande en el armero, para apreciar el modelo.
12. Como jugador, quiero rotar y hacer zoom al arma con el ratón, para inspeccionarla.
13. Como jugador, quiero ver los slots del arma (Cañón/Boca, Corredera, Cachas, Cargador), para entender qué es modificable.
14. Como jugador, quiero poner un silenciador en la boca del arma, para disparar más discretamente.
15. Como jugador, quiero quitar el silenciador, para volver a la configuración original.
16. Como jugador, quiero que el silenciador solo sea montable si el cañón es roscado, para que las reglas sean creíbles.
17. Como jugador, quiero que al montar el silenciador se monte automáticamente el cañón roscado compatible, para no bloquearme.
18. Como jugador, quiero cambiar entre variantes visuales disponibles (p. ej. cachas de madera / polímero), para personalizar el arma.
19. Como jugador, quiero ver cómo cambian las estadísticas (ergonomía, retroceso, peso, longitud, sonoridad) al modificar, para decidir con información.
20. Como jugador, quiero ver en rojo/verde qué empeora o mejora, para leer los cambios de un vistazo.
21. Como jugador, quiero que una pieza incompatible aparezca bloqueada con el motivo, para entender por qué no encaja.
22. Como jugador, quiero que la configuración se guarde entre sesiones, para no repetirla cada vez.
23. Como jugador, quiero un botón "Restablecer", para volver a la configuración de fábrica.

### Silenciador (asset)
24. Como jugador, quiero un silenciador de .45 ACP realista para M1911 (tubo cilíndrico, roscado, ~15–18 cm, ~3,5 cm de diámetro), para que encaje con el arma.
25. Como jugador, quiero que el silenciador tenga el mismo estilo low-poly con contorno negro que la pistola, para coherencia visual.
26. Como jugador, quiero que el silenciador añada longitud y peso visibles en primera persona, para sentir el cambio.
27. Como jugador, quiero que con silenciador el fogonazo sea mucho menor y aparezca un poco de humo en la boca, para que el disparo se vea distinto.
28. Como jugador, quiero que con silenciador el disparo "suene" más apagado cuando exista audio, para completar la sensación.
29. Como jugador, quiero que el silenciador aumente ligeramente el peso y la longitud (más colisión con paredes), para que tenga coste.
30. Como jugador, quiero que el silenciador reduzca algo el retroceso, para que tenga beneficio.

### Arma en partida
31. Como jugador, quiero que la configuración del lobby se aplique al arma al entrar, para jugar con lo que elegí.
32. Como jugador, quiero que la boca del arma (fogonazo, dirección de disparo) se desplace al final del silenciador, para que los efectos salgan del sitio correcto.
33. Como jugador, quiero que las manos sigan agarrando bien el arma con silenciador, para que no se rompa la presentación.
34. Como jugador en multijugador (Fase 2+), quiero que los demás vean mi silenciador, para que la información sea justa.

### Vagabundo — aspecto
35. Como jugador, quiero un enemigo con aspecto de vagabundo/scav pobre (ropa gastada, gorro o capucha, chaqueta vieja, pantalón sucio), para identificarlo como enemigo.
36. Como jugador, quiero que tenga las proporciones y el esqueleto del Operator, para que las animaciones y hitboxes funcionen igual.
37. Como jugador, quiero que tenga el mismo estilo low-poly facetado con contorno negro, para coherencia visual.
38. Como jugador, quiero que su cabeza se vea completa (no es el jugador local), para leerlo bien.

### Vagabundo — comportamiento
39. Como jugador, quiero que el vagabundo deambule por la zona, para que el mundo se sienta vivo.
40. Como jugador, quiero que me detecte por vista (cono y distancia) y por ruido de disparos, para que el sigilo importe.
41. Como jugador, quiero que al detectarme se gire, se acerque o se cubra, para que haya tensión.
42. Como jugador, quiero que me ataque (cuerpo a cuerpo o con arma sencilla en una fase posterior), para que suponga una amenaza.
43. Como jugador, quiero que al recibir un disparo reaccione (se tambalee, retroceda, se agarre la zona), para sentir el impacto.
44. Como jugador, quiero que con una pierna destruida cojee o se arrastre, para que el daño por zonas tenga consecuencias visibles.
45. Como jugador, quiero que con un brazo destruido deje de usarlo, para que el daño importe.
46. Como diseñador, quiero poder colocar vagabundos en la escena con un spawner, para montar encuentros.

### Impacto, reacción física y sangre
47. Como jugador, quiero que cada bala empuje la parte del cuerpo alcanzada (cabeza, torso, brazo, pierna) en la dirección del disparo, para que se sienta físico.
48. Como jugador, quiero que la reacción sea proporcional al daño y a la zona, para que un disparo en la cabeza se note más que en un brazo.
49. Como jugador, quiero que el cuerpo recupere la animación suavemente tras el empujón, para que no sea rígido ni exagerado.
50. Como jugador, quiero una salpicadura de sangre en el punto de impacto orientada según la bala, para confirmar el acierto.
51. Como jugador, quiero una salida de sangre por detrás si la bala atraviesa, para más realismo.
52. Como jugador, quiero manchas de sangre en el suelo/paredes cercanas, para que el combate deje huella.
53. Como jugador, quiero heridas visibles (decal) sobre el cuerpo en la zona impactada, para ver dónde he dado.
54. Como jugador, quiero un sonido/feedback de impacto en carne distinto del de metal/hormigón, para distinguirlos (cuando haya audio).
55. Como jugador, quiero que la cámara no tiemble al acertar, pero que el cuerpo sí reaccione, para leer el acierto sin marearme.
56. Como jugador, quiero una opción para reducir o desactivar la sangre y el desmembramiento, para jugar con menos gore.

### Desmembramiento
57. Como jugador, quiero que una parte muy castigada (destruida y con daño acumulado extra) pueda desmembrarse con una probabilidad muy baja, para momentos raros y memorables.
58. Como jugador, quiero que la probabilidad dependa del daño acumulado y del tipo de bala, para que no sea puramente aleatorio.
59. Como jugador, quiero que el miembro amputado salga despedido con físicas y sangre, para que el efecto sea creíble.
60. Como jugador, quiero ver un muñón limpio (tapa con textura de carne) donde estaba el miembro, para que no se vea un agujero.
61. Como jugador, quiero que la cabeza también pueda desmembrarse (muy rara), para momentos extremos.
62. Como jugador, quiero que un personaje desmembrado de una pierna caiga o se arrastre, y de la cabeza muera al instante, para coherencia.
63. Como jugador en multijugador (Fase 2+), quiero que todos vean el mismo desmembramiento, para que sea justo (decidido por el servidor).

### Muerte y ragdoll
64. Como jugador, quiero que al morir el enemigo pase a ragdoll inmediatamente, para una muerte física.
65. Como jugador, quiero que el ragdoll herede el impulso de la bala letal y la velocidad que llevaba, para que caiga en la dirección correcta.
66. Como jugador, quiero articulaciones con límites anatómicos (codos, rodillas, cuello, columna), para que no se retuerza de forma imposible.
67. Como jugador, quiero que el ragdoll no atraviese el suelo ni tiemble, para que se vea estable.
68. Como jugador, quiero poder seguir disparando al cuerpo y que reaccione, para que el mundo sea físico.
69. Como jugador, quiero que los cuerpos se queden un tiempo y luego se retiren (o se limiten a N), para no afectar al rendimiento.
70. Como jugador (Fase de loot), quiero que el cuerpo sea registrable, para preparar el loot futuro.

### Rendimiento y sistema
71. Como desarrollador, quiero pools para sangre, decals y miembros, para no hacer Instantiate/Destroy en combate.
72. Como desarrollador, quiero que el ragdoll use pocas piezas (pelvis, torso, cabeza, brazos, antebrazos, muslos, piernas), para mantenerlo barato.
73. Como desarrollador, quiero que las reglas de daño y desmembramiento sean simulación pura y testeable, para que el servidor las decida en multijugador.
74. Como desarrollador, quiero que las reglas de accesorios (compatibilidad, estadísticas) sean datos + lógica pura testeable, para añadir armas y piezas sin tocar código.
75. Como desarrollador, quiero que el Vagabundo reutilice HealthComponent, hitboxes humanoides y el pipeline de personaje, para no duplicar sistemas.

## Implementation Decisions

### Proyecto A — Lobby y armero
- **Escena de Lobby** separada de la arena; es la escena de arranque. La arena pasa a cargarse desde el lobby (carga asíncrona con transición).
- **Referencia de UX: Escape from Tarkov, siempre.** Menú principal y pantalla de personaje (equipo e inventario) de Tarkov; el armero replica su pantalla de modificación de armas (arma grande en el centro, slots conectados por líneas, lista de piezas compatibles, estadísticas a un lado). Solo **Entrar** y **Armero** activos; los demás paneles se muestran bloqueados.
- **UI:** UI Toolkit (ya presente) para el lobby; el personaje y el arma se renderizan en 3D en la propia escena (cámara de escaparate), no en render texture, salvo que sea necesario.
- **Datos de accesorios (ScriptableObjects, estáticos):** `AttachmentDefinition` (id, slot, prefab/malla, sockets, modificadores de estadísticas, compatibilidades) y una lista de accesorios por arma en la definición del arma existente.
- **Slots de la M1911:** Muzzle (ninguno / silenciador), Barrel (estándar / roscado), Grips (madera / polímero si se genera), Magazine (estándar 7). El silenciador requiere Barrel = roscado; montarlo selecciona el roscado automáticamente.
- **Estado (runtime) `WeaponBuild`:** mapa slot → accesorio elegido. Serializable a un string/JSON para guardarse en preferencias del jugador y enviarse al servidor en Fase 2.
- **Lógica pura `LoadoutRules`:** valida una build (compatibilidades, slots obligatorios), resuelve dependencias (silenciador ⇒ cañón roscado), y **calcula las estadísticas efectivas** aplicando modificadores sobre las estadísticas base del arma (ergonomía, retroceso, peso, longitud, sonoridad/firma de fogonazo). Sin dependencias de escena.
- **Aplicación en partida:** al crear el modelo del arma se instancian los accesorios en sus sockets (el silenciador en `Socket_Muzzle`), y el socket de boca efectivo pasa al extremo del silenciador (fogonazo, dirección de disparo, colisión con paredes que usa la longitud real).
- **Efectos con silenciador:** variante de fogonazo reducida (sin pétalos, casi sin luz) + humo de boca; audio "suprimido" cuando exista el sistema de audio.
- **Asset del silenciador:** investigar silenciadores .45 ACP para 1911 (dimensiones, roscado, forma). Generar con Tripo desde una imagen de referencia de nuestra hoja (sección MUZZLE DEVICES → SUPPRESSOR) o modelar a mano si Tripo no da forma limpia; low-poly, contorno de malla invertida, textura 512 filtro punto. Cañón roscado: variante del cañón con rosca visible sobresaliendo del casquillo.
- **Multiplayer:** la build se envía al servidor al entrar; el servidor valida con `LoadoutRules` y es autoridad sobre las estadísticas; los clientes remotos reciben la build para la presentación.

### Proyecto B — Vagabundo, impacto, gore y ragdoll
- **Modelo:** generado con Tripo (referencia de concepto de vagabundo/scav que se preparará), procesado con el mismo pipeline que el Operator: low-poly, **mismo esqueleto** (mismos 52 huesos, roll 0, pose T), animaciones compartidas, cabeza visible, contorno negro. Se añaden clips propios cuando haga falta (cojear, arrastrarse, reacción de golpe).
- **Salud:** reutiliza `HealthModel` (7 partes, estilo Tarkov) y los hitboxes humanoides por hueso existentes.
- **Extensión pura `WoundModel` (junto a HealthModel):** por parte, acumula daño "excedente" después de destruida; calcula la probabilidad de desmembramiento a partir de ese excedente, el daño del impacto y un multiplicador por calibre/tipo de bala; decide con RNG determinista con semilla (misma idea que la dispersión del arma) para que el servidor y los clientes coincidan. Probabilidad base muy baja (orientativo: ≤ 2–5 % por impacto elegible, solo en partes ya destruidas y con excedente mínimo). Partes amputables: brazos (a la altura de codo/hombro), piernas (rodilla/cadera), cabeza (muy rara). Resultado: evento `Dismembered(part)`.
- **Reacción al impacto (presentación):** blend "animación + física" por hueso: al recibir un impacto se aplica un impulso angular/lineal amortiguado en la cadena de huesos alcanzada (hueso impactado y padres, con atenuación), con retorno por muelle a la pose animada. Magnitud según daño y zona. Barato, sin ragdoll activo mientras vive (el ragdoll completo solo al morir).
- **Ragdoll:** se construye a partir del esqueleto humanoide (11 cuerpos: pelvis, torso, cabeza, brazos superiores/antebrazos, muslos/piernas) con `CharacterJoint`/`ConfigurableJoint` con límites anatómicos, masas realistas, colisionadores cápsula, interpolación y proyección de joints para estabilidad. Activación al morir: se desactiva el Animator, se copian las poses y se aplican la velocidad del cuerpo y el impulso de la bala letal. Los hitboxes existentes se reutilizan/alinean con los colisionadores del ragdoll.
- **Desmembramiento visual:** la malla se divide en Blender en piezas por región (con los mismos pesos) o se usa escalado a cero del hueso + malla de miembro separada; al amputar se oculta la región, se muestra la **tapa de muñón** (malla/decal de carne) y se lanza el miembro como cuerpo físico con pool. Sangre en chorro breve desde el muñón.
- **Sangre:** efectos de partículas con pool (salpicadura de entrada, salida trasera si atraviesa, niebla fina), decals de heridas en el cuerpo (limitados por personaje) y manchas en superficies cercanas por raycast. Superficie `Flesh` en `SurfaceMaterial`.
- **Gore opcional:** ajuste de jugador (sangre: completa / reducida / sin sangre; desmembramiento sí/no).
- **IA básica:** máquina de estados pura (Idle → Deambular → Sospecha → Persecución/Ataque → Herido → Muerto) con percepción por vista y ruido de disparo; navegación con NavMesh (AI Navigation ya instalado). Ataque cuerpo a cuerpo en esta fase.
- **Multiplayer:** el servidor decide daño, muerte y desmembramiento (eventos replicados con la parte, dirección e impulso); la reacción física, la sangre y el ragdoll son presentación local disparada por esos eventos. Los cuerpos se sincronizan como posición de pelvis + estado de muerte (ragdoll local, no replicado hueso a hueso).
- **Rendimiento:** pools para sangre, decals, miembros; límite de ragdolls simultáneos (p. ej. 6) con retirada del más antiguo; físicas de ragdoll en capa propia que no colisiona con debris ni con la cápsula del jugador.

### Seams de prueba (propuestas, a confirmar)
1. **`LoadoutRules`** (lógica pura de accesorios): una build de entrada → validez, dependencias resueltas y estadísticas efectivas.
2. **`HealthModel` + `WoundModel`** (lógica pura de daño): secuencia de impactos → estado de salud, muerte y eventos de desmembramiento, determinista con semilla.
Todo lo demás (UI, reacción física, sangre, ragdoll, IA en escena) se valida en juego con capturas y herramientas de debug.

## Testing Decisions

- Un buen test prueba comportamiento externo por la API pública (entradas → salidas/eventos), no detalles internos ni valores de presentación.
- **`LoadoutRules`:** build por defecto válida; silenciador sin cañón roscado inválido; montar silenciador fuerza cañón roscado; quitar cañón roscado quita el silenciador; estadísticas efectivas = base + modificadores (retroceso menor, peso/longitud mayores con silenciador); slot incompatible rechazado con motivo; serialización ida y vuelta de la build.
- **`WoundModel`:** una parte no destruida nunca se desmiembra; por debajo del excedente mínimo la probabilidad es 0; con semilla fija el resultado es reproducible; la probabilidad crece con el excedente y el daño; la tasa observada en N impactos simulados está dentro del rango objetivo (muy baja); desmembrar la cabeza mata; desmembrar una pierna marca la parte como destruida y se refleja en `DestroyedLegs`.
- **IA (opcional, si se separa como lógica pura):** transiciones de la máquina de estados ante estímulos (ver jugador, oír disparo, recibir daño, morir).
- **Prior art:** tests EditMode existentes de `MovementMotor` y de la simulación del arma (deterministas, sin escena) y de `HealthModel`.

## Out of Scope

- Inventario real, loot, economía, tienda, alijo, quests (los paneles del lobby se muestran pero bloqueados).
- Más armas o accesorios que el silenciador, el cañón roscado y (si se genera) cachas alternativas.
- Audio real (se deja el gancho para el disparo suprimido e impactos en carne).
- IA con armas de fuego, coberturas avanzadas, grupos o comunicación entre enemigos.
- Sincronización de ragdoll hueso a hueso por red.
- Matchmaking/lobby multijugador real (el lobby es local por ahora).

## Further Notes

- Escape from Tarkov es SIEMPRE la referencia de diseño (menús, armero, comportamiento de scavs, daño por zonas).
- Orden sugerido: (1) `LoadoutRules` + datos + tests, (2) asset del silenciador y cañón roscado, (3) montaje en partida (sockets, efectos), (4) escena de lobby y armero, (5) modelo del Vagabundo con el pipeline Tripo, (6) `WoundModel` + tests, (7) reacción física por hueso, (8) sangre y decals, (9) ragdoll, (10) desmembramiento visual, (11) IA básica y spawner, (12) ajuste de gore y rendimiento.
- Todo generado con Tripo pasa por el mismo pipeline reproducible (scripts en ArtSource/Tools) y el mismo estilo: low-poly facetado, contorno negro, texturas 512 con filtro punto.

## Progreso (actualizar al avanzar)

| Paso | Estado | Notas |
|---|---|---|
| 1. LoadoutRules + datos + tests | HECHO | Weapons/Simulation (AttachmentCatalog, WeaponBuild, LoadoutRules), AD_*.asset |
| 2. Silenciador + cañón roscado | HECHO | ArtSource/Tools/build_m1911_attachments.py (35x190 mm). Re-exportar para incluir Socket_SuppressorMuzzle (hoy se crea en runtime) |
| 3. Montaje en partida | HECHO | PlayerWeapon.ApplyBuild, boca efectiva, fogonazo suprimido, F9 debug (antes F7, chocaba con el simulador de red) |
| 4. Lobby + armero (estilo Tarkov) | HECHO (sin probar en Unity) | Menú principal Tarkov, pantalla de modding: arma centrada, slots con líneas, lista de piezas, stats |
| 5. Modelo del Vagabundo | HECHO | Assets/_Project/Art/Characters/Scav/Scav.fbx (mismo esqueleto, 18 clips). Falta: import humanoide (el postprocesador solo cubre Operator), materiales URP + outline, prefab |
| 6. WoundModel + tests | HECHO | Combat/Simulation/Wound.cs, DamageResult.Overflow |
| 7. Reacción física por hueso | HECHO | Combat/HitReactor.cs (versión local, probada en Unity) |
| 8. Sangre y decals | HECHO | Weapons/BloodEffects.cs, WoundDecals, prefabs FX_Blood_* |
| 9. Ragdoll | HECHO | Combat/Ragdoll.cs, RagdollProfile, máx. 6 simultáneos |
| 10. Desmembramiento visual | HECHO | Combat/Dismemberment.cs + Simulation/MeshSplit.cs (corte de malla en runtime, sin tocar modelos). F10 = modo prueba |
| 11. IA básica + spawner | PENDIENTE | NavMesh, estados puros |
| 12. Ajuste de gore y rendimiento | PENDIENTE | |

Conocido: NullReferenceException intermitente en ApplyBuild al alternar F7 (no reproducido).
