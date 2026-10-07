using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BGMPlayer : MonoBehaviour
{
    [Header("Audio Settings")]
    public AudioSource audioSource;
    public List<AudioClip> bgmClips;
    public bool autoPlay = false;

    [Header("UI References")]
    public Text songTitleText;
    public Button playPauseButton;
    public Button nextButton;
    public Button prevButton;

    public GameState runtime;

    private int currentTrackIndex = 0;
    private bool isPlaying = false;

    void Awake()
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

        if (runtime == null) runtime = FindObjectOfType<GameState>();
    }

    void OnEnable()
    {
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

        if (runtime != null)
        {
            runtime.OnGameStart += StartPlay;
            if (runtime.Phase == GameSessionPhase.Playing || autoPlay) StartPlay();
        }
        UpdateUI();
    }

    void StartPlay()
    {
        PlayTrack(currentTrackIndex);
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
        if (bgmClips == null || bgmClips.Count == 0) return;

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
        if (bgmClips == null || bgmClips.Count == 0) return;

        currentTrackIndex = (currentTrackIndex + 1) % bgmClips.Count;
        PlayTrack(currentTrackIndex);
    }

    public void PreviousTrack()
    {
        if (bgmClips == null || bgmClips.Count == 0) return;

        currentTrackIndex--;
        if (currentTrackIndex < 0)
        {
            currentTrackIndex = bgmClips.Count - 1;
        }
        PlayTrack(currentTrackIndex);
    }

    void PlayTrack(int index)
    {
        if (bgmClips != null && index >= 0 && index < bgmClips.Count && bgmClips[index] != null)
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
            if (bgmClips != null && bgmClips.Count > 0 && currentTrackIndex < bgmClips.Count && bgmClips[currentTrackIndex] != null)
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
    void OnDisable()
    {
        if (runtime != null) runtime.OnGameStart -= StartPlay;
        if (playPauseButton != null) playPauseButton.onClick.RemoveListener(TogglePlayPause);
        if (nextButton != null) nextButton.onClick.RemoveListener(NextTrack);
        if (prevButton != null) prevButton.onClick.RemoveListener(PreviousTrack);
    }
}
