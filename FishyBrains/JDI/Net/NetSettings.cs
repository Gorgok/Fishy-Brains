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

using JDI.NETMF.Storage;


namespace JDI.NETMF.Net
{
    public class NetSettings : FlashSettings
    {
        #region Properties

        public string HostName;
        public string MACAddress;
        public bool DHCPEnabled;
        public string IPAddress;
        public string IPMask;
        public string IPGateway;
        public string DNSAddress;
        public bool NTPEnabled;
        public string NTPServer;
        public string NTPOffset;
        public bool HTTPEnabled;
        public string HTTPPrefix;
        public string HTTPPort;

        public byte[] MACAddressBytes
        {
            get { return NetUtils.MACAddressToBytes(this.MACAddress); }
        }

        public byte[] IPAddressBytes
        {
            get { return NetUtils.IPAddressToBytes(this.IPAddress); }
        }

        public byte[] IPMaskBytes
        {
            get { return NetUtils.IPAddressToBytes(this.IPMask); }
        }

        public byte[] IPGatewayBytes
        {
            get { return NetUtils.IPAddressToBytes(this.IPGateway); }
        }

        public byte[] DNSAddressBytes
        {
            get { return NetUtils.IPAddressToBytes(this.DNSAddress); }
        }

        public int NTPOffsetInt
        {
            get { try { return int.Parse(this.NTPOffset); } catch { return 0; } }
        }

        public int HTTPPortInt
        {
            get { try { return int.Parse(this.HTTPPort); } catch { return 0; } }
        }

        protected override int numSettings
        {
            get { return base.numSettings + 13; }
        }

        #endregion


        #region Methods

        protected override void InitSettings()
        {
            // init base appSettings
            base.InitSettings();

            // init network appSettings
            this.HostName = "";
            this.MACAddress = "";
            this.DHCPEnabled = false;
            this.IPAddress = "";
            this.IPMask = "";
            this.IPGateway = "";
            this.DNSAddress = "";
            this.NTPEnabled = false;
            this.NTPServer = "";
            this.NTPOffset = "0";
            this.HTTPEnabled = false;
            this.HTTPPrefix = "http";
            this.HTTPPort = "80";
        }

        protected override string[] GetSettings()
        {
            // get base appSettings
            string[] settings = base.GetSettings();
            int index = base.numSettings;

            // add network appSettings
            settings[index++] = this.HostName;
            settings[index++] = this.MACAddress;
            settings[index++] = this.DHCPEnabled.ToString();
            settings[index++] = this.IPAddress;
            settings[index++] = this.IPMask;
            settings[index++] = this.IPGateway;
            settings[index++] = this.DNSAddress;
            settings[index++] = this.NTPEnabled.ToString();
            settings[index++] = this.NTPServer;
            settings[index++] = this.NTPOffset;
            settings[index++] = this.HTTPEnabled.ToString();
            settings[index++] = this.HTTPPrefix;
            settings[index] = this.HTTPPort;

            return settings;
        }

        protected override void SetSettings(string[] settings)
        {
            // load base appSettings
            base.SetSettings(settings);
            int index = base.numSettings;

            // load network appSettings
            this.HostName = settings[index++];
            this.MACAddress = settings[index++];
            this.DHCPEnabled = (settings[index++] == bool.TrueString ? true : false);
            this.IPAddress = settings[index++];
            this.IPMask = settings[index++];
            this.IPGateway = settings[index++];
            this.DNSAddress = settings[index++];
            this.NTPEnabled = (settings[index++] == bool.TrueString ? true : false);
            this.NTPServer = settings[index++];
            this.NTPOffset = settings[index++];
            this.HTTPEnabled = (settings[index++] == bool.TrueString ? true : false);
            this.HTTPPrefix = settings[index++];
            this.HTTPPort = settings[index++];
        }

        protected override bool ValidateSettings()
        {
            // validate base appSettings
            if (base.ValidateSettings() == false)
            {
                return false;
            }

            // validate network appSettings
            if (this.HostName.Length == 0)
            {
                this.lastErrorMsg = "Host Name is required.";
                return false;
            }
            if (NetUtils.MACAddressToBytes(this.MACAddress) == null)
            {
                this.lastErrorMsg = "Invalid MAC address.";
                return false;
            }
            if (this.DHCPEnabled == false)
            {
                if (NetUtils.IPAddressToBytes(this.IPAddress) == null)
                {
                    this.lastErrorMsg = "Invalid IP address.";
                    return false;
                }
                if (NetUtils.IPAddressToBytes(this.IPMask) == null)
                {
                    this.lastErrorMsg = "Invalid IP mask.";
                    return false;
                }
                if (NetUtils.IPAddressToBytes(this.IPGateway) == null)
                {
                    this.lastErrorMsg = "Invalid IP gateway.";
                    return false;
                }
                if (NetUtils.IPAddressToBytes(this.DNSAddress) == null)
                {
                    this.lastErrorMsg = "Invalid DNS address.";
                    return false;
                }
            }
            if (this.NTPEnabled)
            {
                if (this.NTPServer.Length == 0)
                {
                    this.lastErrorMsg = "NTP Server is required.";
                    return false;
                }
                try { int.Parse(this.NTPOffset); }
                catch
                {
                    this.lastErrorMsg = "Invalid NTP offset.";
                    return false;
                }
            }
            if (this.HTTPEnabled)
            {
                this.HTTPPrefix = this.HTTPPrefix.ToLower();
                if (this.HTTPPrefix != "http" && this.HTTPPrefix != "https")
                {
                    this.lastErrorMsg = "Invalid HTTP prefix.";
                    return false;
                }
                try { int.Parse(this.HTTPPort); }
                catch
                {
                    this.lastErrorMsg = "Invalid HTTP port.";
                    return false;
                }
            }

            return true;
        }

        #endregion
    }
}
