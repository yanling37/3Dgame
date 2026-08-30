using System.Collections.Generic;
using UnityEngine;

namespace Game.Jianglin
{
    public enum JianglinPlayerMode
    {
        General = 0,
        Casting = 1,
        Prayer = 2,
        Design = 3
    }

    public enum JianglinElement
    {
        Wind = 0,
        Fire = 1,
        Earth = 2,
        Water = 3
    }

    public enum JianglinSpellId
    {
        None = 0,
        Fireball = 1,
        Spark = 2,
        WaterBolt = 3,
        WindBlade = 4,
        EarthChunk = 5,
        MudMire = 6,
        IceBolt = 7,
        FlameStream = 8,
        WaterStream = 9,
        WindDash = 10,
        StoneShield = 11,
        Meteor = 12
    }

    public enum JianglinCastKind
    {
        Projectile = 0,
        Channel = 1,
        Skyfall = 2,
        Dash = 3,
        Shield = 4
    }

    public struct JianglinElementCharge
    {
        public JianglinElement Element;
        public int Level;

        public JianglinElementCharge(JianglinElement element, int level)
        {
            Element = element;
            Level = level;
        }
    }

    public struct JianglinProjectileSpec
    {
        public string Name;
        public PrimitiveType Shape;
        public Vector3 Scale;
        public Color Color;
        public Color BurstColor;
        public float Speed;
        public float Life;
        public float Gravity;
        public float BurstScale;
        public float BurstLife;
        public bool LeaveMire;
        public float MireRadius;
        public float MireDuration;
        public float MireSlow;
        public float Damage;
        public float SplashRadius;
    }

    public static class JianglinSpellbook
    {
        public const int MinLevel = 1;
        public const int MaxLevel = 5;

        public static JianglinSpellId Resolve(IReadOnlyList<JianglinElementCharge> recipe)
        {
            if (recipe == null || recipe.Count == 0)
            {
                return JianglinSpellId.None;
            }

            // Two-element recipes first so they are not eaten by a single-element prefix.
            if (Matches(recipe, JianglinElement.Wind, 1, JianglinElement.Fire, 1))
            {
                return JianglinSpellId.Fireball;
            }

            if (Matches(recipe, JianglinElement.Fire, 1, JianglinElement.Earth, 1))
            {
                return JianglinSpellId.Meteor;
            }

            if (Matches(recipe, JianglinElement.Water, 1, JianglinElement.Earth, 1))
            {
                return JianglinSpellId.MudMire;
            }

            if (Matches(recipe, JianglinElement.Wind, 1, JianglinElement.Water, 1))
            {
                return JianglinSpellId.IceBolt;
            }

            if (Matches(recipe, JianglinElement.Fire, 2))
            {
                return JianglinSpellId.FlameStream;
            }

            if (Matches(recipe, JianglinElement.Water, 2))
            {
                return JianglinSpellId.WaterStream;
            }

            if (Matches(recipe, JianglinElement.Wind, 2))
            {
                return JianglinSpellId.WindDash;
            }

            if (Matches(recipe, JianglinElement.Earth, 2))
            {
                return JianglinSpellId.StoneShield;
            }

            if (Matches(recipe, JianglinElement.Fire, 1))
            {
                return JianglinSpellId.Spark;
            }

            if (Matches(recipe, JianglinElement.Water, 1))
            {
                return JianglinSpellId.WaterBolt;
            }

            if (Matches(recipe, JianglinElement.Wind, 1))
            {
                return JianglinSpellId.WindBlade;
            }

            if (Matches(recipe, JianglinElement.Earth, 1))
            {
                return JianglinSpellId.EarthChunk;
            }

            return JianglinSpellId.None;
        }

        public static string SpellName(JianglinSpellId id)
        {
            switch (id)
            {
                case JianglinSpellId.Fireball:
                    return "火球术（单发）";
                case JianglinSpellId.Spark:
                    return "小火花";
                case JianglinSpellId.WaterBolt:
                    return "水弹";
                case JianglinSpellId.WindBlade:
                    return "风刃";
                case JianglinSpellId.EarthChunk:
                    return "土块";
                case JianglinSpellId.MudMire:
                    return "泥沼";
                case JianglinSpellId.IceBolt:
                    return "冰弹";
                case JianglinSpellId.FlameStream:
                    return "炎流（持续）";
                case JianglinSpellId.WaterStream:
                    return "水柱（持续）";
                case JianglinSpellId.WindDash:
                    return "疾风突进";
                case JianglinSpellId.StoneShield:
                    return "岩盾";
                case JianglinSpellId.Meteor:
                    return "陨石（天降）";
                default:
                    return "未知配方";
            }
        }

        public static string ElementName(JianglinElement element)
        {
            switch (element)
            {
                case JianglinElement.Wind:
                    return "风";
                case JianglinElement.Fire:
                    return "火";
                case JianglinElement.Earth:
                    return "土";
                case JianglinElement.Water:
                    return "水";
                default:
                    return "?";
            }
        }

        public static string ModeName(JianglinPlayerMode mode)
        {
            switch (mode)
            {
                case JianglinPlayerMode.Casting:
                    return "施法模式";
                case JianglinPlayerMode.Prayer:
                    return "祈祷模式";
                case JianglinPlayerMode.General:
                    return "一般模式";
                case JianglinPlayerMode.Design:
                    return "设计模式";
                default:
                    return mode.ToString();
            }
        }

        public static JianglinCastKind KindOf(JianglinSpellId id)
        {
            switch (id)
            {
                case JianglinSpellId.FlameStream:
                case JianglinSpellId.WaterStream:
                    return JianglinCastKind.Channel;
                case JianglinSpellId.Meteor:
                    return JianglinCastKind.Skyfall;
                case JianglinSpellId.WindDash:
                    return JianglinCastKind.Dash;
                case JianglinSpellId.StoneShield:
                    return JianglinCastKind.Shield;
                default:
                    return JianglinCastKind.Projectile;
            }
        }

        public static string CastHint(JianglinSpellId id)
        {
            string name = SpellName(id);
            switch (KindOf(id))
            {
                case JianglinCastKind.Channel:
                    return name + "  ·  按住左键持续";
                case JianglinCastKind.Skyfall:
                    return name + "  ·  左键砸向准星/锁定";
                case JianglinCastKind.Dash:
                    return name + "  ·  左键向前突进";
                case JianglinCastKind.Shield:
                    return name + "  ·  左键开盾";
                default:
                    return name + "  ·  左键放出";
            }
        }

        public static string RecipeSheet()
        {
            return "风1→火1 火球  ·  火1 火花  ·  水1 水弹  ·  风1 风刃\n"
                 + "土1 土块  ·  水1→土1 泥沼  ·  风1→水1 冰弹\n"
                 + "火2 炎流  ·  水2 水柱  ·  风2 突进  ·  土2 岩盾  ·  火1→土1 陨石";
        }

        public static JianglinSpellId ResolveUnordered(IReadOnlyList<JianglinElementCharge> recipe)
        {
            if (recipe == null || recipe.Count == 0)
            {
                return JianglinSpellId.None;
            }

            var collapsed = Collapse(recipe);
            if (collapsed.Count == 1)
            {
                return Resolve(collapsed);
            }

            if (collapsed.Count == 2)
            {
                JianglinSpellId direct = Resolve(collapsed);
                if (direct != JianglinSpellId.None)
                {
                    return direct;
                }

                return Resolve(new[] { collapsed[1], collapsed[0] });
            }

            return JianglinSpellId.None;
        }

        public static string ChargeText(IReadOnlyList<JianglinElementCharge> charges)
        {
            if (charges == null || charges.Count == 0)
            {
                return "";
            }

            var builder = new System.Text.StringBuilder();
            for (int i = 0; i < charges.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append("+");
                }

                builder.Append(ElementName(charges[i].Element));
                builder.Append(charges[i].Level);
            }

            return builder.ToString();
        }

        static List<JianglinElementCharge> Collapse(IReadOnlyList<JianglinElementCharge> recipe)
        {
            var list = new List<JianglinElementCharge>(4);
            for (int i = 0; i < recipe.Count; i++)
            {
                StackCharge(list, recipe[i]);
            }

            return list;
        }

        public static void StackCharge(List<JianglinElementCharge> list, JianglinElementCharge add)
        {
            for (int i = 0; i < list.Count; i++)
            {
                var current = list[i];
                if (current.Element != add.Element)
                {
                    continue;
                }

                current.Level = Mathf.Clamp(current.Level + add.Level, MinLevel, MaxLevel);
                list[i] = current;
                return;
            }

            list.Add(new JianglinElementCharge(
                add.Element,
                Mathf.Clamp(add.Level, MinLevel, MaxLevel)));
        }

        public static Color ElementColor(JianglinElement element)
        {
            switch (element)
            {
                case JianglinElement.Wind:
                    return new Color(0.28f, 0.78f, 0.38f);
                case JianglinElement.Fire:
                    return new Color(0.92f, 0.28f, 0.18f);
                case JianglinElement.Earth:
                    return new Color(0.82f, 0.68f, 0.22f);
                case JianglinElement.Water:
                    return new Color(0.25f, 0.55f, 0.92f);
                default:
                    return Color.white;
            }
        }

        public static string ShortSpellName(JianglinSpellId id)
        {
            switch (id)
            {
                case JianglinSpellId.Fireball:
                    return "火球";
                case JianglinSpellId.Spark:
                    return "火花";
                case JianglinSpellId.WaterBolt:
                    return "水弹";
                case JianglinSpellId.WindBlade:
                    return "风刃";
                case JianglinSpellId.EarthChunk:
                    return "土块";
                case JianglinSpellId.MudMire:
                    return "泥沼";
                case JianglinSpellId.IceBolt:
                    return "冰弹";
                case JianglinSpellId.FlameStream:
                    return "炎流";
                case JianglinSpellId.WaterStream:
                    return "水柱";
                case JianglinSpellId.WindDash:
                    return "突进";
                case JianglinSpellId.StoneShield:
                    return "岩盾";
                case JianglinSpellId.Meteor:
                    return "陨石";
                default:
                    return "未知";
            }
        }

        static bool Matches(IReadOnlyList<JianglinElementCharge> recipe, JianglinElement a, int aLevel)
        {
            return recipe.Count == 1
                   && recipe[0].Element == a
                   && recipe[0].Level == aLevel;
        }

        static bool Matches(
            IReadOnlyList<JianglinElementCharge> recipe,
            JianglinElement a,
            int aLevel,
            JianglinElement b,
            int bLevel)
        {
            return recipe.Count == 2
                   && recipe[0].Element == a
                   && recipe[0].Level == aLevel
                   && recipe[1].Element == b
                   && recipe[1].Level == bLevel;
        }
    }
}
