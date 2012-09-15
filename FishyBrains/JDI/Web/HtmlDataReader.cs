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

using System.Text;
using System.Collections;


namespace JDI.NETMF.Web
{
    public class HtmlDataReader : HtmlReader
    {
        #region Constructors / Destructors

        /// <summary>
        /// Constructor..
        /// </summary>
        /// <param name="sourceHtml">Byte array containing source Html.</param>
        /// <param name="tokens">Token name-value pairs.</param>
        /// <param name="tokenDelimiter">Token name delimiter.</param>
        /// <param name="lineBufferSize">Line buffer size. Must be larger than longest line length including \r\n.</param>
        public HtmlDataReader(byte[] sourceHtml, Hashtable tokens, char tokenDelimiter = '~', int lineBufferSize = constLineBufferSize)
            : base(tokens, tokenDelimiter, lineBufferSize)
        {
            this.sourceHtml = sourceHtml;
            this.sourceIndex = 0;
        }

        /// <summary>
        /// Constructor..
        /// </summary>
        /// <param name="sourceHtml">String containing source Html.</param>
        /// <param name="tokens">Token name-value pairs.</param>
        /// <param name="tokenDelimiter">Token name delimiter.</param>
        /// <param name="lineBufferSize">Line buffer size. Must be larger than longest line length including \r\n.</param>
        public HtmlDataReader(string sourceHtml, Hashtable tokens, char tokenDelimiter = '~', int lineBufferSize = constLineBufferSize)
            : base(tokens, tokenDelimiter, lineBufferSize)
        {
            this.sourceHtml = Encoding.UTF8.GetBytes(sourceHtml);
            this.sourceIndex = 0;
        }

        public override void Dispose()
        {
            this.sourceHtml = null;
            base.Dispose();
        }

        #endregion


        #region Line Processing Methods

        // Returns next byte from source Html.
        protected override int GetNextByte()
        {
            if (this.sourceIndex < this.sourceHtml.Length)
            {
                return this.sourceHtml[this.sourceIndex++];
            }
            return -1; // eos
        }

        #endregion


        #region Fields

        protected byte[] sourceHtml;
        protected int sourceIndex;

        #endregion
    }
}
