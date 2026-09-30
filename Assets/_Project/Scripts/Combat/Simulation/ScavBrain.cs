using System;

namespace Polykov.Combat
{
    public enum BrainMode : byte { Idle, Wander, Suspicion, Chase, Attack, Hurt, Dead }

    /// <summary>What the body should do this tick; the NavMesh agent / animator only execute it.</summary>
    public enum BrainGoal : byte { Stay, Wander, Investigate, Chase, FaceTarget }

    /// <summary>Static AI tuning (meters, seconds, degrees).</summary>
    [Serializable]
    public struct BrainTuning
    {
        public float SightRange, FovHalfAngle, HearRange, AttackRange;
        public float WanderSpeed, InvestigateSpeed, ChaseSpeed;
        public float IdleTime, WanderTime;
        public float SuspicionRise, SuspicionDecay, SuspicionToChase;
        public float LoseTargetTime, HurtTime, AttackInterval;
        /// <summary>Speed multipliers when legs are lost.</summary>
        public float OneLegSpeed, NoLegsSpeed;

        public static BrainTuning Default => new BrainTuning
        {
            SightRange = 30f, FovHalfAngle = 65f, HearRange = 55f, AttackRange = 2.0f,
            WanderSpeed = 1.2f, InvestigateSpeed = 2f, ChaseSpeed = 4.2f,
            IdleTime = 3f, WanderTime = 6f,
            SuspicionRise = 1.2f, SuspicionDecay = 0.25f, SuspicionToChase = 1f,
            LoseTargetTime = 5f, HurtTime = 0.5f, AttackInterval = 1.2f,
            OneLegSpeed = 0.5f, NoLegsSpeed = 0.18f,
        };
    }

    /// <summary>What the scav perceives this tick, computed by the presentation layer (raycasts, distances).</summary>
    public struct BrainSenses
    {
        public bool TargetAlive;
        public float TargetDistance;
        /// <summary>Absolute angle between the scav's forward and the target, degrees.</summary>
        public float TargetAngle;
        public bool LineOfSight;
        /// <summary>A shot was fired this tick; distance is to its origin.</summary>
        public bool HeardShot;
        public float HeardDistance;
        /// <summary>The scav took damage this tick.</summary>
        public bool WasHit;
        public bool Dead;
        public bool LeftLegSevered, RightLegSevered, LeftArmSevered, RightArmSevered;
    }

    public struct BrainState
    {
        public BrainMode Mode;
        public float Timer;
        /// <summary>0..1 awareness of the target.</summary>
        public float Suspicion;
        /// <summary>Time since the target was last perceived.</summary>
        public float TimeSinceSeen;
        public float AttackCooldown;
        public BrainMode ReturnMode;
        // Outputs
        public BrainGoal Goal;
        public float Speed;
        public bool Attack;
        /// <summary>The scav was told where the target is (heard / saw / was shot): the presenter records the position.</summary>
        public bool TargetKnown;

        public static BrainState Initial => new BrainState { Mode = BrainMode.Idle, Goal = BrainGoal.Stay };
    }

    /// <summary>Pure scav state machine: Idle→Wander→Suspicion→Chase/Attack→Hurt→Dead. Deterministic.</summary>
    public static class ScavBrain
    {
        public static BrainState Step(BrainState s, in BrainSenses n, in BrainTuning t, float dt)
        {
            s.Attack = false;
            if (n.Dead || s.Mode == BrainMode.Dead)
            {
                s.Mode = BrainMode.Dead; s.Goal = BrainGoal.Stay; s.Speed = 0f;
                return s;
            }

            bool seen = n.TargetAlive && n.LineOfSight && n.TargetDistance <= t.SightRange && n.TargetAngle <= t.FovHalfAngle;
            bool heard = n.TargetAlive && n.HeardShot && n.HeardDistance <= t.HearRange;
            if (seen)
            {
                s.TimeSinceSeen = 0f;
                s.TargetKnown = true;
                // Closer targets are noticed faster.
                float closeness = 1f + (1f - Clamp01(n.TargetDistance / t.SightRange)) * 2f;
                s.Suspicion = Clamp01(s.Suspicion + t.SuspicionRise * closeness * dt);
            }
            else
            {
                s.TimeSinceSeen += dt;
                s.Suspicion = Clamp01(s.Suspicion - t.SuspicionDecay * dt);
            }
            if (heard) { s.Suspicion = Clamp01(s.Suspicion + 0.6f); s.TargetKnown = true; s.TimeSinceSeen = 0f; }

            if (n.WasHit && s.Mode != BrainMode.Hurt)
            {
                s.ReturnMode = BrainMode.Chase;
                s.Mode = BrainMode.Hurt;
                s.Timer = t.HurtTime;
                s.Suspicion = 1f;
                s.TargetKnown = true;
            }

            switch (s.Mode)
            {
                case BrainMode.Idle:
                    s.Goal = BrainGoal.Stay; s.Speed = 0f;
                    s.Timer += dt;
                    if (s.Suspicion > 0.2f) Enter(ref s, BrainMode.Suspicion);
                    else if (s.Timer >= t.IdleTime) Enter(ref s, BrainMode.Wander);
                    break;

                case BrainMode.Wander:
                    s.Goal = BrainGoal.Wander; s.Speed = t.WanderSpeed;
                    s.Timer += dt;
                    if (s.Suspicion > 0.2f) Enter(ref s, BrainMode.Suspicion);
                    else if (s.Timer >= t.WanderTime) Enter(ref s, BrainMode.Idle);
                    break;

                case BrainMode.Suspicion:
                    s.Goal = seen ? BrainGoal.FaceTarget : BrainGoal.Investigate;
                    s.Speed = seen ? 0f : t.InvestigateSpeed;
                    if (s.Suspicion >= t.SuspicionToChase) Enter(ref s, BrainMode.Chase);
                    else if (s.Suspicion <= 0.01f) Enter(ref s, BrainMode.Idle);
                    break;

                case BrainMode.Chase:
                    s.Goal = BrainGoal.Chase; s.Speed = t.ChaseSpeed;
                    if (!n.TargetAlive) Enter(ref s, BrainMode.Idle);
                    else if (s.TimeSinceSeen >= t.LoseTargetTime) { s.Suspicion = 0.5f; Enter(ref s, BrainMode.Suspicion); }
                    else if (seen && n.TargetDistance <= t.AttackRange && CanAttack(n)) Enter(ref s, BrainMode.Attack);
                    break;

                case BrainMode.Attack:
                    s.Goal = BrainGoal.FaceTarget; s.Speed = 0f;
                    s.AttackCooldown -= dt;
                    if (!n.TargetAlive) { Enter(ref s, BrainMode.Idle); break; }
                    if (!CanAttack(n) || !seen || n.TargetDistance > t.AttackRange * 1.4f) { Enter(ref s, BrainMode.Chase); break; }
                    if (s.AttackCooldown <= 0f) { s.Attack = true; s.AttackCooldown = t.AttackInterval; }
                    break;

                case BrainMode.Hurt:
                    s.Goal = BrainGoal.Stay; s.Speed = 0f;
                    s.Timer -= dt;
                    if (s.Timer <= 0f) Enter(ref s, s.ReturnMode);
                    break;
            }

            s.Speed *= LegFactor(n, t);
            return s;
        }

        /// <summary>Speed multiplier from lost legs (both gone = crawl).</summary>
        public static float LegFactor(in BrainSenses n, in BrainTuning t)
        {
            if (n.LeftLegSevered && n.RightLegSevered) return t.NoLegsSpeed;
            return n.LeftLegSevered || n.RightLegSevered ? t.OneLegSpeed : 1f;
        }

        /// <summary>A scav without arms cannot strike.</summary>
        public static bool CanAttack(in BrainSenses n) => !(n.LeftArmSevered && n.RightArmSevered);

        private static void Enter(ref BrainState s, BrainMode mode)
        {
            s.Mode = mode;
            s.Timer = 0f;
            if (mode == BrainMode.Attack) s.AttackCooldown = 0.3f;
        }

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
