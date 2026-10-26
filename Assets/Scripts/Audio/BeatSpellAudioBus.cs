namespace BeatSpell.Audio
{
    /// <summary>
    /// 音频分组。Combat 实际音量再乘 Sfx 与 Master，便于以后接 Mixer。
    /// </summary>
    public enum BeatSpellAudioBus
    {
        Master = 0,
        Bgm = 1,
        Sfx = 2,
        Ui = 3,
        Combat = 4,
        Voice = 5
    }
}
