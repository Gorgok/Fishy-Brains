/*
	Copyright (c), 2011,2012 JASDev International  http://www.jasdev.com
	All rights reserved.

	Licensed under the Apache License, Version 2.0 (the "License").
	You may not use this file except in compliance with the License. 
	You may obtain a copy of the License at
 
		http://www.apache.org/licenses/LICENSE-2.0

	Unless required by applicable law or agreed to in writing, software 
	distributed under the License is distributed on an "AS IS" BASIS 
	WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
*/

using System;
using System.Threading;

using Microsoft.SPOT.Hardware;

using GHIElectronics.NETMF.Net;
using GHIElectronics.NETMF.Net.NetworkInformation;

using JDI.NETMF.Net;


namespace JDI.NETMF.Shields
{
    public class FEZConnect : IDisposable
    {
        #region Enums

        public enum DevStatus
        {
            Startup = 0,
            Ready,
            Error
        }

        public enum NetStatus
        {
            Startup = 0,
            Ready,
            Error
        }

        #endregion


        #region Constructors and Destructors

        public FEZConnect()
        {
            this.lastResetCause = GHIElectronics.NETMF.Hardware.LowLevel.Watchdog.LastResetCause;
            this.deviceStatus = DevStatus.Startup;
            this.networkStatus = NetStatus.Startup;
            this.lastErrorMsg = "";
        }

        public void Dispose()
        {
            this.lastErrorMsg = null;
        }

        #endregion


        #region Properties

        public GHIElectronics.NETMF.Hardware.LowLevel.Watchdog.ResetCause LastResetCause
        {
            get { return this.lastResetCause; }
        }

        public DevStatus DeviceStatus
        {
            get { return this.deviceStatus; }
        }

        public NetStatus NetworkStatus
        {
            get { return this.networkStatus; }
        }

        public string LastErrorMsg
        {
            get
            {
                string temp = this.lastErrorMsg;
                this.lastErrorMsg = "";
                return temp;
            }
        }

        #endregion


        #region Methods

        /// <summary>
        /// Initializes the FEZ Connect device.
        /// </summary>
        /// <param name="spiModule"></param>
        /// <param name="chipSelectPin"></param>
        /// <param name="resetPin"></param>
        /// <param name="reserveSocket"></param>
        public void InitializeDevice(SPI.SPI_module spiModule, Cpu.Pin chipSelectPin, Cpu.Pin resetPin, bool reserveSocket)
        {
            this.deviceStatus = DevStatus.Startup;
            try
            {
                WIZnet_W5100.Enable(spiModule, chipSelectPin, resetPin, reserveSocket);
                this.deviceStatus = DevStatus.Ready;
            }
            catch (Exception e)
            {
                this.deviceStatus = DevStatus.Error;
                this.lastErrorMsg = e.Message;
            }
        }

        /// <summary>
        /// Initializes the network stack.
        /// </summary>
        /// <param name="netSettings"></param>
        public void InitializeNetwork(NetSettings netSettings)
        {
            this.networkStatus = NetStatus.Startup;
            try
            {
                // stop network if already running
                if (this.networkStatus == NetStatus.Ready)
                {
                    Dhcp.ReleaseDhcpLease();
                    Thread.Sleep(8000);	// allow time for dhcp changes
                }

                // initialize tcp stack
                WIZnet_W5100.ReintializeNetworking();

                // set IP address
                if (netSettings.DHCPEnabled)
                {
                    // dynamic IP address
                    Dhcp.EnableDhcp(netSettings.MACAddressBytes, netSettings.HostName);
                }
                else
                {
                    // static IP address
                    NetworkInterface.EnableStaticIP(netSettings.IPAddressBytes, netSettings.IPMaskBytes,
                                                    netSettings.IPGatewayBytes, netSettings.MACAddressBytes);
                    NetworkInterface.EnableStaticDns(netSettings.DNSAddressBytes);
                }

                this.networkStatus = NetStatus.Ready;
            }
            catch (Exception e)
            {
                this.networkStatus = NetStatus.Error;
                this.lastErrorMsg = e.Message;
            }
        }

        #endregion


        #region Fields

        private GHIElectronics.NETMF.Hardware.LowLevel.Watchdog.ResetCause lastResetCause;
        private DevStatus deviceStatus;
        private NetStatus networkStatus;
        private string lastErrorMsg;

        #endregion

    }
}
