// IGameController.cs
//
// Copyright (c) 2009 Mike Strobel
//
// This source code is subject to the terms of the Microsoft Reciprocal License (Ms-RL).
// For details, see <http://www.opensource.org/licenses/ms-rl.html>.
//
// All other rights reserved.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Threading;

using Microsoft.Practices.Composite.Events;
using Microsoft.Practices.Composite.Presentation.Events;
using Microsoft.Practices.Composite.Regions;
using Microsoft.Practices.ServiceLocation;
using Microsoft.Practices.Unity;

using Supremacy.Annotations;
using Supremacy.Client.Commands;
using Supremacy.Client.Controls;
using Supremacy.Client.Dialogs;
using Supremacy.Client.Events;
using Supremacy.Client.Input;
using Supremacy.Client.Services;
using Supremacy.Client.Views;
using Supremacy.Combat;
using Supremacy.Game;

using System.Linq;
using Supremacy.Client.Context;
using Supremacy.Utility;

namespace Supremacy.Client
{
    public interface IGameController
    {
        event EventHandler Terminated;

        void RunLocal([NotNull] GameInitData initData);
        void RunRemote([NotNull] string playerName, [NotNull] string remoteHost);
        void Terminate();
    }

    public class GameController : IGameController, IDisposable
    {
        public const string LocalPlayerName = "Player";

        private readonly IUnityContainer _container;
        private readonly INavigationService _navigationService;
        private readonly IGameWindow _gameWindow;
        private readonly SitRepDialog _sitRepDialog;
        private readonly IAppContext _appContext;
        private readonly IPlayerOrderService _playerOrderService;
        private readonly Dictionary<EventBase, SubscriptionToken> _eventSubscriptionTokens;
        private readonly List<IPresenter> _screenPresenters;
        private readonly DelegateCommand<object> _endTurnCommand;
        private readonly DelegateCommand<object> _showEndOfTurnSummaryCommand;
        private readonly Dispatcher _dispatcher;
        private IDisposable _connectWaitCursorHandle;
        private IDisposable _gameStartWaitCursorHandle;
        private IDisposable _turnWaitCursorHandle;
        private IGameClient _client;
        private IGameServer _server;
        private GameOptions _gameOptions;
        private bool _suppressClientEvents;
        private bool _isServerLocal;
        private bool _lobbyScreenShown;
        private bool _isDisposed;
        private bool _firstTurnStarted;

        public GameController(
            [NotNull] IUnityContainer container,
            [NotNull] INavigationService navigationService,
            [NotNull] IGameWindow gameWindow,
            [NotNull] IRegionManager regionManager,
            [NotNull] IRegionViewRegistry regionViewRegistry,
            [NotNull] IAppContext appContext,
            [NotNull] IGameClient client,
            [NotNull] IPlayerOrderService playerOrderService)
        {
            if (container == null)
                throw new ArgumentNullException("container");
            if (navigationService == null)
                throw new ArgumentNullException("navigationService");
            if (gameWindow == null)
                throw new ArgumentNullException("gameWindow");
            if (regionManager == null)
                throw new ArgumentNullException("regionManager");
            if (regionViewRegistry == null)
                throw new ArgumentNullException("regionViewRegistry");
            if (appContext == null)
                throw new ArgumentNullException("appContext");
            if (client == null)
                throw new ArgumentNullException("client");
            if (playerOrderService == null)
                throw new ArgumentNullException("playerOrderService");

            _container = container;
            _navigationService = navigationService;
            _gameWindow = gameWindow;
            _sitRepDialog = container.Resolve<SitRepDialog>();
            _appContext = appContext;
            _client = client;
            _playerOrderService = playerOrderService;
            _endTurnCommand = new DelegateCommand<object>(ExecuteTurnCommand) { IsActive = false };
            _showEndOfTurnSummaryCommand = new DelegateCommand<object>(ExecuteShowEndOfTurnSummaryCommand) { IsActive = true };
            _eventSubscriptionTokens = new Dictionary<EventBase, SubscriptionToken>();
            _screenPresenters = new List<IPresenter>();
            _playerOrderService.ClearOrders();
            _dispatcher = Dispatcher.CurrentDispatcher;
        }

        private void ExecuteShowEndOfTurnSummaryCommand(object obj)
        {
            ShowSummary(true);
        }

        private static void ExecuteTurnCommand(object obj)
        {
            ClientEvents.TurnEnded.Publish(ClientEventArgs.Default);
        }

        #region Implementation of IGameController
        public event EventHandler Terminated;

        public void RunLocal([NotNull] GameInitData initData)
        {
            if (initData == null)
                throw new ArgumentNullException("initData");

            CheckDisposed();

            _isServerLocal = true;

            _dispatcher.Invoke(
                (Action)SetConnectWaitCursor,
                DispatcherPriority.Normal);

            HookCommandAndEventHandlers();

            try
            {
                if (initData.Options == null)
                    initData.Options = _container.Resolve<GameOptions>();

                _gameOptions = initData.Options;

                StartServer(initData.IsMultiplayerGame);

                Connect(() => _client.HostAndConnect(initData, "localhost"));
            }
            catch
            {
                UnhookCommandAndEventHandlers();

                _dispatcher.Invoke(
                    (Action)ClearWaitCursors,
                    DispatcherPriority.Normal);

                throw;
            }

        }

        private void SetConnectWaitCursor()
        {
            var handle = _gameWindow.EnterWaitCursorScope();

            if (Interlocked.CompareExchange(ref _connectWaitCursorHandle, handle, null) != null)
                handle.Dispose();
        }

        private void UnhookCommandAndEventHandlers()
        {
            ClientCommands.EndTurn.UnregisterCommand(_endTurnCommand);
            ClientCommands.ShowEndOfTurnSummary.UnregisterCommand(_showEndOfTurnSummaryCommand);
            ClientEvents.InvasionUpdateReceived.Unsubscribe(OnInvasionUpdateReceived);

            lock (_eventSubscriptionTokens)
            {
                foreach (var subscribedEvent in _eventSubscriptionTokens.Keys)
                    subscribedEvent.Unsubscribe(_eventSubscriptionTokens[subscribedEvent]);

                _eventSubscriptionTokens.Clear();
            }
        }

        private void OnLocalPlayerJoined(LocalPlayerJoinedEventArgs args)
        {
            if (_lobbyScreenShown)
                return;

            SubscriptionToken subscriptionToken;

            if (_eventSubscriptionTokens.TryGetValue(ClientEvents.LocalPlayerJoined, out subscriptionToken))
                ClientEvents.LocalPlayerJoined.Unsubscribe(subscriptionToken);

            if (!_appContext.IsSinglePlayerGame)
                _navigationService.ActivateScreen(StandardGameScreens.MultiplayerLobby);

            _lobbyScreenShown = true;

            ClearConnectWaitCursor();
        }

        private void OnTerminated()
        {
            var handler = Terminated;
            if (handler != null)
                handler(this, EventArgs.Empty);
        }

        private void HookCommandAndEventHandlers()
        {
            ClientCommands.EndTurn.RegisterCommand(_endTurnCommand);
            ClientCommands.ShowEndOfTurnSummary.RegisterCommand(_showEndOfTurnSummaryCommand);
            ClientEvents.InvasionUpdateReceived.Subscribe(OnInvasionUpdateReceived, ThreadOption.UIThread);

            lock (_eventSubscriptionTokens)
            {
                var subscriptionToken = ClientEvents.LocalPlayerJoined.Subscribe(
                    OnLocalPlayerJoined,
                    ThreadOption.UIThread);

                _eventSubscriptionTokens[ClientEvents.LocalPlayerJoined] = subscriptionToken;

                subscriptionToken = ClientEvents.GameStarting.Subscribe(
                    OnGameStarting,
                    ThreadOption.UIThread);

                _eventSubscriptionTokens[ClientEvents.GameStarting] = subscriptionToken;

                subscriptionToken = ClientEvents.GameStarted.Subscribe(
                    OnGameStarted,
                    ThreadOption.UIThread);

                _eventSubscriptionTokens[ClientEvents.GameStarted] = subscriptionToken;

                subscriptionToken = ClientEvents.GameEnded.Subscribe(
                    OnGameEnded,
                    ThreadOption.UIThread);

                _eventSubscriptionTokens[ClientEvents.GameEnded] = subscriptionToken;

                subscriptionToken = ClientEvents.TurnStarted.Subscribe(
                    OnTurnStarted,
                    ThreadOption.UIThread);

                _eventSubscriptionTokens[ClientEvents.TurnStarted] = subscriptionToken;

                subscriptionToken = ClientEvents.TurnEnded.Subscribe(
                    OnTurnEnded,
                    ThreadOption.UIThread);

                _eventSubscriptionTokens[ClientEvents.TurnEnded] = subscriptionToken;

                subscriptionToken = ClientEvents.AllTurnEnded.Subscribe(
                    OnAllTurnEnded,
                    ThreadOption.UIThread);

                _eventSubscriptionTokens[ClientEvents.AllTurnEnded] = subscriptionToken;
            }
        }

        private void OnInvasionUpdateReceived(ClientDataEventArgs<InvasionArena> e)
        {
            var presenter = _container.Resolve<ViewModelPresenter<SystemAssaultScreenViewModel, ISystemAssaultScreenView>>();
            if (presenter.Model.IsRunning)
                return;

            presenter.Model.ProcessUpdate(e.Value);
            presenter.Run();
        }

        private void OnGameEnded(EventArgs t)
        {
            Terminate();
        }

        private void OnTurnEnded(EventArgs t)
        {
            _endTurnCommand.IsActive = false;
        }

        private void OnAllTurnEnded(EventArgs t)
        {
            SetTurnWaitCursor();
        }

        private void OnGameStarting(EventArgs t)
        {
            SetGameStartWaitCursor();
        }

        private void SetGameStartWaitCursor()
        {
            var handle = _gameWindow.EnterWaitCursorScope();
            if (Interlocked.CompareExchange(ref _gameStartWaitCursorHandle, handle, null) != null)
                handle.Dispose();
        }

        private void SetTurnWaitCursor()
        {
            var handle = _gameWindow.EnterWaitCursorScope();
            if (Interlocked.CompareExchange(ref _turnWaitCursorHandle, handle, null) != null)
                handle.Dispose();
        }

        private void OnTurnStarted(EventArgs args)
        {
            var currentGame = _appContext.CurrentGame;
            if (currentGame == null)
                return;
            
            ClientEvents.ScreenRefreshRequired.Publish(ClientEventArgs.Default);
            
            if (!_firstTurnStarted)
            {
                _firstTurnStarted = true;
                _navigationService.ActivateScreen(StandardGameScreens.GalaxyScreen);
                ClearGameStartWaitCursor();
            }

            foreach (var infoCardSubject in InfoCardService.Current.InfoCards.Select(o => o.Subject).Where(o => o != null))
                infoCardSubject.RefreshData();

            ClearTurnWaitCursor();

            _endTurnCommand.IsActive = true;

            ProcessSitRepEntries();
        }

        private void ProcessSitRepEntries()
        {
            if (_appContext.LocalPlayerEmpire.SitRepEntries.Count <= 0)
                return;

            foreach (var sitRepEntry in _appContext.LocalPlayerEmpire.SitRepEntries)
            {
                GameLog.Print("###################### SUMMARY: {0}", sitRepEntry.SummaryText);
                if (sitRepEntry.HasDetails && ClientSettings.Current.EnableCombatScreen)   // only show Detail_Dialog if also CombatScreen are shown (if not, a quicker game is possible)
                    SitRepDetailDialog.Show(sitRepEntry);
            }

            ShowSummary(false);
        }

        private void ShowSummary(bool showIfEmpty)
        {
            if (!_appContext.IsGameInPlay)
                return;

            _sitRepDialog.SitRepEntries = _appContext.LocalPlayerEmpire.SitRepEntries;

            var service = ServiceLocator.Current.GetInstance<IPlayerOrderService>();

            if (showIfEmpty)
                _sitRepDialog.Show();
            else if (!service.AutoTurn)
            {
                // works but doubled
                if (ClientSettings.Current.EnableCombatScreen == false)   // only show SUMMARY if also CombatScreen are shown (if not, a quicker game is possible)
                {
                    GameLog.Print("################ Setting EnableCombatScreen = {0} - SUMMARY not shown at false - just click manually to SUMMARY if you want", ClientSettings.Current.EnableCombatScreen.ToString());
                    _sitRepDialog.ShowIfAnyVisibleEntries();
                }
            }
        }

        private void ClearTurnWaitCursor()
        {
            var handle = Interlocked.Exchange(ref _turnWaitCursorHandle, null);
            if (handle != null)
                handle.Dispose();
        }

        private void ClearWaitCursors()
        {
            ClearTurnWaitCursor();
            ClearGameStartWaitCursor();
            ClearConnectWaitCursor();
        }

        private void ClearConnectWaitCursor()
        {
            var handle = Interlocked.Exchange(ref _connectWaitCursorHandle, null);
            if (handle != null)
                handle.Dispose();
        }

        private void ClearGameStartWaitCursor()
        {
            var handle = Interlocked.Exchange(ref _gameStartWaitCursorHandle, null);
            if (handle != null)
                handle.Dispose();
        }

        private void OnGameStarted(DataEventArgs<GameStartData> args)
        {
            CreatePresenters();
        }

        private void CreatePresenters()
        {
            var initializedPresenters = new List<IPresenter>();

            var _CreatePresenterTracing = false;

            if (_CreatePresenterTracing)
                GameLog.Print("BEGINNING: CreatePresenters");

            try
            {
                _screenPresenters.Add(_container.Resolve<IGalaxyScreenPresenter>());
                if (_CreatePresenterTracing)
                    GameLog.Print("DONE: IGalaxyScreenPresenter");

                _screenPresenters.Add(_container.Resolve<IColonyScreenPresenter>());
                if (_CreatePresenterTracing)
                    GameLog.Print("DONE: IColonyScreenPresenter");

                _screenPresenters.Add(_container.Resolve<ViewModelPresenter<DiplomacyScreenViewModel, INewDiplomacyScreenView>>());
                if (_CreatePresenterTracing)
                    GameLog.Print("DONE: INewDiplomacyScreenView");

                _screenPresenters.Add(_container.Resolve<IScienceScreenPresenter>());
                if (_CreatePresenterTracing)
                    GameLog.Print("DONE: IScienceScreenPresenter");

                _screenPresenters.Add(_container.Resolve<IAssetsScreenPresenter>());
                if (_CreatePresenterTracing)
                    GameLog.Print("DONE: IAssetsScreenPresenter");

                foreach (var presenter in _screenPresenters)
                {
                    try
                    { 
                        presenter.Run();
                        initializedPresenters.Add(presenter);
                        if (_CreatePresenterTracing)
                            GameLog.Print("DONE: {0}", presenter.ToString());
                    }
                    catch (Exception e) //ToDo: Just log or additional handling necessary?
                    {
                        GameLog.Print("###### problem with {0}", presenter.ToString());
                        GameLog.LogException(e);
                        throw;
                    }
                }
            }
            catch
            {
                _screenPresenters.Clear();
                foreach (var presenter in initializedPresenters)
                {
                    try
                    {
                        GameLog.Print("###### problem with Terminate Presenter {0}", presenter.ToString());
                        presenter.Terminate();
                    }
                    catch (Exception e) //ToDo: Just log or additional handling necessary?
                    {
                        GameLog.Print("###### problem with {0}", presenter.ToString());
                        GameLog.LogException(e);
                    }
                }
                throw;
            }
        }

        protected void CheckDisposed()
        {
            if (_isDisposed)
                throw new ObjectDisposedException("GameClient");
        }

        protected void StartServer(bool allowRemoteConnections)
        {
            CheckDisposed();
            try
            {
                _server = _container.Resolve<IGameServer>();
                _server.Faulted += OnServerFaulted;
                _server.Start(_gameOptions, allowRemoteConnections);
            }
            catch
            {
                StopServer();
                ClientEvents.ServerInitializationFailed.Publish(new ClientEventArgs(_appContext));
                throw;
            }
        }

        private void OnServerFaulted(EventArgs t)
        {
            try
            {
                Disconnect();
            }
            catch (Exception e) //ToDo: Just log or additional handling necessary?
            {
                GameLog.LogException(e);
            }

            try
            {
                Dispose();
            }
            catch (Exception e) //ToDo: Just log or additional handling necessary?
            {
                GameLog.LogException(e);
            }
        }

        protected void StopServer()
        {
            var server = Interlocked.Exchange(ref _server, null);
            if (server == null)
                return;
            server.Faulted -= OnServerFaulted;
            if (server.IsRunning)
            {
                try
                {
                    server.Stop();
                }
                catch (Exception e) //ToDo: Just log or additional handling necessary?
                {
                    GameLog.LogException(e);
                }
            }

            try
            {
                server.Dispose();
            }
            catch (Exception e) //ToDo: Just log or additional handling necessary?
            {
                GameLog.LogException(e);
            }
        }

        private void Connect(Action connectAction)
        {
            if (connectAction == null)
                throw new ArgumentNullException("connectAction");

            var client = Interlocked.CompareExchange(ref _client, null, null);
            if (client == null)
                return;

            HookClientEventHandlers(client);

            try
            {
                connectAction();
            }
            catch
            {
                UnhookClientEventHandlers(client);
                UnhookCommandAndEventHandlers();
                _dispatcher.Invoke(
                    (Action)ClearWaitCursors,
                    DispatcherPriority.Normal);
                Interlocked.Exchange(ref _client, null);
                ClientEvents.ClientConnectionFailed.Publish(ClientEventArgs.Default);
                Terminate();
            }
        }

        protected void HookClientEventHandlers(IGameClient client)
        {
            if (client == null)
                return;
            client.Connected += OnClientConnected;
            client.Disconnected += OnClientDisconnected;
        }

        protected void UnhookClientEventHandlers(IGameClient client)
        {
            if (client == null)
                return;
            client.Connected -= OnClientConnected;
            client.Disconnected -= OnClientDisconnected;
        }

        // ReSharper disable MemberCanBeMadeStatic
        private void OnClientDisconnected(ClientDataEventArgs<ClientDisconnectReason> args)
        {
            if (_suppressClientEvents)
                return;
            _suppressClientEvents = true;
            ClientEvents.ClientDisconnected.Publish(args);
        }

        private void OnClientConnected(EventArgs args)
        {
            if (_suppressClientEvents)
                return;
            ClientEvents.ClientConnected.Publish(new ClientConnectedEventArgs(_isServerLocal));
        }
        // ReSharper restore MemberCanBeMadeStatic

        private void Disconnect()
        {
            var client = Interlocked.Exchange(ref _client, null);
            if (client == null)
                return;

            UnhookClientEventHandlers(client);

            try
            {
                client.Disconnect();
            }
            catch (Exception e) //ToDo: Just log or additional handling necessary?
            {
                GameLog.LogException(e);
            }

            if (!_suppressClientEvents)
                ClientEvents.ClientDisconnected.Publish(new ClientDataEventArgs<ClientDisconnectReason>(ClientDisconnectReason.Disconnected));
        }

        public void RunRemote([NotNull] string playerName, [NotNull] string remoteHost)
        {
            if (playerName == null)
                throw new ArgumentNullException("playerName");
            if (remoteHost == null)
                throw new ArgumentNullException("remoteHost");

            CheckDisposed();

            _dispatcher.Invoke(
                    (Action)SetConnectWaitCursor,
                    DispatcherPriority.Normal);

            HookCommandAndEventHandlers();

            try
            {
                try
                {
                    Connect(() => _client.Connect(playerName, remoteHost));
                }
                catch
                {
                    try
                    {
                        Terminate();
                    }
                    catch (Exception e) //ToDo: Just log or additional handling necessary?
                    {
                        GameLog.LogException(e);
                    }

                    ClientEvents.ClientConnectionFailed.Publish(ClientEventArgs.Default);
                }
            }
            catch
            {
                UnhookCommandAndEventHandlers();
                _dispatcher.Invoke(
                    (Action)ClearWaitCursors,
                    DispatcherPriority.Normal);
                throw;
            }
        }

        public void Terminate()
        {
            Dispose();
        }
        #endregion

        #region Implementation of IDisposable
        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;

            if (_appContext.IsGameInPlay)
            {
                try
                {
                    ClientEvents.GameEnding.Publish(ClientEventArgs.Default);
                }
                catch (Exception e) //ToDo: Just log or additional handling necessary?
                {
                    GameLog.LogException(e);
                }
            }

            _navigationService.ActivateScreen(StandardGameScreens.MenuScreen);

            ClearScreenViews();

            try
            {
                Disconnect();
                StopServer();
            }
            finally
            {
                _dispatcher.Invoke((Action)ClearWaitCursors);
            }

            UnhookCommandAndEventHandlers();

            _dispatcher.BeginInvoke((Action)OnTerminated);
        }

        private void ClearScreenViews()
        {
            foreach (var presenter in _screenPresenters)
            {
                try
                {
                    presenter.Terminate();
                }
                catch (Exception e) //ToDo: Just log or additional handling necessary?
                {
                    GameLog.LogException(e);
                }
            }
            _screenPresenters.Clear();
        }
        #endregion
    }
}