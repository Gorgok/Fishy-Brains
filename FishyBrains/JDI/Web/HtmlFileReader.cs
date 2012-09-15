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

using System.IO;
using System.Collections;


namespace JDI.NETMF.Web
{
    public class HtmlFileReader : HtmlReader
    {
        #region Constructors / Destructors

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="filePath">Full path to HTML file.</param>
        /// <param name="tokens">Token name-value pairs.</param>
        /// <param name="lineBufferSize">Must be larger than longest line length including \r\n.</param>
        public HtmlFileReader(string filePath, Hashtable tokens, char tokenDelimiter = '~', int lineBufferSize = constLineBufferSize)
            : base(tokens, tokenDelimiter, lineBufferSize)
        {
            this.fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.None, constFileBufferSize);
        }

        public override void Dispose()
        {
            if (this.fileStream != null)
            {
                this.fileStream.Close();
                this.fileStream.Dispose();
                this.fileStream = null;
            }
            base.Dispose();
        }

        #endregion


        #region Line Processing Methods

        protected override int GetNextByte()
        {
            return this.fileStream.ReadByte();
        }

        #endregion


        #region Fields

        protected FileStream fileStream;

        #endregion


        #region Constants

        protected const int constFileBufferSize = 512;

        #endregion
    }
}
