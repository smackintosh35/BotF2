// GalaxyGenerator.cs
//
// Copyright (c) 2007 Mike Strobel
//
// This source code is subject to the terms of the Microsoft Reciprocal License (Ms-RL).
// For details, see <http://www.opensource.org/licenses/ms-rl.html>.
//
// All other rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using log4net;

using Supremacy.Collections;
using Supremacy.Data;
using Supremacy.Entities;
using Supremacy.Game;
using Supremacy.Resources;
using Supremacy.Types;
using Supremacy.Utility;

using Wintellect.PowerCollections;
using System.Threading.Tasks;
using Supremacy.IO;

namespace Supremacy.Universe
{
    public static class GalaxyGenerator
    {
        public const double MinDistanceBetweenStars = 1.25;
        public const int MinHomeworldDistanceFromInterference = 2;

        private static readonly ILog Log;

        private static TableMap UniverseTables;

        private static readonly Dictionary<StarType, int> StarTypeDist;
        private static readonly DoubleKeyedSet<StarType, PlanetSize, int> StarTypeModToPlanetSizeDist;
        private static readonly DoubleKeyedSet<int, PlanetSize, int> SlotModToPlanetSizeDist;
        private static readonly DoubleKeyedSet<StarType, PlanetType, int> StarTypeModToPlanetTypeDist;
        private static readonly DoubleKeyedSet<PlanetSize, PlanetType, int> PlanetSizeModToPlanetTypeDist;
        private static readonly DoubleKeyedSet<int, PlanetType, int> SlotModToPlanetTypeDist;
        private static readonly DoubleKeyedSet<PlanetSize, MoonSize, int> PlanetSizeModToMoonSizeDist;
        private static readonly DoubleKeyedSet<PlanetType, MoonSize, int> PlanetTypeModToMoonSizeDist;

        static GalaxyGenerator()
        {
            UniverseTables = UniverseManager.Tables;
            Log = GameLog.Debug.General;

            StarTypeDist = new Dictionary<StarType, int>();
            StarTypeModToPlanetSizeDist = new DoubleKeyedSet<StarType, PlanetSize, int>();
            StarTypeModToPlanetTypeDist = new DoubleKeyedSet<StarType, PlanetType, int>();
            SlotModToPlanetSizeDist = new DoubleKeyedSet<int, PlanetSize, int>();
            SlotModToPlanetTypeDist = new DoubleKeyedSet<int, PlanetType, int>();
            PlanetSizeModToPlanetTypeDist = new DoubleKeyedSet<PlanetSize, PlanetType, int>();
            PlanetSizeModToMoonSizeDist = new DoubleKeyedSet<PlanetSize, MoonSize, int>();
            PlanetTypeModToMoonSizeDist = new DoubleKeyedSet<PlanetType, MoonSize, int>();


            foreach (var starType in EnumHelper.GetValues<StarType>())
            {
                StarTypeDist[starType] = Number.ParseInt32(UniverseTables["StarTypeDist"][starType.ToString()][0]);
                foreach (var planetSize in EnumHelper.GetValues<PlanetSize>())
                {
                    StarTypeModToPlanetSizeDist[starType, planetSize] =
                        Number.ParseInt32(
                            UniverseTables["StarTypeModToPlanetSizeDist"][starType.ToString()][planetSize.ToString()]);
                }
                foreach (var planetType in EnumHelper.GetValues<PlanetType>())
                {
                    StarTypeModToPlanetTypeDist[starType, planetType] =
                        Number.ParseInt32(
                            UniverseTables["StarTypeModToPlanetTypeDist"][starType.ToString()][planetType.ToString()]);
                }
            }

            for (var i = 0; i < StarSystem.MaxPlanetsPerSystem; i++)
            {
                foreach (var planetSize in EnumHelper.GetValues<PlanetSize>())
                {
                    SlotModToPlanetSizeDist[i, planetSize] =
                        Number.ParseInt32(UniverseTables["SlotModToPlanetSizeDist"][i][planetSize.ToString()]);
                }
                foreach (var planetType in EnumHelper.GetValues<PlanetType>())
                {
                    SlotModToPlanetTypeDist[i, planetType] =
                        Number.ParseInt32(UniverseTables["SlotModToPlanetTypeDist"][i][planetType.ToString()]);
                }
            }

            foreach (var planetSize in EnumHelper.GetValues<PlanetSize>())
            {
                foreach (var planetType in EnumHelper.GetValues<PlanetType>())
                {
                    PlanetSizeModToPlanetTypeDist[planetSize, planetType] =
                        Number.ParseInt32(
                            UniverseTables["PlanetSizeModToPlanetTypeDist"][planetSize.ToString()][planetType.ToString()
                                ]);
                }
            }

            foreach (var moonSize in EnumHelper.GetValues<MoonSize>())
            {
                foreach (var planetSize in EnumHelper.GetValues<PlanetSize>())
                {
                    PlanetSizeModToMoonSizeDist[planetSize, moonSize] =
                        Number.ParseInt32(
                            UniverseTables["PlanetSizeModToMoonSizeDist"][planetSize.ToString()][moonSize.ToString()]);
                }
                foreach (var planetType in EnumHelper.GetValues<PlanetType>())
                {
                    PlanetTypeModToMoonSizeDist[planetType, moonSize] =
                        Number.ParseInt32(
                            UniverseTables["PlanetTypeModToMoonSizeDist"][planetType.ToString()][moonSize.ToString()]);
                }
            }
        }

        private static Collections.CollectionBase<string> GetStarNames()
        {
            var file = new FileStream(
                ResourceManager.GetResourcePath("Resources/Tables/StarNames.txt"),
                FileMode.Open,
                FileAccess.Read);

            var reader = new StreamReader(file);
            var names = new Collections.CollectionBase<string>();

            while (!reader.EndOfStream)
            {
                var line = reader.ReadLine();
                if (line == null)
                    break;

                names.Add(line.Trim());
            }

            return names;
        }

        private static IList<string> GetNebulaNames()
        {
            var file = new FileStream(
                ResourceManager.GetResourcePath("Resources/Tables/NebulaNames.txt"),
                FileMode.Open,
                FileAccess.Read);
            
            var reader = new StreamReader(file);
            var names = new List<string>();
            
            while (!reader.EndOfStream)
            {
                var line = reader.ReadLine();
                if (line == null)
                    break;

                names.Add(line.Trim());
            }

            return names;
        }

        private static IList<string> GetWormholeNames()
        {
            var file = new FileStream(
                ResourceManager.GetResourcePath("Resources/Tables/WormholeNames.txt"),
                FileMode.Open,
                FileAccess.Read);

            var reader = new StreamReader(file);
            var names = new List<string>();

            while (!reader.EndOfStream)
            {
                var line = reader.ReadLine();
                if (line == null)
                    break;

                names.Add(line.Trim());
            }

            return names;
        }

        private static int GetMinDistanceBetweenHomeworlds()
        {
            var size = Math.Min(
                GameContext.Current.Universe.Map.Width,
                GameContext.Current.Universe.Map.Height);
            
            // If its an MP game, we want the different Empires to be sufficiently far away from each others
            // TODO Disabled this for now as it turns out that it is still able to fail to place homeworlds.
            // Tried to rework the loop so that either it restarted the placement process or tried to re-place
            // this failing homeworld with a smaller distance requirement. But it keeps failing or crashing.
            /*if (GameContext.Current.IsMultiplayerGame)
            {
                return (int)(0.3f * (float)size);  // min distance would be of 30% of the full map distance, higher than that it starts to be difficult to place all 5 empires
            }*/

            // Ensure empireCount has a positive value to avoid a divide-by-zero error.
            var empireCount = Math.Max(1, GameContext.Current.Civilizations.Count(o => o.IsEmpire));

            return (size / empireCount);
        }

        public static void GenerateGalaxy(GameContext game)
        {
            GameContext.PushThreadContext(game);
            try
            {
                while (true)
                {
                    /* We reload the Universe Tables so that any changes made to the tables
                     * during runtime will be applied without restarting the game.  This
                     * will be useful for tweaking the tables during development.  We can
                     * fall back to using UniverseManager.Tables later on.
                     */
                    UniverseTables = TableMap.ReadFromFile(
                        ResourceManager.GetResourcePath("Resources/Tables/UniverseTables.txt"));

                    var galaxySizes = UniverseTables["GalaxySizes"];

                    var mapSize = new Dimension(
                        Number.ParseInt32(galaxySizes[game.Options.GalaxySize.ToStringCached()]["Width"]),
                        Number.ParseInt32(galaxySizes[game.Options.GalaxySize.ToStringCached()]["Height"]));

                    var universe = new UniverseManager(mapSize);

                    GameContext.Current.Universe = universe;

                    var starPositions = GetStarPositions();
                    var starNames = GetStarNames();

                    Algorithms.RandomShuffleInPlace(starNames);

                    Collections.CollectionBase<MapLocation> homeLocations;

                    if (!PlaceHomeworlds(starPositions, starNames, out homeLocations))
                        continue;

                    GenerateSystems(starPositions, starNames, homeLocations);
                    PlaceMoons();

                    break;
                }
            }
            finally
            {
                GameContext.PopThreadContext();
            }
        }

        private static Collections.CollectionBase<MapLocation> GetStarPositions()
        {
            IGalaxyLayout layout;

            var width = GameContext.Current.Universe.Map.Width;
            var height = GameContext.Current.Universe.Map.Height;
            var number = (width * height);

            switch (GameContext.Current.Options.StarDensity)
            {
                case StarDensity.Sparse:
                    number /= 12;
                    break;
                case StarDensity.Medium:
                    number /= 10;
                    break;
                case StarDensity.Dense:
                default:
                    number /= 8;
                    break;
            }

            switch (GameContext.Current.Options.GalaxyShape)
            {
                case GalaxyShape.Ring:
                    layout = new RingGalaxyLayout();
                    break;
                case GalaxyShape.Cluster:
                    layout = new ClusterGalaxyLayout();
                    break;
                case GalaxyShape.Spiral:
                    layout = new SpiralGalaxyLayout();
                    break;
                case GalaxyShape.Elliptical:
                    layout = new EllipticalGalaxyLayout();
                    break;
                default:
                case GalaxyShape.Irregular:
                    layout = new IrregularGalaxyLayout();
                    break;
            }

            ICollection<MapLocation> positions;

            layout.GetStarPositions(out positions, number, width, height);

            var result = new Collections.CollectionBase<MapLocation>(positions.Count);

            positions.CopyTo(result);

            return result;
        }

        public static StarSystemDescriptor GenerateHomeSystem(Civilization civ)
        {
            var system = new StarSystemDescriptor();
            StarType starType;

            while (!(starType = GetStarType()).SupportsPlanets() || (starType == StarType.Nebula))
                continue;

            system.StarType = starType;
            system.Name = civ.HomeSystemName;
            system.Inhabitants = civ.Race.Key;
            system.Bonuses = (civ.CivilizationType == CivilizationType.MinorPower)
                                 ? SystemBonus.RawMaterials
                                 : SystemBonus.Dilithium | SystemBonus.RawMaterials;

            GeneratePlanetsWithHomeworld(system, civ);
            GameLog.Client.GameData.DebugFormat("GalaxyGenerator.cs: No HomeSystem defined - HomeSystemsGeneration will be done for={0}", civ.Name);
            return system;
        }

        private static void SetPlanetNames(StarSystem system)
        {
            if (system == null)
                throw new ArgumentNullException("system");
            for (var i = 0; i < system.Planets.Count; i++)
            {
                if (String.IsNullOrEmpty(system.Planets[i].Name))
                {
                    system.Planets[i].Name = (system.Planets[i].PlanetType == PlanetType.Asteroids)
                                                 ? "Asteroids"
                                                 : system.Name + " " + RomanNumber.Get(i + 1);
                }
            }
        }
        
        private static int GetIdealSlot(StarSystemDescriptor system, PlanetDescriptor planet)
        {
            var bestScore = 0;
            var bestSlot = 0;

            // ReSharper disable PossibleInvalidOperationException

            for (var iSlot = 0; iSlot <= system.Planets.Count; iSlot++)
            {
                var score = GetPlanetSizeScore(system.StarType.Value, planet.Size.Value, iSlot) +
                            GetPlanetTypeScore(system.StarType.Value, planet.Size.Value, planet.Type.Value, iSlot);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestSlot = iSlot;
                }
            }

            // ReSharper restore PossibleInvalidOperationException

            return bestSlot;
        }

        private static void FinalizaHomeworldPlacement(IList<string> starNames,
            HomeSystemsDatabase homeSystemDatabase, 
            Civilization civ, 
            MapLocation location)
        {
            var civManager = new CivilizationManager(GameContext.Current, civ);

            GameContext.Current.CivilizationManagers.Add(civManager);

            var homeSystemDescriptor = (homeSystemDatabase.ContainsKey(civ.Key))
                                           ? homeSystemDatabase[civ.Key]
                                           : GenerateHomeSystem(civ);

            //EnumHelper.TryParse((string)element.Element(ns + "CivilizationType"), out _civType);

            var planets = new List<Planet>();
            var race = civ.Race;
            var homeSystem = new StarSystem();
            //var civType = new CivilizationType();

            //CivilizationType NotInGameRace = default(CivilizationType);

            //if (civType == NotInGameRace)
            //{
            //    GameLog.Client.GameData.DebugFormat("GalaxyGenerator.cs: CivilizationType={0} for _civ.Name={1}", civ.CivilizationType, civ.Name);
            //    return;
            //}

            if (!homeSystemDescriptor.IsNameDefined)
            {
                if (starNames.Count > 0)
                {
                    homeSystemDescriptor.Name = starNames[0];
                    starNames.RemoveAt(0);
                }
                else
                {
                    homeSystemDescriptor.Name = civ.ShortName + " Home System";
                }
            }

            homeSystem.Name = homeSystemDescriptor.Name;
            homeSystem.Location = location;

            if (homeSystemDescriptor.IsInhabitantsDefined)
                race = GameContext.Current.Races[homeSystemDescriptor.Inhabitants];

            if (homeSystemDescriptor.StarType.HasValue)
            {
                homeSystem.StarType = homeSystemDescriptor.StarType.Value;
            }
            else
            {
                StarType starType;
                while (!(starType = GetStarType()).SupportsPlanets())
                    continue;
                homeSystem.StarType = starType;
            }

            if (homeSystemDescriptor.HasBonuses)
                homeSystem.AddBonus(homeSystemDescriptor.Bonuses);

            if (homeSystemDescriptor.Planets.Count == 0)
                GeneratePlanetsWithHomeworld(homeSystemDescriptor, civ);
            else
                GenerateUnspecifiedPlanets(homeSystemDescriptor);

            foreach (var planetDescriptor in homeSystemDescriptor.Planets)
            {
                if (planets.Count >= StarHelper.MaxNumberOfPlanets(homeSystem.StarType))
                    break;

                if (!planetDescriptor.IsSinglePlanet)
                    continue;

                var planet = new Planet();

                if (planetDescriptor.IsNameDefined)
                    planet.Name = planetDescriptor.Name;

                if (planetDescriptor.Size.HasValue)
                    planet.PlanetSize = planetDescriptor.Size.Value;

                if (planetDescriptor.Type.HasValue)
                {
                    if (!planetDescriptor.Size.HasValue)
                    {
                        switch (planetDescriptor.Type)
                        {
                            case PlanetType.Asteroids:
                                planet.PlanetSize = PlanetSize.Asteroids;
                                break;
                            case PlanetType.GasGiant:
                                planet.PlanetSize = PlanetSize.GasGiant;
                                break;
                        }
                    }

                    planet.PlanetType = planetDescriptor.Type.Value;
                }

                if (planetDescriptor.HasBonuses)
                    planet.AddBonus(planetDescriptor.Bonuses);

                planet.Variation = Statistics.Random(Planet.MaxVariations);
                planets.Add(planet);
            }

            homeSystem.AddPlanets(planets);

            SetPlanetNames(homeSystem);

            homeSystem.Owner = civ;
            GameContext.Current.Universe.Map[homeSystem.Location].System = homeSystem;

            PlaceBonuses(homeSystem);
            CreateHomeColony(civ, homeSystem, race);

            if (civManager.HomeColony == null)
                civManager.HomeColony = homeSystem.Colony;

            civManager.Colonies.Add(homeSystem.Colony);

            GameContext.Current.Universe.Objects.Add(homeSystem);
            GameContext.Current.Universe.Objects.Add(homeSystem.Colony);
            // works: GameLog.Client.GameData.DebugFormat("GalaxyGenerator.cs: HomeSystemsGeneration is done at={0} for={1}", homeSystem.Colony.Location, homeSystem.Owner.Name);
        }

        private static bool PlaceEmpireHomeworlds(Collections.CollectionBase<MapLocation> positions,
            IList<string> starNames,
            HomeSystemsDatabase homeSystemDatabase,
            List<Civilization> empireCivs,
            Collections.CollectionBase<MapLocation> empireHomeLocations,
            List<Civilization> chosenCivs,
            bool mustRespectQuadrants)
        {
            var minHomeDistance = GetMinDistanceBetweenHomeworlds();

            for (var index = 0; index < empireCivs.Count; index++)
            {
                var localIndex = index;
                int iPosition = Algorithms.FindFirstIndexWhere(
                                            positions,
                                            delegate(MapLocation location)
                                            {
                                                if (mustRespectQuadrants)
                                                {
                                                    if (GameContext.Current.Universe.Map.GetQuadrant(location) != empireCivs[localIndex].HomeQuadrant)
                                                        return false;
                                                }

                                                return empireHomeLocations.All(t => MapLocation.GetDistance(location, t) >= minHomeDistance);
                                            });

                if (iPosition >= 0)
                {
                    var location = positions[iPosition];
                    empireHomeLocations.Add(location);
                    chosenCivs.Add(empireCivs[index]);

                    GameLog.Print("Civilization {0} placed as {2}", empireCivs[index].ShortName, location, empireCivs[index].CivilizationType);
                    // within Location        GameLog.Print("Civilization {0} placed at location {1} as {2}", empireCivs[index].ShortName, location, empireCivs[index].CivilizationType);
                    // old                     GameLog.Print("Civilization {0} placed", empireCivs[index].ShortName);

                    positions.RemoveAt(iPosition);

                    FinalizaHomeworldPlacement(starNames, homeSystemDatabase, empireCivs[localIndex], location);
                }
                else
                {
                    Log.WarnFormat("Failed to find a suitable home sector for civilization {0}.  Galaxy generation will start over.",
                        empireCivs[index].ShortName);
                    empireCivs.RemoveAt(index--);
                    return false;
                }
            }

            return true;
        }


        private static void PlaceWormholes()
        {
            MapLocation location = new MapLocation();
            int _galaxyWidth = GameContext.Current.Universe.Map.Width;
            int _galaxyHeigth = GameContext.Current.Universe.Map.Height;
            int _wormholeNumber = 4;


            if (GameContext.Current.Options.GalaxySize > GalaxySize.Small)
                _wormholeNumber = 8;

            if (GameContext.Current.Options.GalaxySize > GalaxySize.Large)
                _wormholeNumber = 12;

            int _wormholePairs = _wormholeNumber / 2;

            GameLog.Print("will generate {0} wormholes equal to {1} pairs", _wormholeNumber, _wormholePairs);

            // here 4 wormholes in 4 corners

            // wormhole 1:   3,2 
            var system = new StarSystem
            {
                Location = new MapLocation((int)3, (int)2),
                StarType = StarType.Wormhole,
            };
            system.Name = StarType.Wormhole.ToString() + system.Location;
            GameContext.Current.Universe.Objects.Add(system);
            GameContext.Current.Universe.Map[location].System = system;
            //GameLog.Print("Wormhole placed at {0}", system.Location);

            var wormhole_1_Location = system.Location;
            StorageManager.WriteSetting("wormhole_1_Location", system.Location);
            //GameLog.Print("writing {0}, Location = {1}", wormhole_1_Location, system.Location);


            // wormhole 2: 
            system = new StarSystem
            {
                Location = new MapLocation((int)_galaxyWidth - 3, (int) 5),
                StarType = StarType.Wormhole
            };
            system.Name = StarType.Wormhole.ToString() + system.Location;
            GameContext.Current.Universe.Objects.Add(system);
            GameContext.Current.Universe.Map[location].System = system;
            //GameLog.Print("Wormhole placed at {0}", system.Location);

            var wormhole_2_Location = system.Location;
            StorageManager.WriteSetting("wormhole_2_Location", system.Location);
            //GameLog.Print("writing {0}, Location = {1}", wormhole_2_Location, system.Location);



            // wormhole 3:  
            system = new StarSystem
            {
                Location = new MapLocation((int) 4, (int)_galaxyHeigth - 4),
                StarType = StarType.Wormhole

            };
            system.Name = StarType.Wormhole.ToString() + system.Location;
            GameContext.Current.Universe.Objects.Add(system);
            GameContext.Current.Universe.Map[location].System = system;
            //GameLog.Print("Wormhole placed at {0}", system.Location);

            var wormhole_3_Location = system.Location;
            StorageManager.WriteSetting("wormhole_3_Location", system.Location);
            //GameLog.Print("writing {0}, Location = {1}", wormhole_3_Location, system.Location);


            // wormhole 4:   
            system = new StarSystem
            {
                Location = new MapLocation((int)_galaxyWidth-3, (int)_galaxyHeigth-4),
                StarType = StarType.Wormhole
            };
            system.Name = StarType.Wormhole.ToString() + system.Location;
            GameContext.Current.Universe.Objects.Add(system);
            GameContext.Current.Universe.Map[location].System = system;
            //GameLog.Print("Wormhole placed at {0}", system.Location);

            var wormhole_4_Location = system.Location;
            StorageManager.WriteSetting("wormhole_4_Location", system.Location);
            //GameLog.Print("writing {0}, Location = {1}", wormhole_4_Location, system.Location);

            var wormholePair1 = wormhole_1_Location.ToString() + wormhole_2_Location.ToString();
            var wormholePair2 = wormhole_2_Location.ToString() + wormhole_4_Location.ToString();
            GameLog.Print("defined wormholePair1 = {0}", wormhole_1_Location.ToString() + wormhole_3_Location.ToString());
            GameLog.Print("defined wormholePair2 = {0}", wormhole_2_Location.ToString() + wormhole_4_Location.ToString());

            //GameLog.Print("Wormholes 1 to 4: {0} and {1} and {2} and {3}", wormhole_1_Location, wormhole_2_Location, wormhole_3_Location, wormhole_4_Location);


            if (_wormholeNumber == 8)
            {
                // here another 4 wormholes calculated from the center

                // wormhole 5:   
                system = new StarSystem
                {
                    Location = new MapLocation(((int)_galaxyWidth / 2) - 3, ((int)_galaxyHeigth / 2) - 4),
                    StarType = StarType.Wormhole,
                };
                system.Name = StarType.Wormhole.ToString() + system.Location;
                GameContext.Current.Universe.Objects.Add(system);
                GameContext.Current.Universe.Map[location].System = system;
                //GameLog.Print("Wormhole placed at {0}", system.Location);

                var wormhole_5_Location = system.Location;
                StorageManager.WriteSetting("wormhole_5_Location", system.Location);
                //GameLog.Print("writing {0}, Location = {1}", wormhole_5_Location, system.Location);


                // wormhole 6: 
                system = new StarSystem
                {
                    Location = new MapLocation(((int)_galaxyWidth / 2) + 3, ((int)_galaxyHeigth / 2) - 4),
                    StarType = StarType.Wormhole
                };
                system.Name = StarType.Wormhole.ToString() + system.Location;
                GameContext.Current.Universe.Objects.Add(system);
                GameContext.Current.Universe.Map[location].System = system;
                //GameLog.Print("Wormhole placed at {0}", system.Location);

                var wormhole_6_Location = system.Location;
                StorageManager.WriteSetting("wormhole_6_Location", system.Location);
                //GameLog.Print("writing {0}, Location = {1}", wormhole_6_Location, system.Location);


                // wormhole 7:  
                system = new StarSystem
                {
                    Location = new MapLocation(((int)_galaxyWidth / 2) - 3, ((int)_galaxyHeigth / 2) + 4),
                    StarType = StarType.Wormhole
                };
                system.Name = StarType.Wormhole.ToString() + system.Location;
                GameContext.Current.Universe.Objects.Add(system);
                GameContext.Current.Universe.Map[location].System = system;
                //GameLog.Print("Wormhole placed at {0}", system.Location);

                var wormhole_7_Location = system.Location;
                StorageManager.WriteSetting("wormhole_7_Location", system.Location);
                //GameLog.Print("writing {0}, Location = {1}", wormhole_7_Location, system.Location);


                var wormholeList = new List<Object>
                {
                    wormhole_1_Location,
                    wormhole_2_Location,
                    wormhole_3_Location,
                    wormhole_4_Location
                };
                GameLog.Print("Wormholes: {0} and {1} and {2} and {3}", wormhole_1_Location, wormhole_2_Location, wormhole_3_Location, wormhole_4_Location);


                // wormhole 8:   
                system = new StarSystem
                {
                    Location = new MapLocation(((int)_galaxyWidth / 2) + 3, ((int)_galaxyHeigth / 2) + 4),
                    StarType = StarType.Wormhole
                };
                system.Name = StarType.Wormhole.ToString() + system.Location;
                GameContext.Current.Universe.Objects.Add(system);
                GameContext.Current.Universe.Map[location].System = system;
                //GameLog.Print("Wormhole placed at {0}", system.Location);


                var wormhole_8_Location = system.Location;
                StorageManager.WriteSetting("wormhole_8_Location", system.Location);
                //GameLog.Print("writing {0}, Location = {1}", wormhole_8_Location, system.Location);

                var wormholePair3 = wormhole_5_Location.ToString() + wormhole_7_Location.ToString();
                var wormholePair4 = wormhole_6_Location.ToString() + wormhole_8_Location.ToString();
                GameLog.Print("defined wormholePair3 = {0}", wormhole_5_Location.ToString() + wormhole_7_Location.ToString());
                GameLog.Print("defined wormholePair4 = {0}", wormhole_6_Location.ToString() + wormhole_8_Location.ToString());

                //GameLog.Print("Wormholes 5 to 8: {0} and {1} and {2} and {3}", wormhole_5_Location, wormhole_6_Location, wormhole_7_Location, wormhole_8_Location);

                //end if number = 8 
            }

            //var _pairName = "";
            var _wormholeName = "";
            var _wormholeLocationString = "";

            //try
            //{
            //    for (int i = 1; i < _wormholePairs +1; i++)
            //    {
            //        _pairName = "wormholepair" + i.ToString();
            //        _wormholeLocationString = "wormholePair" + i.ToString();
            //        StorageManager.WriteSetting(_pairName, wormhole_1_Location.ToString() + wormhole_3_Location.ToString());
            //        GameLog.Print("{0} = {1}", _pairName, wormhole_1_Location.ToString() + wormhole_3_Location.ToString());
            //    }

            StorageManager.WriteSetting("numberDefinedWormholes", _wormholeNumber);

            //for (int i = 1; i < _wormholeNumber + 1; i++)
            //
            //    _wormholeName = "wormholeName" + i.ToString();
            //    //doesn't work the way i like     _wormholeLocationString = "wormhole_" + i.ToString() + "_location";

            //    StorageManager.WriteSetting(_wormholeName, _wormholeLocationString);
            //    GameLog.Print("_wormholeName = {0}, Location = {1}", _wormholeName, _wormholeLocationString);
            //}


            //    //}
            //}
            //catch (Exception e) //ToDo: Just log or additional handling necessary?
            //{
            //    GameLog.LogException(e);
            //}


            //var wormholeList = new List<Object>
            //{
            //    wormhole_1_Location,
            //    wormhole_2_Location,
            //    wormhole_3_Location,
            //    wormhole_4_Location
            //};

        }

        private static void PlaceMinorRaceHomeworlds(Collections.CollectionBase<MapLocation> positions,
            IList<string> starNames,
            HomeSystemsDatabase homeSystemDatabase,
            List<Civilization> minorRaceCivs,
            Collections.CollectionBase<MapLocation> minorHomeLocations,
            List<Civilization> chosenCivs)
        {
            var minorRaceFrequency = GameContext.Current.Options.MinorRaceFrequency;
            var totalMinorRaces = 0;
            var minorRaces = new Dictionary<Quadrant, List<Civilization>>();
            foreach (var quadrant in EnumHelper.GetValues<Quadrant>())
                minorRaces[quadrant] = new List<Civilization>();

            foreach (var civ in minorRaceCivs)
            {
                if (!minorRaces.ContainsKey(civ.HomeQuadrant))
                    minorRaces[civ.HomeQuadrant] = new List<Civilization>();

                minorRaces[civ.HomeQuadrant].Add(civ);
                totalMinorRaces++;
            }

            float minorRacePercentage = 0.25f;
            int minorRaceLimit = 9999;

            var minorRaceTable = GameContext.Current.Tables.UniverseTables["MinorRaceFrequency"];
            if (minorRaceTable != null)
            {
                try
                {
                    var divisor = (double?)minorRaceTable.GetValue(minorRaceFrequency.ToStringCached(), "AvailableSystemsDivisor");
                    if (divisor.HasValue)
                        minorRacePercentage = (float)(1d / divisor.Value);
                }
                catch (Exception e) //ToDo: Just log or additional handling necessary?
                {
                    GameLog.LogException(e);
                }

                try
                {
                    var limit = (int?)minorRaceTable.GetValue(minorRaceFrequency.ToStringCached(), "MaxCount");
                    if (limit.HasValue)
                        minorRaceLimit = limit.Value;
                }
                catch (Exception e) //ToDo: Just log or additional handling necessary?
                {
                    GameLog.LogException(e);
                }
            }

            if (minorRacePercentage <= 0.0f)
                minorRacePercentage = 0.0f;
            else
                minorRacePercentage = Math.Min(1.0f, minorRacePercentage);

            var wantedMinorRaceCount = positions.Count * minorRacePercentage;
            wantedMinorRaceCount = Math.Min(wantedMinorRaceCount, minorRaceLimit);

            foreach (var quadrant in EnumUtilities.GetValues<Quadrant>())
                Algorithms.RandomShuffleInPlace(minorRaces[quadrant]);

            /* -- Dead code, commented out to suppress compiler warnings 
            if (false)
            {
                //  Random distribution
                // Since we are populating minors in regards to a system's ratio, we cannot approach this by going through minors and quadrants first.
                // So we go the other way around, we start with the first location (which are already shuffled) and find a minor that fits the same quadrant.
                // If we are out of minors then either we choose one from another quadrant, or simply jump that position and continue on.

                for (int i = 0; i < wantedMinorRaceCount; i++)
                {
                    var location = positions[0];
                    Quadrant posQuadrant = GameContext.Current.Universe.Map.GetQuadrant(location);

                    // If there isn't any minors left in the quadrant just jump over it
                    if (minorRaces[posQuadrant].Count > 0)
                    {
                        var minor = minorRaces[posQuadrant][0];

                        minorRaces[posQuadrant].RemoveAt(0);

                        minorHomeLocations.Add(location);
                        chosenCivs.Add(minor);

                        FinalizaHomeworldPlacement(starNames, homeSystemDatabase, minor, location);

                        //GameLog.Print("Civilization {0} placed at location {1}", minor.ShortName, location);


                        // hide for data protection      GameLog.Print("Civilization {0} placed as {1}", minor.ShortName, minor.CivilizationType);
                    }

                    positions.RemoveAt(0);
                }
        }
            else */
            {
                /* Equally divided amongst quadrants distribution */

                var chosenMinorRaces = new Dictionary<Quadrant, List<Civilization>>();
                foreach (var quadrant in EnumHelper.GetValues<Quadrant>())
                    chosenMinorRaces[quadrant] = new List<Civilization>();
                
                for (int i = 0; i < wantedMinorRaceCount; i++)
                {
                    int smallestMinorRaceCount = int.MaxValue;
                    Quadrant quadrantWithLessMinors = EnumHelper.GetValues<Quadrant>().First();

                    foreach (var quadrant in EnumHelper.GetValues<Quadrant>())
                    {
                        if ((minorRaces[quadrant].Count > 0) && (chosenMinorRaces[quadrant].Count < smallestMinorRaceCount))
                        {
                            smallestMinorRaceCount = chosenMinorRaces[quadrant].Count;
                            quadrantWithLessMinors = quadrant;
                        }
                    }

                    if (minorRaces[quadrantWithLessMinors].Count == 0)
                    {
                        Log.WarnFormat("No more minor race definitions available (create more).  Galaxy generation will stop.");
                        return;
                    }

                    var minor = minorRaces[quadrantWithLessMinors][0];

                    minorRaces[quadrantWithLessMinors].RemoveAt(0);

                    int iPosition = Algorithms.FindFirstIndexWhere(
                        positions,
                        location => (GameContext.Current.Universe.Map.GetQuadrant(location) == minor.HomeQuadrant));

                    if (iPosition >= 0)
                    {
                        var chosenLocation = positions[iPosition];

                        minorHomeLocations.Add(chosenLocation);
                        chosenCivs.Add(minor);
                        chosenMinorRaces[quadrantWithLessMinors].Add(minor);

                        FinalizaHomeworldPlacement(starNames, homeSystemDatabase, minor, chosenLocation);

                        positions.RemoveAt(iPosition);

                        //GameLog.Print("Civilization {0} placed at location {1}", minor.ShortName, chosenLocation);

                        // hide for data protection      GameLog.Print("Civilization {0} placed as {1}", minor.ShortName, minor.CivilizationType);
                    }
                    else
                    {
                        Log.WarnFormat(
                            "Failed to find a suitable home sector for civilization {0}.  Galaxy generation will stop.",
                            minor.ShortName);
                        return;
                    }
                }
            }
        }

        private static bool PlaceHomeworlds(Collections.CollectionBase<MapLocation> positions,
            IList<string> starNames,
            out Collections.CollectionBase<MapLocation> homeLocations)
        {
            var homeSystemDatabase = HomeSystemsDatabase.Load();
            var minorRaceFrequency = GameContext.Current.Options.MinorRaceFrequency;
            var empires = new List<Civilization>();
            var minorRaces = new List<Civilization>();

            Algorithms.RandomShuffleInPlace(positions);

            foreach (var civ in GameContext.Current.Civilizations)
            {
                if (civ.IsEmpire)
                {
                    empires.Add(civ);
                }
                else if (minorRaceFrequency != MinorRaceFrequency.None)
                {
                    minorRaces.Add(civ);
                }
            }

            homeLocations = new Collections.CollectionBase<MapLocation>();
            var chosenCivs = new List<Civilization>();

            PlaceWormholes();

            Boolean result = PlaceEmpireHomeworlds(positions, starNames, homeSystemDatabase, empires, homeLocations, chosenCivs, !GameContext.Current.IsMultiplayerGame);
            if (minorRaceFrequency != MinorRaceFrequency.None)
                PlaceMinorRaceHomeworlds(positions, starNames, homeSystemDatabase, minorRaces, homeLocations, chosenCivs);

            var unusedCivs = GameContext.Current.Civilizations.Except(chosenCivs).Select(o => o.CivID).ToHashSet();

            GameContext.Current.Civilizations.RemoveRange(unusedCivs);
            GameContext.Current.CivilizationManagers.RemoveRange(unusedCivs);

            return result;
        }

        private static void PlaceBonuses(StarSystem system)
        {
            if (system == null)
                throw new ArgumentNullException("system");

            /*
             * Dilithium and Raw Materials System Bonuses
             */
            if (system.IsInhabited && system.Colony.Owner.CanExpand)
            {
                system.AddBonus(SystemBonus.Dilithium | SystemBonus.RawMaterials);
            }
            else if (system.HasBonus(SystemBonus.Random))
            {
                if (system.Planets.Any(p => p.PlanetType.IsHabitable()))
                {
                    if (!system.HasDilithiumBonus && DieRoll.Chance(4))
                        system.AddBonus(SystemBonus.Dilithium);

                    if (!system.HasRawMaterialsBonus && DieRoll.Chance(3))
                        system.AddBonus(SystemBonus.RawMaterials);
                }
            }

            system.RemoveBonus(SystemBonus.Random);

            var foodPlacementCount = 0;
            var energyPlacementCount = 0;

            foreach (var planet in system.Planets)
            {
                if (planet.HasFoodBonus)
                    ++foodPlacementCount;

                if (planet.HasEnergyBonus)
                    ++energyPlacementCount;
            }

            /*
             * Energy and Food Planet Bonus
             */
            foreach (var planet in system.Planets)
            {
                if (planet.HasBonus(PlanetBonus.Random))
                {
                    if (!planet.HasEnergyBonus && energyPlacementCount < 2)
                    {
                        if (planet.PlanetType == PlanetType.Volcanic && DieRoll.Chance(2) ||
                            planet.PlanetType == PlanetType.Desert && DieRoll.Chance(3))
                        {
                            planet.AddBonus(PlanetBonus.Energy);
                            ++energyPlacementCount;
                        }
                    }

                    if (!planet.HasFoodBonus && foodPlacementCount < 2)
                    {
                        if ((planet.PlanetType == PlanetType.Terran ||
                             planet.PlanetType == PlanetType.Oceanic ||
                             planet.PlanetType == PlanetType.Jungle) && DieRoll.Chance(3))
                        {
                            planet.AddBonus(PlanetBonus.Food);
                            ++foodPlacementCount;
                        }
                    }

                    planet.RemoveBonus(PlanetBonus.Random);
                }
            }
        }

        private static void GeneratePlanetsWithHomeworld(StarSystemDescriptor system, Civilization civ)
        {
            var homePlanet = new PlanetDescriptor();
            PlanetSize planetSize;
            homePlanet.Type = civ.Race.HomePlanetType;
            while (!(planetSize = RandomProvider.NextEnum<PlanetSize>()).IsHabitable())
                continue;

            if (!system.IsStarTypeDefined)
            {
                while (!(system.StarType = GetStarType()).Value.SupportsPlanets() || (system.StarType.Value == StarType.Nebula))
                    continue;
            }

            homePlanet.Size = planetSize;
            homePlanet.Name = system.Name + " Prime";

            // ReSharper disable PossibleInvalidOperationException
            GeneratePlanets(system, StarHelper.MaxNumberOfPlanets(system.StarType.Value) - 1);
            // ReSharper restore PossibleInvalidOperationException

            system.Planets.Insert(
                GetIdealSlot(system, homePlanet),
                homePlanet);
        }

        private static void GenerateUnspecifiedPlanets(StarSystemDescriptor system)
        {
            GeneratePlanets(system, 0);
        }

        private static int GetDefinedPlanetCount(StarSystemDescriptor system)
        {
            var result = 0;
            foreach (var planetDescriptor in system.Planets)
            {
                if (planetDescriptor.IsSinglePlanet)
                    result++;
            }
            return result;
        }

        private static void GeneratePlanets(StarSystemDescriptor system, int maxNewPlanets)
        {
            int initialCount;
            if (!system.IsStarTypeDefined)
            {
                while (!(system.StarType = GetStarType()).Value.SupportsPlanets() ||
                       (system.StarType.Value == StarType.Nebula))
                    continue;
            }
            for (var i = 0; i < system.Planets.Count; i++)
            {
                if (!system.Planets[i].IsSinglePlanet)
                {
                    var attemptNumber = 0;
                    var newPlanets = 0;
                    var planetDescriptor = system.Planets[i];

                    initialCount = GetDefinedPlanetCount(system);
                    system.Planets.RemoveAt(i--);

                    // ReSharper disable PossibleInvalidOperationException

                    while ((newPlanets < planetDescriptor.MinNumberOfPlanets || attemptNumber < planetDescriptor.MaxNumberOfPlanets) &&
                           initialCount + attemptNumber < StarHelper.MaxNumberOfPlanets(system.StarType.Value))
                    {
                        var planetSize = GetPlanetSize(system.StarType.Value, initialCount);
                        if (planetSize != PlanetSize.NoWorld)
                        {
                            var planet = new PlanetDescriptor
                            {
                                Size = planetSize,
                                Type = GetPlanetType(
                                    system.StarType.Value,
                                    planetSize,
                                    initialCount + attemptNumber)
                            };
                            system.Planets.Insert(++i, planet);
                            newPlanets++;
                        }

                        attemptNumber++;
                    }

                    // ReSharper restore PossibleInvalidOperationException
                }
            }

            // ReSharper disable PossibleInvalidOperationException

            for (var i = 0; i < system.Planets.Count; i++)
            {
                var planetDescriptor = system.Planets[i];
                if (planetDescriptor.IsSinglePlanet)
                {
                    if (!planetDescriptor.IsSizeDefined)
                    {
                        while ((planetDescriptor.Size = GetPlanetSize(system.StarType.Value, i)) == PlanetSize.NoWorld)
                            continue;
                    }

                    if (!planetDescriptor.IsTypeDefined)
                        planetDescriptor.Type = GetPlanetType(system.StarType.Value, planetDescriptor.Size.Value, i);
                }
            }

            initialCount = GetDefinedPlanetCount(system);

            for (var i = 0;
                 (i < maxNewPlanets) &&
                 ((initialCount + i) < StarHelper.MaxNumberOfPlanets(system.StarType.Value));
                 i++)
            {
                var planetSize = GetPlanetSize(system.StarType.Value, initialCount + i);
                if (planetSize != PlanetSize.NoWorld)
                {
                    var planet = new PlanetDescriptor
                    {
                        Size = planetSize,
                        Type = GetPlanetType(system.StarType.Value, planetSize, initialCount + i)
                    };
                    system.Planets.Add(planet);
                }
            }

            // ReSharper restore PossibleInvalidOperationException        
        }

        private static void CreateHomeColony(Civilization civ, StarSystem system, Race inhabitants)
        {
            var civManager = GameContext.Current.CivilizationManagers[civ];
            var colony = new Colony(system, inhabitants);
            var baseMorale = GameContext.Current.Tables.MoraleTables["BaseMoraleLevels"];

            colony.Population.BaseValue = (int)(0.6f * system.GetMaxPopulation(inhabitants));
            colony.Population.Reset();
            colony.Name = system.Name;

            system.Colony = colony;

            if (baseMorale[civ.Key] != null)
                colony.Morale.BaseValue = Number.ParseInt32(baseMorale[civ.Key][0]);
            else
                colony.Morale.BaseValue = Number.ParseInt32(baseMorale[0][0]);

            colony.Morale.Reset();

            civManager.MapData.SetExplored(colony.Location, true);
            civManager.MapData.SetScanned(colony.Location, true, 1);

            GameContext.Current.Universe.HomeColonyLookup[civ] = colony;
        }

        private static void GenerateSystems(
            IEnumerable<MapLocation> positions,
            IList<string> starNames,
            IIndexedCollection<MapLocation> homeLocations)
        {
            int maxPlanets;
            var nebulaNames = GetNebulaNames();
            var wormholeNames = GetWormholeNames();

            switch (GameContext.Current.Options.PlanetDensity)
            {
                case PlanetDensity.Sparse:
                    maxPlanets = StarSystem.MaxPlanetsPerSystem - 4;
                    break;
                case PlanetDensity.Medium:
                    maxPlanets = StarSystem.MaxPlanetsPerSystem - 2;
                    break;
                default:
                    maxPlanets = StarSystem.MaxPlanetsPerSystem;
                    break;
            }

            Algorithms.RandomShuffleInPlace(nebulaNames);
            Algorithms.RandomShuffleInPlace(wormholeNames);

            var gameContext = GameContext.Current;

            Parallel.ForEach(
                positions,
                position =>
                {
                    GameContext.PushThreadContext(gameContext);

                    try
                    {
                        var system = new StarSystem();
                        var planets = new List<Planet>();

                        StarType starType;

                        do { starType = GetStarType(); }
                        while (!StarHelper.CanPlaceStar(starType, position, homeLocations));

                        system.StarType = starType;
                        system.Location = position;

                        switch (system.StarType)
                        {
                            case StarType.BlackHole:
                                system.Name = "Black Hole";
                                break;
                            case StarType.NeutronStar:
                                system.Name = "Neutron Star";
                                break;
                            case StarType.Quasar:
                                system.Name = "Quasar";
                                break;
                            case StarType.RadioPulsar:
                                system.Name = "Radio Pulsar";
                                break;
                            case StarType.XRayPulsar:
                                system.Name = "X-Ray Pulsar";
                                break;
                            case StarType.Nebula:
                                if (nebulaNames.Count == 0)
                                    break;
                                system.Name = nebulaNames[0];
                                nebulaNames.RemoveAt(0);
                                GameLog.Print("Nebula at {0}", system.Location);  // just for comparing with wormholes - in StarSystemHelper Nebula = -8, Wormhole = -2 (atm)
                                break;
                            case StarType.Wormhole:
                                system.Name = wormholeNames[0];
                                wormholeNames.RemoveAt(0);
                                GameLog.Print("Wormhole at {0}", system.Location);
                                break;
                            default:
                                if (starNames.Count == 0)
                                {
                                    system.Name = "System " + system.ObjectID;
                                    break;
                                }
                                system.Name = starNames[0];
                                starNames.RemoveAt(0);
                                break;
                        }

                        if (starType.SupportsPlanets())
                        {
                            for (var i = 0; i < maxPlanets - 1; i++)
                            {
                                var planetSize = GetPlanetSize(system.StarType, i);
                                if (planetSize != PlanetSize.NoWorld)
                                {
                                    var planet = new Planet
                                                 {
                                                     PlanetSize = planetSize,
                                                     PlanetType = GetPlanetType(system.StarType, planetSize, i),
                                                     Variation = Statistics.Random(Planet.MaxVariations),
                                                     Bonuses = PlanetBonus.Random
                                                 };
                                    //planet.Slot = i;
                                    //if (planet.PlanetType != PlanetType.Asteroids)
                                    //    PlaceMoons(planet);
                                    planets.Add(planet);
                                }
                                if (system.StarType == StarType.Nebula)
                                    break;
                            }

                            var rndSystemBonusType = Statistics.Random(8);
                            switch (rndSystemBonusType)
                            {
                                case 1:
                                    system.AddBonus(SystemBonus.Dilithium);
                                    break;
                                case 2:
                                case 3:
                                case 4:
                                    system.AddBonus(SystemBonus.RawMaterials);
                                    break;
                                case 5:
                                    system.AddBonus(SystemBonus.Dilithium);
                                    system.AddBonus(SystemBonus.RawMaterials);
                                    break;
                                default:
                                    break;
                            }

                            system.AddPlanets(planets);
                            SetPlanetNames(system);
                            PlaceBonuses(system);
                        }

                        // There's no planet to have resources so doesn't make sense to have bonuses for them
                        if (system.Planets.Count == 0)
                        {
                            system.RemoveBonus(SystemBonus.Dilithium);
                            system.RemoveBonus(SystemBonus.Random);
                            system.RemoveBonus(SystemBonus.RawMaterials);
                        }

                        GameContext.Current.Universe.Objects.Add(system);
                        GameContext.Current.Universe.Map[position].System = system;
                    }
                    finally
                    {
                        GameContext.PopThreadContext();
                    }
                });
        }

        private static StarType GetStarType()
        {
            var result = StarType.White;
            var maxRoll = 0;
            foreach (var type in EnumUtilities.GetValues<StarType>())
            {
                var currentRoll = DieRoll.Roll(100 + StarTypeDist[type]);
                if (currentRoll > maxRoll)
                {
                    result = type;
                    maxRoll = currentRoll;
                }
            }
            return result;
        }

        private static int GetPlanetSizeScore(StarType starType, PlanetSize planetSize, int slot)
        {
            return DieRoll.Roll(100)
                   + StarTypeModToPlanetSizeDist[starType, planetSize]
                   + SlotModToPlanetSizeDist[slot, planetSize];
        }

        private static PlanetSize GetPlanetSize(StarType starType, int slot)
        {
            var result = PlanetSize.NoWorld;
            var maxRoll = 0;
            foreach (var size in EnumUtilities.GetValues<PlanetSize>())
            {
                var currentRoll = GetPlanetSizeScore(starType, size, slot);
                if (currentRoll > maxRoll)
                {
                    result = size;
                    maxRoll = currentRoll;
                }
            }
            return result;
        }

        private static PlanetType GetPlanetType(StarType starType, PlanetSize size, int slot)
        {
            if (size == PlanetSize.Asteroids)
                return PlanetType.Asteroids;

            var result = PlanetType.Barren;
            var maxRoll = 0;

            foreach (var type in EnumUtilities.GetValues<PlanetType>())
            {
                var currentRoll = GetPlanetTypeScore(starType, size, type, slot);
                if (currentRoll > maxRoll)
                {
                    result = type;
                    maxRoll = currentRoll;
                }
            }

            return result;
        }

        private static int GetPlanetTypeScore(StarType starType, PlanetSize planetSize, PlanetType planetType, int slot)
        {
            return DieRoll.Roll(100)
                   + StarTypeModToPlanetTypeDist[starType, planetType]
                   + PlanetSizeModToPlanetTypeDist[planetSize, planetType]
                   + SlotModToPlanetTypeDist[slot, planetType];
        }

        private static void PlaceMoons()
        {
            var moons = new List<MoonType>(Planet.MaxMoonsPerPlanet);
            foreach (var system in GameContext.Current.Universe.Find<StarSystem>())
            {
                foreach (var planet in system.Planets)
                {
                    var handicap = 0;

                    moons.Clear();

                    if (planet.PlanetType == PlanetType.Asteroids)
                        continue;

                    for (var i = 0; i < Planet.MaxMoonsPerPlanet; i++)
                    {
                        var maxRoll = handicap;
                        var moonSize = MoonSize.NoMoon;

                        foreach (var moon in EnumUtilities.GetValues<MoonSize>())
                        {
                            var currentRoll = DieRoll.Roll(100)
                                              + PlanetSizeModToMoonSizeDist[planet.PlanetSize, moon]
                                              + PlanetTypeModToMoonSizeDist[planet.PlanetType, moon]
                                              - handicap;

                            if (currentRoll > maxRoll)
                            {
                                moonSize = moon;
                                maxRoll = currentRoll;
                            }
                        }

                        if (moonSize != MoonSize.NoMoon)
                            moons.Add(moonSize.GetType(RandomProvider.NextEnum<MoonShape>()));

                        handicap += (maxRoll / Planet.MaxMoonsPerPlanet);
                    }
                    planet.Moons = moons.ToArray();
                }
            }
        }
    }
}