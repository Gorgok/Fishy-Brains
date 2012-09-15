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
using System.Text;

using Microsoft.SPOT;
using Microsoft.SPOT.Hardware;

using GHIElectronics.NETMF.System;
using GHIElectronics.NETMF.Hardware;


namespace JDI.NETMF.Storage
{
    public abstract class FlashSettings
    {
        #region Constructors

        public FlashSettings()
        {
            this.InitSettings();
        }

        #endregion


        #region Properties

        public string LastErrorMsg
        {
            get
            {
                string temp = this.lastErrorMsg;
                this.lastErrorMsg = "";
                return temp;
            }
        }

        protected virtual int numSettings
        {
            get { return 0; }
        }

        #endregion


        #region Methods

        public bool SaveToFlash()
        {
            byte[] buffer = null;

            try
            {
                // validate appSettings
                if (this.ValidateSettings() == false)
                {
                    return false;
                }
                Debug.GC(true);

                // get serialized appSettings
                string settingsData = "";
                string[] settings = this.GetSettings();
                foreach (string setting in settings)
                {
                    settingsData += (settingsData.Length > 0 ? "|" : "") + setting;
                }

                // create temporary byteBuffer
                buffer = new byte[InternalFlashStorage.Size];

                // write length of serialized appSettings to byteBuffer
                Utility.InsertValueIntoArray(buffer, 0, 2, (uint)settingsData.Length);

                // write serialized appSettings to byteBuffer
                Util.InsertValueIntoArray(settingsData, buffer, 6, false);

                // write checksum to byteBuffer
                uint checkSum = Utility.ComputeCRC(buffer, 6, settingsData.Length, 0);
                Utility.InsertValueIntoArray(buffer, 2, 4, checkSum);

                // write appSettings to Flash          
                InternalFlashStorage.Write(buffer);

                return true;
            }
            catch (Exception e)
            {
                this.lastErrorMsg = e.Message;
            }
            finally
            {
                // cleanup
                buffer = null;
                Debug.GC(true);
            }

            return false;
        }

        public bool LoadFromFlash()
        {
            byte[] buffer = null;
            byte[] settingsBytes = null;
            string settingsString = null;
            string[] settings = null;

            try
            {
                // create temporary byteBuffer
                buffer = new byte[InternalFlashStorage.Size];

                // read appSettings from Flash
                InternalFlashStorage.Read(buffer);

                // get appSettings length
                int dataLen = (int)Utility.ExtractValueFromArray(buffer, 0, 2);
                if (dataLen <= 0 || dataLen >= constMaxDataLength)
                {
                    this.lastErrorMsg = "Total data length is invalid (" + dataLen + ").";
                    return false;
                }

                // get checksum
                uint checkSum = Utility.ExtractValueFromArray(buffer, 2, 4);
                uint checkSumComputed = Utility.ComputeCRC(buffer, 6, dataLen, 0);
                if (checkSum != checkSumComputed)
                {
                    this.lastErrorMsg = "Checksum is invalid.";
                    return false;
                }

                // clear existing appSettings
                this.InitSettings();

                // get appSettings
                settingsBytes = Utility.ExtractRangeFromArray(buffer, 6, dataLen);
                settingsString = new String(Encoding.UTF8.GetChars(settingsBytes));

                // parse appSettings
                settings = settingsString.Split('|');
                if (settings.Length != this.numSettings)
                {
                    this.lastErrorMsg = "Number of settings is invalid (" + settings.Length + ").";
                    return false;
                }

                // set appSettings from saved data
                this.SetSettings(settings);

                return true;
            }
            catch (Exception e)
            {
                this.lastErrorMsg = e.Message;
            }
            finally
            {
                // cleanup
                buffer = null;
                settingsBytes = null;
                settingsString = null;
                settings = null;
                Debug.GC(true);
            }

            return false;
        }

        protected virtual void InitSettings()
        {
            this.lastErrorMsg = "";
        }

        protected virtual string[] GetSettings()
        {
            return new string[this.numSettings];
        }

        protected virtual void SetSettings(string[] settings)
        {
        }

        protected virtual bool ValidateSettings()
        {
            return true;
        }

        #endregion


        #region Helper Methods

        protected int ParseInt(string value, int defaultValue)
        {
            try
            {
                return int.Parse(value);
            }
            catch
            {
                return defaultValue;
            }
        }

        #endregion


        #region Fields

        protected string lastErrorMsg;

        #endregion


        #region Constants

        private const int constMaxDataLength = 500;

        #endregion
    }
}
