using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace GameProject.Skills
{
    /// <summary>
    /// Runtime-editable data definition for a movement-combo skill.
    /// </summary>
    [Serializable]
    public class CustomSkillData
    {
        public string skillName = "Chiêu thức mới";
        public List<MoveStep> comboSteps = new List<MoveStep>();
        public List<SkillEffectType> effects = new List<SkillEffectType>();
        [Min(0f)] public float staminaCost = 25f;
        [Min(0.1f)] public float cooldown = 2.5f;

        public CustomSkillData()
        {
        }

        public CustomSkillData(
            string name,
            IEnumerable<MoveStep> steps,
            IEnumerable<SkillEffectType> skillEffects,
            float cost,
            float cd)
        {
            skillName = name;
            comboSteps = steps != null ? new List<MoveStep>(steps) : new List<MoveStep>();
            effects = skillEffects != null ? new List<SkillEffectType>(skillEffects) : new List<SkillEffectType>();
            staminaCost = Mathf.Max(0f, cost);
            cooldown = Mathf.Max(0.1f, cd);
        }

        public bool IsValid()
        {
            return !string.IsNullOrWhiteSpace(skillName)
                && comboSteps != null && comboSteps.Count >= 3 && comboSteps.Count <= 4
                && effects != null && effects.Count >= 1;
        }

        public bool HasSameCombo(List<MoveStep> otherSteps)
        {
            if (comboSteps == null || otherSteps == null || comboSteps.Count != otherSteps.Count)
            {
                return false;
            }

            for (int i = 0; i < comboSteps.Count; i++)
            {
                if (comboSteps[i] != otherSteps[i])
                {
                    return false;
                }
            }

            return true;
        }

        public string FormatComboString()
        {
            if (comboSteps == null || comboSteps.Count == 0)
            {
                return string.Empty;
            }

            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < comboSteps.Count; i++)
            {
                if (i > 0) sb.Append(" → ");
                sb.Append(SkillLabelUtility.GetStepShortKey(comboSteps[i]));
            }

            return sb.ToString();
        }

        public string FormatEffectsString()
        {
            if (effects == null || effects.Count == 0)
            {
                return string.Empty;
            }

            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < effects.Count; i++)
            {
                if (i > 0) sb.Append(" + ");
                sb.Append(SkillLabelUtility.GetEffectDisplayName(effects[i]));
            }

            return sb.ToString();
        }
    }
}
