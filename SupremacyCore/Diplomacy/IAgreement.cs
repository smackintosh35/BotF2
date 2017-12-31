﻿// IAgreement.cs
//
// Copyright (c) 2007 Mike Strobel
//
// This source code is subject to the terms of the Microsoft Reciprocal License (Ms-RL).
// For details, see <http://www.opensource.org/licenses/ms-rl.html>.
//
// All other rights reserved.

using System;
using System.Collections.Generic;

using Supremacy.Annotations;
using Supremacy.Entities;
using Supremacy.Game;
using Supremacy.Collections;

namespace Supremacy.Diplomacy
{
    public interface IDiplomaticExchange
    {
        Civilization Sender { get; }
        Civilization Recipient { get; }
    }

    public interface IAgreement : IDiplomaticExchange
    {
        GameObjectID SenderID { get; }
        GameObjectID RecipientID { get; }
        TurnNumber StartTurn { get; }
        TurnNumber EndTurn { get; }
        IProposal Proposal { get; }
        IDictionary<object, object> Data { get; } 
    }

    [Serializable]
    public class NewAgreement : IAgreement
    {
        private readonly IProposal _proposal;
        private readonly TurnNumber _startTurn;
        private readonly IDictionary<object, object> _data;
        private TurnNumber _endTurn;

        public NewAgreement([NotNull] IProposal proposal, TurnNumber startTurn, IDictionary<object, object> data)
        {
            if (proposal == null)
                throw new ArgumentNullException("proposal");

            _proposal = proposal;
            _startTurn = startTurn;
            _endTurn = TurnNumber.Undefined;
            _data = data;
        }

        #region Implementation of IAgreement

        public GameObjectID SenderID
        {
            get { return Proposal.Sender.CivID; }
        }

        public GameObjectID RecipientID
        {
            get { return Proposal.Recipient.CivID; }
        }

        public Civilization Sender
        {
            get { return GameContext.Current.Civilizations[SenderID]; }
        }

        public Civilization Recipient
        {
            get { return GameContext.Current.Civilizations[RecipientID]; }
        }

        public TurnNumber StartTurn
        {
            get { return _startTurn; }
        }

        public TurnNumber EndTurn
        {
            get { return _endTurn; }
        }

        public IProposal Proposal
        {
            get { return _proposal; }
        }

        public IDictionary<object, object> Data
        {
            get
            {
                if (_data == null)
                    return null;
                return _data.AsReadOnly();
            }
        }

        #endregion

        public void End()
        {
            if (_endTurn.IsUndefined)
                _endTurn = GameContext.Current.TurnNumber;
        }
    }
}
