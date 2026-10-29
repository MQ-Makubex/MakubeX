#if UNITY_EDITOR
using System;
using System.IO;
using UnityEngine;
namespace MakubeX
{
    // Passive listener tap; never modifies samples, triggers gameplay, or captures the microphone.
    public sealed class AudioOutputEvidence:MonoBehaviour
    {
        readonly object gate=new object();float[] ring;int cursor,count,channels,rate;
        public float Peak{get;private set;}public long SampleCount{get;private set;}
        void Awake(){rate=AudioSettings.outputSampleRate;ring=new float[rate*2*8];}
        void OnAudioFilterRead(float[] data,int channelCount)
        {
            if(ring==null)return;
            lock(gate)
            {
                channels=channelCount;
                for(int i=0;i<data.Length;i++){float v=data[i];ring[cursor]=v;cursor=(cursor+1)%ring.Length;if(count<ring.Length)count++;float a=v<0?-v:v;if(a>Peak)Peak=a;}
                SampleCount+=data.Length;
            }
        }
        public void Save(string path)
        {
            float[] copy;int ch;
            lock(gate){if(count==0)return;copy=new float[count];ch=channels;int start=(cursor-count+ring.Length)%ring.Length;int first=Math.Min(count,ring.Length-start);Array.Copy(ring,start,copy,0,first);if(first<count)Array.Copy(ring,0,copy,first,count-first);}
            using(var writer=new BinaryWriter(File.Create(path)))
            {
                int bytes=copy.Length*2;writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+bytes);writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));writer.Write(16);writer.Write((short)1);writer.Write((short)ch);writer.Write(rate);writer.Write(rate*ch*2);writer.Write((short)(ch*2));writer.Write((short)16);writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));writer.Write(bytes);foreach(var v in copy)writer.Write((short)(Math.Max(-1,Math.Min(1,v))*32767));
            }
        }
    }
}
#endif
