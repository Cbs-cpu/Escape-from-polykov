# Qué probar en cada commit

Registro de cambios pensado para probar en el editor. Lo más nuevo arriba. Cada entrada dice **qué ha cambiado,
cómo probarlo y qué debería sentirse**. Si algo no va, dilo indicando el commit.

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

