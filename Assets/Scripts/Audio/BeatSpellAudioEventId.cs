namespace BeatSpell.Audio
{
    /// <summary>
    /// 稳定音频事件 ID。玩法层只引用枚举，不直接依赖文件名或 AudioClip。
    /// 前 6 个绑定现有 SFX；其余为后续 BGM / UI / 敌人 / 胜负预留。
    /// </summary>
    public enum BeatSpellAudioEventId
    {
        ChantNormal = 0,
        ChantPerfect = 1,
        CastSuccess = 2,
        SpellNormal = 3,
        SpellPerfect = 4,
        CastFailed = 5,
        BgmBattle = 6,
        UiConfirm = 7,
        UiCancel = 8,
        EnemyWarning = 9,
        EnemyAttack = 10,
        EnemyHit = 11,
        PlayerHit = 12,
        Victory = 13,
        Defeat = 14
    }
}
