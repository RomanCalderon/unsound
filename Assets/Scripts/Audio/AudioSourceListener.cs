using System.Collections.Generic;
using UnityEngine;

namespace SilverDogGames.Audio
{
    using Sirenix.OdinInspector;
    using System.Linq;

    [System.Serializable]
    public struct AudioSourceData
    {
        public string Name;
        [Range(0f, 1f)] public float Loudness;
        public Vector3 Position;

        public AudioSourceData(string name, float loudness, Vector3 position)
        {
            Name = name;
            Loudness = loudness;
            Position = position;
        }
    }

    public class AudioSourceListener : SerializedMonoBehaviour
    {
        [SerializeField] private float sourceDetectionRadius = 50f;
        [SerializeField] private LayerMask audioSourceMask;
        [SerializeField] private List<AudioSource> allAudioSources = new List<AudioSource>();
        [SerializeField] private float sampleUpdateStep = 0.1f;
        [SerializeField] private int sampleLength = 1024;
        [SerializeField] private float offset = 0f;
        [SerializeField] private float sensitivity = 10f;
        [SerializeField, ReadOnly] private List<AudioSourceData> sourceData = new List<AudioSourceData>();
        [SerializeField, ReadOnly] private float updateTime = 0f;
        private float[] audioSpectrum = null;

        // Start is called before the first frame update
        private void Start()
        {
            allAudioSources = new List<AudioSource>();

            // Initialize buffer
            audioSpectrum = new float[sampleLength];
        }

        public AudioSourceData[] GetSourceData()
        {
            return sourceData?.ToArray();
        }

        private void FixedUpdate()
        {
            GetAudioSources();
        }
        private void Update()
        {
            updateTime += Time.deltaTime;
            if (updateTime >= sampleUpdateStep)
            {
                updateTime = 0f;
                ProcessSpectrumData();
            }
        }

        private void GetAudioSources()
        {
            allAudioSources.Clear();
            Collider[] colliders = Physics.OverlapSphere(transform.position, sourceDetectionRadius, audioSourceMask);
            foreach (var collider in colliders)
            {
                if (collider.TryGetComponent(out AudioSource audioSource))
                {
                    allAudioSources.Add(audioSource);
                }
            }
        }
        private void ProcessSpectrumData()
        {
            sourceData.Clear();
            if (allAudioSources.Count > 0 && sampleLength > 0)
            {
                foreach (AudioSource source in allAudioSources)
                {
                    if (source.clip?.loadState == AudioDataLoadState.Loaded)
                    {
                        source.clip.GetData(audioSpectrum, source.timeSamples);
                        float loudness = 0f;
                        float distance = Mathf.Max(1f, Vector3.Distance(transform.position, source.transform.position));
                        // Calculate clip loudness
                        foreach (var sample in audioSpectrum)
                        {
                            loudness += Mathf.Abs(sample) + offset;
                        }
                        loudness /= sampleLength;
                        loudness *= (-Mathf.Pow(distance - 1, 2) / (sensitivity * sensitivity)) + 1;
                        loudness *= source.volume;
                        loudness = Mathf.Clamp01(loudness * sensitivity);
                        sourceData.Add(new AudioSourceData(source.name, loudness, source.transform.position));

#if UNITY_EDITOR
                        Vector3 dir = (source.transform.position - transform.position);
                        Debug.DrawRay(transform.position, dir * loudness, Color.white, sampleUpdateStep, true);
#endif
                    }
                }
                sourceData.OrderByDescending(sourceData => sourceData.Loudness);
            }
        }
    }
}
