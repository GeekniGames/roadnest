using DG.Tweening;
using UnityEngine;

/// <summary>
/// 엔진 사운드 관리. 루프 전환은 볼륨 페이드로 처리. 클립이 비어 있으면 무시.
/// Engine sound control. Loops are swapped by volume fades. Empty clips are ignored.
/// </summary>
public class AudioManager : MonoBehaviour
{
    [Header("클립 / Clips")]
    [SerializeField] AudioClip ignitionClip;
    [SerializeField] AudioClip idleLoop;
    [SerializeField] AudioClip driveLoop;
    [SerializeField] AudioClip engineOffClip;

    [Header("볼륨 / Volume")]
    [SerializeField, UnityEngine.Range(0f, 1f)] float sfxVolume = 1f;
    [SerializeField, UnityEngine.Range(0f, 1f)] float idleVolume = 0.6f;
    [SerializeField, UnityEngine.Range(0f, 1f)] float driveVolume = 0.8f;
    [SerializeField] float fadeTime = 0.4f;

    AudioSource _sfx, _idle, _drive;
    Tween _idleFade, _driveFade;
    TweenCallback _idleDone, _driveDone;

    void Awake()
    {
        _sfx = CreateSource(null, false);
        _idle = CreateSource(idleLoop, true);
        _drive = CreateSource(driveLoop, true);
        _idleDone = () => StopIfSilent(_idle);
        _driveDone = () => StopIfSilent(_drive);
    }

    public void PlayIgnition() => PlayOnce(ignitionClip);

    public void StartIdle()
    {
        FadeLoop(_idle, idleVolume, ref _idleFade, _idleDone);
        FadeLoop(_drive, 0f, ref _driveFade, _driveDone);
    }

    public void StartDrive()
    {
        FadeLoop(_drive, driveVolume, ref _driveFade, _driveDone);
        FadeLoop(_idle, 0f, ref _idleFade, _idleDone);
    }

    public void EngineOff()
    {
        FadeLoop(_idle, 0f, ref _idleFade, _idleDone);
        FadeLoop(_drive, 0f, ref _driveFade, _driveDone);
        PlayOnce(engineOffClip);
    }

    void PlayOnce(AudioClip clip)
    {
        if (clip != null) _sfx.PlayOneShot(clip, sfxVolume);
    }

    void FadeLoop(AudioSource src, float target, ref Tween tween, TweenCallback done)
    {
        if (tween != null && tween.IsActive()) tween.Kill();
        if (src.clip == null) return;

        if (target > 0f && !src.isPlaying)
        {
            src.volume = 0f;
            src.Play();
        }
        if (!src.isPlaying) return;

        tween = src.DOFade(target, fadeTime).SetLink(gameObject).OnComplete(done);
    }

    static void StopIfSilent(AudioSource s)
    {
        if (s.volume <= 0.001f) s.Stop();
    }

    AudioSource CreateSource(AudioClip clip, bool loop)
    {
        var s = gameObject.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.loop = loop;
        s.clip = clip;
        s.volume = loop ? 0f : sfxVolume;
        return s;
    }
}