using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BGMPlayer : MonoBehaviour
{
    [Header("Audio Settings")]
    public AudioSource audioSource;
    public List<AudioClip> bgmClips;
    public bool autoPlay = true;

    [Header("UI References")]
    public Text songTitleText;
    public Button playPauseButton;
    public Button nextButton;
    public Button prevButton;

    private int currentTrackIndex = 0;
    private bool isPlaying = false;

    void Start()
    {
        // Ensure AudioSource is assigned
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
        }

        // Setup Buttons
        if (playPauseButton != null)
        {
            playPauseButton.onClick.AddListener(TogglePlayPause);
        }
        if (nextButton != null)
        {
            nextButton.onClick.AddListener(NextTrack);
        }
        if (prevButton != null)
        {
            prevButton.onClick.AddListener(PreviousTrack);
        }

        // Start playing if we have clips and autoPlay is true
        if (bgmClips != null && bgmClips.Count > 0)
        {
            if (autoPlay)
            {
                PlayTrack(currentTrackIndex);
            }
            else
            {
                UpdateUI();
            }
        }
    }

    void TogglePlayPause()
    {
        if (audioSource.isPlaying)
        {
            Pause();
        }
        else
        {
            Play();
        }
    }

    public void Play()
    {
        if (bgmClips.Count == 0) return;
        
        audioSource.UnPause();
        if (!audioSource.isPlaying)
        {
            audioSource.Play();
        }
        isPlaying = true;
        UpdateUI();
    }

    public void Pause()
    {
        audioSource.Pause();
        isPlaying = false;
        UpdateUI();
    }

    public void NextTrack()
    {
        if (bgmClips.Count == 0) return;

        currentTrackIndex = (currentTrackIndex + 1) % bgmClips.Count;
        PlayTrack(currentTrackIndex);
    }

    public void PreviousTrack()
    {
        if (bgmClips.Count == 0) return;

        currentTrackIndex--;
        if (currentTrackIndex < 0)
        {
            currentTrackIndex = bgmClips.Count - 1;
        }
        PlayTrack(currentTrackIndex);
    }

    void PlayTrack(int index)
    {
        if (index >= 0 && index < bgmClips.Count)
        {
            audioSource.clip = bgmClips[index];
            audioSource.Play();
            isPlaying = true;
            UpdateUI();
        }
    }

    void UpdateUI()
    {
        if (songTitleText != null)
        {
            if (bgmClips.Count > 0 && currentTrackIndex < bgmClips.Count)
            {
                songTitleText.text = bgmClips[currentTrackIndex].name;
            }
            else
            {
                songTitleText.text = "No Music";
            }
        }

        if (playPauseButton != null)
        {
            Text btnText = playPauseButton.GetComponentInChildren<Text>();
            if (btnText != null)
            {
                btnText.text = isPlaying ? "Pause" : "Play";
            }
        }
    }
}
