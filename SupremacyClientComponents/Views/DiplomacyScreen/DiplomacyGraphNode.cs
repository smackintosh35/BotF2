﻿using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading;
using System.Windows.Input;

using Supremacy.Entities;
using Supremacy.Utility;

namespace Supremacy.Client.Views
{
    public class DiplomacyGraphNode : INotifyPropertyChanged
    {
        private readonly Civilization _civilization;
        private readonly ICommand _selectNodeCommand;
        private readonly ObservableCollection<DiplomacyGraphNode> _children;

        public DiplomacyGraphNode(Civilization civilization, ICommand selectNodeCommand)
        {
            if (civilization == null)
                throw new ArgumentNullException("civilization");

            _civilization = civilization;
            _selectNodeCommand = selectNodeCommand;
            _children = new ObservableCollection<DiplomacyGraphNode>();
        }

        public Civilization Civilization
        {
            get { return _civilization; }
        }

        public ICommand SelectNodeCommand
        {
            get { return _selectNodeCommand; }
        }

        public ObservableCollection<DiplomacyGraphNode> Children
        {
            get { return _children; }
        }

        public string ToolTip
        {
            get { return _civilization.ShortName; }
        }

        #region Implementation of INotifyPropertyChanged

        [NonSerialized] private PropertyChangedEventHandler _propertyChanged;

        event PropertyChangedEventHandler INotifyPropertyChanged.PropertyChanged
        {
            add
            {
                while (true)
                {
                    var oldHandler = _propertyChanged;
                    var newHandler = (PropertyChangedEventHandler)Delegate.Combine(oldHandler, value);

                    if (Interlocked.CompareExchange(ref _propertyChanged, newHandler, oldHandler) == oldHandler)
                        return;
                }
            }
            remove
            {
                while (true)
                {
                    var oldHandler = _propertyChanged;
                    var newHandler = (PropertyChangedEventHandler)Delegate.Remove(oldHandler, value);

                    if (Interlocked.CompareExchange(ref _propertyChanged, newHandler, oldHandler) == oldHandler)
                        return;
                }
            }
        }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            _propertyChanged.Raise(this, propertyName);
        }

        #endregion
    }
}