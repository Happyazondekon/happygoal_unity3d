using UnityEngine;

// Reuses HappyGoal's own sound effects (copied from the Flutter project's
// assets/audio) so the 3D prototype doesn't need its own audio pass -
// wired to the same events GameManager already fires.
public class GameAudio : MonoBehaviour
{
    public AudioClip whistle;
    public AudioClip kick;
    public AudioClip goal;
    public AudioClip crowdCheer;
    public AudioClip goalkeeperSave;
    public AudioClip background;

    [Range(0f, 1f)] public float sfxVolume = 0.85f;
    [Range(0f, 1f)] public float musicVolume = 0.25f;

    AudioSource sfxSource;
    AudioSource musicSource;

    void Awake()
    {
        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;

        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.playOnAwake = false;
        musicSource.loop = true;
        musicSource.volume = musicVolume;
    }

    void Start()
    {
        PlaySfx(whistle);
        if (background != null)
        {
            musicSource.clip = background;
            musicSource.Play();
        }
    }

    public void PlayKick() => PlaySfx(kick);

    public void PlayGoal()
    {
        PlaySfx(goal);
        PlaySfx(crowdCheer);
    }

    public void PlaySave() => PlaySfx(goalkeeperSave);

    void PlaySfx(AudioClip clip)
    {
        if (clip == null) return;
        sfxSource.PlayOneShot(clip, sfxVolume);
    }
}
