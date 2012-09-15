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

using GHIElectronics.NETMF.Net;
using GHIElectronics.NETMF.Net.Sockets;


namespace JDI.NETMF.WIZnet
{
    public class NTPClient : IDisposable
    {
        #region Constructors and Destructors

        public NTPClient()
        {
            this.lastErrorMsg = "";
        }

        public virtual void Dispose()
        {
            this.lastErrorMsg = null;
        }

        #endregion

        #region Properties

        public string LastErrorMsg
        {
            get { return this.lastErrorMsg; }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Get time from NTP server
        /// </summary>
        /// <param name="timeServer">Time server to use</param>
        /// <param name="gmtOffset">GMT offset in minutes</param>
        /// <returns>NTP date-time or DateTime.MinValue when an error has occured.</returns>
        public DateTime GetNTPTime(string timeServer, int gmtOffset = 0)
        {
            this.lastErrorMsg = "";
            Socket s = null;
            try
            {
                // init socket
                EndPoint remoteEP = new IPEndPoint(Dns.GetHostEntry(timeServer).AddressList[0], 123);
                s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

                // init request
                byte[] ntpData = new byte[48];
                Array.Clear(ntpData, 0, 48);
                ntpData[0] = 0x1B; // set protocol version

                // send request
                s.SendTo(ntpData, remoteEP);

                // wait 30s if no response, timeout
                if (s.Poll(30 * 1000 * 1000, SelectMode.SelectRead))
                {
                    // get response
                    s.ReceiveFrom(ntpData, ref remoteEP);

                    s.Close();

                    // parse time value
                    byte offsetTransmitTime = 40;
                    ulong intpart = 0;
                    ulong fractpart = 0;
                    for (int i = 0; i <= 3; i++) intpart = (intpart << 8) | ntpData[offsetTransmitTime + i];
                    for (int i = 4; i <= 7; i++) fractpart = (fractpart << 8) | ntpData[offsetTransmitTime + i];
                    ulong milliseconds = (intpart * 1000 + (fractpart * 1000) / 0x100000000L);

                    DateTime ntpTime = new DateTime(1900, 1, 1) + TimeSpan.FromTicks((long)milliseconds * TimeSpan.TicksPerMillisecond);

                    return ntpTime.AddMinutes(gmtOffset);
                }
                else
                {
                    // timeout
                    s.Close();
                    this.lastErrorMsg = "Timed out.";
                }
            }
            catch (Exception e)
            {
                try
                {
                    s.Close();
                }
                catch { }
                this.lastErrorMsg = e.Message;
            }

            return DateTime.MinValue;
        }

        #endregion

        #region Fields

        private string lastErrorMsg;

        #endregion
    }
}
