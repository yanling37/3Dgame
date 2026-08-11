using System;
using DivineWorld.Simulation.Data;
using UnityEngine;

namespace DivineWorld.Simulation.Player
{
    /// <summary>
    /// Phase 1 观察者微调。
    /// 设计约束：只能改「概率/倍率」，不能直接把人口或力量写成固定值。
    /// 1.0 = 无影响；&gt;1 加强对应效果；&lt;1 削弱。
    /// HUD 滑条范围目前限制在约 0.7~1.3。
    /// </summary>
    [Serializable]
    public class ObserverInfluence
    {
        [Tooltip("乘在日生育率上。1=默认，>1 更容易涨人口。")]
        [Range(0.5f, 1.5f)] public float FertilityBlessing = 1f;

        [Tooltip("乘在粮食产量上。1=默认，>1 收成更好。")]
        [Range(0.5f, 1.5f)] public float HarvestBlessing = 1f;

        [Tooltip("乘在疫病压力增长与疫病死亡上。>1 更糟，<1 更缓解。")]
        [Range(0.5f, 1.5f)] public float DiseaseCurse = 1f;

        [Tooltip("乘在粮仓富余时的稳定回升速度上。")]
        [Range(0.5f, 1.5f)] public float StabilityBlessing = 1f;

        /// <summary>
        /// 注视焦点地区。null = 全域同等生效。
        /// 有焦点时：焦点地区吃满倍率，其他地区只吃 30%（见 RegionMultiplier）。
        /// </summary>
        public RegionId? FocusRegion;

        public void ResetSoft()
        {
            FertilityBlessing = 1f;
            HarvestBlessing = 1f;
            DiseaseCurse = 1f;
            StabilityBlessing = 1f;
        }

        /// <summary>
        /// 把某个微调倍率映射到具体地区。
        /// 调试「为什么海没怎么涨」时：先看 FocusRegion 是不是教廷/帝国。
        /// </summary>
        public float RegionMultiplier(RegionId region, float value)
        {
            if (FocusRegion.HasValue && FocusRegion.Value != region)
            {
                // 非焦点地区：在 1.0 与目标倍率之间按 0.3 插值
                // 例：祝福=1.3 → 非焦点得到 1.09
                return Mathf.Lerp(1f, value, 0.3f);
            }

            return value;
        }
    }
}
