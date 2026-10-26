using System;
using UnityEngine;

namespace BeatSpell.Audio
{
    /// <summary>
    /// 一条可播放音频配置。先用可序列化类挂在 AudioSystem 上，暂不拆 ScriptableObject。
    /// </summary>
    [Serializable]
    public class BeatSpellAudioCue
    {
        [SerializeField] private BeatSpellAudioEventId eventId;
        [SerializeField] private AudioClip clip;
        [SerializeField] private BeatSpellAudioBus bus = BeatSpellAudioBus.Combat;
        [SerializeField] [Range(0f, 1f)] private float volume = 1f;
        [SerializeField] [Range(0.1f, 3f)] private float pitch = 1f;
        [SerializeField] private bool loop;
        [SerializeField] private bool allowOverlap = true;
        [SerializeField] private float cooldownSeconds;

        public BeatSpellAudioEventId EventId => eventId;
        public AudioClip Clip => clip;
        public BeatSpellAudioBus Bus => bus;
        public float Volume => volume;
        public float Pitch => pitch;
        public bool Loop => loop;
        public bool AllowOverlap => allowOverlap;
        public float CooldownSeconds => cooldownSeconds;

        public BeatSpellAudioCue()
        {
        }

        public BeatSpellAudioCue(BeatSpellAudioEventId eventId, BeatSpellAudioBus bus, bool loop = false)
        {
            this.eventId = eventId;
            this.bus = bus;
            this.loop = loop;
            volume = 1f;
            pitch = 1f;
            allowOverlap = true;
        }

        public void BindClip(AudioClip audioClip)
        {
            clip = audioClip;
        }
    }
}
