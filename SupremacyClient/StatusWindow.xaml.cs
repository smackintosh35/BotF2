// StatusWindow.xaml.cs
//
// Copyright (c) 2007 Mike Strobel
//
// This source code is subject to the terms of the Microsoft Reciprocal License (Ms-RL).
// For details, see <http://www.opensource.org/licenses/ms-rl.html>.
//
// All other rights reserved.

using System;

using Microsoft.Practices.Composite.Events;
using Microsoft.Practices.Composite.Presentation.Events;

using Supremacy.Client.Events;
using Supremacy.Client.Views;
using Supremacy.Data;
using Supremacy.Game;
using Supremacy.Messages;
using Supremacy.Messaging;

namespace Supremacy.Client
{
    /// <summary>
    /// Interaction logic for StatusWindow.xaml
    /// </summary>
    public partial class StatusWindow
    {
        #region Fields
        private readonly Table _turnStrings;
        private bool _shouldBeOpen;
        #endregion

        #region Constructors
        public StatusWindow()
        {
            InitializeComponent();

            try
            {
                var enumTables = GameTables.Load().EnumTables;
                if (enumTables != null)
                    _turnStrings = enumTables["TurnPhase"];
            }
            catch
            {
                _turnStrings = null;
            }

            Channel<TurnProgressChangedMessage>.Public.Subscribe(
                onNext: o => ProcessTurnPhaseChange(o.TurnPhase),
                threadOption: ChannelThreadOption.UIThread);

            //ClientEvents.TurnPhaseChanged.Subscribe(OnTurnPhaseChanged, ThreadOption.UIThread);
            ClientEvents.TurnStarted.Subscribe(OnTurnStarted, ThreadOption.UIThread);
            ClientEvents.AllTurnEnded.Subscribe(OnAllTurnEnded, ThreadOption.UIThread);
            ClientEvents.GameStarted.Subscribe(OnGameStarted, ThreadOption.UIThread);
            ClientEvents.GameEnded.Subscribe(OnGameEnded, ThreadOption.UIThread);
            ClientEvents.ClientDisconnected.Subscribe(OnClientDisconnected, ThreadOption.UIThread);
            ClientEvents.ViewActivating.Subscribe(OnViewActivating, ThreadOption.UIThread);
        }

        private void OnViewActivating(ViewActivatingEventArgs e)
        {
            if (e.View is ISystemAssaultScreenView)
            {
                if (IsOpen)
                    Close();
            }
            else
            {
                if (_shouldBeOpen && !IsOpen)
                    Show();
            }
        }

        #endregion

        #region Methods
        private void OnClientDisconnected(EventArgs t)
        {
            _shouldBeOpen = false;

            if (IsOpen)
                Close();
        }

        private void OnGameEnded(EventArgs t)
        {
            _shouldBeOpen = false;

            if (IsOpen)
                Close();
        }

        private void OnGameStarted(DataEventArgs<GameStartData> t)
        {
            ProcessTurnPhaseChange(TurnPhase.WaitOnPlayers);
        }

        private void OnTurnStarted(EventArgs t)
        {
            _shouldBeOpen = false;

            if (IsOpen)
                Close();
        }

        private void OnAllTurnEnded(EventArgs t)
        {
            _shouldBeOpen = true;

            if (IsActive)
                return;
            if (!IsOpen)
                Show();
            Activate();
        }

/*
        private void OnTurnPhaseChanged(DataEventArgs<TurnPhase> t)
        {
            ProcessTurnPhaseChange(t.Value);
        }
*/

        private void ProcessTurnPhaseChange(TurnPhase phase)
        {
            // ToDo: Get out of en.txt: PROCESSING_TURN (didn't find a way yet)
            Header = "Processing Turn";

            if (_turnStrings != null && _turnStrings[phase.ToString()] != null)
                Content = _turnStrings[phase.ToString()][0] + "...";
            else
                Content = phase + "...";
        }
        #endregion
    }
}