﻿using System.Windows.Automation.Peers;

namespace Supremacy.Client.Views
{
    public partial class ColonyList
    {
        public ColonyList()
        {
            InitializeComponent();
        }

        //public SpiedOneColonyList()
        //{
        //    InitializeComponent();
        //}
        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return null;
        }
    }
}