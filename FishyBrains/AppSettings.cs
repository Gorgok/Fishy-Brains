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

using JDI.NETMF.Net;

namespace FishyBrains
{
    public class AppSettings : NetSettings
    {
        #region Properties
        public string Password;
        public string wStartH;
        public string wStartM;
        public string wEndH;
        public string wEndM;
        public string wMax;
        public string wRamp;
        public string bStartH;
        public string bStartM;
        public string bEndH;
        public string bEndM;
        public string bMax1;
        public string bMax2;
        public string bRamp;
        public string rStartH;
        public string rStartM;
        public string rEndH;
        public string rEndM;

        public int wStartHint
        { get { try { return int.Parse(this.wStartH); } catch { return 0; } } }
        public int wStartMint
        { get { try { return int.Parse(this.wStartM); } catch { return 0; } } }
        public int wEndHint
        { get { try { return int.Parse(this.wEndH); } catch { return 0; } } }
        public int wEndMint
        { get { try { return int.Parse(this.wEndM); } catch { return 0; } } }
        public int wMaxint
        { get { try { return int.Parse(this.wMax); } catch { return 0; } } }
        public int wRampint
        { get { try { return int.Parse(this.wRamp); } catch { return 0; } } }
        public int bStartHint
        { get { try { return int.Parse(this.bStartH); } catch { return 0; } } }
        public int bStartMint
        { get { try { return int.Parse(this.bStartM); } catch { return 0; } } }
        public int bEndHint
        { get { try { return int.Parse(this.bEndH); } catch { return 0; } } }
        public int bEndMint
        { get { try { return int.Parse(this.bEndM); } catch { return 0; } } }
        public int bMax1int
        { get { try { return int.Parse(this.bMax1); } catch { return 0; } } }
        public int bMax2int
        { get { try { return int.Parse(this.bMax2); } catch { return 0; } } }
        public int bRampint
        { get { try { return int.Parse(this.bRamp); } catch { return 0; } } }
        public int rStartHint
        { get { try { return int.Parse(this.rStartH); } catch { return 0; } } }
        public int rStartMint
        { get { try { return int.Parse(this.rStartM); } catch { return 0; } } }
        public int rEndHint
        { get { try { return int.Parse(this.rEndH); } catch { return 0; } } }
        public int rEndMint
        { get { try { return int.Parse(this.rEndM); } catch { return 0; } } }

        protected override int numSettings
        {
            get { return base.numSettings + 18; }
        }
        #endregion

        #region Methods
        protected override void InitSettings()
        {
            // init base appSettings
            base.InitSettings();

            // init application appSettings
            this.Password = "";
            this.wStartH = "11";
            this.wStartM = "00";
            this.wEndH = "21";
            this.wEndM = "00";
            this.wMax = "80";
            this.wRamp = "120";
            this.bStartH = "10";
            this.bStartM = "00";
            this.bEndH = "22";
            this.bEndM = "00";
            this.bMax1 = "80";
            this.bMax2 = "80";
            this.bRamp = "120";
            this.rStartH = "20";
            this.rStartM = "00";
            this.rEndH = "12";
            this.rEndM = "00";
        }

        protected override string[] GetSettings()
        {
            // get base appSettings
            string[] settings = base.GetSettings();
            int index = base.numSettings;

            // add appSettings
            settings[index++] = this.Password;
            settings[index++] = this.wStartH;
            settings[index++] = this.wStartM;
            settings[index++] = this.wEndH;
            settings[index++] = this.wEndM;
            settings[index++] = this.wMax;
            settings[index++] = this.wRamp;
            settings[index++] = this.bStartH;
            settings[index++] = this.bStartM;
            settings[index++] = this.bEndH;
            settings[index++] = this.bEndM;
            settings[index++] = this.bMax1;
            settings[index++] = this.bMax2;
            settings[index++] = this.bRamp;
            settings[index++] = this.rStartH;
            settings[index++] = this.rStartM;
            settings[index++] = this.rEndH;
            settings[index] = this.rEndM;
            return settings;
        }

        protected override void SetSettings(string[] settings)
        {
            // load base appSettings
            base.SetSettings(settings);
            int index = base.numSettings;

            // load appSettings
            this.Password = settings[index++];
            this.wStartH = settings[index++];
            this.wStartM = settings[index++];
            this.wEndH = settings[index++];
            this.wEndM = settings[index++];
            this.wMax = settings[index++];
            this.wRamp = settings[index++];
            this.bStartH = settings[index++];
            this.bStartM = settings[index++];
            this.bEndH = settings[index++]; ;
            this.bEndM = settings[index++];
            this.bMax1 = settings[index++];
            this.bMax2 = settings[index++];
            this.bRamp = settings[index++];
            this.rStartH = settings[index++];
            this.rStartM = settings[index++];
            this.rEndH = settings[index++];
            this.rEndM = settings[index++];
        }

        protected override bool ValidateSettings()
        {
            // validate base appSettings
            if (base.ValidateSettings() == false)
            {
                return false;
            }

            // validate appSettings
            if (this.Password.Length == 0)
            {
                this.lastErrorMsg = "Password is required.";
                return false;
            }

            return true;
        }
        #endregion
    }
}
