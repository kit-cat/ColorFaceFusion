/*
 * This file is part of the Face Fusion project. 
 *
 * Copyright (c) 2013 Joshua Blake
 *
 * This code is licensed to you under the terms of the MIT license.
 * See https://facefusion.codeplex.com/license for a copy of the license.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using NAudio.Wave;

namespace FaceFusion.Services
{
    public abstract class WaveProvider32 : IWaveProvider
    {
        private WaveFormat waveFormat;

        public WaveProvider32()
            : this(44100, 1)
        {
        }

        public WaveProvider32(int sampleRate, int channels)
        {
            SetWaveFormat(sampleRate, channels);
        }

        public void SetWaveFormat(int sampleRate, int channels)
        {
            this.waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels);
        }

        public int Read(byte[] buffer, int offset, int count)
        {
            WaveBuffer waveBuffer = new WaveBuffer(buffer);
            int samplesRequired = count / 4;
            int samplesRead = Read(waveBuffer.FloatBuffer, offset / 4, samplesRequired);
            return samplesRead * 4;
        }

        public abstract int Read(float[] buffer, int offset, int sampleCount);

        public WaveFormat WaveFormat
        {
            get { return waveFormat; }
        }
    }

    class Tone
    {
        public double PhaseAngle;
        public double SemitoneOffset;
        public double Amplitude;

        public Tone(double semitoneOffset, double amplitude)
        {
            this.SemitoneOffset = semitoneOffset;
            this.Amplitude = amplitude;
        }

        private double GetFrequencyForSemitone(double semitone)
        {
            return 440.0 * Math.Pow(2, (semitone - 69) / 12.0);
        }

        public double GetSinValue(int sampleRate, double semitone)
        {
            double freq = Math.Round(GetFrequencyForSemitone(semitone + SemitoneOffset));
            double sin = Amplitude * Math.Sin(PhaseAngle);
            PhaseAngle += (2 * Math.PI * freq) / sampleRate;
            if (PhaseAngle > 2 * Math.PI)
                PhaseAngle -= 2 * Math.PI;

            return sin;
        }

    }

    class SineWaveProvider32 : WaveProvider32
    {
        List<Tone> _rootTones = new List<Tone>();
        List<Tone> _triadChordTones = new List<Tone>();
        List<Tone> _errorChordTones = new List<Tone>();

        public double ActualSemitone { get; private set; }
        public bool IsPlayingTriadChord { get; set; }
        public bool IsPlayingRoot { get; set; }
        public bool IsPlayingErrorChord { get; set; }
        public double Semitone { get; set; }
        public double Amplitude { get; set; }

        public SineWaveProvider32()
        {
            ActualSemitone = 60;
            Semitone = 60;
            Amplitude = 0.25f; // let's not hurt our ears  

            InitTones();
        }

        private void InitTones()
        {
            _rootTones.Add(new Tone(0, 1.0));
            _rootTones.Add(new Tone(12, 0.5));
            _rootTones.Add(new Tone(12+7, 0.45));
            _rootTones.Add(new Tone(12+12, 0.4));
            _rootTones.Add(new Tone(12+12+4, 0.35));
            _rootTones.Add(new Tone(12+12+7, 0.3));
            _rootTones.Add(new Tone(12+12+10, 0.25));


            _errorChordTones.Add(new Tone(-2 + 4 + 0, 1.0));
            _errorChordTones.Add(new Tone(-2 + 4 + 12, 0.5));
            _errorChordTones.Add(new Tone(-2 + 4 + 12 + 7, 0.45));
            _errorChordTones.Add(new Tone(-2 + 4 + 12 + 12, 0.4));
            _errorChordTones.Add(new Tone(-2 + 4 + 12 + 12 + 4, 0.35));
            _errorChordTones.Add(new Tone(-2 + 4 + 12 + 12 + 7, 0.3));
            _errorChordTones.Add(new Tone(-2 + 4 + 12 + 12 + 10, 0.25));


            _triadChordTones.Add(new Tone(4 + 0, 1.0));
            _triadChordTones.Add(new Tone(4 + 12, 0.5));
            _triadChordTones.Add(new Tone(4 + 12 + 7, 0.45));
            _triadChordTones.Add(new Tone(4 + 12 + 12, 0.4));
            _triadChordTones.Add(new Tone(4 + 12 + 12 + 4, 0.35));
            _triadChordTones.Add(new Tone(4 + 12 + 12 + 7, 0.3));
            _triadChordTones.Add(new Tone(4 + 12 + 12 + 10, 0.25));

            _triadChordTones.Add(new Tone(7 + 0, 1.0));
            _triadChordTones.Add(new Tone(7 + 12, 0.5));
            _triadChordTones.Add(new Tone(7 + 12 + 7, 0.45));
            _triadChordTones.Add(new Tone(7 + 12 + 12, 0.4));
            _triadChordTones.Add(new Tone(7 + 12 + 12 + 4, 0.35));
            _triadChordTones.Add(new Tone(7 + 12 + 12 + 7, 0.3));
            _triadChordTones.Add(new Tone(7 + 12 + 12 + 10, 0.25));
        }

        public override int Read(float[] buffer, int offset, int sampleCount)
        {
            int sampleRate = WaveFormat.SampleRate;

            int toneAdjustSamples = (int)(sampleCount * 1);

            for (int index = 0; index < sampleCount; index++)
            {
                List<double> values = new List<double>();

                if (Semitone != ActualSemitone)
                {
                    ActualSemitone = ((toneAdjustSamples - index - 1) * ActualSemitone + Semitone) / (toneAdjustSamples - index);
                }

                if (IsPlayingRoot)
                {
                    foreach (var tone in _rootTones)
                    {
                        values.Add(Amplitude * tone.GetSinValue(sampleRate, ActualSemitone));
                    }
                }

                if (IsPlayingErrorChord)
                {
                    foreach (var tone in _errorChordTones)
                    {
                        values.Add(Amplitude * tone.GetSinValue(sampleRate, ActualSemitone));
                    }
                }

                if (IsPlayingTriadChord)
                {
                    foreach (var tone in _triadChordTones)
                    {
                        values.Add(Amplitude * tone.GetSinValue(sampleRate, ActualSemitone));
                    }
                }
                //values.Add(GetOvertone(sampleRate, currentSemitone, 1.0, 0, ref phaseAngle0));
                //values.Add(GetOvertone(sampleRate, currentSemitone, 0.5, 12, ref phaseAngle1));
                //values.Add(GetOvertone(sampleRate, currentSemitone, 0.45, 12 + 7, ref phaseAngle2));
                //values.Add(GetOvertone(sampleRate, currentSemitone, 0.4, 12 + 12, ref phaseAngle3));
                //values.Add(GetOvertone(sampleRate, currentSemitone, 0.35, 12 + 12 + 4, ref phaseAngle4));
                //values.Add(GetOvertone(sampleRate, currentSemitone, 0.3, 12 + 12 + 7, ref phaseAngle5));
                //values.Add(GetOvertone(sampleRate, currentSemitone, 0.25, 12 + 12 + 10, ref phaseAngle6));
                //values.Add(GetOvertone(sampleRate, currentSemitone, 0.2, 12 + 12 + 12));
                //values.Add(GetOvertone(sampleRate, currentSemitone, 0.15, 12 + 12 + 12 + 2));
                //values.Add(GetOvertone(sampleRate, currentSemitone, 0.1, 12 + 12 + 12 + 4));
                //values.Add(GetOvertone(sampleRate, currentSemitone, 0.05, 12 + 12 + 12 + 6));

                float avg = 0;
                if (values.Count > 0)
                {
                    avg = (float)values.Average();
                }
                buffer[index + offset] = avg;

            }
            return sampleCount;
        }

        private double AdjustFreqBySemitones(double frequency, double semitones)
        {
            return frequency * GetToneMultiplier(semitones);
        }

        private double GetToneMultiplier(double semitones)
        {
            double newPitch = semitones / 12.0;
            return (float)Math.Exp(0.69314718056f * newPitch);
        }
    }

    class LoopStream : WaveProvider32
    {
        float[] overlapBuffer;
        bool isBackwardsLoop;

        AudioFileReader sourceStream;
        WaveFileWriter writer;

        public int LoopStartSample { get; set; }
        public int LoopEndSample { get; set; }
        public int OverlapSampleCount { get; set; }

        /// <summary>
        /// Use this to turn looping on or off
        /// </summary>
        public bool IsLoopingEnabled { get; set; }

        /// <summary>
        /// Creates a new Loop stream
        /// </summary>
        /// <param name="sampleProvider">The sample provider to read from.</param>
        public LoopStream(AudioFileReader audioFileReader)
        {
            if (audioFileReader == null)
                throw new ArgumentNullException("audioFileReader");

            this.sourceStream = audioFileReader;
            this.IsLoopingEnabled = true;

            this.SetWaveFormat(sourceStream.WaveFormat.SampleRate,
                               sourceStream.WaveFormat.Channels);


            writer = new WaveFileWriter("out.wav", audioFileReader.WaveFormat);

        }

        public void CompleteWriting()
        {
            writer.Close();
            writer.Dispose();
        }

        public long SamplePosition
        {
            get
            {
                return sourceStream.Position / sourceStream.BlockAlign;
            }
            set
            {
                sourceStream.Position = value * sourceStream.BlockAlign;
            }
        }

        public override int Read(float[] buffer, int offset, int sampleCount)
        {
            if (overlapBuffer == null || overlapBuffer.Length != sampleCount)
            {
                overlapBuffer = new float[sampleCount];
            }

            int numChannels = sourceStream.WaveFormat.Channels;
            
            int totalSamplesRead = 0;

            while (totalSamplesRead < sampleCount)
            {
                int samplesToRead = sampleCount - totalSamplesRead;

                long startSample = SamplePosition;
                int playbackDirection = isBackwardsLoop ? -1 : 1;
                long estimatedEndSample = startSample + playbackDirection * samplesToRead / numChannels;

                if (!isBackwardsLoop && estimatedEndSample > LoopEndSample)
                {
                    samplesToRead = (int)(LoopEndSample - startSample) * numChannels;
                }
                else if (isBackwardsLoop && estimatedEndSample < LoopStartSample)
                {
                    samplesToRead = (int)(startSample - LoopStartSample) * numChannels;
                }

                totalSamplesRead += ReadSamplesToLocalBuffer(samplesToRead);

                WriteSamplesToBuffer(buffer, offset, totalSamplesRead);

                if (!isBackwardsLoop && SamplePosition >= LoopEndSample && IsLoopingEnabled)
                {
                    // loop
                    isBackwardsLoop = true;

                    //SamplePosition = LoopStartSample;
                }
                else if (isBackwardsLoop && SamplePosition <= LoopStartSample)
                {
                    isBackwardsLoop = false;
                }
            }

            return totalSamplesRead;
        }

        private void WriteSamplesToBuffer(float[] buffer, int offset, int samplesRead)
        {
            if (isBackwardsLoop)
            {
                for (int i = 0; i < samplesRead - 1; i += 2)
                {
                    int j = samplesRead - 2 - i;

                    float sample = overlapBuffer[j];
                    buffer[i + offset] = sample;
                    writer.WriteSample(sample);

                    sample = overlapBuffer[j + 1];
                    buffer[i + offset + 1] = sample;
                    writer.WriteSample(sample);
                }
            }
            else
            {
                for (int i = 0; i < samplesRead; i++)
                {
                    buffer[i + offset] = overlapBuffer[i];
                }

                writer.WriteSamples(overlapBuffer, 0, samplesRead);
            }
        }

        private int ReadSamplesToLocalBuffer(int sampleCount)
        {
            int totalSamplesRead = 0;
            
            int numChannels = sourceStream.WaveFormat.Channels;
            if (isBackwardsLoop)
            {
                SamplePosition -= sampleCount / numChannels;
            }

            while (totalSamplesRead < sampleCount)
            {
                int samplesToRead = sampleCount - totalSamplesRead;

                int samplesRead = sourceStream.Read(overlapBuffer, totalSamplesRead, samplesToRead);

                totalSamplesRead += samplesRead;
            }

            if (isBackwardsLoop)
            {
                SamplePosition -= totalSamplesRead / numChannels;
            }

            return totalSamplesRead;
        }

        public int Read2(float[] buffer, int offset, int sampleCount)
        {
            int totalSamplesRead = 0;

            int numChannels = sourceStream.WaveFormat.Channels;
            while (totalSamplesRead < sampleCount)
            {
                int samplesToRead = sampleCount - totalSamplesRead;

                long startSample = SamplePosition;
                long estimatedEndSample = startSample + samplesToRead / numChannels;

                if (estimatedEndSample > LoopEndSample)
                {
                    samplesToRead = (int)(LoopEndSample - startSample) * numChannels;
                }

                int samplesRead = sourceStream.Read(buffer, offset + totalSamplesRead, samplesToRead);

                int overlapSample = LoopEndSample - OverlapSampleCount;
                int overlapSamplePosition = (int)(startSample - overlapSample);

                bool inOverlap = overlapSamplePosition >= 0;
                bool overlapToStart = overlapSamplePosition + samplesToRead / numChannels > 0;
                if (inOverlap || overlapToStart)
                {
                    long savedPosition = SamplePosition;

                    SamplePosition = LoopStartSample + overlapSamplePosition;
                    int overlapSamplesToRead = samplesToRead;
                    if (overlapSamplePosition < 0)
                        overlapSamplesToRead += overlapSamplePosition * numChannels;

                    if (overlapBuffer == null || overlapBuffer.Length != sampleCount)
                    {
                        overlapBuffer = new float[sampleCount];
                    }

                    int overlapBytesRead = sourceStream.Read(overlapBuffer, 0, overlapSamplesToRead);

                    int overlapStart = offset + totalSamplesRead;
                    if (overlapSamplePosition < 0)
                        overlapStart -= overlapSamplePosition * numChannels;
                    int overlapSampleEnd = overlapStart + overlapSamplesToRead;
                    int j = 0;

                    for (int i = overlapStart; i < overlapSampleEnd; i++)
                    {
                        float overlapProgress = (startSample + i / numChannels - overlapSample) / OverlapSampleCount;
                        buffer[i] = buffer[i] * (1 - overlapProgress) + overlapBuffer[j] * overlapProgress;
                        j++;
                    }

                    SamplePosition = savedPosition;
                }


                if (SamplePosition >= LoopEndSample)
                {
                    if (sourceStream.Position == 0 || !IsLoopingEnabled)
                    {
                        // something wrong with the source stream
                        break;
                    }
                    // loop
                    SamplePosition = LoopStartSample + OverlapSampleCount;
                }
                totalSamplesRead += samplesRead;
            }
            return totalSamplesRead;
        }
    }
}
