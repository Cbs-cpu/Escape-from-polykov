# Qué probar en cada commit

Registro de cambios pensado para probar en el editor. Lo más nuevo arriba. Cada entrada dice **qué ha cambiado,
cómo probarlo y qué debería sentirse**. Si algo no va, dilo indicando el commit.

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

