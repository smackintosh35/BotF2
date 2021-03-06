// CombatOrders.cs
//
// Copyright (c) 2007 Mike Strobel
//
// This source code is subject to the terms of the Microsoft Reciprocal License (Ms-RL).
// For details, see <http://www.opensource.org/licenses/ms-rl.html>.
//
// All other rights reserved.

using System;
using System.Collections;
using System.Collections.Generic;

using Supremacy.Entities;
using Supremacy.Game;
using Supremacy.Orbitals;
using Supremacy.Utility;

namespace Supremacy.Combat
{

    [Serializable]
    public class  CombatTargetPrimaries 
    {
        private readonly int _combatId;
        private readonly int _ownerId; 
        private readonly Dictionary<int, Civilization> _targetPrimaries;

        public int CombatID
        {
            get { return _combatId; }
        }

        public int OwnerID
        {
            get { return _ownerId; }
        }
        public Civilization Owner
        {
            get { return GameContext.Current.Civilizations[_ownerId]; }
        }

        public CombatTargetPrimaries(Civilization owner, int combatId)
        {
            if (owner == null)
            {
                throw new ArgumentNullException("owner");
            }
            _ownerId = owner.CivID;
            _targetPrimaries = new Dictionary<int, Civilization>();
            _combatId = combatId;

        }

        public void SetTargetOneCiv(Orbital source, Civilization targetOne)
        {
            if (source == null)
                //.Core.Test.DebugFormat("Orbital source = null (!!!)");
            if (targetOne == null)
                //GameLog.Core.Test.DebugFormat("target one Civ = null(!!!)");

            GameLog.Core.CombatDetails.DebugFormat("Dictionary attacker = {0} {1} Target = {2}",source.Owner.Key, source.Name, targetOne.Key);
            _targetPrimaries[source.ObjectID] = targetOne;   // Ditctionary of orbital shooter object id and its civ target          
        }

        public bool IsTargetOneSet(Orbital source)
        {
            if (source == null)
                return false;
            return _targetPrimaries.ContainsKey(source.ObjectID);
        }

        public Civilization GetTargetOne(Orbital source)
        {
            if (source == null)
            {
                throw new ArgumentNullException("source");
            }
            if (!_targetPrimaries.ContainsKey(source.ObjectID))
            {
                _targetPrimaries[source.ObjectID] = CombatHelper.GetDefaultHoldFireCiv();
                //throw new ArgumentException("No target one has been set for the specified source");
            }
            GameLog.Core.CombatDetails.DebugFormat("Orbital name {0} in GetTargetOne() targeting {1}", source.Name, _targetPrimaries[source.ObjectID]);
            return _targetPrimaries[source.ObjectID];           
        }

    }
}
