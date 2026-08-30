using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Game.Jianglin
{
    /// <summary>
    /// Local 降临 mana + element conversion. Not the civilization ResourceId.Magic pool.
    /// </summary>
    public sealed class JianglinCastingSession
    {
        public float ManaMax = 100f;
        public float RegenPerSecond = 8f;
        public float Mana = 100f;
        public int DraftLevel = 1;
        public bool Dragging;
        public bool Locked;
        public JianglinSpellId Resolved;
        public readonly List<JianglinElementCharge> Recipe = new List<JianglinElementCharge>(8);

        public bool IsReadyToFire => Locked && Resolved != JianglinSpellId.None;

        public void Tick(float deltaTime)
        {
            if (Mana >= ManaMax)
            {
                Mana = ManaMax;
                return;
            }

            Mana = Mathf.Min(ManaMax, Mana + RegenPerSecond * deltaTime);
        }

        public bool TrySpend(int amount)
        {
            if (amount <= 0)
            {
                return true;
            }

            if (Mana < amount)
            {
                return false;
            }

            Mana -= amount;
            return true;
        }

        public bool TryConvert(JianglinElement element, int level)
        {
            if (Locked)
            {
                return false;
            }

            int cost = Mathf.Clamp(level, JianglinSpellbook.MinLevel, JianglinSpellbook.MaxLevel);
            if (Mana < cost)
            {
                return false;
            }

            Mana -= cost;
            Recipe.Add(new JianglinElementCharge(element, cost));
            DraftLevel = cost;
            return true;
        }

        public bool TrySetLastLevel(int level)
        {
            if (Locked || Recipe.Count == 0)
            {
                DraftLevel = Mathf.Clamp(level, JianglinSpellbook.MinLevel, JianglinSpellbook.MaxLevel);
                return true;
            }

            int next = Mathf.Clamp(level, JianglinSpellbook.MinLevel, JianglinSpellbook.MaxLevel);
            int lastIndex = Recipe.Count - 1;
            var last = Recipe[lastIndex];
            int delta = next - last.Level;
            if (delta > 0 && Mana < delta)
            {
                return false;
            }

            Mana -= delta;
            last.Level = next;
            Recipe[lastIndex] = last;
            DraftLevel = next;
            return true;
        }

        public void AdjustDraft(int delta)
        {
            int next = DraftLevel + delta;
            if (Recipe.Count > 0 && !Dragging)
            {
                TrySetLastLevel(next);
                return;
            }

            DraftLevel = Mathf.Clamp(next, JianglinSpellbook.MinLevel, JianglinSpellbook.MaxLevel);
        }

        public void LockRecipe()
        {
            if (Recipe.Count == 0)
            {
                return;
            }

            Locked = true;
            Resolved = JianglinSpellbook.Resolve(Recipe);
        }

        public void CancelAndRefund()
        {
            for (int i = 0; i < Recipe.Count; i++)
            {
                Mana += Recipe[i].Level;
            }

            Mana = Mathf.Min(Mana, ManaMax);
            Recipe.Clear();
            Locked = false;
            Resolved = JianglinSpellId.None;
            Dragging = false;
            DraftLevel = 1;
        }

        public void ConsumeReadySpell()
        {
            Recipe.Clear();
            Locked = false;
            Resolved = JianglinSpellId.None;
            Dragging = false;
            DraftLevel = 1;
        }

        public string RecipeText()
        {
            if (Recipe.Count == 0)
            {
                return "空";
            }

            var builder = new StringBuilder();
            for (int i = 0; i < Recipe.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(" → ");
                }

                builder.Append(JianglinSpellbook.ElementName(Recipe[i].Element));
                builder.Append(Recipe[i].Level);
            }

            return builder.ToString();
        }
    }
}
