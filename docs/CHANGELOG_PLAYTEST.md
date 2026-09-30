# Qué probar en cada commit

Registro de cambios pensado para probar en el editor. Lo más nuevo arriba. Cada entrada dice **qué ha cambiado,
cómo probarlo y qué debería sentirse**. Si algo no va, dilo indicando el commit.

## Desmembramiento visual (paso 10)
- Cuando `WoundModel` decide cortar (probabilidad baja, solo en un miembro ya **destruido** y con suficiente daño de sobra), el
  miembro **sale del cuerpo**: se recorta en runtime la malla del Scav por los huesos afectados, se tapa el muñón con una
  tapa de carne y el miembro sale despedido como objeto físico (con su copia de huesos y colisiones), con chorro de sangre
  y mancha en el suelo. Brazos y piernas se cortan por el codo/rodilla si el disparo fue por debajo, si no por hombro/cadera.
  Cabeza: muy rara, y mata al instante.
- No se ha tocado ningún modelo 3D. El FBX del Scav se reimporta solo con lectura de malla activada (necesaria para cortar);
  si en consola sale `Dismemberment needs a readable mesh`, activa *Read/Write* en el importador de `Scav.fbx`.
- **Cómo probarlo:** en la arena, pulsa **F10** (modo desmembrar, aviso arriba), dispara a un brazo o pierna de un Scav
  hasta destruirlo (vida a 0) y dispara otra vez: el miembro sale despedido. Ajustes de sangre y desmembramiento en el
  menú de pausa (Esc). Máx. 8 miembros a la vez en escena (el más viejo se retira) y 45 s de vida.
- Límites conocidos: el corte sigue la frontera de triángulos (aspecto facetado, coherente con el estilo low-poly); el miembro
  es un objeto rígido (sin ragdoll propio por dedos/codos).
- Pruebas automáticas: 8 tests de `MeshSplitter`/`StumpCap` (partición sin pérdida de triángulos, anillo de corte cerrado
  aunque haya vértices duplicados por costuras UV, orientación de la tapa).

## Fusión con tu trabajo local (commit «update» de main)
- Arma: se mantiene el sistema de la nube como base (seguro/inspeccionar/recámara, red, salud, agarre verificado).
- De tu versión se conserva:
  - la **M1911 con texturas** (`M1911.fbx` + `M_M1911`);
  - los **VFX**: fogonazo, impactos por superficie (hormigón/metal), agujeros de bala;
  - los **casquillos** que salen despedidos y el **cargador que cae** al recargar (nuevo componente `WeaponEffects`);
  - las **dianas colgantes** de la arena y las superficies (`SurfaceMaterial`, `SteelTarget`);
  - el pipeline de modificadores de cámara;
  - los cambios de movimiento: **frenada lateral nítida** (`TurnTime`), aceleraciones nuevas, y **apuntar ralentiza
    (x0.6) y bloquea el sprint**.
- Tu script `build_m1911.py` es el oficial. El mío se guarda como `build_m1911_blockout.py`.
- Se ha retirado tu lógica de arma duplicada (PlayerWeaponController, WeaponSimulation, WeaponPose, WeaponMechanics, ArmIK,
  WeaponCameraEffects). Sigue en el historial de git si quieres recuperar algo.
- Qué revisar: que la pistola texturizada se vea bien orientada en la mano y al apuntar (miras alineadas), fogonazo,
  casquillos, cargador al suelo, e impactos con chispas en metal y polvo en hormigón.
- **Importante:** `main` se había sobrescrito con un push forzado. Para no perder trabajo, la próxima vez usa
  `git pull` antes de `git push`, y nunca `--force`.

## Salud por zonas + muñeco de entrenamiento
- Daño estilo Tarkov: cabeza 35, tórax 85, estómago 70, brazos 60, piernas 65. Muere con cabeza o tórax a 0. Un miembro a 0
  reparte el daño siguiente al resto (estómago x1.5, brazos x0.7, piernas x1).
- La M1911 hace 62: **1 tiro a la cabeza o 2 al tórax** matan.
- **F3** ahora también pone un **Operator de entrenamiento** a 7 m con hitboxes por hueso. Encima muestra la vida de cada
  zona y el último impacto (`-62 tórax`). Al morir cae hacia atrás y a los 3 s se levanta curado.
- Qué revisar: que los disparos en cada parte del cuerpo resten a la zona correcta (cabeza, brazos, piernas...).

## Manipulación del arma: seguro, inspeccionar, comprobar recámara
- **B** seguro: la palanca sube, el gatillo queda bloqueado (ni dispara ni hace clic), HUD `SEGURO (B)`. Se puede recargar con seguro.
- **L** inspeccionar (~3 s): la pistola se gira para ver el lado izquierdo y luego el derecho, a una mano.
  L otra vez, apuntar, disparar, recargar o esprintar la cancelan (el clic que cancela **no** dispara).
- **T** comprobar recámara (~1.3 s): pinzado frontal de la corredera, se abre un poco y vuelve; mensaje
  `Recámara: con bala` / `VACÍA`.
- Poses verificadas en Blender: `docs/screenshots/grip_inspect_*`, `grip_check_*`. Todo ajustable en `WD_M1911`
  (secciones *Inspect* y *Chamber check*).
- 11 tests nuevos del arma; el comando de red pasa a 13 bytes (nuevo byte de acciones).

## Simulador de red dentro del juego (F6) — Fase 2 sin red
- **F6** activa un "servidor" invisible: tus comandos le llegan con **100 ms ±10**, **5 % de pérdida** y redundancia x4;
  él te simula con colisiones reales y te devuelve el estado; tu personaje **predice** y **reconcilia** como en multijugador.
  Arriba a la derecha: correcciones, error en mm, comandos perdidos.
- Lo esperado: moverte, saltar, agacharte y chocar con paredes **sin notar nada** (0 correcciones o casi): el motor es
  determinista y el input se cuantiza igual en ambos lados.
- **F7** empuja al servidor 60 cm a un lado (algo que tú no podías predecir): deberías ver una corrección **suave** (no un
  teletransporte) de ~0.15 s. **F8** muestra una bolita donde el servidor cree que estás.
- Parámetros en el componente `LatencySimulator` del Player (latencia, jitter, pérdida, redundancia, búfer).
- Por dentro: la física de un tick se ha extraído a `CharacterBody` (re-ejecutable). El movimiento normal debería sentirse
  **exactamente igual** que antes; si notas cualquier diferencia sin F6 activo, dímelo.

## Correcciones del arma con el modelo importado
- **Apuntado**: ya no usa la rotación del punto `SightLine` del FBX (traía la conversión de ejes de Blender y habría
  girado la pistola al apuntar). La línea de mira es el eje del arma.
- **Recarga**: la mano izquierda coge la base del cargador con una orientación verificada en Blender
  (`docs/screenshots/grip_reload_*.png`); la pose de recarga se ha subido para que se vea en pantalla.
- **Fogonazo**: colocado delante de la boca con los ejes del arma.

## Animaciones de agacharse + inclinación corregida al correr
- 5 clips nuevos en el Operator: `Crouch_Idle`, `Crouch_F/B/L/R` (zancada a 1.5 m/s, pies apoyados: tobillos a ±2 cm del
  suelo medido en Blender). Hoja: `docs/screenshots/anim_crouch_and_lean_sheet.png`.
- **Arreglado un fallo previo**: correr, esprintar y strafe inclinaban el torso **hacia atrás** (signo invertido en
  `build_animations.py`). Ahora se inclinan hacia delante y la cabeza compensa para mirar al frente.
- Al abrir Unity, el `AC_Operator` **se regenera solo** (una vez) con un árbol Standing/Crouched mezclado por el
  parámetro `Crouch`; conserva el GUID, así que el prefab no se rompe. Si no se regenera: menú *Polykov > Build Operator Animator*.
- Con los clips activos, la pose procedural de agacharse (bajar pelvis en FootIK, doblar espalda) se desactiva sola.
- Qué revisar: agacharse parado y andando en las 4 direcciones (pies sin patinar), y que al correr/esprintar el cuerpo
  vaya hacia delante. Consola: debe salir `[Polykov] ... rebuilding AC_Operator` la primera vez.

## Agarre de la pistola verificado con el esqueleto real
- Las manos se han ajustado **viendo al Operator en Blender** con el mismo IK que usa Unity
  (`ArtSource/Tools/grip_preview.py`). Mira `docs/screenshots/grip_*.png`: cadera y apuntado, en primera y tercera persona.
- Mano derecha más atrás (antes los nudillos quedaban delante de la empuñadura); la izquierda envuelve los dedos de la derecha.
- **Pulgares hacia delante** por el lado izquierdo del armazón (nuevo en `HandIK` + `WD_M1911` → `RightThumbForward` /
  `LeftThumbForward`). Durante la recarga el pulgar izquierdo se suelta.
- Qué revisar: compara lo que ves en Unity con esas capturas. Si difiere mucho, el problema está en la importación del
  esqueleto o del FBX (no en los valores), y me lo dices.

## M1911 modelada en Blender
- La pistola ya no es de cajas: modelo low-poly de la M1911A1 (~1.500 tris) hecho en Blender con
  `ArtSource/Tools/build_m1911.py` (corredera con estrías, miras, ventana de expulsión, casquillo, guardamonte, martillo,
  seguro, retén, cachas de madera con tornillos, cargador con bala visible). Previews en `docs/screenshots/m1911_*.png`.
- Import: `Assets/_Project/Art/Weapons/M1911/M1911.fbx`. El juego mide sus puntos (mira, boca, empuñadura) y lo orienta
  solo, así que da igual la conversión de ejes. Si por lo que sea el FBX no carga, vuelve automáticamente al modelo de cajas.
- Materiales del proyecto (`Materials/Weapons`): acero, acero oscuro, madera, madera oscura, latón.
- Corregido: el gatillo ahora se mueve hacia atrás al apretar (antes iba hacia delante).
- Qué revisar: que la pistola se vea con materiales correctos (no rosa) y bien orientada en la mano; que la corredera
  retroceda al disparar y el cargador salga por la empuñadura al recargar.

## Resistencia (stamina)
- Esprintar gasta resistencia (8 s de sprint desde lleno) y cada salto gasta ~1.1 s. Barra fina abajo en el centro
  (solo aparece si no está llena; roja si estás agotado). Overlay F1: `Stamina: 75%`.
- Al agotarte **no puedes esprintar ni saltar** hasta recuperar el 35 %. Recupera tras 1.1 s sin gastar (más rápido parado).
- Con poca resistencia el arma se balancea más.
- Perfiles F2: *Tactical* tiene menos resistencia (6 s) y saltos más caros; *Arcade* la tiene infinita.
- Todos los valores en `MovementSettings.asset` → *Stamina*. `MaxStamina = 0` la desactiva.

## Sonido procedural + inercia del arma
- Sonidos **sintetizados en código** (provisionales, sin archivos): disparo, clic en vacío, sacar/meter cargador, soltar
  corredera (recarga en vacío), pasos (cadencia según distancia recorrida: nunca desincronizados con la velocidad; agachado
  suenan más bajo), salto y aterrizaje (más fuerte cuanto más alta la caída).
- Nuevo ajuste **Volumen** en el menú de pausa.
- El arma baja un poco al saltar y se hunde al aterrizar (inercia), menos al apuntar.
- Qué revisar: que el disparo suene contundente y sin chasquidos raros, y que los pasos coincidan con los pies.
  Volúmenes en `PlayerAudio` (componente del Player).

## Arma: M1911 en las manos (primera versión jugable)
- El personaje lleva una **M1911 provisional hecha de cajas** (proporciones reales, corredera, cargador, martillo, gatillo, miras).
  El modelo bueno se hará en Blender con los mismos nombres de piezas (ver `docs/TODO_LOCAL.md`).
- **Clic izquierdo** dispara (semiautomática: un disparo por clic, máx. ~450 disparos/min). **Clic derecho** apunta por las miras
  (zoom suave y sensibilidad ×0.8). **R** recarga. Munición abajo a la derecha: `7+1 / 28`.
- Recarga táctica (queda bala en recámara → 7+1, ~1.7 s) y en vacío (corredera bloqueada atrás, más larga ~2.2 s, al final
  la corredera vuelve). La mano izquierda va a por el cargador y vuelve al arma.
- Esprintar baja el arma y la lleva a una mano; al dejar de esprintar tarda ~0.2 s en poder disparar.
- Pegado a una pared el arma se recoge con el cañón arriba y **no dispara** (HUD: `BLOQUEADA`).
- Retroceso: la cámara sube y vuelve ~70 % sola; el arma tiene golpe hacia atrás y levanta la boca; balanceo al girar y al andar.
- **F3** coloca dianas de acero a 5/10/15/25 m delante de ti (caen al darles y se levantan). **F4** rellena munición.
- Qué revisar (lo más importante):
  1. **Manos**: ¿la derecha agarra la empuñadura y la izquierda la envuelve? Mira hacia abajo y en tercera persona (Scene view).
     Si están mal colocadas o giradas, ajusta en Play Mode `WD_M1911` → *Hands* (`RightHandPosition/Forward/Up`,
     `LeftHand...`) y `HandIK` (pistas de codo, curvatura de dedos) y apunta los valores buenos.
  2. **Apuntado**: la mira trasera y delantera deben quedar centradas; si no, `AdsSightDistance` y el objeto `SightLine`.
  3. **Posición de cadera** (`HipPosition/HipEuler`) y cuánto retroceso (`KickBack`, `KickRotation`, `CameraRecoilReturn`).

## Agacharse (C)
- **C** agacha (mantener). En el menú de pausa puedes cambiarlo a *alternar*; en modo alternar, esprintar o saltar te levanta.
- Agachado: velocidad máx. 1.5 m/s, no se puede esprintar ni saltar, la transición dura ~0.22 s.
- Si te agachas bajo algo bajo y sueltas C, **sigues agachado** hasta salir (overlay F1: `Crouch: 1.00 (ceiling)`).
- Pose sin animaciones nuevas: la pelvis baja 0.42 m y el IK de pies dobla las rodillas; la espalda se inclina un poco.
  Mira hacia abajo agachado: los pies deben seguir apoyados y las rodillas dobladas hacia delante.
- Qué revisar: que la cámara no atraviese techos bajos agachado; si las rodillas se doblan raro o la cámara queda demasiado
  alta/baja, ajusta `FootIK.crouchPelvisDrop` y `PlayerMotor.crouchHeight` y dime los valores.

## Menú de pausa + perfiles de movimiento
- **Esc** abre/cierra el menú de pausa (antes solo soltaba el ratón). Clic ya no reanuda: usa el botón *Reanudar* o Esc.
- Ajustes guardados entre sesiones: sensibilidad del ratón, FOV, balanceo de cámara, sacudidas de cámara, invertir Y.
  Poner *Balanceo* a 0 debe quitar el head bob y la inclinación al strafear; *Sacudidas* a 0 quita el hundimiento al aterrizar.
- **F2** (jugando) alterna el perfil de movimiento: *Asset → Default → Tactical (pesado) → Arcade (ágil)*. Aparece un aviso
  arriba y el overlay F1 muestra `Profile:`. Compara correr/frenar/cambiar de dirección y saltar entre perfiles y dime cuál
  se acerca más a lo que buscas (o qué mezclar). El asset no se modifica.
- El FOV del asset `CameraSettings` ya no se usa: ahora es un ajuste del jugador.


## Lobby + armero (paso 4)
- Nueva escena `Scenes/Lobby.unity` (primera en Build Settings). Menú **ENTRAR AL JUEGO** (carga `MovementTestArena` con fundido) y **ARMERO**.
  Menú *Polykov → Play from Lobby* hace que Play arranque siempre en el lobby. En la pausa hay **Volver al lobby**.
- Armero: arrastra para orbitar, rueda para zoom, cajas por slot con líneas al anclaje, panel de stats con deltas verde/rojo,
  lista de piezas con avisos "monta también / quita", RESTABLECER. Se guarda solo (PlayerPrefs) y la partida aplica el montaje.
- Qué revisar: que el personaje y el arma salgan bien encuadrados y el silenciador aparezca al elegirlo; Esc vuelve al menú principal.
  Si la escena sale vacía, dime el error de consola (la escena se regenera con `python3 Tools/make_lobby_scene.py`).
