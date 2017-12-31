﻿using System;
using System.Collections.Generic;
using System.Linq;
using Supremacy.Annotations;
using Supremacy.Resources;
using Supremacy.Client.Context;
using Supremacy.Utility;

namespace Supremacy.Client.Audio
{
    [Flags]
    public enum PlaybackMode
    {
        None = 0,
        Sequential = 1,
        Random = 1 << 1,
        Loop = 1 << 2,
        Fade = 1 << 3
    }

    public interface IMusicPlayer : IDisposable
    {
        float Volume { get; set; }
        PlaybackMode PlayMode { get; set; }
        float FadeTime { get; set; }
        bool IsPlaying { get; }
        MusicEntry CurrentMusicEntry { get; }
        IAudioTrack CurrentAudioTrack { get; }

        void LoadMusic(MusicPack musicPack, string trackName = null);
        void SwitchMusic(string packName);
        void Play();
        bool Switch(string trackName);
        void Stop();
        void Next();
        void Prev();
        void Update();
    }

    public class MusicPlayer : IMusicPlayer
    {
        #region Fields
        private const int UpdateInterval = 40; // milli seconds
        private const float DefaultFadeTime = 2.0f; // seconds

        private bool _isDisposed = false;
        private readonly object _updateLock = new object();
        private IAppContext _appContext = null;
        private IAudioEngine _engine = null;
        private IAudioGrouping _channelGroup = null;

        private bool _audioTraceLocally = false;    // turn to true if you want

        private MusicPack _musicPack = null;
        private KeyValuePair<int, MusicEntry> _musicEntry;
        private IAudioTrack _audioTrack = null;
        private List<IAudioTrack> _endingTracks = new List<IAudioTrack>();
        private PlaybackMode _playMode = PlaybackMode.None;
        private bool _isPlaying = false;

        private float _fadeTime = DefaultFadeTime;
        private readonly IObservable<long> _updateTimer = null;
        private IDisposable _updateTimerSubscription = null;
        #endregion

        #region Properties
        public float Volume
        {
            get { return _channelGroup.Volume; }
            set { _channelGroup.Volume = value; }
        }

        public PlaybackMode PlayMode
        {
            get { return _playMode; }
            set { _playMode = value; }
        }

        public float FadeTime
        {
            get { return _fadeTime; }
            set { _fadeTime = value; }
        }

        public float FadeFactor
        {
            get { return UpdateInterval / (1000.0f * _fadeTime); }
            set { _fadeTime = UpdateInterval / (1000.0f * value); }
        }

        public bool IsPlaying { get { return _isPlaying; } }
        public MusicEntry CurrentMusicEntry { get { return _musicEntry.Value; } }
        public IAudioTrack CurrentAudioTrack { get { return _audioTrack; } }
        #endregion

        #region Construction & Lifetime
        public MusicPlayer([NotNull] IAudioEngine engine, [NotNull] IAppContext appContext)
        {
            if (_audioTraceLocally)
                GameLog.Print("called!");

            if (engine == null)
                throw new ArgumentNullException("engine");
            if (appContext == null)
                throw new ArgumentNullException("appContext");

            _engine = engine;
            _appContext = appContext;
            _channelGroup = _engine.CreateGrouping("music");
            _updateTimer = Observable.Interval(TimeSpan.FromMilliseconds(UpdateInterval), _engine.Scheduler).Do(_ => Update());
        }

        public void Dispose()
        {
            if (_audioTraceLocally)
                GameLog.Print("called!");

            if (_isDisposed)
                return;

            _isDisposed = true;

            lock (_updateLock)
            {
                if (_updateTimerSubscription != null)
                {
                    _updateTimerSubscription.Dispose();
                    _updateTimerSubscription = null;
                }

                if (_audioTrack != null)
                {
                    try
                    {
                        _audioTrack.Stop();
                        _audioTrack.Dispose();
                        _audioTrack = null;
                    }
                    catch (Exception e) //ToDo: Just log or additional handling necessary?
                    {
                        GameLog.Print("################# problem at MusicPlayer.cs - Dispose - _audioTrack != null");
                        GameLog.LogException(e);
                    }
                }

                foreach (var track in _endingTracks)
                {
                    try
                    {
                        track.Stop();
                        track.Dispose();
                    }
                    catch (Exception e) //ToDo: Just log or additional handling necessary?
                    {
                        GameLog.Print("################# problem at MusicPlayer.cs - Dispose - foreach track Stop & Dispose");
                        GameLog.LogException(e);
                    }
                }
                _endingTracks.Clear();

                if (_channelGroup != null)
                {
                    _channelGroup.Dispose();
                    _channelGroup = null;
                }
                _engine = null;
            }
        }
        #endregion

        #region Methods
        public void LoadMusic(MusicPack musicPack, string trackName = null)
        {
            //GameLog.Print("called! Trackname: {0}", trackName);    trackName still empty at this moment

            try
            {
                lock (_updateLock)
                {
                    bool play = _isPlaying;
                    Stop();

                    _musicPack = musicPack;

                    if (trackName != null)
                        _musicEntry = _musicPack.FindByName(trackName);

                    if (trackName == null || _musicEntry.Value == null)
                    {
                        if (_playMode.HasFlag(PlaybackMode.Random))
                            _musicEntry = _musicPack.Random();
                        else if (_playMode.HasFlag(PlaybackMode.Sequential))
                            _musicEntry = _musicPack.Next();
                    }

                    if (play && _musicEntry.Value != null)
                    {
                        //if (_audioTraceLocally)
                            GameLog.Print("called! Trackname: {0}, {1}, playMode={2}", _musicPack.Name, _musicEntry.Value.FileName, _playMode.ToString());
                        Play();
                    }
                }
            }
            catch (Exception e) //ToDo: Just log or additional handling necessary?
            {
                GameLog.Print("################# problem at MusicPlayer.cs - LoadMusic");
                GameLog.LogException(e);
            }
        }

        public void SwitchMusic(string packName)
        {
            if (_audioTraceLocally)
                GameLog.Print("called! packName: {0}", packName);

            try
            {
                MusicPack pack;
                if (_appContext.ThemeMusicLibrary.MusicPacks.TryGetValue(packName, out pack) && pack.HasEntries()
                    || _appContext.DefaultMusicLibrary.MusicPacks.TryGetValue(packName, out pack) && pack.HasEntries())
                    LoadMusic(pack);
            }
            catch (Exception e) //ToDo: Just log or additional handling necessary?
            {
                GameLog.Print("################# problem at MusicPlayer.cs - SwitchMusic");
                GameLog.LogException(e);
            }
        }

        public void Play()
        {
            //GameLog.Print("called!");
            try
            {
                lock (_updateLock)
                {
                    Stop();
                    _isPlaying = true;

                    if (_musicEntry.Value != null)
                    {
                        _audioTrack = _engine.CreateTrack(
                            ResourceManager.GetResourcePath(_musicEntry.Value.FileName));

                        if (_audioTraceLocally)
                            GameLog.Print("called! _musicEntry.Value.FileName: {0}", _musicEntry.Value.FileName);

                        if (_audioTrack != null)
                        {
                            _audioTrack.Group = _channelGroup;
                            _audioTrack.Play(OnTrackEnd);

                            if (_updateTimerSubscription == null)
                                _updateTimerSubscription = _updateTimer.Subscribe();
                        }
                    }
                }

            }
            catch (Exception e) //ToDo: Just log or additional handling necessary?
            {
                GameLog.Print("################# problem at MusicPlayer.cs - Play");
                GameLog.LogException(e);
            }
        }

        public bool Switch(string trackName)
        {
            if (_audioTraceLocally)
                GameLog.Print("called! Trackname: {0}", trackName);

            try
            {
                lock (_updateLock)
                {
                    // TODO: restart track if already played?
                    if (_musicEntry.Value != null && _musicEntry.Value.TrackName.ToUpper().Equals(trackName.ToUpper()))
                    {
                        GameLog.Print("Switch = true (1)");
                        return true;
                    }

                    var newTrack = _musicPack.FindByName(trackName);
                    if (newTrack.Value == null)
                        return false;

                    bool play = _isPlaying;
                    Stop();
                    _musicEntry = newTrack;

                    if (play) Play();
                    {
                        if (_audioTraceLocally)
                            GameLog.Print("Switch = true (1), _musicPack={0}, _musicEntry={1}", _musicPack.Name, _musicEntry.Key);
                        return true;
                    }
                }
            }
            catch (Exception e) //ToDo: Just log or additional handling necessary?
            {
                GameLog.Print("################# problem at MusicPlayer.cs - Switch");
                GameLog.LogException(e);
                return false;
            }
        }

        public void Stop()
        {
            //GameLog.Print("called!");
            try
            {
                lock (_updateLock)
                {
                    _isPlaying = false;

                    if (_audioTrack == null)
                        return;

                    if (!_audioTrack.IsPlaying)
                    {
                        try
                        {
                            _audioTrack.Stop();
                            if (_audioTraceLocally)
                                GameLog.Print("################# Stop - Group={0}, Track={1}", _audioTrack.Group.ToString(), _audioTrack);
                            _audioTrack.Dispose();
                            _audioTrack = null;
                        }
                        catch (Exception e) //ToDo: Just log or additional handling necessary?
                        {
                            GameLog.Print("################# problem at MusicPlayer.cs - Stop - Group={0}", _audioTrack.Group.ToString());
                            GameLog.LogException(e);
                        }
                    }
                    else
                    {
                        _endingTracks.Add(_audioTrack);
                        _audioTrack = null;
                    }
                }
            }
            catch (Exception e) //ToDo: Just log or additional handling necessary?
            {
                GameLog.Print("################# problem at MusicPlayer.cs - Stop");
                GameLog.LogException(e);
            }
        }

        public void Next()
        {
            //GameLog.Print("called!");
            try
            {
                lock (_updateLock)
                {
                    if (_musicPack == null)
                        return;

                    if (_playMode.HasFlag(PlaybackMode.Random))
                        _musicEntry = _musicPack.Random(_musicEntry.Key);
                    else if (_playMode.HasFlag(PlaybackMode.Sequential))
                        _musicEntry = _musicPack.Next(_musicEntry.Key);
                    if (_audioTraceLocally)
                        GameLog.Print("################# MusicPlayer.cs - Next at _musicPack={0}, _musicEntry={1}", _musicPack.Name, _musicEntry.Value.FileName);

                    Play();
                }
            }
            catch (Exception e) //ToDo: Just log or additional handling necessary?
            {
                GameLog.Print("################# problem at MusicPlayer.cs - Next at _musicPack={0}, _musicEntry={1}", _musicPack.Name, _musicEntry.Value.FileName);
                GameLog.LogException(e);
            }
        }

        public void Prev()
        {
            if (_audioTraceLocally)
                GameLog.Print("called!");
            try
            {
                lock (_updateLock)
                {
                    if (_musicPack == null)
                        return;

                    if (_playMode.HasFlag(PlaybackMode.Random))
                        _musicEntry = _musicPack.Random(_musicEntry.Key);
                    else if (_playMode.HasFlag(PlaybackMode.Sequential))
                        _musicEntry = _musicPack.Prev(_musicEntry.Key);

                    if (_audioTraceLocally)
                        GameLog.Print("################# MusicPlayer.cs - Prev at _musicPack={0}, _musicEntry={1}", _musicPack.Name, _musicEntry.Value.FileName);
                    Play();
                }
            }
            catch (Exception e) //ToDo: Just log or additional handling necessary?
            {
                GameLog.Print("################# problem at MusicPlayer.cs - Prev at _musicPack={0}, _musicEntry={1}", _musicPack.Name, _musicEntry.Value.FileName);
                GameLog.LogException(e);
            }
        }

        private void OnTrackEnd(IAudioTrack track)
        {
            if (_audioTraceLocally)
                GameLog.Print("called!");
            try
            {
                lock (_updateLock)
                {
                    try
                    {
                        track.Dispose();
                    }
                    catch (Exception e) //ToDo: Just log or additional handling necessary?
                    {
                        GameLog.Print("################# problem at MusicPlayer.cs - OnTrackEnd - Dispose at track.Group={0}", track.Group.ToString());
                        GameLog.LogException(e);
                    }

                    if (track == _audioTrack)
                    {
                        _audioTrack = null;
                        Next();
                    }
                    else _endingTracks.Remove(track);

                    if (_audioTrack == null && _endingTracks.Count == 0)
                    {
                        if (_updateTimerSubscription != null)
                        {
                            _updateTimerSubscription.Dispose();
                            _updateTimerSubscription = null;
                        }
                    }
                }
            }
            catch (Exception e) //ToDo: Just log or additional handling necessary?
            {
                GameLog.Print("################# problem at MusicPlayer.cs - OnTrackEnd - DIspose");
                GameLog.LogException(e);
            }
        }

        public void Update()
        {
            //if (_audioTraceLocally)
                //GameLog.Print("called!"); --> Logging dislabled because this function is called every 40ms!!
                try
            {
                lock (_updateLock)
                {
                    if (_audioTrack != null && _audioTrack.IsPlaying && _audioTrack.Volume < 1.0f)
                        _audioTrack.FadeIn(FadeFactor / _fadeTime);

                    for (int i = _endingTracks.Count - 1; i >= 0; --i)
                    {
                        var track = _endingTracks[i];
                        track.FadeOut(FadeFactor / _fadeTime);
                        if (track.Volume <= 0.0f)
                        {
                            track.Dispose();
                            _endingTracks.RemoveAt(i);
                        }
                    }
                }
            }
            catch (Exception e) //ToDo: Just log or additional handling necessary?
            {
                GameLog.Print("################# problem at MusicPlayer.cs - Update");
                GameLog.LogException(e);
            }
        }
    }
    #endregion

}

