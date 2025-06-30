using System.Collections.Generic;
using UnityEngine;

public enum SoundType
{
    Build,
    Click,
    UI,
    Unlock,
    Failed
}
public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }
    [SerializeField] private AudioClip[] soundList;
    private AudioSource audioSource;

    private static Dictionary<SoundType, float> lastPlayTime = new();
    private static float soundCooldown = 0.1f; // 100ms 쿨타임
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Debug.LogWarning("이미 SoundManager 인스턴스가 존재합니다. 이 인스턴스를 파괴합니다.");
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        TryGetComponent(out audioSource);
    }
    

    public static void PlaySound(SoundType sound, float volume = 1)
    {
        float now = Time.time;
        if (lastPlayTime.TryGetValue(sound, out float lastTime))
        {
            if (now - lastTime < soundCooldown)
                return; // 쿨타임 내에는 재생하지 않음
        }
        lastPlayTime[sound] = now;
        Instance.audioSource.PlayOneShot(Instance.soundList[(int)sound], volume);
    }

    /*public static void PlaySound(SoundType sound, float volume = 1)
    {
        instance.audioSource.PlayOneShot(instance.soundList[(int)sound], volume);
    }*/
}
