using UnityEngine;

/// <summary>Music and one-shot sound effects for Level 1.</summary>
public class Level1Audio : MonoBehaviour
{
    [Header("Sources")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource effectsSource;

    [Header("Music")]
    [SerializeField] private AudioClip dungeonMusic;
    [SerializeField, Range(0f, 1f)] private float musicVolume = 0.22f;

    [Header("Combat")]
    [SerializeField] private AudioClip playerAttack;
    [SerializeField] private AudioClip enemyAttack;
    [SerializeField] private AudioClip enemyHit;
    [SerializeField] private AudioClip enemyDefeat;

    [Header("World")]
    [SerializeField] private AudioClip doorOpen;
    [SerializeField] private AudioClip footstepA;
    [SerializeField] private AudioClip footstepB;
    [SerializeField] private AudioClip restCrystal;
    [SerializeField] private AudioClip levelComplete;
    [SerializeField] private AudioClip uiClick;

    private float nextStepTime;
    private bool alternateStep;
    private bool victoryPlayed;

    private void Start()
    {
        if (musicSource == null || dungeonMusic == null)
            return;

        musicSource.playOnAwake = false;
        musicSource.loop = true;
        musicSource.spatialBlend = 0f;
        musicSource.clip = dungeonMusic;
        musicSource.volume = musicVolume;
        musicSource.Play();
    }

    public void PlayFootstepIfDue()
    {
        if (Time.time < nextStepTime)
            return;

        nextStepTime = Time.time + 0.38f;
        alternateStep = !alternateStep;
        Play(alternateStep ? footstepA : footstepB, 0.18f);
    }

    public void PlayPlayerAttack() => Play(playerAttack, 0.65f);
    public void PlayEnemyAttack() => Play(enemyAttack, 0.42f);
    public void PlayEnemyHit(bool defeated) =>
        Play(defeated ? enemyDefeat : enemyHit, defeated ? 0.64f : 0.55f);
    public void PlayDoor() => Play(doorOpen, 0.58f);
    public void PlayUiClick() => Play(uiClick, 0.25f);
    public void PlayMagic() => Play(restCrystal, 0.38f);

    public void EnterRest()
    {
        Play(restCrystal, 0.58f);
        if (musicSource != null)
            musicSource.volume = musicVolume * 0.55f;
    }

    public void LeaveRest()
    {
        if (musicSource != null && !victoryPlayed)
            musicSource.volume = musicVolume;
    }

    public void PlayVictory()
    {
        if (victoryPlayed)
            return;

        victoryPlayed = true;
        if (musicSource != null)
            musicSource.Stop();
        Play(levelComplete, 0.75f);
    }

    private void Play(AudioClip clip, float volume)
    {
        if (effectsSource != null && clip != null)
            effectsSource.PlayOneShot(clip, volume);
    }
}
