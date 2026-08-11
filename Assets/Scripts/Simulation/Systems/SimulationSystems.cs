using System.Collections.Generic;
using DivineWorld.Simulation.Data;
using DivineWorld.Simulation.Player;
using UnityEngine;

namespace DivineWorld.Simulation.Systems
{
    /// <summary>
    /// Phase 1 资源日结算。
    /// 调试入口：在 SimulationWorld.AdvanceDay 里每个 Region 会先调用本类 TickDay。
    /// 公式对齐 GDD：产量 ≈ 基础 × 技术 × 劳动力 × 环境 ×（玩家收成微调）。
    /// </summary>
    public static class ResourceSystem
    {
        /// <summary>
        /// 对单个地区结算「一天」的资源生产、消耗，以及缺粮引发的稳定/疫病变化。
        /// </summary>
        /// <param name="region">当前地区运行时状态（会被原地修改）</param>
        /// <param name="race">该地区主导种族参数</param>
        /// <param name="influence">观察者概率微调（只改倍率，不直接写死数值）</param>
        /// <param name="rng">世界级随机源（与 SimulationWorld.seed 绑定，便于复现）</param>
        public static void TickDay(RegionState region, RaceDefinition race, ObserverInfluence influence, System.Random rng)
        {
            // ---- 公共乘数（调试时优先看这四个）----
            // labor：劳动力。人口 / 10000，夹在 [0.2, 8]
            //   例：42000 人 → 4.2；少于 2000 人也会至少按 0.2 算，避免产量归零。
            float labor = Mathf.Clamp(region.Population / 10000f, 0.2f, 8f);

            // tech：技术水平。0.6 + Education*0.8 → Education=0 时 0.6，Education=1 时 1.4
            float tech = 0.6f + region.Education * 0.8f;

            // env：天气/环境系数。由每日末尾缓慢漂移，范围大约 [0.6, 1.3]
            float env = region.WeatherFactor;

            // harvest：玩家「收成祝福」。若注视其他地区，只会吃到 30% 强度（见 ObserverInfluence）。
            float harvest = influence.RegionMultiplier(region.Id, influence.HarvestBlessing);

            // ---- 日产量 ----
            // Base(存量) 至少按 50 起算，避免存量被吃光后永远产不出东西（便于早期调试）。
            // Food: Base * 0.02 * tech * labor * env * harvest * GrowthFactor
            float foodYield = Base(region, ResourceId.Food) * 0.02f * tech * labor * env * harvest * race.GrowthFactor;

            // Water: 主要吃环境，不跟劳动力强绑定（简化：水源更偏自然）
            float waterYield = Base(region, ResourceId.Water) * 0.015f * env;

            // Timber / Ore: 人鱼地区几乎不产陆地原料，给极低常数保底，方便对比种族差异
            float timberYield = race.PrefersSea
                ? 0.2f
                : Base(region, ResourceId.Timber) * 0.01f * labor * tech;
            float oreYield = race.PrefersSea
                ? 0.3f
                : Base(region, ResourceId.Ore) * 0.008f * labor * tech;

            // 社会资源：按人口比例慢慢堆（调试长期趋势时看这些）
            // Faith: Pop * 0.0004 * FaithLevel * FaithTendency
            float faithYield = region.Population * 0.0004f * region.FaithLevel * race.FaithTendency;
            // Knowledge: Pop * 0.00025 * Education * KnowledgeTendency
            float knowledgeYield = region.Population * 0.00025f * region.Education * race.KnowledgeTendency;
            // Magic: Pop * 0.0001 * MagicAffinity * (海族 1.4 / 陆族 0.7)
            float magicYield = region.Population * 0.0001f * race.MagicAffinity * (race.PrefersSea ? 1.4f : 0.7f);

            region.Add(ResourceId.Food, foodYield);
            region.Add(ResourceId.Water, waterYield);
            region.Add(ResourceId.Timber, timberYield);
            region.Add(ResourceId.Ore, oreYield);
            region.Add(ResourceId.Faith, faithYield);
            region.Add(ResourceId.Knowledge, knowledgeYield);
            region.Add(ResourceId.Magic, magicYield);

            // ---- 日消耗（先产再耗；若要调试「净变化」，可用断点看 Add 前后差值）----
            // 每人每天约消耗 0.02 粮、0.015 水。
            // 例：50000 人 → 粮耗 1000 / 日。若产量长期 < 消耗，库存会下降并触发短缺。
            float foodNeed = region.Population * 0.02f;
            float waterNeed = region.Population * 0.015f;
            region.Add(ResourceId.Food, -foodNeed);
            region.Add(ResourceId.Water, -waterNeed);

            // ---- 短缺 / 富余对稳定与疫病的反馈 ----
            // 短缺阈值：粮 < 人口 * 0.2  （约 10 天口粮以下）
            // 富余阈值：粮 > 人口 * 0.8
            float foodNow = region.Get(ResourceId.Food);
            if (foodNow < region.Population * 0.2f)
            {
                // 疫病压力每日 +0.01 * 疫病微调（夹到 0..1）
                // 稳定每日 -0.004（下限 0.05）
                region.DiseasePressure = Mathf.Clamp01(
                    region.DiseasePressure + 0.01f * influence.RegionMultiplier(region.Id, influence.DiseaseCurse));
                region.Stability = Mathf.Max(0.05f, region.Stability - 0.004f);

                // 8% 概率刷新近况文案（不影响数值，只方便观察仪阅读）
                if (rng.NextDouble() < 0.08)
                {
                    region.LastEvent = "粮食短缺引发不安";
                }
            }
            else if (foodNow > region.Population * 0.8f)
            {
                // 粮仓充裕时稳定缓慢回升，受「稳定祝福」影响
                region.Stability = Mathf.Min(
                    1.5f,
                    region.Stability + 0.0015f * influence.RegionMultiplier(region.Id, influence.StabilityBlessing));
            }

            // ---- 天气缓慢随机游走：每天 ±最多约 0.01，夹在 [0.6, 1.3] ----
            // 想复现某次天气走势：固定 SimulationWorld.seed，从同一日起跑。
            region.WeatherFactor = Mathf.Clamp(
                region.WeatherFactor + ((float)rng.NextDouble() - 0.5f) * 0.02f,
                0.6f,
                1.3f);
        }

        /// <summary>
        /// 产量基数：取当前存量，但最低 50，防止「库存归零 → 产量永久为 0」的死锁。
        /// 调试时若发现粮产异常高，检查是否因为 Base 保底 + 高 labor 叠加。
        /// </summary>
        static float Base(RegionState region, ResourceId id)
        {
            return Mathf.Max(50f, region.Get(id));
        }
    }

    /// <summary>
    /// Phase 1 人口日结算（在资源结算之后执行，因此出生/死亡能吃到当天新的粮库存）。
    /// </summary>
    public static class PopulationSystem
    {
        /// <summary>
        /// 对单个地区结算「一天」的出生、自然死亡、疫病死亡，并缓慢拉动教育/信仰。
        /// </summary>
        public static void TickDay(RegionState region, RaceDefinition race, ObserverInfluence influence, System.Random rng)
        {
            // ---- 出生 ----
            // fertility 日生育率基数 0.00035，再乘种族生育与玩家祝福。
            // 例：人类 FertilityFactor=1、祝福=1 → fertility=0.00035
            // foodRatio：粮 / (人口*0.5)，夹到 0..1。粮越足，出生乘数 (0.5+foodRatio) 越接近 1.5
            // birth = Pop * fertility * (0.5 + foodRatio)
            //   粗算：5 万人、foodRatio=1 → 出生 ≈ 50000 * 0.00035 * 1.5 ≈ 26.25 人/日
            float fertility = 0.00035f
                * race.FertilityFactor
                * influence.RegionMultiplier(region.Id, influence.FertilityBlessing);
            float foodRatio = Mathf.Clamp01(
                region.Get(ResourceId.Food) / Mathf.Max(1f, region.Population * 0.5f));
            float birth = region.Population * fertility * (0.5f + foodRatio);

            // ---- 死亡 ----
            // 自然死亡：Pop * (0.00022 / LifespanFactor)
            //   寿命因子越大（人鱼 1.3），日死亡率越低。
            float naturalDeath = region.Population * (0.00022f / race.LifespanFactor);

            // 疫病死亡：Pop * DiseasePressure * 0.0015 * 疫病微调
            //   DiseasePressure=0.1、5 万人、微调=1 → 约 7.5 人/日
            float diseaseDeath = region.Population
                * region.DiseasePressure
                * 0.0015f
                * influence.RegionMultiplier(region.Id, influence.DiseaseCurse);

            // 人口下限 100，避免地区被算「灭族」导致后续除零/归零连锁难调
            region.Population = Mathf.Max(100f, region.Population + birth - naturalDeath - diseaseDeath);

            // 疫病压力自然衰减：每日 *0.995（约 140 日衰减到一半左右量级，便于观察）
            region.DiseasePressure = Mathf.Clamp01(region.DiseasePressure * 0.995f);

            // ---- 教育 / 信仰：向「知识库存」「信仰库存」暗示的目标值缓慢逼近 ----
            // 目标教育 ≈ clamp01(Knowledge / 20000)
            // 目标信仰 ≈ clamp01(FaithResource / 25000)
            // Lerp 系数 0.002 → 变化很慢；调试短期波动时几乎可忽略这两项。
            float knowledge = region.Get(ResourceId.Knowledge);
            float faith = region.Get(ResourceId.Faith);
            region.Education = Mathf.Clamp01(
                Mathf.Lerp(region.Education, Mathf.Clamp01(knowledge / 20000f), 0.002f));
            region.FaithLevel = Mathf.Clamp01(
                Mathf.Lerp(region.FaithLevel, Mathf.Clamp01(faith / 25000f), 0.002f));

            // ---- 低稳定随机事件（约 1% 且 Stability < 0.45）----
            // 只扣稳定并改 LastEvent，方便观察仪看到「地方骚乱传闻」。
            if (rng.NextDouble() < 0.01 && region.Stability < 0.45f)
            {
                region.LastEvent = "地方骚乱传闻";
                region.Stability = Mathf.Max(0.1f, region.Stability - 0.02f);
            }

            // 逐步调试人口时，可临时取消下一行注释：
            // Debug.Log($"[Pop] {region.DisplayName} birth={birth:0.0} deathN={naturalDeath:0.0} deathD={diseaseDeath:0.0} pop={region.Population:0}");
        }
    }

    /// <summary>
    /// 按 RaceId 查找种族定义；找不到时回退到列表第一项（Phase 1 默认是人类）。
    /// </summary>
    public static class RegionLookup
    {
        public static RaceDefinition FindRace(IReadOnlyList<RaceDefinition> races, RaceId id)
        {
            for (int i = 0; i < races.Count; i++)
            {
                if (races[i].Id == id)
                {
                    return races[i];
                }
            }

            return races[0];
        }
    }
}
