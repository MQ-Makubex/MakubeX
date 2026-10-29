using UnityEngine;
using UnityEngine.Audio;
namespace MakubeX
{
    public sealed class MakubeXAudioCatalog:ScriptableObject
    {
        public AudioMixer mixer;
        public AudioMixerGroup music,ambienceOutside,ambienceInside,sfx;
        public AudioClip mapLoop,outsideLoop,insideLoop,nameFragment,homeTheme,doorOpen,doorClose,bag,chair,crt,pat;
    }
}
