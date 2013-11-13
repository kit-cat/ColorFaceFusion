/*
 * This file is part of the Face Fusion project. 
 *
 * Copyright (c) 2013 Joshua Blake
 *
 * This code is licensed to you under the terms of the MIT license.
 * See https://facefusion.codeplex.com/license for a copy of the license.
 */

using System;
using System.Windows.Threading;
using NAudio.Wave;

namespace FaceFusion.Services
{
    enum AudioState
    {
        SlidingNote,
        Chord,
        Error,
        None
    }

    class AudioManager
    {
        #region Fields

        private WaveOut _waveOut;

        private SineWaveProvider32 _sineGenerator = new SineWaveProvider32();

        DispatcherTimer _chordTimer;

        #endregion

        private double _semitone = 60;
        public double Semitone
        {
            get
            {
                return _semitone;
            }
            set
            {
                if (_semitone == value)
                    return;
                _semitone = value;
                _sineGenerator.Semitone = _semitone;
            }
        }

        private AudioState _state = AudioState.None;
        public AudioState State
        {
            get
            {
                return _state;
            }
            set
            {
                if (_state == value)
                    return;
                _state = value;
                UpdateAudioState();
            }
        }

        public AudioManager()
        {
            _sineGenerator = new SineWaveProvider32();
            _sineGenerator.SetWaveFormat(16000, 1); // 16kHz mono
            _sineGenerator.Semitone = (float)Semitone;
            _sineGenerator.Amplitude = 0.25f;
            
            _chordTimer = new DispatcherTimer();
            _chordTimer.Tick += new EventHandler(chordTimer_Tick);
            _chordTimer.Interval = TimeSpan.FromSeconds(0.5);
        }

        void chordTimer_Tick(object sender, EventArgs e)
        {
            _sineGenerator.IsPlayingRoot = false;
            _sineGenerator.IsPlayingErrorChord = false;
            _sineGenerator.IsPlayingTriadChord = false;
            _chordTimer.Stop();
        }

        public void Start()
        {
            if (_waveOut == null)
            {
                _waveOut = new WaveOut();
                _waveOut.Init(_sineGenerator);
                _waveOut.Play();
            }
        }

        public void Stop()
        {
            if (_waveOut != null)
            {
                _waveOut.Stop();
                _waveOut.Dispose();
                _waveOut = null;
            }
        }

        private void UpdateAudioState()
        {
            switch (State)
            {
                case AudioState.SlidingNote:
                    _sineGenerator.IsPlayingRoot = true;
                    _sineGenerator.IsPlayingTriadChord = false;
                    _sineGenerator.IsPlayingErrorChord = false;
                    _chordTimer.Stop();
                    break;
                case AudioState.Chord:
                    _sineGenerator.IsPlayingRoot = true;
                    _sineGenerator.IsPlayingTriadChord = true;
                    _sineGenerator.IsPlayingErrorChord = false;
                    //_chordTimer.Stop();
                    //_chordTimer.Start();
                    break;
                case AudioState.Error:
                    _sineGenerator.IsPlayingRoot = true;
                    _sineGenerator.IsPlayingErrorChord = true;
                    _sineGenerator.IsPlayingTriadChord = false;
                    _chordTimer.Stop();
                    _chordTimer.Start();
                    break;
                case AudioState.None:
                    if (!_chordTimer.IsEnabled)
                    {
                        _sineGenerator.IsPlayingRoot = false;
                        _sineGenerator.IsPlayingErrorChord = false;
                        _sineGenerator.IsPlayingTriadChord = false;
                        _chordTimer.Stop();
                    }
                    break;
            }
        }

    }
}
