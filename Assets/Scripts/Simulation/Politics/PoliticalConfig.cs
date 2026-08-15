using System;
using DivineWorld.Simulation.Data;

namespace DivineWorld.Simulation.Politics
{
    [Serializable]
    public class InitialPoliticalRelation
    {
        public RegionId RegionA;
        public RegionId RegionB;
        public float RelationValue;
    }

    /// <summary>
    /// Central politics configuration. Thresholds and initial pairs live here, not scattered in systems.
    /// Independent from frozen <c>SimulationConfig</c>.
    /// </summary>
    [Serializable]
    public class PoliticalConfig
    {
        public float MinRelationValue = -100f;
        public float MaxRelationValue = 100f;
        public float DebugAdjustmentMagnitude = 10f;

        /// <summary>value &gt;= FriendlyMin → Friendly. Default 50 matches +75 友好; +25 正常 stays Neutral.</summary>
        public float FriendlyMin = 50f;

        /// <summary>HostileMax &lt; value &lt;= TenseMax → Tense. Default -25 matches 紧张.</summary>
        public float TenseMax = -25f;

        /// <summary>value &lt;= HostileMax → Hostile. Default -75 matches 敌对 / 极端敌对.</summary>
        public float HostileMax = -75f;

        public InitialPoliticalRelation[] InitialPoliticalRelations;

        public static PoliticalConfig CreateDefault()
        {
            return new PoliticalConfig
            {
                InitialPoliticalRelations = new[]
                {
                    new InitialPoliticalRelation
                    {
                        RegionA = RegionId.Theocracy,
                        RegionB = RegionId.Empire,
                        RelationValue = 0f
                    },
                    new InitialPoliticalRelation
                    {
                        RegionA = RegionId.Theocracy,
                        RegionB = RegionId.Sea,
                        RelationValue = 0f
                    },
                    new InitialPoliticalRelation
                    {
                        RegionA = RegionId.Empire,
                        RegionB = RegionId.Sea,
                        RelationValue = 0f
                    }
                }
            };
        }

        public PoliticalConfig Clone()
        {
            var copy = new PoliticalConfig
            {
                MinRelationValue = MinRelationValue,
                MaxRelationValue = MaxRelationValue,
                DebugAdjustmentMagnitude = DebugAdjustmentMagnitude,
                FriendlyMin = FriendlyMin,
                TenseMax = TenseMax,
                HostileMax = HostileMax,
                InitialPoliticalRelations = Array.Empty<InitialPoliticalRelation>()
            };

            if (InitialPoliticalRelations != null && InitialPoliticalRelations.Length > 0)
            {
                copy.InitialPoliticalRelations = new InitialPoliticalRelation[InitialPoliticalRelations.Length];
                for (int i = 0; i < InitialPoliticalRelations.Length; i++)
                {
                    var src = InitialPoliticalRelations[i];
                    copy.InitialPoliticalRelations[i] = src == null
                        ? null
                        : new InitialPoliticalRelation
                        {
                            RegionA = src.RegionA,
                            RegionB = src.RegionB,
                            RelationValue = src.RelationValue
                        };
                }
            }

            return copy;
        }
    }
}
