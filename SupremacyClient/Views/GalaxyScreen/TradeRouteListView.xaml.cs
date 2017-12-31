using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Supremacy.Annotations;
using Supremacy.Client.Commands;
using Supremacy.Resources;
using Supremacy.Universe;
using Supremacy.Client.Context;

namespace Supremacy.Client.Views
{
    public partial class TradeRouteListView
    {
        private readonly IAppContext _appContext;
        private readonly IResourceManager _resourceManager;

        #region Constructors and Finalizers
        public TradeRouteListView([NotNull] IAppContext appContext, [NotNull] IResourceManager resourceManager)
        {
            if (appContext == null)
                throw new ArgumentNullException("appContext");
            if (resourceManager == null)
                throw new ArgumentNullException("resourceManager");

            _appContext = appContext;
            _resourceManager = resourceManager;

            InitializeComponent();
        }
        #endregion

        protected override void OnContextMenuOpening(ContextMenuEventArgs e)
        {
            var mouseTarget = InputHitTest(Mouse.GetPosition(this)) as DependencyObject;
            var targetListViewItem = UIHelpers.FindVisualAncestorByType<ListBoxItem>(mouseTarget);
            if (targetListViewItem == null)
            {
                e.Handled = true;
                return;
            }

            var tradeRoute = targetListViewItem.DataContext as TradeRoute;
            if (tradeRoute == null)
            {
                e.Handled = true;
                return;
            }

            PopulateTradeRouteMenu(tradeRoute);

            base.OnContextMenuOpening(e);
        }

        protected override void OnMouseRightButtonDown(MouseButtonEventArgs e)
        {
            var mouseTarget = InputHitTest(Mouse.GetPosition(this)) as DependencyObject;
            var targetListViewItem = UIHelpers.FindVisualAncestorByType<ListBoxItem>(mouseTarget);
            if (targetListViewItem != null)
            {
                e.Handled = true;
                return;
            }
            base.OnMouseRightButtonDown(e);
        }

        private void PopulateTradeRouteMenu(TradeRoute tradeRoute)
        {
            if (tradeRoute == null)
                throw new ArgumentNullException("tradeRoute");

            var contextMenu = ContextMenu;
            if (contextMenu != null)
                contextMenu.Items.Clear();

            if (tradeRoute.SourceColony.OwnerID != _appContext.LocalPlayer.EmpireID)
                return;

            if (contextMenu == null)
            {
                contextMenu = new ContextMenu();
                ContextMenu = contextMenu;
            }

            contextMenu.Items.Add(
                new MenuItem
                {
                    Header = _resourceManager.GetString("Cancel trade route"),
                    CommandParameter = tradeRoute,
                    Command = GalaxyScreenCommands.CancelTradeRoute
                });
        }
    }
}