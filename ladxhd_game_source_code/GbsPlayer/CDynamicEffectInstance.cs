using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Audio;

#if WINDOWS
using SharpDX;
using SharpDX.Multimedia;
using SharpDX.XAudio2;
#endif

namespace GBSPlayer
{
    public class CDynamicEffectInstance
    {
#if WINDOWS
        struct AudioBlock
        {
            public AudioBuffer AudioBuffer;
            public byte[] ByteBuffer;
        }

        private object _voiceLock = new Object();

        private static ByteBufferPool _bufferPool = new ByteBufferPool();

        private Queue<AudioBlock> _queuedBlocks = new Queue<AudioBlock>();

        private SourceVoice _voice;
        private WaveFormat _format;
#else
        // Linux/DesktopGL: Use MonoGame's cross-platform DynamicSoundEffectInstance
        private DynamicSoundEffectInstance _dynamicSound;
        private object _soundLock = new Object();
        private int _pendingBuffers = 0;
#endif

        public SoundState State = SoundState.Stopped;

        public CDynamicEffectInstance(int sampleRate)
        {
#if WINDOWS
            var xaudio2 = new XAudio2();
            var masteringVoice = new MasteringVoice(xaudio2);

            _format = new WaveFormat(sampleRate, 1);
            _voice = new SourceVoice(xaudio2, _format, true);
            _voice.BufferEnd += OnBufferEnd;
#else
            // Create MonoGame DynamicSoundEffectInstance for Linux
            // 16-bit mono audio at the specified sample rate
            _dynamicSound = new DynamicSoundEffectInstance(sampleRate, AudioChannels.Mono);
            _dynamicSound.BufferNeeded += OnBufferNeeded;
#endif
        }

        public int GetPendingBufferCount()
        {
#if WINDOWS
            lock (_voiceLock)
            {
                return _queuedBlocks.Count;
            }
#else
            lock (_soundLock)
            {
                return _dynamicSound?.PendingBufferCount ?? 0;
            }
#endif
        }

        public void Play()
        {
#if WINDOWS
            lock (_voiceLock)
            {
                State = SoundState.Playing;
                _voice.Start();
            }
#else
            lock (_soundLock)
            {
                State = SoundState.Playing;
                if (_dynamicSound != null && _dynamicSound.State != SoundState.Playing)
                    _dynamicSound.Play();
            }
#endif
        }

        public void Pause()
        {
#if WINDOWS
            lock (_voiceLock)
            {
                State = SoundState.Paused;
                _voice.Stop();
            }
#else
            lock (_soundLock)
            {
                State = SoundState.Paused;
                _dynamicSound?.Pause();
            }
#endif
        }

        public void Resume()
        {
#if WINDOWS
            lock (_voiceLock)
            {
                State = SoundState.Playing;
                _voice.Start();
            }
#else
            lock (_soundLock)
            {
                State = SoundState.Playing;
                _dynamicSound?.Resume();
            }
#endif
        }

        public void Stop()
        {
#if WINDOWS
            lock (_voiceLock)
            {
                State = SoundState.Stopped;

                _voice.Stop();
                // Dequeue all the submitted buffers
                _voice.FlushSourceBuffers();
            }
#else
            lock (_soundLock)
            {
                State = SoundState.Stopped;
                _dynamicSound?.Stop();
            }
#endif
        }

        public void SetVolume(float volume)
        {
#if WINDOWS
            lock (_voiceLock)
            {
                _voice.SetVolume(volume);
            }
#else
            lock (_soundLock)
            {
                if (_dynamicSound != null)
                    _dynamicSound.Volume = Math.Clamp(volume, 0f, 1f);
            }
#endif
        }

        public void SubmitBuffer(byte[] buffer, int offset, int count)
        {
#if WINDOWS
            var audioBlock = new AudioBlock();

            audioBlock.ByteBuffer = _bufferPool.Get(count);

            // we need to copy so datastream does not pin the buffer that the user might modify later
            Buffer.BlockCopy(buffer, offset, audioBlock.ByteBuffer, 0, count);

            var stream = DataStream.Create(audioBlock.ByteBuffer, true, false, 0, true);
            audioBlock.AudioBuffer = new AudioBuffer(stream);
            audioBlock.AudioBuffer.AudioBytes = count;

            _queuedBlocks.Enqueue(audioBlock);

            lock (_voiceLock)
                _voice.SubmitSourceBuffer(audioBlock.AudioBuffer, null);
#else
            lock (_soundLock)
            {
                if (_dynamicSound != null)
                {
                    // Copy the buffer data
                    byte[] audioData = new byte[count];
                    Buffer.BlockCopy(buffer, offset, audioData, 0, count);
                    _dynamicSound.SubmitBuffer(audioData);
                    _pendingBuffers++;
                }
            }
#endif
        }

#if WINDOWS
        private void OnBufferEnd(IntPtr obj)
        {
            // Release the buffer
            if (_queuedBlocks.Count > 0)
            {
                var block = _queuedBlocks.Dequeue();
                block.AudioBuffer.Stream.Dispose();
                _bufferPool.Return(block.ByteBuffer);
            }
        }
#else
        private void OnBufferNeeded(object sender, EventArgs e)
        {
            // Buffer finished playing
            lock (_soundLock)
            {
                if (_pendingBuffers > 0)
                    _pendingBuffers--;
            }
        }
#endif
    }
}
