/*
 * This file is part of the Face Fusion project. 
 *
 * Copyright (c) 2013 Joshua Blake
 *
 * This code is licensed to you under the terms of the MIT license.
 * See https://facefusion.codeplex.com/license for a copy of the license.
 */

using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Kinect;
using Microsoft.Speech.AudioFormat;
using Microsoft.Speech.Recognition;

namespace FaceFusion.Services
{
    class VoiceCommand
    {
        SpeechRecognitionEngine speechRecognizer;
        KinectSensor sensor;
        DispatcherTimer readyTimer;

        SynchronizationContext syncContext;

        public const string StartCommand = "Fusion Start";
        public const string ResetCommand = "Fusion Reset";
        public const string PauseCommand = "Fusion Pause";

        private bool _isListening;
        public bool IsListening
        {
            get
            {
                return _isListening;
            }
            private set
            {
                if (_isListening == value)
                    return;
                _isListening = value;
                RaiseIsListeningChanged();
            }
        }

        #region Events

        public event EventHandler IsListeningChanged;

        private void RaiseIsListeningChanged()
        {
            if (IsListeningChanged == null)
                return;
            IsListeningChanged(this, EventArgs.Empty);
        }

        public event EventHandler FusionStart;
 
        private void RaiseFusionStart()
        {
            if (FusionStart == null)
                return;
            FusionStart(this, EventArgs.Empty);
        }

        public event EventHandler FusionReset;

        private void RaiseFusionReset()
        {
            if (FusionReset == null)
                return;
            FusionReset(this, EventArgs.Empty);
        }

        public event EventHandler FusionPause;

        private void RaiseFusionPause()
        {
            if (FusionPause == null)
                return;
            FusionPause(this, EventArgs.Empty);
        }

        #endregion

        public VoiceCommand(KinectSensor sensor)
        {
            this.sensor = sensor;
            syncContext = SynchronizationContext.Current;

            InitializeSpeechRecognition();
        }

        private static RecognizerInfo GetKinectRecognizer()
        {
            Func<RecognizerInfo, bool> matchingFunc = r =>
            {
                string value;
                r.AdditionalInfo.TryGetValue("Kinect", out value);
                return "True".Equals(value, StringComparison.InvariantCultureIgnoreCase) && "en-US".Equals(r.Culture.Name, StringComparison.InvariantCultureIgnoreCase);
            };
            return SpeechRecognitionEngine.InstalledRecognizers().Where(matchingFunc).FirstOrDefault();
        }

        private void InitializeSpeechRecognition()
        {
            RecognizerInfo ri = GetKinectRecognizer();
            if (ri == null)
            {
                MessageBox.Show(
                    @"There was a problem initializing Speech Recognition.
Ensure you have the Microsoft Speech SDK installed.",
                    "Failed to load Speech SDK",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            try
            {
                speechRecognizer = new SpeechRecognitionEngine(ri.Id);
            }
            catch
            {
                MessageBox.Show(
                    @"There was a problem initializing Speech Recognition.
Ensure you have the Microsoft Speech SDK installed and configured.",
                    "Failed to load Speech SDK",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }

            var phrases = new Choices();
            phrases.Add(StartCommand);
            phrases.Add(PauseCommand);
            phrases.Add(ResetCommand);

            var gb = new GrammarBuilder();
            //Specify the culture to match the recognizer in case we are running in a different culture.                                 
            gb.Culture = ri.Culture;
            gb.Append(phrases);

            // Create the actual Grammar instance, and then load it into the speech recognizer.
            var g = new Grammar(gb);

            speechRecognizer.LoadGrammar(g);
            speechRecognizer.SpeechRecognized += SreSpeechRecognized;
            speechRecognizer.SpeechHypothesized += SreSpeechHypothesized;
            speechRecognizer.SpeechRecognitionRejected += SreSpeechRecognitionRejected;

            this.readyTimer = new DispatcherTimer();
            this.readyTimer.Tick += this.ReadyTimerTick;
            this.readyTimer.Interval = new TimeSpan(0, 0, 4);
            this.readyTimer.Start();

        }

        private void ReadyTimerTick(object sender, EventArgs e)
        {
            this.StartSpeechRecognition();
            this.readyTimer.Stop();
            this.readyTimer.Tick -= ReadyTimerTick;
            this.readyTimer = null;
        }

        private void StartSpeechRecognition()
        {
            if (sensor == null || speechRecognizer == null)
                return;

            var audioSource = this.sensor.AudioSource;
            audioSource.BeamAngleMode = BeamAngleMode.Adaptive;
            var kinectStream = audioSource.Start();

            speechRecognizer.SetInputToAudioStream(
                    kinectStream, new SpeechAudioFormatInfo(EncodingFormat.Pcm, 16000, 16, 1, 32000, 2, null));
            speechRecognizer.RecognizeAsync(RecognizeMode.Multiple);

            IsListening = true;
        }

        void SreSpeechRecognitionRejected(object sender, SpeechRecognitionRejectedEventArgs e)
        {
            Trace.WriteLine("\nSpeech Rejected, confidence: " + e.Result.Confidence);
        }

        void SreSpeechHypothesized(object sender, SpeechHypothesizedEventArgs e)
        {
            Trace.Write("\rSpeech Hypothesized: \t{0}", e.Result.Text);
        }

        void SreSpeechRecognized(object sender, SpeechRecognizedEventArgs e)
        {
            //This first release of the Kinect language pack doesn't have a reliable confidence model, so 
            //we don't use e.Result.Confidence here.
            if (e.Result.Confidence < 0.70)
            {
                Trace.WriteLine("\nSpeech Rejected filtered, confidence: " + e.Result.Confidence);
                return;
            }

            Trace.WriteLine("\nSpeech Recognized, confidence: " + e.Result.Confidence + ": \t{0}", e.Result.Text);

            if (e.Result.Text == StartCommand)
            {
                syncContext.Post((o) =>
                    {
                        RaiseFusionStart();
                    }, null);
            }
            else if (e.Result.Text == PauseCommand)
            {
                syncContext.Post((o) =>
                {
                    RaiseFusionPause();
                }, null);
            }
            else if (e.Result.Text == ResetCommand)
            {
                syncContext.Post((o) =>
                {
                    RaiseFusionReset();
                }, null);
            }
        }

    }
}
