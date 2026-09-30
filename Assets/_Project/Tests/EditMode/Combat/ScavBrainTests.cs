using NUnit.Framework;

namespace Polykov.Combat.Tests
{
    public class ScavBrainTests
    {
        private static BrainTuning T => BrainTuning.Default;
        private const float Dt = 0.1f;

        private static BrainSenses Sees(float distance) => new BrainSenses
        {
            TargetAlive = true, TargetDistance = distance, TargetAngle = 0f, LineOfSight = true,
        };

        private static BrainSenses Nothing => new BrainSenses { TargetAlive = true, TargetDistance = 100f, TargetAngle = 180f };

        private static BrainState Run(BrainState s, BrainSenses n, float seconds)
        {
            for (float t = 0; t < seconds; t += Dt) s = ScavBrain.Step(s, n, T, Dt);
            return s;
        }

        [Test]
        public void Idle_BecomesWander_ThenIdleAgain()
        {
            var s = Run(BrainState.Initial, Nothing, T.IdleTime + 0.3f);
            Assert.AreEqual(BrainMode.Wander, s.Mode);
            Assert.AreEqual(BrainGoal.Wander, s.Goal);
            s = Run(s, Nothing, T.WanderTime + 0.3f);
            Assert.AreEqual(BrainMode.Idle, s.Mode);
        }

        [Test]
        public void SeeingTarget_EscalatesToChase()
        {
            var s = Run(BrainState.Initial, Sees(15f), 3f);
            Assert.AreEqual(BrainMode.Chase, s.Mode);
            Assert.AreEqual(T.ChaseSpeed, s.Speed, 1e-4f);
        }

        [Test]
        public void TargetBehindOrBeyondSight_IsNotSeen()
        {
            var behind = Sees(10f); behind.TargetAngle = 120f;
            Assert.AreEqual(BrainMode.Wander, Run(BrainState.Initial, behind, 4f).Mode);
            var far = Sees(T.SightRange + 5f);
            Assert.AreEqual(BrainMode.Wander, Run(BrainState.Initial, far, 4f).Mode);
        }

        [Test]
        public void NoLineOfSight_IsNotSeen()
        {
            var wall = Sees(10f); wall.LineOfSight = false;
            Assert.AreNotEqual(BrainMode.Chase, Run(BrainState.Initial, wall, 4f).Mode);
        }

        [Test]
        public void Gunshot_MakesItInvestigate_TwoShotsMakeItChase()
        {
            var shot = Nothing; shot.HeardShot = true; shot.HeardDistance = 30f;
            var s = ScavBrain.Step(BrainState.Initial, shot, T, Dt);
            s = ScavBrain.Step(s, Nothing, T, Dt);
            Assert.AreEqual(BrainMode.Suspicion, s.Mode);
            Assert.AreEqual(BrainGoal.Investigate, s.Goal);
            Assert.IsTrue(s.TargetKnown);
            s = ScavBrain.Step(s, shot, T, Dt);
            s = ScavBrain.Step(s, Nothing, T, Dt);
            Assert.AreEqual(BrainMode.Chase, s.Mode);
        }

        [Test]
        public void DistantGunshot_IsIgnored()
        {
            var shot = Nothing; shot.HeardShot = true; shot.HeardDistance = T.HearRange + 10f;
            Assert.AreEqual(BrainMode.Idle, ScavBrain.Step(BrainState.Initial, shot, T, Dt).Mode);
        }

        [Test]
        public void Suspicion_DecaysBackToIdle()
        {
            var shot = Nothing; shot.HeardShot = true; shot.HeardDistance = 20f;
            var s = ScavBrain.Step(BrainState.Initial, shot, T, Dt);
            s = Run(s, Nothing, 10f);
            Assert.AreNotEqual(BrainMode.Suspicion, s.Mode);
            Assert.AreNotEqual(BrainMode.Chase, s.Mode);
        }

        [Test]
        public void CloseTarget_Attacks_WithCooldown()
        {
            var s = Run(BrainState.Initial, Sees(15f), 3f);
            int attacks = 0;
            var close = Sees(1.5f);
            for (float t = 0; t < 3f; t += Dt)
            {
                s = ScavBrain.Step(s, close, T, Dt);
                if (s.Attack) attacks++;
            }
            Assert.AreEqual(BrainMode.Attack, s.Mode);
            Assert.That(attacks, Is.InRange(2, 3));
        }

        [Test]
        public void NoArms_CannotAttack_KeepsChasing()
        {
            var s = Run(BrainState.Initial, Sees(15f), 3f);
            var close = Sees(1.5f); close.LeftArmSevered = close.RightArmSevered = true;
            s = Run(s, close, 3f);
            Assert.AreEqual(BrainMode.Chase, s.Mode);
        }

        [Test]
        public void LostLegs_SlowDown()
        {
            var seen = Sees(15f);
            var s = Run(BrainState.Initial, seen, 3f);
            seen.LeftLegSevered = true;
            Assert.AreEqual(T.ChaseSpeed * T.OneLegSpeed, ScavBrain.Step(s, seen, T, Dt).Speed, 1e-4f);
            seen.RightLegSevered = true;
            Assert.AreEqual(T.ChaseSpeed * T.NoLegsSpeed, ScavBrain.Step(s, seen, T, Dt).Speed, 1e-4f);
        }

        [Test]
        public void Hurt_StaggersThenChases()
        {
            var hit = Nothing; hit.WasHit = true;
            var s = ScavBrain.Step(BrainState.Initial, hit, T, Dt);
            Assert.AreEqual(BrainMode.Hurt, s.Mode);
            Assert.AreEqual(0f, s.Speed);
            s = Run(s, Nothing, T.HurtTime + 0.3f);
            Assert.AreEqual(BrainMode.Chase, s.Mode);
        }

        [Test]
        public void LosingTarget_FallsBackToSuspicion()
        {
            var s = Run(BrainState.Initial, Sees(15f), 3f);
            s = Run(s, Nothing, T.LoseTargetTime + 0.5f);
            Assert.AreNotEqual(BrainMode.Chase, s.Mode);
        }

        [Test]
        public void Dead_IsTerminal()
        {
            var dead = Nothing; dead.Dead = true;
            var s = ScavBrain.Step(BrainState.Initial, dead, T, Dt);
            s = Run(s, Sees(1f), 3f);
            Assert.AreEqual(BrainMode.Dead, s.Mode);
            Assert.AreEqual(0f, s.Speed);
        }
    }
}
