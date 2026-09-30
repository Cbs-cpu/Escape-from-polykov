using System;
using System.Collections.Generic;
using NUnit.Framework;
using Polykov.Movement;
using Polykov.Weapons;
using UnityEngine;

namespace Polykov.Netcode.Tests
{
    public class PredictionTests
    {
        private const float Dt = 1f / 60f;

        private static PlayerCommand Cmd(uint tick, float x, float y, float yaw, bool sprint = false, bool jump = false)
            => new PlayerCommand(tick, new MovementInput(new Vector2(x, y), yaw, sprint, false, jump, 0f, false), default, 0f)
                .Quantized();

        /// <summary>Scripted input: run, turn, sprint, jump, strafe — enough to exercise the motor.</summary>
        private static PlayerCommand Script(uint tick)
        {
            float yaw = tick * 1.7f;
            if (tick < 60) return Cmd(tick, 0f, 1f, yaw);
            if (tick < 140) return Cmd(tick, 0.2f, 1f, yaw, sprint: true, jump: tick == 100);
            if (tick < 200) return Cmd(tick, -1f, 0.3f, yaw);
            return Cmd(tick, 0f, 0f, yaw);
        }

        [Test]
        public void MatchingServer_IsConfirmed()
        {
            var sim = new FlatSimulator();
            var prediction = new ClientPrediction();
            var state = new NetPlayerState();
            for (uint t = 1; t <= 30; t++)
            {
                state = sim.Simulate(state, Script(t), Dt);
                prediction.Record(Script(t), state);
            }
            prediction.TryGetState(20, out NetPlayerState at20);
            ReconcileResult result = prediction.Reconcile(20, at20, sim, Dt, out NetPlayerState corrected, out float error);
            Assert.AreEqual(ReconcileResult.Confirmed, result);
            Assert.AreEqual(0f, error);
            Assert.AreEqual(state.Position, corrected.Position);
        }

        [Test]
        public void DivergedServer_ReplaysInputsOnTopOfServerState()
        {
            var sim = new FlatSimulator();
            var prediction = new ClientPrediction();
            var state = new NetPlayerState();
            for (uint t = 1; t <= 40; t++)
            {
                state = sim.Simulate(state, Script(t), Dt);
                prediction.Record(Script(t), state);
            }

            // Server says that at tick 25 we were pushed 0.5 m to the side (e.g. by a collision we didn't predict).
            prediction.TryGetState(25, out NetPlayerState server);
            server.Position += new Vector3(0.5f, 0f, 0f);
            ReconcileResult result = prediction.Reconcile(25, server, sim, Dt, out NetPlayerState corrected, out float error);

            Assert.AreEqual(ReconcileResult.Corrected, result);
            Assert.AreEqual(0.5f, error, 1e-4f);
            Assert.AreEqual(0f, Vector3.Distance(state.Position + new Vector3(0.5f, 0f, 0f), corrected.Position), 1e-3f,
                "replaying the same inputs from the corrected state must carry the offset forward");
            prediction.TryGetState(40, out NetPlayerState latest);
            Assert.AreEqual(corrected.Position, latest.Position, "history is rewritten with the replayed states");
        }

        [Test]
        public void UnknownTick_ReportsNoHistory()
        {
            var prediction = new ClientPrediction(16);
            var sim = new FlatSimulator();
            var state = new NetPlayerState();
            for (uint t = 1; t <= 40; t++)
            {
                state = sim.Simulate(state, Script(t), Dt);
                prediction.Record(Script(t), state);
            }
            Assert.AreEqual(ReconcileResult.NoHistory, prediction.Reconcile(3, state, sim, Dt, out _, out _));
        }

        private struct LinkResult
        {
            public int Corrections;
            public int Confirmations;
            public int Missed;
            public float FinalError;
        }

        /// <summary>
        /// Client predicts and sends each command <paramref name="redundancy"/> times through a link with one-way
        /// latency and packet loss; the server runs the same motor on what it receives and sends states back.
        /// </summary>
        private static LinkResult SimulateLink(int redundancy, float loss, int seed, int bufferTicks, int latency = 6)
        {
            var sim = new FlatSimulator();
            var rng = new System.Random(seed);

            var prediction = new ClientPrediction();
            var serverQueue = new ServerInputQueue();
            var toServer = new List<(int arrive, PlayerCommand cmd)>();
            var toClient = new List<(int arrive, uint tick, NetPlayerState state)>();
            var clientState = new NetPlayerState();
            var serverState = new NetPlayerState();
            uint lastServerTick = 0;
            var lastServerState = new NetPlayerState();
            var recent = new Queue<PlayerCommand>();
            int corrections = 0, confirmations = 0, missed = 0;

            for (int frame = 1; frame <= 300; frame++)
            {
                // Client: predict and send (current + 2 previous commands).
                PlayerCommand cmd = Script((uint)frame);
                clientState = sim.Simulate(clientState, cmd, Dt);
                prediction.Record(cmd, clientState);
                recent.Enqueue(cmd);
                if (recent.Count > redundancy) recent.Dequeue();
                if (rng.NextDouble() > loss)
                    foreach (PlayerCommand c in recent) toServer.Add((frame + latency, c));

                // Server: start once the buffer holds bufferTicks commands, then one per tick.
                foreach (var p in toServer.FindAll(p => p.arrive == frame)) serverQueue.Enqueue(p.cmd);
                toServer.RemoveAll(p => p.arrive <= frame);
                if (frame > latency + bufferTicks && serverQueue.TryDequeue(out PlayerCommand serverCmd))
                {
                    serverState = sim.Simulate(serverState, serverCmd, Dt);
                    lastServerTick = serverCmd.Tick;
                    lastServerState = serverState;
                    if (rng.NextDouble() > loss) toClient.Add((frame + latency, serverCmd.Tick, serverState));
                }

                // Client: reconcile with whatever arrived.
                foreach (var s in toClient.FindAll(s => s.arrive == frame))
                {
                    ReconcileResult r = prediction.Reconcile(s.tick, s.state, sim, Dt, out _, out _);
                    if (r == ReconcileResult.Corrected) corrections++;
                    if (r == ReconcileResult.Confirmed) confirmations++;
                }
                toClient.RemoveAll(s => s.arrive <= frame);
            }
            missed = serverQueue.MissedCommands;

            // Deliver the last authoritative state and check the client agrees with it.
            prediction.Reconcile(lastServerTick, lastServerState, sim, Dt, out _, out _);
            prediction.TryGetState(lastServerTick, out NetPlayerState clientView);
            return new LinkResult
            {
                Corrections = corrections,
                Confirmations = confirmations,
                Missed = missed,
                FinalError = Vector3.Distance(clientView.Position, lastServerState.Position),
            };
        }

        /// <summary>Deterministic simulation + quantized inputs + enough redundancy = the server never disagrees.</summary>
        [Test]
        public void LaggyLossyLink_WithRedundancy_NeedsNoCorrections()
        {
            LinkResult r = SimulateLink(redundancy: 5, loss: 0.2f, seed: 7, bufferTicks: 5);
            Assert.Greater(r.Confirmations, 150);
            Assert.AreEqual(0, r.Missed, "5x redundancy should cover 20% loss");
            Assert.AreEqual(0, r.Corrections);
            Assert.AreEqual(0f, r.FinalError, 1e-4f);
        }

        /// <summary>When commands do get lost the server improvises, and reconciliation brings the client back.</summary>
        [TestCase(7)]
        [TestCase(21)]
        [TestCase(99)]
        public void BadLink_MissingCommands_ClientStillConvergesToServer(int seed)
        {
            LinkResult r = SimulateLink(redundancy: 1, loss: 0.3f, seed: seed, bufferTicks: 2);
            Assert.Greater(r.Missed, 0, "this link must actually lose commands");
            Assert.Greater(r.Corrections, 0);
            Assert.AreEqual(0f, r.FinalError, 1e-3f);
        }

        [Test]
        public void Smoother_HidesSmallCorrections_SnapsLargeOnes()
        {
            var smoother = new VisualErrorSmoother();
            smoother.OnCorrection(new Vector3(0.2f, 0f, 0f), Vector3.zero);
            Assert.AreEqual(0.2f, smoother.Offset.x, 1e-5f);
            for (int i = 0; i < 12; i++) smoother.Update(Dt);
            Assert.Less(smoother.Offset.magnitude, 0.2f * 0.1f, "mostly gone after 0.2 s");

            smoother.OnCorrection(new Vector3(5f, 0f, 0f), Vector3.zero);
            Assert.AreEqual(Vector3.zero, smoother.Offset);
        }
    }
}
