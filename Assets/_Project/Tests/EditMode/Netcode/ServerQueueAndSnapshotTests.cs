using NUnit.Framework;
using Polykov.Movement;
using Polykov.Weapons;
using UnityEngine;

namespace Polykov.Netcode.Tests
{
    public class ServerQueueAndSnapshotTests
    {
        private static PlayerCommand Cmd(uint tick, bool jump = false)
            => new PlayerCommand(tick, new MovementInput(new Vector2(0f, 1f), 0f, false, false, jump, 0f, false),
                new WeaponInput(false, false, false, false), 0f);

        [Test]
        public void Queue_ConsumesInOrder_IgnoresDuplicatesAndLateOnes()
        {
            var q = new ServerInputQueue();
            Assert.IsFalse(q.TryDequeue(out _), "nothing before the first command");
            Assert.IsTrue(q.Enqueue(Cmd(10)));
            Assert.IsTrue(q.Enqueue(Cmd(11)));
            Assert.IsFalse(q.Enqueue(Cmd(11)), "duplicate");
            Assert.AreEqual(2, q.BufferedCount);

            q.TryDequeue(out PlayerCommand a);
            q.TryDequeue(out PlayerCommand b);
            Assert.AreEqual(10u, a.Tick);
            Assert.AreEqual(11u, b.Tick);
            Assert.IsFalse(q.Enqueue(Cmd(9)), "older than what was processed");
        }

        [Test]
        public void Queue_MissingCommand_RepeatsLastWithoutOneShots()
        {
            var q = new ServerInputQueue();
            q.Enqueue(Cmd(1, jump: true));
            q.TryDequeue(out PlayerCommand first);
            Assert.IsTrue(first.Movement.Jump);

            q.TryDequeue(out PlayerCommand repeated);
            Assert.AreEqual(2u, repeated.Tick);
            Assert.IsFalse(repeated.Movement.Jump);
            Assert.AreEqual(1f, repeated.Movement.Move.y);
            Assert.AreEqual(1, q.MissedCommands);
            Assert.IsFalse(q.Enqueue(Cmd(2)), "arrived too late: already simulated as a repeat");
        }

        [Test]
        public void TickRate_SpeedsUpWhenStarved_SlowsWhenFlooded()
        {
            var adj = new TickRateAdjuster();
            Assert.Greater(adj.Update(0), 1f);
            adj = new TickRateAdjuster();
            Assert.Less(adj.Update(10), 1f);
            adj = new TickRateAdjuster();
            Assert.AreEqual(1f, adj.Update(2), 1e-5f);
            adj = new TickRateAdjuster();
            Assert.LessOrEqual(adj.Update(-100), 1.08f + 1e-5f);
        }

        private static NetPlayerState At(float x, float yaw, float vx = 0f)
        {
            var s = new NetPlayerState { Position = new Vector3(x, 0f, 0f), Yaw = yaw };
            s.Movement.Velocity = new Vector3(vx, 0f, 0f);
            return s;
        }

        [Test]
        public void Snapshots_InterpolateBetweenTicks_IncludingYawWrap()
        {
            var buffer = new SnapshotBuffer();
            buffer.Add(10, At(0f, 350f));
            buffer.Add(11, At(1f, 10f));
            Assert.IsTrue(buffer.Sample(10.5f, 1f / 60f, out NetPlayerState mid));
            Assert.AreEqual(0.5f, mid.Position.x, 1e-5f);
            Assert.AreEqual(0f, Mathf.DeltaAngle(mid.Yaw, 0f), 1e-3f, "shortest way across 0/360");
        }

        [Test]
        public void Snapshots_BridgeLostPackets()
        {
            var buffer = new SnapshotBuffer();
            buffer.Add(10, At(0f, 0f));
            buffer.Add(14, At(4f, 0f));
            buffer.Sample(12f, 1f / 60f, out NetPlayerState s);
            Assert.AreEqual(2f, s.Position.x, 1e-5f);
        }

        [Test]
        public void Snapshots_ExtrapolateABit_ThenHold()
        {
            var buffer = new SnapshotBuffer { MaxExtrapolationTicks = 3f };
            buffer.Add(10, At(0f, 0f, vx: 6f));
            buffer.Sample(12f, 0.1f, out NetPlayerState near);
            Assert.AreEqual(1.2f, near.Position.x, 1e-4f);
            buffer.Sample(30f, 0.1f, out NetPlayerState far);
            Assert.AreEqual(1.8f, far.Position.x, 1e-4f, "bounded to MaxExtrapolationTicks");
        }
    }
}
