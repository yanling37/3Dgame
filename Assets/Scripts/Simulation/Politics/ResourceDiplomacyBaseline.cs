namespace DivineWorld.Simulation.Politics
{
    /// <summary>
    /// P2-C v0.2 resource design record only.
    /// Not read by <see cref="PoliticsSystem.ApplyDiplomaticAction"/>.
    /// Resource → diplomatic relation is forbidden this version. Resource trade is deferred.
    /// </summary>
    public static class ResourceDiplomacyBaseline
    {
        public const bool CoupledToDiplomacy = false;
        public const bool ResourceTradeImplemented = false;

        public const string Water =
            "生存资源；影响粮食生产；影响承载力。本轮不接入外交公式。";

        public const string Food =
            "人口生存；与出生/人口系统相关；与疾病/稳定相关。本轮不接入外交公式。";

        public const string Faith =
            "玩家等级；玩家权限；信仰强度与人口总数、物资表现等相关。本轮不重新实现信仰公式，不接入外交。";

        public const string Wood =
            "自然产出；年度产出存在上限；日常消耗；战备时消耗增加。本轮不实现木材贸易。";

        public const string Mineral =
            "日常消耗；科技发展；战备时消耗增加。本轮不实现矿物贸易。";

        public const string Magic =
            "魔法发展；战备时消耗增加。本轮不实现魔力贸易。";

        public const string KnowledgeEducation =
            "当前已有发展系统。本轮不修改现有公式，不接入外交。";

        public static bool IsRecordOnly()
        {
            return !CoupledToDiplomacy && !ResourceTradeImplemented;
        }
    }
}
