using System;
using BeatSpell.Rhythm;
using UnityEngine;

namespace BeatSpell.Spell
{
    [Serializable]
    public struct SpellStepDefinition
    {
        public BeatInputType inputType;
        public SpellRecipeVertex recipeVertex;
    }

    /// <summary>
    /// 魔法配方：输入类型序列 + 相邻步骤小数拍间隔 + 容差。recipeVertex 供图标/UI，匹配不强制顶点。
    /// Inspector 可填 1 / 0.5 / 0.25；默认容差 0.125 拍。
    /// </summary>
    [CreateAssetMenu(fileName = "NewSpell", menuName = "BeatSpell/Spell Definition")]
    public class SpellDefinition : ScriptableObject
    {
        [SerializeField] private string spellId = "Fireball";
        [SerializeField] private string displayName = "火焰魔法";
        [SerializeField] private int level = 1;
        [SerializeField] private float baseDamage = 10f;
        [SerializeField] private SpellStepDefinition[] steps =
        {
            new SpellStepDefinition { inputType = BeatInputType.Chant, recipeVertex = SpellRecipeVertex.Top },
            new SpellStepDefinition { inputType = BeatInputType.Cast, recipeVertex = SpellRecipeVertex.Right }
        };
        [SerializeField] private float[] intervalsBetweenSteps = { 1f };
        [SerializeField] private float matchToleranceBeats = 0.125f;

        public string SpellId => spellId;
        public string DisplayName => displayName;
        public int Level => level;
        public float BaseDamage => baseDamage;
        public SpellStepDefinition[] Steps => steps;
        public float[] IntervalsBetweenSteps => intervalsBetweenSteps;
        public float MatchToleranceBeats => matchToleranceBeats;
    }
}
