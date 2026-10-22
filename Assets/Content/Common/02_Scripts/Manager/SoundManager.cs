using UnityEngine;

// 배경음 · 효과음 볼륨(0~1)과 재생. Title 씬에 두어 씬이 바뀌어도 배경음이 끊기지 않게 한다.
// 볼륨은 PlayerPrefs 메모리에만 적는다. 슬라이더를 움직이는 동안 계속 불려서, 파일 쓰기는 화면을 떠날 때(MainScene) 한다
public class SoundManager : Singleton<SoundManager>
{
    const string BGM_VOLUME_KEY = "BgmVolume";
    const string SFX_VOLUME_KEY = "SfxVolume";

    public AudioSource bgmSource;   // 배경음 1곡. Loop, 클립은 Streaming
    public AudioSource sfxSource;   // 효과음은 PlayOneShot 으로 겹쳐 낸다

    public float BgmVolume => bgmSource.volume;
    public float SfxVolume => sfxSource.volume;

    protected override void Awake()
    {
        base.Awake();
        
        if (Instance != this)
        {
            return;
        }

        bgmSource.volume = PlayerPrefs.GetFloat(BGM_VOLUME_KEY, 1.0f);
        sfxSource.volume = PlayerPrefs.GetFloat(SFX_VOLUME_KEY, 1.0f);
    }
    
    public void PlayBGM(AudioClip audioClip)
    {
        if (bgmSource.clip == audioClip && bgmSource.isPlaying)
        {
            return;
        }

        bgmSource.clip = audioClip;
        bgmSource.Play();
    }

    public void StopBGM()
    {
        bgmSource.Stop();
    }

    public void SetBgmVolume(float volume)
    {
        bgmSource.volume = volume;
        PlayerPrefs.SetFloat(BGM_VOLUME_KEY, volume);
    }

    public void SetSfxVolume(float volume)
    {
        sfxSource.volume = volume;
        PlayerPrefs.SetFloat(SFX_VOLUME_KEY, volume);
    }

    public void PlaySfx(AudioClip clip)
    {
        sfxSource.PlayOneShot(clip);
    }
}
