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
    /// 魔法配方：输入类型序列 + 相邻步骤拍间隔。recipeVertex 供图标/第 4 轮 UI，本轮匹配不强制顶点。
    /// 火焰默认：J 咏唱 → 1 拍 → K 施法。
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
        [SerializeField] private int[] intervalsBetweenSteps = { 1 };

        public string SpellId => spellId;
        public string DisplayName => displayName;
        public int Level => level;
        public float BaseDamage => baseDamage;
        public SpellStepDefinition[] Steps => steps;
        public int[] IntervalsBetweenSteps => intervalsBetweenSteps;
    }
}
