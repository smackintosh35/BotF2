using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using Microsoft.Practices.Composite.Presentation.Events;
using Microsoft.Practices.Unity;

using Supremacy.Annotations;
using Supremacy.Buildings;
using Supremacy.Client.Commands;
using Supremacy.Client.Dialogs;
using Supremacy.Client.Events;
using Supremacy.Client.Input;
using Supremacy.Economy;

using System.Linq;

using Supremacy.Game;
using Supremacy.Orbitals;
using Supremacy.Tech;
using Supremacy.Universe;

using CompositeRegionManager = Microsoft.Practices.Composite.Presentation.Regions.RegionManager;
using Supremacy.Utility;
using System.Collections.Generic;

using Wintellect.PowerCollections;

namespace Supremacy.Client.Views
{
    public class ColonyScreenPresenter : GameScreenPresenterBase<ColonyScreenPresentationModel, IColonyScreenView>,
                                         IColonyScreenPresenter
    {
        private readonly DelegateCommand<BuildProject> _addToPlanetaryBuildQueueCommand;
        private readonly DelegateCommand<BuildProject> _addToShipyardBuildQueueCommand;
        //private readonly DelegateCommand<BuildProject> _addToIntelyardBuildQueueCommand;
        private readonly DelegateCommand<BuildQueueItem> _removeFromPlanetaryBuildQueueCommand;
        private readonly DelegateCommand<BuildQueueItem> _removeFromShipyardBuildQueueCommand;
        //private readonly DelegateCommand<BuildQueueItem> _removeFromIntelyardBuildQueueCommand;
        private readonly DelegateCommand<BuildProject> _cancelBuildProjectCommand;
        private readonly DelegateCommand<BuildProject> _buyBuildProjectCommand;
        private readonly DelegateCommand<ProductionCategory> _activateFacilityCommand;
        private readonly DelegateCommand<ProductionCategory> _deactivateFacilityCommand;
        private readonly DelegateCommand<ProductionCategory> _scrapFacilityCommand;
        private readonly DelegateCommand<ProductionCategory> _unscrapFacilityCommand;
        private readonly DelegateCommand<object> _toggleBuildingScrapCommand;
        private readonly DelegateCommand<Building> _toggleBuildingIsActiveCommand;
        private readonly DelegateCommand<ShipyardBuildSlot> _toggleShipyardBuildSlotCommand;
        //private readonly DelegateCommand<IntelyardBuildSlot> _toggleIntelyardBuildSlotCommand;
        private readonly DelegateCommand<ShipyardBuildSlot> _selectShipBuildProjectCommand;
        //private readonly DelegateCommand<IntelyardBuildSlot> _selectBuildIntelProjectCommand;
        private readonly DelegateCommand<Sector> _selectSectorCommand;
        private readonly DelegateCommand<object> _previousColonyCommand;
        private readonly DelegateCommand<object> _nextColonyCommand;

        private GameObjectID _newColonySelection;

        #region Constructors and Finalizers
        public ColonyScreenPresenter(
            [NotNull] IUnityContainer container,
            [NotNull] ColonyScreenPresentationModel model,
            [NotNull] IColonyScreenView view) : base(container, model, view)
        {
            _addToPlanetaryBuildQueueCommand = new DelegateCommand<BuildProject>(
                ExecuteAddToPlanetaryBuildQueueCommand,
                CanExecuteAddToPlanetaryBuildQueueCommand);

            _addToShipyardBuildQueueCommand = new DelegateCommand<BuildProject>(
                ExecuteAddToShipyardBuildQueueCommand,
                CanExecuteAddToShipyardBuildQueueCommand);

            //_addToIntelyardBuildQueueCommand = new DelegateCommand<BuildProject>(
            //    this.ExecuteAddToIntelyardBuildQueueCommand,
            //    this.CanExecuteAddToIntelyardBuildQueueCommand);

            _removeFromPlanetaryBuildQueueCommand = new DelegateCommand<BuildQueueItem>(
                ExecuteRemoveFromPlanetaryBuildQueueCommand,
                CanExecuteRemoveFromPlanetaryBuildQueueCommand);

            _removeFromShipyardBuildQueueCommand = new DelegateCommand<BuildQueueItem>(
                ExecuteRemoveFromShipyardBuildQueueCommand,
                CanExecuteRemoveFromShipyardBuildQueueCommand);

            //_removeFromIntelyardBuildQueueCommand = new DelegateCommand<BuildQueueItem>(
            //    this.ExecuteRemoveFromIntelyardBuildQueueCommand,
            //    this.CanExecuteRemoveFromIntelyardBuildQueueCommand);

            _cancelBuildProjectCommand = new DelegateCommand<BuildProject>(
                ExecuteCancelBuildProjectCommand,
                CanExecuteCancelBuildProjectCommand);

            _buyBuildProjectCommand = new DelegateCommand<BuildProject>(
                ExecuteBuyBuildProjectCommand,
                CanExecuteBuyBuildProjectCommand);

            _activateFacilityCommand = new DelegateCommand<ProductionCategory>(
                ExecuteActivateFacilityCommand,
                CanExecuteActivateFacilityCommand);

            _deactivateFacilityCommand = new DelegateCommand<ProductionCategory>(
                ExecuteDeactivateFacilityCommand,
                CanExecuteDeactivateFacilityCommand);

            _scrapFacilityCommand = new DelegateCommand<ProductionCategory>(
                ExecuteScrapFacilityCommand,
                CanExecuteScrapFacilityCommand);

            _unscrapFacilityCommand = new DelegateCommand<ProductionCategory>(
                ExecuteUnscrapFacilityCommand,
                CanExecuteUnscrapFacilityCommand);

            _toggleBuildingScrapCommand = new DelegateCommand<object>(
                ExecuteToggleBuildingScrapCommand,
                CanExecuteToggleBuildingScrapCommand);

            _toggleBuildingIsActiveCommand = new DelegateCommand<Building>(
                ExecuteToggleBuildingIsActiveCommand,
                CanExecuteToggleBuildingIsActiveCommand);
            
            _toggleShipyardBuildSlotCommand = new DelegateCommand<ShipyardBuildSlot>(
                ExecuteToggleShipyardBuildSlotCommand,
                CanExecuteToggleShipyardBuildSlotCommand);

            //_toggleIntelyardBuildSlotCommand = new DelegateCommand<IntelyardBuildSlot>(
            //    this.ExecuteToggleIntelyardBuildSlotCommand,
            //    this.CanExecuteToggleIntelyardBuildSlotCommand);

            _selectShipBuildProjectCommand = new DelegateCommand<ShipyardBuildSlot>(
                ExecuteSelectShipBuildProjectCommand,
                CanExecuteSelectShipBuildProjectCommand);

            //_selectBuildIntelProjectCommand = new DelegateCommand<IntelyardBuildSlot>(
            //    this.ExecuteSelectBuildIntelProjectCommand,
            //    this.CanExecuteSelectBuildIntelProjectCommand);

            _selectSectorCommand = new DelegateCommand<Sector>(
                sector =>
                {
                    var system = sector.System;
                    if (system == null)
                        return;

                    var colony = system.Colony;
                    if (colony == null || colony.OwnerID != AppContext.LocalPlayer.EmpireID)
                        return;

                    _newColonySelection = colony.ObjectID;
                });

            _previousColonyCommand = new DelegateCommand<object>(ExecutePreviousColonyCommand);
            _nextColonyCommand = new DelegateCommand<object>(ExecuteNextColonyCommand);
        }

        private void ExecutePreviousColonyCommand(object _)
        {
            var colonies = Model.Colonies.ToList();
            var currentColony = Model.SelectedColony;

            var currentColonyIndex = colonies.IndexOf(currentColony);
            if (currentColonyIndex <= 0)
            {
                if (colonies.Count == 0)
                    return;

                Model.SelectedColony = colonies[colonies.Count - 1];
            }
            else
            {
                Model.SelectedColony = colonies[currentColonyIndex - 1];
            }
        }

        private void ExecuteNextColonyCommand(object _)
        {
            var colonies = Model.Colonies.ToList();
            var currentColony = Model.SelectedColony;

            var currentColonyIndex = colonies.IndexOf(currentColony);
            if ((currentColonyIndex == (colonies.Count - 1)) || (currentColonyIndex < 0))
                Model.SelectedColony = colonies[0];
            else
                Model.SelectedColony = colonies[currentColonyIndex + 1];
        }

        protected override void OnViewActivating()
        {
            var newColonySelection = _newColonySelection;
            if (!newColonySelection.IsValid)
            {
                Model.SelectedColony = AppContext.LocalPlayerEmpire.SeatOfGovernment;
                return;
            }
            Model.SelectedColony = AppContext.CurrentGame.Universe.Objects[newColonySelection] as Colony;
        }

        private bool CanExecuteToggleBuildingIsActiveCommand(Building building)
        {
            return (Model.SelectedColony != null);
        }

        private void ExecuteToggleBuildingIsActiveCommand(Building building)
        {
            var colony = Model.SelectedColony;
            if (colony == null)
                return;

            if (building.IsActive)
                colony.DeactivateBuilding(building);
            else
                colony.ActivateBuilding(building);

            PlayerOrderService.AddOrder(new UpdateBuildingOrder(building));
        }

        private bool CanExecuteToggleShipyardBuildSlotCommand(ShipyardBuildSlot buildSlot)
        {
            if (buildSlot == null)
                return false;

            var colony = Model.SelectedColony;
            if (colony == null || colony.Shipyard != buildSlot.Shipyard)
                return false;

            return true;
        }

        private void ExecuteToggleShipyardBuildSlotCommand(ShipyardBuildSlot buildSlot)
        {
            if (buildSlot == null)
                return;

            var colony = Model.SelectedColony;
            if (colony == null || colony.Shipyard != buildSlot.Shipyard)
                return;

            if (buildSlot.IsActive)
                colony.DeactivateShipyardBuildSlot(buildSlot);
            else
                colony.ActivateShipyardBuildSlot(buildSlot);

            PlayerOrderService.AddOrder(new ToggleShipyardBuildSlotOrder(buildSlot));
        }
        
        private bool CanExecuteSelectShipBuildProjectCommand(ShipyardBuildSlot buildSlot)
        {
            if (buildSlot == null)
                return false;

            var colony = Model.SelectedColony;
            if (colony == null || colony.Shipyard != buildSlot.Shipyard)
                return false;

            return buildSlot.IsActive && !buildSlot.HasProject;
        }

        private void ExecuteSelectShipBuildProjectCommand(ShipyardBuildSlot buildSlot)
        {
            if (buildSlot == null)
                return;

            var colony = Model.SelectedColony;
            if (colony == null || colony.Shipyard != buildSlot.Shipyard)
                return;

            if (!buildSlot.IsActive || buildSlot.HasProject)
                return;

            var view = new NewShipSelectionView(buildSlot);
            //var viewIntel = new NewIntelSelectionView(buildSlot);
            var statsViewModel = new TechObjectDesignViewModel();

            BindingOperations.SetBinding(
                statsViewModel,
                TechObjectDesignViewModel.DesignProperty,
                new Binding
                {
                    Source = view,
                    Path = new PropertyPath("SelectedBuildProject.BuildDesign")
                });

            view.AdditionalContent = statsViewModel;

            var result = view.ShowDialog();

            if (!result.HasValue || !result.Value)
                return;

            var project = view.SelectedBuildProject;
            if (project == null)
                return;

            buildSlot.Project = project;
            
            PlayerOrderService.AddOrder(new UpdateProductionOrder(buildSlot.Shipyard));
        }
        ////private bool CanExecuteToggleIntelyardBuildSlotCommand(IntelyardBuildSlot buildSlot)
        ////{
        ////    if (buildSlot == null)
        ////        return false;

        ////    var colony = this.Model.SelectedColony;
        ////    if (colony == null || colony.Intelyard != buildSlot.Intelyard)
        ////        return false;

        ////    return true;
        ////}
        //private void ExecuteToggleIntelyardBuildSlotCommand(IntelyardBuildSlot buildSlot)
        //{
        //    if (buildSlot == null)
        //        return;

        //    var colony = this.Model.SelectedColony;
        //    if (colony == null || colony.Intelyard != buildSlot.Intelyard)
        //        return;

        //    if (buildSlot.IsActive)
        //        colony.DeactivateIntelyardBuildSlot(buildSlot);
        //    else
        //        colony.ActivateIntelyardBuildSlot(buildSlot);

        //    this.PlayerOrderService.AddOrder(new ToggleIntelyardBuildSlotOrder(buildSlot));
        //}

        //private bool CanExecuteSelectBuildIntelProjectCommand(IntelyardBuildSlot buildSlot)
        //{
        //    if (buildSlot == null)
        //        return false;

        //    var colony = this.Model.SelectedColony;
        //    if (colony == null || colony.Intelyard != buildSlot.Intelyard)
        //        return false;

        //    return buildSlot.IsActive && !buildSlot.HasProject;
        //}

        //private void ExecuteSelectBuildIntelProjectCommand(IntelyardBuildSlot buildSlot)
        //{
        //    if (buildSlot == null)
        //        return;

        //    var colony = this.Model.SelectedColony;
        //    if (colony == null || colony.Intelyard != buildSlot.Intelyard)
        //        return;

        //    if (!buildSlot.IsActive || buildSlot.HasProject)
        //        return;

        //    //var view = new NewIntelSelectionView(buildSlot);
        //    var view= new NewIntelSelectionView(buildSlot);
        //    var statsViewModel = new TechObjectDesignViewModel();

        //    BindingOperations.SetBinding(
        //        statsViewModel,
        //        TechObjectDesignViewModel.DesignProperty,
        //        new Binding
        //        {
        //            Source = view,
        //            Path = new PropertyPath("SelectedBuildProject.BuildDesign")
        //        });

        //    view.AdditionalContent = statsViewModel;

        //    var result = view.ShowDialog();

        //    if (!result.HasValue || !result.Value)
        //        return;

        //    var project = view.SelectedBuildProject;
        //    if (project == null)
        //        return;

        //    buildSlot.Project = project;

        //    this.PlayerOrderService.AddOrder(new UpdateProductionOrder(buildSlot.Intelyard));
        //}
        private bool CanExecuteToggleBuildingScrapCommand(object parameter)
        {
            var checkableParameter = parameter as ICheckableCommandParameter;
            if (checkableParameter != null)
            {
                var building = checkableParameter.InnerParameter as Building;
                if (building == null)
                {
                    checkableParameter.IsChecked = false;
                    return false;
                }
                checkableParameter.IsChecked = building.Scrap;
                checkableParameter.Handled = true;
            }
            else if (!(parameter is Building))
            {
                return false;
            }
            return (Model.SelectedColony != null);
        }

        private void ExecuteToggleBuildingScrapCommand(object parameter)
        {
            var building = parameter as Building;
            if (building != null)
            {
                building.Scrap = !building.Scrap;
            }
            else
            {
                var checkableParameter = parameter as ICheckableCommandParameter;
                if (checkableParameter == null)
                    return;
                
                building = checkableParameter.InnerParameter as Building;
                if (building == null)
                    return;

                checkableParameter.IsChecked = (building.Scrap = !building.Scrap);
                checkableParameter.Handled = true;
            }

            PlayerOrderService.AddOrder(new UpdateBuildingOrder(building));
        }

        private bool CanExecuteUnscrapFacilityCommand(ProductionCategory category)
        {
            return (Model.SelectedColony != null);
        }

        private void ExecuteUnscrapFacilityCommand(ProductionCategory category)
        {
            var colony = Model.SelectedColony;
            if (colony == null)
                return;

            var facilitiesToScrap = colony.GetScrappedFacilities(category);
            if (facilitiesToScrap == 0)
                return;

            colony.SetScrappedFacilities(category, --facilitiesToScrap);

            PlayerOrderService.AddOrder(new FacilityScrapOrder(colony));
        }

        private bool CanExecuteScrapFacilityCommand(ProductionCategory category)
        {
            return (Model.SelectedColony != null);
        }

        private void ExecuteScrapFacilityCommand(ProductionCategory category)
        {
            var colony = Model.SelectedColony;
            if (colony == null)
                return;

            var facilitiesToScrap = colony.GetScrappedFacilities(category);
            if (facilitiesToScrap >= colony.GetTotalFacilities(category))
                return;

            colony.SetScrappedFacilities(category, ++facilitiesToScrap);

            PlayerOrderService.AddOrder(new FacilityScrapOrder(colony));
        }

        private bool CanExecuteDeactivateFacilityCommand(ProductionCategory category)
        {
            return (Model.SelectedColony != null);
        }

        private void ExecuteDeactivateFacilityCommand(ProductionCategory category)
        {
            var colony = Model.SelectedColony;
            if (colony == null)
                return;

            colony.DeactivateFacility(category);

            PlayerOrderService.AddOrder(new SetColonyProductionOrder(colony));
        }

        private bool CanExecuteActivateFacilityCommand(ProductionCategory category)
        {
            return (Model.SelectedColony != null);
        }

        private void ExecuteActivateFacilityCommand(ProductionCategory category)
        {
            var colony = Model.SelectedColony;
            if (colony == null)
                return;

            colony.ActivateFacility(category);

            PlayerOrderService.AddOrder(new SetColonyProductionOrder(colony));
        }

        protected override void RunOverride()
        {
            Model.Colonies = AppContext.LocalPlayerEmpire.Colonies;

            var selectedColony = Model.SelectedColony;
            if (selectedColony == null)
                Model.SelectedColony = AppContext.LocalPlayerEmpire.SeatOfGovernment;
        }

        protected override void TerminateOverride()
        {
            var selectedColony = Model.SelectedColony;
            if (selectedColony != null)
                selectedColony.PropertyChanged -= OnSelectedColonyPropertyChanged;

            Model.Colonies = null;
            Model.SelectedColony = null;
            Model.SelectedPlanetaryBuildProject = null;
            Model.SelectShipBuildProjectCommand = null;
            //this.Model.SelectBuildIntelProjectCommand = null;
        }

        private void OnSelectedColonyPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "NetEnergy" || e.PropertyName == "ActiveOrbitalBatteries")
            {
                UpdateOrbitalBatteries();
            }
            else if (e.PropertyName == "NetIndustry")
            {
                UpdateBuildLists();

                // Update buildItems in colony queue
                var colony = Model.SelectedColony;
                if (colony == null)
                    return;

                foreach (BuildQueueItem item in colony.BuildQueue)
                    item.InvalidateTurnsRemaining();
            }
        }

        private void OnSelectedColonyChanged(object sender, EventArgs args)
        {
            var e = (PropertyChangedRoutedEventArgs<Colony>)args;

            if (!IsRunning)
                return;

            //if (this.Model.Colonies == null)
            //    this.Model.Colonies = this.AppContext.LocalPlayerEmpire.Colonies;

            if (e.OldValue != null)
                e.OldValue.PropertyChanged -= OnSelectedColonyPropertyChanged;

            if (e.NewValue != null)
                e.NewValue.PropertyChanged += OnSelectedColonyPropertyChanged;

            UpdateBuildLists();

            Model.ActiveOrbitalBatteries = (e.NewValue != null) ? e.NewValue.ActiveOrbitalBatteries : 0;

            UpdateOrbitalBatteries();

            var selectedColony = Model.SelectedColony;
            if (selectedColony != null)
            {
                var regionManager = CompositeRegionManager.GetRegionManager((DependencyObject)View);

                if (!regionManager.Regions.ContainsRegionWithName(CommonGameScreenRegions.PlanetsView))
                    CompositeRegionManager.UpdateRegions();

                if (regionManager.Regions.ContainsRegionWithName(CommonGameScreenRegions.PlanetsView))
                {
                    var planetsViewRegion = regionManager.Regions[CommonGameScreenRegions.PlanetsView];
                    planetsViewRegion.Context = selectedColony.Sector;
                }
            }

            InvalidateCommands();
        }

        private void OnActiveOrbitalBatteriesChanged(object sender, EventArgs eventArgs)
        {
            UpdateOrbitalBatteries();
        }

        private bool _updatingOrbitalBatteries;

        private void UpdateOrbitalBatteries()
        {
            if (_updatingOrbitalBatteries)
                return;

            _updatingOrbitalBatteries = true;

            try
            {
                var selectedColony = Model.SelectedColony;
                if (selectedColony == null || selectedColony.OrbitalBatteryDesign == null)
                {
                    Model.ActiveOrbitalBatteries = 0;
                    Model.MaxActiveOrbitalBatteries = 0;
                    return;
                }

                var activeCountDifference = Model.ActiveOrbitalBatteries - selectedColony.ActiveOrbitalBatteries;
                if (activeCountDifference != 0)
                {
                    do
                    {
                        if (activeCountDifference > 0)
                        {
                            if (selectedColony.ActivateOrbitalBattery())
                                --activeCountDifference;
                            else
                                break;
                        }
                        else
                        {
                            if (selectedColony.DeactivateOrbitalBattery())
                                ++activeCountDifference;
                            else
                                break;
                        }
                    }
                    while (activeCountDifference != 0);

                    PlayerOrderService.AddOrder(new UpdateOrbitalBatteriesOrder(selectedColony));
                }

                var maxActiveOrbitalBatteries = selectedColony.ActiveOrbitalBatteries;
                if (selectedColony.NetEnergy > 0)
                {
                    var possibleActivations = selectedColony.NetEnergy / selectedColony.OrbitalBatteryDesign.UnitEnergyCost;
                    if (possibleActivations > 0)
                        maxActiveOrbitalBatteries += possibleActivations;
                }

                Model.MaxActiveOrbitalBatteries = maxActiveOrbitalBatteries;
                Model.ActiveOrbitalBatteries = selectedColony.ActiveOrbitalBatteries;
            }
            finally
            {
                _updatingOrbitalBatteries = false;
            }
        }

        // *** original void UpdateBuildLists() *** old one from Overlord's code
        //private void UpdateBuildLists()
        //{
        //    var selectedColony = this.Model.SelectedColony;
        //    if (selectedColony != null)
        //    {
        //        this.Model.PlanetaryBuildProjects = TechTreeHelper.GetBuildProjects(this.Model.SelectedColony);
        //        if (selectedColony.Shipyard != null)
        //            this.Model.ShipyardBuildProjects = TechTreeHelper.GetShipyardBuildProjects(selectedColony.Shipyard);
        //        else
        //            this.Model.ShipyardBuildProjects = Enumerable.Empty<BuildProject>();
        //    }
        //    else
        //    {
        //        this.Model.PlanetaryBuildProjects = Enumerable.Empty<BuildProject>();
        //    }
        //}



        private void UpdateBuildLists()
        {
            var selectedColony = Model.SelectedColony;
            if (selectedColony != null)
            {
                Model.PlanetaryBuildProjects = TechTreeHelper.GetBuildProjects(Model.SelectedColony);
                if (selectedColony.Shipyard != null)
                {
                    IList<BuildProject> shipList = TechTreeHelper.GetShipyardBuildProjects(selectedColony.Shipyard);
                    //GameLog.Client.GameData.DebugFormat("ColonyScreenPresenter.cs: colony: {0}, Shipyard: {1}, shiplist_FIRST:{2}", selectedColony.Name, selectedColony.Shipyard.Name, shipList.First());

                    BuildProject[] shipListArray = Algorithms.Sort(shipList.AsEnumerable<BuildProject>(),
                        new Comparison<BuildProject>(
                            delegate(BuildProject a, BuildProject b) { return a.BuildDesign.BuildCost.CompareTo(b.BuildDesign.BuildCost) * -1 /*to reverse the order */; }));

                    Model.ShipyardBuildProjects = shipListArray;

                }
                else
                    Model.ShipyardBuildProjects = Enumerable.Empty<BuildProject>();
                //GameLog disabled due to giving a crash when double clicking the (new) colony, maybe because having no Shipyard yet (Gamelog last value)
                //GameLog.Client.GameData.DebugFormat("ColonyScreenPresenter.cs: colony: {0}, shipyard: {1} Build-list is empty", selectedColony.Name, selectedColony.Shipyard.Name);

                //if (selectedColony.Intelyard != null)
                //{
                //            //GameLog.Client.GameData.DebugFormat("ColonyScreenPresenter.cs: colony: {0}, intelyard: {1} existing!", selectedColony.Name, selectedColony.Intelyard.Name);
                //    //IList<BuildProject> intelList = TechTreeHelper.GetIntelyardBuildProjects(selectedColony.Intelyard);
                //    //        GameLog.Client.GameData.DebugFormat("ColonyScreenPresenter.cs: colony: {0}, intelyard: {1}, intelDesign_FIRST: {2}", selectedColony.Name, selectedColony.Intelyard.Name, intelList.First());

                //    //BuildProject[] intelListArray = Algorithms.Sort(intelList.AsEnumerable<BuildProject>(),
                //    //    new Comparison<BuildProject>(
                //    //        delegate (BuildProject a, BuildProject b) { return a.BuildDesign.BuildCost.CompareTo(b.BuildDesign.BuildCost) * -1 /*to reverse the order */; }));

                //    //this.Model.IntelyardBuildProjects = intelListArray;
                //            //GameLog.Client.GameData.DebugFormat("ColonyScreenPresenter.cs: colony: {0}, intelyard: {1}, intelListArray_FIRST: {2}", selectedColony.Name, selectedColony.Intelyard.Name, intelListArray.First());
                //}
                //else
                //    this.Model.IntelyardBuildProjects = Enumerable.Empty<BuildProject>();
                //            GameLog.Client.GameData.DebugFormat("ColonyScreenPresenter.cs: colony: {0}, intelyard: {1} Build-list is empty", selectedColony.Name, selectedColony.Intelyard.Name);
            }
            else
            {
                Model.PlanetaryBuildProjects = Enumerable.Empty<BuildProject>();
                //GameLog.Client.GameData.DebugFormat("ColonyScreenPresenter.cs: colony: {0}, intelyard: {1} Build-list for whole system is empty", selectedColony.Name, selectedColony.Intelyard.Name);
            }
        }

        protected override void InvalidateCommands()
        {
            base.InvalidateCommands();

            _addToPlanetaryBuildQueueCommand.RaiseCanExecuteChanged();
            _addToShipyardBuildQueueCommand.RaiseCanExecuteChanged();
            //_addToIntelyardBuildQueueCommand.RaiseCanExecuteChanged();
            _removeFromPlanetaryBuildQueueCommand.RaiseCanExecuteChanged();
            _removeFromShipyardBuildQueueCommand.RaiseCanExecuteChanged();
            //_removeFromIntelyardBuildQueueCommand.RaiseCanExecuteChanged();
            _cancelBuildProjectCommand.RaiseCanExecuteChanged();
            _buyBuildProjectCommand.RaiseCanExecuteChanged();
            _activateFacilityCommand.RaiseCanExecuteChanged();
            _deactivateFacilityCommand.RaiseCanExecuteChanged();
            _scrapFacilityCommand.RaiseCanExecuteChanged();
            _unscrapFacilityCommand.RaiseCanExecuteChanged();
            _toggleBuildingScrapCommand.RaiseCanExecuteChanged();
            _toggleBuildingIsActiveCommand.RaiseCanExecuteChanged();
            _toggleShipyardBuildSlotCommand.RaiseCanExecuteChanged();
            //_toggleIntelyardBuildSlotCommand.RaiseCanExecuteChanged();
            _selectShipBuildProjectCommand.RaiseCanExecuteChanged();
            //_selectBuildIntelProjectCommand.RaiseCanExecuteChanged();
        }

        protected override void RegisterCommandAndEventHandlers()
        {
            base.RegisterCommandAndEventHandlers();

            Model.AddToPlanetaryBuildQueueCommand = _addToPlanetaryBuildQueueCommand;
            Model.AddToShipyardBuildQueueCommand = _addToShipyardBuildQueueCommand;
            ////this.Model.AddToIntelyardBuildQueueCommand = _addToIntelyardBuildQueueCommand;
            Model.RemoveFromPlanetaryBuildQueueCommand = _removeFromPlanetaryBuildQueueCommand;
            Model.RemoveFromShipyardBuildQueueCommand = _removeFromShipyardBuildQueueCommand;
            //this.Model.RemoveFromIntelyardBuildQueueCommand = _removeFromIntelyardBuildQueueCommand;
            Model.CancelBuildProjectCommand = _cancelBuildProjectCommand;
            Model.BuyBuildProjectCommand = _buyBuildProjectCommand;
            Model.ScrapFacilityCommand = _scrapFacilityCommand;
            Model.UnscrapFacilityCommand = _unscrapFacilityCommand;
            Model.ActivateFacilityCommand = _activateFacilityCommand;
            Model.DeactivateFacilityCommand = _deactivateFacilityCommand;
            Model.ToggleBuildingIsActiveCommand = _toggleBuildingIsActiveCommand;
            Model.ToggleBuildingScrapCommand = _toggleBuildingScrapCommand;
            Model.ToggleShipyardBuildSlotCommand = _toggleShipyardBuildSlotCommand;
            //this.Model.ToggleIntelyardBuildSlotCommand = _toggleIntelyardBuildSlotCommand;
            Model.SelectShipBuildProjectCommand = _selectShipBuildProjectCommand;
            //this.Model.SelectBuildIntelProjectCommand = _selectBuildIntelProjectCommand;

            Model.SelectedColonyChanged += OnSelectedColonyChanged;
            Model.ActiveOrbitalBatteriesChanged += OnActiveOrbitalBatteriesChanged;

            ColonyScreenCommands.ToggleBuildingScrapCommand.RegisterCommand(_toggleBuildingScrapCommand);
            ColonyScreenCommands.PreviousColonyCommand.RegisterCommand(_previousColonyCommand);
            ColonyScreenCommands.NextColonyCommand.RegisterCommand(_nextColonyCommand);

            GalaxyScreenCommands.SelectSector.RegisterCommand(_selectSectorCommand);

            ClientEvents.TurnStarted.Subscribe(OnTurnStarted, ThreadOption.UIThread);
        }

        private void OnTurnStarted(GameContextEventArgs args)
        {
            var selectedColony = Model.SelectedColony;
            if (selectedColony == null)
                Model.SelectedColony = AppContext.LocalPlayerEmpire.SeatOfGovernment;

            Model.Colonies = AppContext.LocalPlayerEmpire.Colonies;
        }

        private bool CanExecuteCancelBuildProjectCommand(BuildProject project)
        {
            if (Model.SelectedColony == null)
                return false;

            if (project is ShipBuildProject)
                return (Model.SelectedColony.Shipyard != null);

            //if (project is BuildIntelProject)
            //    return (this.Model.SelectedColony.Intelyard != null);

            return true;
        }

        private void ExecuteCancelBuildProjectCommand([NotNull] BuildProject project)
        {
            if (project == null)
                throw new ArgumentNullException("project");

            var productionCenter = project.ProductionCenter;
            if (productionCenter == null)
                return;

            var buildSlot = productionCenter.BuildSlots.FirstOrDefault(o => o.Project == project);
            if (buildSlot == null)
                return;

            if (project.IsPartiallyComplete || project.IsRushed)
            {
                var confirmResult = MessageDialog.Show(
                    ResourceManager.GetString("CONFIRM_CANCEL_BUILD_HEADER"),
                    ResourceManager.GetString("CONFIRM_CANCEL_BUILD_MESSAGE"),
                    MessageDialogButtons.YesNo);

                if (confirmResult != MessageDialogResult.Yes)
                    return;
            }

            if (project.IsRushed)
            {
                var civMan = CivilizationManager.For(productionCenter.Owner);
                civMan.Credits.AdjustCurrent(project.GetCurrentIndustryCost());
            }

            project.Cancel();
            productionCenter.ProcessQueue();

            PlayerOrderService.AddOrder(new UpdateProductionOrder(productionCenter));

            UpdateBuildLists();
        }

        private bool CanExecuteBuyBuildProjectCommand(BuildProject project)
        {
            if (project == null)
                return false;

            if (project.IsCancelled || project.IsCompleted || project.IsRushed)
                return false;
            
            if (Model.SelectedColony == null)
                return false;

            var civMan = CivilizationManager.For(Model.SelectedColony.Owner);

            if (civMan.Credits.CurrentValue < project.GetCurrentIndustryCost())
            {
                int missingCredits = project.GetCurrentIndustryCost() - civMan.Credits.CurrentValue;
                string message = "     No Buying\n \n " + missingCredits + " Credits missing for Buying";
                var result = MessageDialog.Show(message, MessageDialogButtons.Ok);
                return false;
            }
       
            var resourceTypes = EnumHelper.GetValues<ResourceType>();
            for (var i = 0; i < resourceTypes.Length; i++)
            {
                var resource = resourceTypes[i];
                if (civMan.Resources[resource].CurrentValue < project.GetCurrentResourceCost(resource))
                {
                    string message = "     No Buying\n \n " + resource + " missing for Buying";
                    var result = MessageDialog.Show(message, MessageDialogButtons.Ok);
                    return false;
                }
            }

            return true;
        }

        private void ExecuteBuyBuildProjectCommand([NotNull] BuildProject project)
        {
            if (project == null)
                throw new ArgumentNullException("project");

            var productionCenter = project.ProductionCenter;
            if (productionCenter == null)
                return;

            var buildSlot = productionCenter.BuildSlots.FirstOrDefault(o => o.Project == project);
            if (buildSlot == null)
                return;

            var civMan = CivilizationManager.For(Model.SelectedColony.Owner);

            string confirmationMessage = "Are you sure you want to rush this project?\nCost:\n" + project.GetCurrentIndustryCost().ToString() + " out of " + civMan.Credits.CurrentValue.ToString() + " Credits\n";
            var resourceTypes = EnumHelper.GetValues<ResourceType>();
            for (var i = 0; i < resourceTypes.Length; i++)
            {
                var resource = resourceTypes[i];
                if (project.GetCurrentResourceCost(resource) > 0)
                    confirmationMessage += project.GetCurrentResourceCost(resource).ToString() + " out of " + civMan.Resources[resource].CurrentValue.ToString() + " " + resource.ToString() + "\n";
            }

            var confirmResult = MessageDialog.Show(
                ResourceManager.GetString("RUSH PRODUCTION"),
                confirmationMessage,
                MessageDialogButtons.YesNo);

            if (confirmResult != MessageDialogResult.Yes)
                return;

            // temporarily update the resources so the player can immediately see the results of his spending, else we would get updated values only at the next turn.
            civMan.Credits.AdjustCurrent(-project.GetCurrentIndustryCost());
            for (var i = 0; i < resourceTypes.Length; i++)
            {
                var resource = resourceTypes[i];
                if (project.GetCurrentResourceCost(resource) > 0)
                    civMan.Resources[resource].AdjustCurrent(-project.GetCurrentResourceCost(resource));
            }

            project.IsRushed = true;
            PlayerOrderService.AddOrder(new RushProductionOrder(productionCenter));
        }

        private bool CanExecuteRemoveFromShipyardBuildQueueCommand(BuildQueueItem item)
        {
            return ((Model.SelectedColony != null) && (Model.SelectedColony.Shipyard != null));
        }

        private void ExecuteRemoveFromShipyardBuildQueueCommand(BuildQueueItem item)
        {
            var colony = Model.SelectedColony;
            if (colony == null)
                return;

            if (colony.Shipyard == null)
                return;

            RemoveItemFromBuildQueue(item, colony.Shipyard);
        }
        //private bool CanExecuteRemoveFromIntelyardBuildQueueCommand(BuildQueueItem item)
        //{
        //    return ((this.Model.SelectedColony != null) && (this.Model.SelectedColony.Intelyard != null));
        //}

        ////private void ExecuteRemoveFromIntelyardBuildQueueCommand(BuildQueueItem item)
        ////{
        ////    var colony = this.Model.SelectedColony;
        ////    if (colony == null)
        ////        return;

        ////    if (colony.Intelyard == null)
        ////        return;

        ////    RemoveItemFromBuildQueue(item, colony.Intelyard);
        ////}

        private bool CanExecuteRemoveFromPlanetaryBuildQueueCommand(BuildQueueItem item)
        {
            return (Model.SelectedColony != null);
        }

        private void ExecuteRemoveFromPlanetaryBuildQueueCommand(BuildQueueItem item)
        {
            var colony = Model.SelectedColony;
            if (colony == null)
                return;

            RemoveItemFromBuildQueue(item, colony);
        }

        private bool CanExecuteAddToShipyardBuildQueueCommand(BuildProject project)
        {
            return ((Model.SelectedColony != null) && (Model.SelectedColony.Shipyard != null));
        }

        private void ExecuteAddToShipyardBuildQueueCommand(BuildProject project)
        {
            var colony = Model.SelectedColony;
            if (colony == null)
                return;

            if (colony.Shipyard == null)
                return;

            AddProjectToBuildQueue(project, colony.Shipyard);
        }
        //private bool CanExecuteAddToIntelyardBuildQueueCommand(BuildProject project)
        //{
        //    return ((this.Model.SelectedColony != null) && (this.Model.SelectedColony.Intelyard != null));
        //}

        //private void ExecuteAddToIntelyardBuildQueueCommand(BuildProject project)
        //{
        //    var colony = this.Model.SelectedColony;
        //    if (colony == null)
        //        return;

        //    if (colony.Intelyard == null)
        //        return;

        //    AddProjectToBuildQueue(project, colony.Intelyard);
        //}
        protected void RemoveItemFromBuildQueue([NotNull] BuildQueueItem item, [NotNull] IProductionCenter productionCenter)
        {
            if (item == null)
                throw new ArgumentNullException("item");
            if (productionCenter == null)
                throw new ArgumentNullException("productionCenter");

            if ((item.Count <= 1) || !item.DecrementCount())
                productionCenter.BuildQueue.Remove(item);

            PlayerOrderService.AddOrder(new UpdateProductionOrder(productionCenter));

            UpdateBuildLists();
        }

        protected void AddProjectToBuildQueue([NotNull] BuildProject project, [NotNull] IProductionCenter productionCenter)
        {
            if (project == null)
                throw new ArgumentNullException("project");
            if (productionCenter == null)
                throw new ArgumentNullException("productionCenter");

            var newItemAdded = true;
            var lastItemInQueue = productionCenter.BuildQueue.LastOrDefault();

            if ((lastItemInQueue != null) && project.IsEquivalent(lastItemInQueue.Project))
            {
                if (lastItemInQueue.IncrementCount())
                    newItemAdded = false;
            }

            if (newItemAdded)
            {
                productionCenter.BuildQueue.Add(new BuildQueueItem(project));
                productionCenter.ProcessQueue();
            }

            PlayerOrderService.AddOrder(new UpdateProductionOrder(productionCenter));

            if (productionCenter is Colony)
                Model.SelectedPlanetaryBuildProject = null;
            else if (productionCenter is Shipyard)
                Model.SelectedShipyardBuildProject = null;
            //else if (productionCenter is Intelyard)
            //    this.Model.SelectedIntelyardBuildProject = null;

            UpdateBuildLists();
        }

        protected override void UnregisterCommandAndEventHandlers()
        {
            base.UnregisterCommandAndEventHandlers();

            Model.AddToPlanetaryBuildQueueCommand = null;
            Model.AddToShipyardBuildQueueCommand = null;
            //this.Model.AddToIntelyardBuildQueueCommand = null;
            Model.RemoveFromPlanetaryBuildQueueCommand = null;
            Model.RemoveFromShipyardBuildQueueCommand = null;
            //this.Model.RemoveFromIntelyardBuildQueueCommand = null;
            Model.CancelBuildProjectCommand = null;
            Model.BuyBuildProjectCommand = null;
            Model.ScrapFacilityCommand = null;
            Model.UnscrapFacilityCommand = null;
            Model.ActivateFacilityCommand = null;
            Model.DeactivateFacilityCommand = null;
            Model.ToggleBuildingIsActiveCommand = null;
            Model.ToggleBuildingScrapCommand = null;

            Model.SelectedColonyChanged -= OnSelectedColonyChanged;
            Model.ActiveOrbitalBatteriesChanged -= OnActiveOrbitalBatteriesChanged;

            ColonyScreenCommands.ToggleBuildingScrapCommand.UnregisterCommand(_toggleBuildingScrapCommand);
            ColonyScreenCommands.PreviousColonyCommand.UnregisterCommand(_previousColonyCommand);
            ColonyScreenCommands.NextColonyCommand.UnregisterCommand(_nextColonyCommand);

            GalaxyScreenCommands.SelectSector.UnregisterCommand(_selectSectorCommand);

            ClientEvents.TurnStarted.Unsubscribe(OnTurnStarted);
        }

        private bool CanExecuteAddToPlanetaryBuildQueueCommand(BuildProject arg)
        {
            return (Model.SelectedColony != null);
        }

        private void ExecuteAddToPlanetaryBuildQueueCommand([NotNull] BuildProject project)
        {
            if (project == null)
                throw new ArgumentNullException("project");

            var colony = Model.SelectedColony;
            if (colony == null)
                return;

            AddProjectToBuildQueue(project, colony);
        }

        #endregion

        #region Overrides of GameScreenPresenterBase<ColonyScreenPresentationModel,IColonyScreenView>

        protected override string ViewName
        {
            get { return StandardGameScreens.ColonyScreen; }
        }

        #endregion
    }
}