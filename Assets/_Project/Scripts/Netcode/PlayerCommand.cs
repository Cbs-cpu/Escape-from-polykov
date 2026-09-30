using Polykov.Movement;
using Polykov.Weapons;
using UnityEngine;

namespace Polykov.Netcode
{
    /// <summary>
    /// Everything the client sends for one simulation tick: movement + weapon intent + view pitch (for server-side
    /// hit validation). Always <see cref="Quantized"/> before being simulated, on the client too, so client
    /// prediction and server authority run on bit-identical inputs.
    /// </summary>
    public readonly struct PlayerCommand
    {
        public const int SerializedSize = 4 + 1 + 1 + 2 + 2 + 1 + 1;

        [System.ThreadStatic] private static byte[] _scratch;

        public readonly uint Tick;
        public readonly MovementInput Movement;
        public readonly WeaponInput Weapon;
        /// <summary>View pitch in degrees (positive = down).</summary>
        public readonly float Pitch;

        public PlayerCommand(uint tick, in MovementInput movement, in WeaponInput weapon, float pitch)
        {
            Tick = tick;
            Movement = movement;
            Weapon = weapon;
            Pitch = pitch;
        }

        /// <summary>The command exactly as the other side will decode it.</summary>
        public PlayerCommand Quantized()
        {
            byte[] buffer = _scratch ??= new byte[SerializedSize];
            var writer = new ByteWriter(buffer);
            Write(ref writer);
            var reader = new ByteReader(buffer, writer.Length);
            return Read(ref reader);
        }

        public void Write(ref ByteWriter w)
        {
            w.WriteUInt(Tick);
            Vector2 move = Vector2.ClampMagnitude(Movement.Move, 1f);
            w.WriteSByte(QuantizeUnit(move.x));
            w.WriteSByte(QuantizeUnit(move.y));
            w.WriteUShort((ushort)(Mathf.Repeat(Movement.Yaw, 360f) / 360f * 65536f + 0.5f));
            w.WriteShort((short)Mathf.RoundToInt(Mathf.Clamp(Pitch, -90f, 90f) * 100f));
            w.WriteSByte(QuantizeUnit(Movement.Lean));
            int flags = (Movement.Sprint ? 1 : 0) | (Movement.Walk ? 2 : 0) | (Movement.Jump ? 4 : 0) | (Movement.Crouch ? 8 : 0)
                        | (Weapon.TriggerHeld ? 16 : 0) | (Weapon.TriggerPressed ? 32 : 0) | (Weapon.AimHeld ? 64 : 0)
                        | (Weapon.ReloadPressed ? 128 : 0);
            w.WriteByte((byte)flags);
        }

        public static PlayerCommand Read(ref ByteReader r)
        {
            uint tick = r.ReadUInt();
            float x = r.ReadSByte() / 127f;
            float y = r.ReadSByte() / 127f;
            float yaw = r.ReadUShort() / 65536f * 360f;
            float pitch = r.ReadShort() / 100f;
            float lean = r.ReadSByte() / 127f;
            int f = r.ReadByte();
            var movement = new MovementInput(new Vector2(x, y), yaw, (f & 1) != 0, (f & 2) != 0, (f & 4) != 0, lean, (f & 8) != 0);
            var weapon = new WeaponInput((f & 16) != 0, (f & 32) != 0, (f & 64) != 0, (f & 128) != 0);
            return new PlayerCommand(tick, movement, weapon, pitch);
        }

        /// <summary>Same intent without one-shot actions: what the server repeats when a command is missing.</summary>
        public PlayerCommand AsRepeat(uint tick)
        {
            var movement = new MovementInput(Movement.Move, Movement.Yaw, Movement.Sprint, Movement.Walk, false, Movement.Lean,
                Movement.Crouch);
            var weapon = new WeaponInput(Weapon.TriggerHeld, false, Weapon.AimHeld, false);
            return new PlayerCommand(tick, movement, weapon, Pitch);
        }

        private static sbyte QuantizeUnit(float v) => (sbyte)Mathf.RoundToInt(Mathf.Clamp(v, -1f, 1f) * 127f);
    }
}
