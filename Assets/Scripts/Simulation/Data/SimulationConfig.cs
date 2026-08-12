using System;
using UnityEngine;

namespace DivineWorld.Simulation.Data
{
    /// <summary>
    /// Central tunable parameters for P2-A macro simulation.
    /// Design rules are fixed; numeric coefficients are configurable defaults.
    /// </summary>
    [Serializable]
    public class SimulationConfig
    {
        public const int DaysPerYear = 360;
        public const int DaysPerSeason = 90;

        [Header("Labor / Tech")]
        public float LaborDivisor = 10000f;
        public float LaborMin = 0.2f;
        public float LaborMax = 8f;
        public float TechBase = 0.6f;
        public float TechFromEducation = 0.8f;

        [Header("Food")]
        public float FoodNeedPerCapita = 0.02f;
        public float FoodBaseSpoilageRate = 0.006f;
        public float FoodShortageRatio = 0.2f;
        public float FoodSurplusRatio = 0.8f;
        public float FoodShortageDiseaseGain = 0.01f;
        public float FoodShortageStabilityLoss = 0.004f;
        public float FoodSurplusStabilityGain = 0.0015f;
        public float FoodShortageEventChance = 0.08f;

        [Header("Water")]
        public float WaterNeedPerCapita = 0.015f;

        [Header("Other resource yields (capacity / population based)")]
        public float TimberLaborScale = 0.01f;
        public float OreLaborScale = 0.008f;
        public float SeaTimberFlatYield = 0.2f;
        public float SeaOreFlatYield = 0.3f;
        public float FaithYieldPerCapita = 0.0004f;
        public float KnowledgeYieldPerCapita = 0.00025f;
        public float MagicYieldPerCapita = 0.0001f;
        public float SeaMagicAffinityBonus = 1.4f;
        public float LandMagicAffinityBonus = 0.7f;

        [Header("Population")]
        public float BaseFertility = 0.00035f;
        public float BaseNaturalDeath = 0.00022f;
        public float DiseaseDeathRate = 0.0015f;
        public float DiseaseDecay = 0.995f;
        public float MinPopulation = 100f;
        public float FoodRatioSoftCap = 0.5f;
        public float CarryingFoodWeight = 0.45f;
        public float CarryingWaterWeight = 0.25f;
        public float CarryingLandWeight = 0.30f;
        public float CarryingTechBase = 0.7f;
        public float CarryingTechFromEducation = 0.3f;
        public float FoodProductionCapacityNorm = 1000f;
        public float WaterAvailabilityNormPerCapita = 0.015f;

        [Header("Society drift")]
        public float EducationLerp = 0.002f;
        public float FaithLerp = 0.002f;
        public float KnowledgeEducationDivisor = 20000f;
        public float FaithLevelDivisor = 25000f;
        public float UnrestChance = 0.01f;
        public float UnrestStabilityThreshold = 0.45f;
        public float UnrestStabilityLoss = 0.02f;

        [Header("Weather continuity")]
        public float WeatherPullToBaseline = 0.08f;
        public float WeatherNoiseAmplitude = 0.03f;

        [Header("Seasonal food production modifiers")]
        public float SpringFoodProduction = 1.00f;
        public float SummerFoodProduction = 0.90f;
        public float AutumnFoodProduction = 1.25f;
        public float WinterFoodProduction = 0.35f;

        [Header("Seasonal food spoilage modifiers (Summer >= Spring > Autumn > Winter)")]
        public float SpringFoodSpoilage = 1.20f;
        public float SummerFoodSpoilage = 1.40f;
        public float AutumnFoodSpoilage = 0.70f;
        public float WinterFoodSpoilage = 0.35f;

        [Header("Seasonal disease modifiers (Summer highest)")]
        public float SpringDisease = 1.00f;
        public float SummerDisease = 1.55f;
        public float AutumnDisease = 1.00f;
        public float WinterDisease = 0.90f;

        [Header("Seasonal death pressure modifiers (Winter highest)")]
        public float SpringDeath = 1.00f;
        public float SummerDeath = 1.00f;
        public float AutumnDeath = 1.05f;
        public float WinterDeath = 1.65f;

        [Header("Seasonal carrying-capacity environment modifiers")]
        public float SpringCarrying = 1.00f;
        public float SummerCarrying = 0.95f;
        public float AutumnCarrying = 1.10f;
        public float WinterCarrying = 0.70f;

        [Header("Seasonal water capacity modifiers")]
        public float SpringWaterCapacity = 1.00f;
        public float SummerWaterCapacity = 0.90f;
        public float AutumnWaterCapacity = 1.05f;
        public float WinterWaterCapacity = 0.75f;

        [Header("Seasonal weather baseline / range")]
        public float SpringWeatherBaseline = 1.00f;
        public float SpringWeatherMin = 0.85f;
        public float SpringWeatherMax = 1.15f;
        public float SummerWeatherBaseline = 1.05f;
        public float SummerWeatherMin = 0.90f;
        public float SummerWeatherMax = 1.25f;
        public float AutumnWeatherBaseline = 1.00f;
        public float AutumnWeatherMin = 0.88f;
        public float AutumnWeatherMax = 1.12f;
        public float WinterWeatherBaseline = 0.75f;
        public float WinterWeatherMin = 0.55f;
        public float WinterWeatherMax = 0.95f;

        public static SimulationConfig CreateDefault() => new SimulationConfig();

        public float FoodProductionModifier(SeasonId season)
        {
            switch (season)
            {
                case SeasonId.Spring: return SpringFoodProduction;
                case SeasonId.Summer: return SummerFoodProduction;
                case SeasonId.Autumn: return AutumnFoodProduction;
                case SeasonId.Winter: return WinterFoodProduction;
                default: return 1f;
            }
        }

        public float FoodSpoilageModifier(SeasonId season)
        {
            switch (season)
            {
                case SeasonId.Spring: return SpringFoodSpoilage;
                case SeasonId.Summer: return SummerFoodSpoilage;
                case SeasonId.Autumn: return AutumnFoodSpoilage;
                case SeasonId.Winter: return WinterFoodSpoilage;
                default: return 1f;
            }
        }

        public float DiseaseModifier(SeasonId season)
        {
            switch (season)
            {
                case SeasonId.Spring: return SpringDisease;
                case SeasonId.Summer: return SummerDisease;
                case SeasonId.Autumn: return AutumnDisease;
                case SeasonId.Winter: return WinterDisease;
                default: return 1f;
            }
        }

        public float DeathModifier(SeasonId season)
        {
            switch (season)
            {
                case SeasonId.Spring: return SpringDeath;
                case SeasonId.Summer: return SummerDeath;
                case SeasonId.Autumn: return AutumnDeath;
                case SeasonId.Winter: return WinterDeath;
                default: return 1f;
            }
        }

        public float CarryingModifier(SeasonId season)
        {
            switch (season)
            {
                case SeasonId.Spring: return SpringCarrying;
                case SeasonId.Summer: return SummerCarrying;
                case SeasonId.Autumn: return AutumnCarrying;
                case SeasonId.Winter: return WinterCarrying;
                default: return 1f;
            }
        }

        public float WaterCapacityModifier(SeasonId season)
        {
            switch (season)
            {
                case SeasonId.Spring: return SpringWaterCapacity;
                case SeasonId.Summer: return SummerWaterCapacity;
                case SeasonId.Autumn: return AutumnWaterCapacity;
                case SeasonId.Winter: return WinterWaterCapacity;
                default: return 1f;
            }
        }

        public void GetWeatherRange(SeasonId season, out float baseline, out float min, out float max)
        {
            switch (season)
            {
                case SeasonId.Spring:
                    baseline = SpringWeatherBaseline;
                    min = SpringWeatherMin;
                    max = SpringWeatherMax;
                    break;
                case SeasonId.Summer:
                    baseline = SummerWeatherBaseline;
                    min = SummerWeatherMin;
                    max = SummerWeatherMax;
                    break;
                case SeasonId.Autumn:
                    baseline = AutumnWeatherBaseline;
                    min = AutumnWeatherMin;
                    max = AutumnWeatherMax;
                    break;
                case SeasonId.Winter:
                    baseline = WinterWeatherBaseline;
                    min = WinterWeatherMin;
                    max = WinterWeatherMax;
                    break;
                default:
                    baseline = 1f;
                    min = 0.6f;
                    max = 1.3f;
                    break;
            }
        }
    }
}
