// SupremacyServiceHost.cs
//
// Copyright (c) 2007 Mike Strobel
//
// This source code is subject to the terms of the Microsoft Reciprocal License (Ms-RL).
// For details, see <http://www.opensource.org/licenses/ms-rl.html>.
//
// All other rights reserved.

using Supremacy.Utility;
using System;
using System.ServiceModel;
using System.ServiceModel.Description;

namespace Supremacy.WCF
{
    [Serializable]
    public sealed class SupremacyServiceHost : MarshalByRefObject
    {
        private ServiceHost _serviceHost;

        public event Action<EventArgs> ServiceOpened;
        public event Action<EventArgs> ServiceFaulted;
        public event Action<EventArgs> ServiceClosed;

        public void StartService(bool localEndpointOnly)
        {
            //Consider putting the baseAddress in the configuration system
            //and getting it here with AppSettings
            //Uri baseAddress = new Uri("http://localhost:8080/SupremacyService");

            //NetNamedPipeBinding localBinding = new NetNamedPipeBinding(NetNamedPipeSecurityMode.None);
            //string localAddress = "net.pipe://localhost/SupremacyService/Local";

            //Instantiate new ServiceHost 
            _serviceHost = new ServiceHost(new SupremacyService());
            //_serviceHost.Opened += OnServiceOpened;
            //_serviceHost.Faulted += OnServiceFaulted;
            //_serviceHost.Closed += OnServiceClosed;

            //_serviceHost.AddServiceEndpoint(typeof(ISupremacyService), localBinding, localAddress);

            for (int i = 0; i < _serviceHost.Description.Endpoints.Count; i++)
            {
                for (int j = 0; j < _serviceHost.Description.Endpoints[i].Contract.Operations.Count; j++)
                {
                    OperationDescription od = _serviceHost.Description.Endpoints[i].Contract.Operations[j];
                    od.Behaviors.Remove<DataContractSerializerOperationBehavior>();
                    od.Behaviors.Add(new PORDCSOB(od));
                }
            }

            if (localEndpointOnly)
            {
                for (int i = 0; i < _serviceHost.Description.Endpoints.Count; i++)
                {
                    if (_serviceHost.Description.Endpoints[i].Binding is NetTcpBinding)
                        _serviceHost.Description.Endpoints.RemoveAt(i--);
                }
            }

            //Open myServiceHost
            _serviceHost.Open();

            ((SupremacyService)_serviceHost.SingletonInstance).Host = _serviceHost;
            ((SupremacyService)_serviceHost.SingletonInstance).StartHeartbeat();
        }

        private void OnServiceFaulted(object sender, EventArgs e)
        {
            var handler = ServiceFaulted;
            if (handler != null)
                handler(EventArgs.Empty);
        }

        private void OnServiceOpened(object sender, EventArgs e)
        {
            var handler = ServiceOpened;
            if (handler != null)
                handler(EventArgs.Empty);
        }

        private void OnServiceClosed(object sender, EventArgs e)
        {
            var handler = ServiceClosed;
            if (handler != null)
                handler(EventArgs.Empty);
        }

        public void StopService()
        {
            //Call StopService from your shutdown logic (i.e. dispose method)
            if (_serviceHost.State != CommunicationState.Closed)
            {
                ((SupremacyService)_serviceHost.SingletonInstance).StopHeartbeat();
                ((SupremacyService)_serviceHost.SingletonInstance).Terminate();

                try
                {
                    _serviceHost.Close();
                }
                catch (Exception e) //ToDo: Just log or additional handling necessary?
                {
                    GameLog.LogException(e);
                }
            }
        }
    }
}
