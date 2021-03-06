// CombatUnit.cs
//
// Copyright (c) 2007 Mike Strobel
//
// This source code is subject to the terms of the Microsoft Reciprocal License (Ms-RL).
// For details, see <http://www.opensource.org/licenses/ms-rl.html>.
//
// All other rights reserved.

using System;
using Supremacy.Entities;
using Supremacy.Game;
using Supremacy.Orbitals;
using Supremacy.Resources;
using Supremacy.Types;
using Supremacy.Utility;

namespace Supremacy.Combat
{
    [Serializable]
    public class CombatUnit : IEquatable<CombatUnit>
    {
        private readonly int _sourceId;
        private readonly int _ownerId;
        private int _hullStrength;
        private int _shieldStrength;
        private int _firepower;
        private int _remainingFirepower;
        private bool _isCloaked;
        private bool _isCamouflaged;
        private bool _isAssimilated;
        private readonly string _name;
        private int _cloakStrength = 0;
        private int _camouflagedStrength = 0;
        private int _scanStrength = 0;
        private double _accuracy = 0d;
        private double _damageControl = 0d;

        protected CombatUnit(System.Collections.Generic.IEnumerable<Ship> ship) { }

        public CombatUnit()
        {

        }
        public CombatUnit(Orbital source)
        {
            if (source == null)
                throw new ArgumentNullException("source");

            var ship = source as Ship;
            if (ship != null)
            {
                _isCloaked = ship.IsCloaked;
                _isCamouflaged = ship.IsCamouflaged;
                _isAssimilated = ship.IsAssimilated;
                if (source.CloakStrength != null)
                    _cloakStrength = source.CloakStrength.CurrentValue;
                if (source.CamouflagedMeter != null)
                    _camouflagedStrength = source.CamouflagedMeter.CurrentValue;
            }
            _sourceId = source.ObjectID;
            _ownerId = source.OwnerID;
            _hullStrength = source.HullStrength.CurrentValue;
            _firepower = (source.OrbitalDesign.PrimaryWeapon.Damage * source.OrbitalDesign.PrimaryWeapon.Count) + (source.OrbitalDesign.SecondaryWeapon.Damage * source.OrbitalDesign.SecondaryWeapon.Count);
            _remainingFirepower = _firepower;
            _shieldStrength = source.ShieldStrength.CurrentValue;
            _name = source.Name;
            _accuracy = source.GetAccuracyModifier();
            _damageControl = source.GetDamageControlModifier();
        }

        public Orbital Source
        {
            get { return GameContext.Current.Universe.Get<Orbital>(_sourceId); }
        }

        public string Name
        {
            get { return _name; }
        }

        public string Description
        {
            get
            {
                //GameLog.Client.GameData.DebugFormat("CombatUnit.cs: : Description.GetString");

                if (Source is Station)
                {
                    return ResourceManager.GetString("COMBAT_DESCRIPTION_STATION");
                }
                if (Source is Ship)
                {
                    return String.Format(
                        ResourceManager.GetString("COMBAT_DESCRIPTION_SHIP"),
                        ((Ship)Source).ShipDesign.ClassName,
                        GameContext.Current.Tables.EnumTables["ShipType"][((Ship)Source).ShipType.ToString()][0]);
                }
                return null;
            }
        }

        public Civilization Owner
        {
            get { return GameContext.Current.Civilizations[_ownerId]; }
        }

        public int OwnerID
        {
            get { return _ownerId; }
        }

        public int Firepower
        {
            get
            {
                return _firepower;
            }
        }

        public int RemainingFirepower
        {
            get
            {
                return _remainingFirepower;
            }
            set
            {
                _remainingFirepower = value;
            }
        }

        public double Accuracy
        {
            get { return _accuracy; }
        }

        public double DamageControl
        {
            get { return _damageControl; }
        } 
        public int HullStrength
        {
            get { return _hullStrength; }
        }

        public Percentage HullIntegrity
        {

            get
            {
                if (HullStrength == 0)
                {
                    return 0;
                }
                else
                return ((float)HullStrength / Source.OrbitalDesign.HullStrength);
            }
        }

        public int ShieldStrength
        {
            get { return _shieldStrength; }
        }

        public Percentage ShieldIntegrity
        {
            get
            {
                if (ShieldStrength == 0)
                {
                    return 0;
                }
                else
                {
                    GameLog.Client.Test.DebugFormat("ShieldStrenth crash owner ={0} {1} {2}", Source.Owner.Key, Source.Design.Key, Source.ShieldStrength.CurrentValue);
                    { return ((float)ShieldStrength / Source.OrbitalDesign.ShieldStrength); }
                }
            }
        }

        public bool IsCloaked
        {
            get { return _isCloaked; }
        }

        public int CloakStrength
        {
            get { return _cloakStrength; }
        }

        public bool IsCamouflaged
        {
            get { return _isCamouflaged; }
        }

        public int CamouflagedStrength
        {
            get { return _camouflagedStrength; }
        }

        public int ScanStrength
        {
            get { return _scanStrength; }
        }

        public bool IsDestroyed
        {
            get { return (HullStrength <= 0); }
        }

        public bool IsAssimilated
        {
            get { return _isAssimilated; }
            set {_isAssimilated = value; }
        }

        public bool IsMobile
        {
            get { return Source.IsMobile; }
        }

        public void Cloak()
        {
            _isCloaked = true;
        }
        public void Camouflage()
        {
            _isCamouflaged = true;
        }

        public void Decloak()
        {
            _isCloaked = false;
        }
   
        public void Decamouflage()
        {
            _isCamouflaged = false;
        }

        public void TakeDamage(int damage)
        {
            var remainingDamage = damage;
            if (_shieldStrength > 0)
            {
                remainingDamage = Math.Max(0, remainingDamage - _shieldStrength);
                _shieldStrength = Math.Max(0, _shieldStrength - damage);
            }
            _hullStrength = Math.Max(0, _hullStrength - remainingDamage);
        }

        public void UpdateSource()
        {
            var source = Source;
            if (source == null)
                return;

            source.ShieldStrength.Reset(ShieldStrength);
            source.HullStrength.Reset(HullStrength);

            if (!source.HullStrength.IsMinimized)
                source.RegenerateShields();
        }

        public static bool operator ==(CombatUnit left, CombatUnit right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(CombatUnit left, CombatUnit right)
        {
            return !Equals(left, right);
        }

        public bool Equals(CombatUnit combatUnit)
        {
            if (ReferenceEquals(combatUnit, this))
                return true;
            if (ReferenceEquals(combatUnit, null))
                return false;
            return (_sourceId == combatUnit._sourceId);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as CombatUnit);
        }

        public override int GetHashCode()
        {
            return _sourceId;
        }
    }
}

