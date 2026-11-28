namespace GameProject.Skills
{
    /// <summary>
    /// Basic movement and action inputs that compose a movement-based skill sequence.
    /// </summary>
    public enum MoveStep
    {
        Forward,   // W
        Backward,  // S
        Left,      // A
        Right,     // D
        Jump,      // Space
        Dash       // Left Shift
    }

    /// <summary>
    /// Modular skill effect pieces that can be combined inside a CustomSkillData.
    /// </summary>
    public enum SkillEffectType
    {
        ForwardLunge,   // Burst forward velocity
        AreaShockwave,  // Glowing circular shockwave around Player
        HighLeap,       // High vertical jump burst
        HealBurst,      // Instant HP restore + green aura
        FlameNova,      // Unlockable: Orange fire blast + 3s Burn DoT
        FrostSlow       // Unlockable: Cyan frost ring + 50% Slow for 3s
    }

    public static class SkillLabelUtility
    {
        public static string GetStepShortKey(MoveStep step)
        {
            switch (step)
            {
                case MoveStep.Forward: return "W";
                case MoveStep.Backward: return "S";
                case MoveStep.Left: return "A";
                case MoveStep.Right: return "D";
                case MoveStep.Jump: return "Space";
                case MoveStep.Dash: return "Shift";
                default: return step.ToString();
            }
        }

        public static string GetStepDisplayName(MoveStep step)
        {
            switch (step)
            {
                case MoveStep.Forward: return "Tiến (W)";
                case MoveStep.Backward: return "Lùi (S)";
                case MoveStep.Left: return "Trái (A)";
                case MoveStep.Right: return "Phải (D)";
                case MoveStep.Jump: return "Nhảy (Space)";
                case MoveStep.Dash: return "Lướt (Shift)";
                default: return step.ToString();
            }
        }

        public static string GetEffectDisplayName(SkillEffectType effect)
        {
            switch (effect)
            {
                case SkillEffectType.ForwardLunge: return "Lao tới";
                case SkillEffectType.AreaShockwave: return "Sóng xung kích";
                case SkillEffectType.HighLeap: return "Bật nhảy cao";
                case SkillEffectType.HealBurst: return "Hồi HP";
                case SkillEffectType.FlameNova: return "FlameNova (Hỏa Bộc)";
                case SkillEffectType.FrostSlow: return "FrostSlow (Băng Giá)";
                default: return effect.ToString();
            }
        }
    }
}
