using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

public class MicManager : SingletonMonoBehaviour<MicManager>
{
    public float sensitivity = 100;
    public float loudness = 0;
    public float pitch = 0;
    AudioSource _audio;



    float[] _samples;
    private float[] _spectrum;
    private float _fSample;

    public float RmsValue;
    public float DbValue;
    public float PitchValue;

    private const int QSamples = 1024;
    private const float RefValue = 0.1f;
    private const float Threshold = 0.02f;

    public bool startMicOnStartUp = true;

    public bool stopMicrophoneListener = false;
    public bool startMicrophoneListener = false;
    public bool microphoneListenerOn = false;
    public bool disableOutputSound = false;

    AudioSource _src;

    [SerializeField]
    AudioMixer masterMixer;

    float timeSinceRestart = 0;

    private int loopTime = 10;
    private int micFreq = 44100;

    // Start is called before the first frame update
    void Start()
    {
        if (startMicOnStartUp)
        {
            RestartMicrophoneListener();
            StartMicrophoneListner();

            _audio = GetComponent<AudioSource>();
            _audio.clip = Microphone.Start(null, true, 10, 44100);
            _audio.loop = true;
            while (!(Microphone.GetPosition(null) > 0)) { }
            _audio.Play();
            _samples = new float[QSamples];
            _spectrum = new float[QSamples];
            _fSample = AudioSettings.outputSampleRate;
        }
    }


    // Update is called once per  frame
    void Update()
    {
        if (startMicrophoneListener)
        {
            StartMicrophoneListner();
        }
        if(stopMicrophoneListener)
        {
            StopMicrophoneListener();
        }
        startMicrophoneListener = false;
        stopMicrophoneListener = false;


        MicrophoneIntoAudioSource(microphoneListenerOn);
        DisableSound(!disableOutputSound);

        loudness = GetAveragedVolume() * sensitivity;
        GetPitch();


        CheckDelay();
    }

    private void GetPitch()
    {
        GetComponent<AudioSource>().GetOutputData(_samples, 0);
        int i;
        float sum = 0;
        for(i = 0; i < QSamples; i++)
        {
            sum += _samples[i] * _samples[i];
        }
        RmsValue = Mathf.Sqrt(sum / QSamples);
        DbValue = 20 * Mathf.Log10(RmsValue / RefValue);
        if (DbValue < -160) DbValue = -160;

        GetComponent<AudioSource>().GetSpectrumData(_spectrum, 0, FFTWindow.BlackmanHarris);
        float maxV = 0;
        var maxN = 0;
        for(i = 0; i < QSamples; i++)
        {
            if (!(_spectrum[i] > maxV) || !(_spectrum[i] > Threshold))
                continue;
            maxV = _spectrum[i];
            maxN = i;
        }
        float freqN = maxN;
        if (maxN > 0 && maxN < QSamples - 1)
        {
            var dL = _spectrum[maxN - 1] / _spectrum[maxN];
            var dR = _spectrum[maxN + 1] / _spectrum[maxN];
            freqN += 0.5f * (dR * dR - dL - dL);
        }
        PitchValue = freqN * (_fSample / 2) / QSamples;


    }

    float GetAveragedVolume()
    {
        float[] data = new float[256];
        float a = 0;
        _audio.GetOutputData(data, 0);
        foreach(float s in data)
        {
            a += Mathf.Abs(s);
        }
        return a / 256;

    }

    private void CheckDelay()
    {
        int delay = Microphone.GetPosition(null) - _src.timeSamples;
        if(delay <0)
        {
            delay += 44100;
        }
        if(delay > 4410)
        {
            int setTime = Microphone.GetPosition(null) - 1500;
            if(setTime < 0)
            {
                setTime += loopTime * micFreq;
            }
            _src.timeSamples = setTime;
        }
    }

    private void DisableSound(bool SoundOn)
    {
        float volume = SoundOn ? 0.0f : -80.0f;
        masterMixer.SetFloat("MasterVolume", volume);
        
    }

    private void MicrophoneIntoAudioSource(bool MicrophoneListenerOn)
    {
        if (MicrophoneListenerOn)
        {
            if (Time.time - timeSinceRestart > 0.5f && !Microphone.IsRecording(null))
            {
                _src.clip = Microphone.Start(null, true, 10, 44100);
            }
            while (!(Microphone.GetPosition(null)>0))
            {
            }
            _src.Play(); 
        }
    }

    public void StopMicrophoneListener()
    {
        microphoneListenerOn = false;
        disableOutputSound = false;
        _src.Stop();
        _src.clip = null;
        Microphone.End(null);
    }

    public void StartMicrophoneListner()
    {
        microphoneListenerOn = true;
        disableOutputSound = true;
        RestartMicrophoneListener();
    }

    public void RestartMicrophoneListener()
    {
        _src = GetComponent<AudioSource>();
        _src.clip = null;
        timeSinceRestart = Time.time;
    }


}
