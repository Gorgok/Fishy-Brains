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
using System.Collections;

using GHIElectronics.NETMF.System;


namespace JDI.NETMF.Web
{
    public abstract class HtmlReader : IDisposable
    {
        #region Constructors / Destructors

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="tokens">Token name-value pairs.</param>
        /// <param name="tokenDelimiter">Token name delimiter.</param>
        /// <param name="lineBufferSize">Line buffer size. Must be larger than longest line length including \r\n.</param>
        public HtmlReader(Hashtable tokens, char tokenDelimiter = '~', int lineBufferSize = constLineBufferSize)
        {
            this.currentIndex = -1;
            this.bytesInBuffer = 0;
            this.lineBuffer = new byte[lineBufferSize];
            this.tokens = tokens;
            this.tokenDelimiter = tokenDelimiter;
        }

        public virtual void Dispose()
        {
            this.lineBuffer = null;
            if (this.tokens != null)
            {
                this.tokens.Clear();
                this.tokens = null;
            }
        }

        #endregion


        #region Public Methods

        /// <summary>
        /// Returns next line, excluding \r\n.
        /// </summary>
        /// <returns></returns>
        public string ReadLine()
        {
            if (this.bytesInBuffer == 0)
            {
                // read the next line
                if (this.FillLineBuffer() == -1)
                {
                    return null;
                }
            }
            return this.GetLineFromBuffer();
        }

        /// <summary>
        /// Reads chars into the buffer until count chars have been read, or until EOS is reached.
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="index"></param>
        /// <param name="count"></param>
        /// <returns>Number of bytes read.</returns>
        public int Read(char[] buffer, int index, int count)
        {
            int bytesCopied = 0;
            int bytesToCopy = 0;
            while (bytesCopied < count)
            {
                if (this.bytesInBuffer == 0)
                {
                    if (this.FillLineBuffer() == -1)
                    {
                        break;
                    }
                }
                bytesToCopy = Math.Min(this.bytesInBuffer, count - bytesCopied);
                Array.Copy(this.lineBuffer, this.currentIndex, buffer, bytesCopied, bytesToCopy);
                bytesCopied += bytesToCopy;
                this.currentIndex += bytesToCopy;
                this.bytesInBuffer -= bytesToCopy;
            }
            return bytesCopied;
        }

        /// <summary>
        /// Reads bytes into the buffer until count bytes have been read, or until EOS is reached.
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="index"></param>
        /// <param name="count"></param>
        /// <returns>Number of bytes read.</returns>
        public int Read(byte[] buffer, int index, int count)
        {
            int bytesCopied = 0;
            int bytesToCopy = 0;
            while (bytesCopied < count)
            {
                if (this.bytesInBuffer == 0)
                {
                    if (this.FillLineBuffer() == -1)
                    {
                        break;
                    }
                }
                bytesToCopy = Math.Min(this.bytesInBuffer, count - bytesCopied);
                Array.Copy(this.lineBuffer, this.currentIndex, buffer, bytesCopied, bytesToCopy);
                bytesCopied += bytesToCopy;
                this.currentIndex += bytesToCopy;
                this.bytesInBuffer -= bytesToCopy;
            }
            return bytesCopied;
        }

        public virtual void Close()
        {
            // does nothing in base class
            // available to derived classes
        }

        #endregion


        #region Line Processing Methods

        // Returns next byte from source Html.
        protected abstract int GetNextByte();

        // Fills the lineBuffer and replaces tokens with their values.
        protected int FillLineBuffer()
        {
            // init buffer, discarding any remaining characters
            this.currentIndex = -1;
            this.bytesInBuffer = 0;

            // read bytes until buffer full or eos or eol
            int nextByte = -1;
            while (true)
            {
                while (true)
                {
                    nextByte = this.GetNextByte();
                    if (nextByte == -1)
                    {
                        // eos
                        break;
                    }
                    if (nextByte > 0 && nextByte < 127)
                    {
                        this.lineBuffer[this.bytesInBuffer++] = (byte)nextByte;
                    }
                    if (nextByte == '\n')
                    {
                        // eol
                        break;
                    }
                    if (this.bytesInBuffer >= this.lineBuffer.Length)
                    {
                        throw new IndexOutOfRangeException("Line length exceeded.");
                    }
                }

                // check for eos
                if (nextByte == -1 && this.bytesInBuffer == 0)
                {
                    return -1;
                }

                // replace tokens with their values
                this.ReplaceTokens();

                // check for bytesInBuffer
                if (this.bytesInBuffer > 0)
                {
                    break;
                }
            }

            // return the resulting line length
            return this.bytesInBuffer;
        }

        // Replaces tokens with their values.
        protected void ReplaceTokens()
        {
            // get the raw line
            string line = new String(Encoding.UTF8.GetChars(this.lineBuffer), 0, this.bytesInBuffer);

            // replace tokens with their values
            if (this.tokens != null && this.tokens.Count > 0 && line.IndexOf(this.tokenDelimiter) >= 0)
            {
                string[] lineSegments = line.Split(this.tokenDelimiter);
                if (lineSegments.Length > 0)
                {
                    this.bytesInBuffer = 0;
                    for (int i = 0; i < lineSegments.Length; i++)
                    {
                        if (this.tokens.Contains(lineSegments[i]))
                        {
                            Util.InsertValueIntoArray((string)this.tokens[lineSegments[i]], this.lineBuffer, this.bytesInBuffer, false);
                            this.bytesInBuffer += ((string)this.tokens[lineSegments[i]]).Length;
                        }
                        else
                        {
                            Util.InsertValueIntoArray(lineSegments[i], this.lineBuffer, this.bytesInBuffer, false);
                            this.bytesInBuffer += lineSegments[i].Length;
                        }
                    }
                }
            }

            // update currentIndex
            this.currentIndex = (this.bytesInBuffer > 0 ? 0 : -1);
        }

        // Reads all bytes from buffer, except \r\n.
        protected string GetLineFromBuffer()
        {
            if (this.bytesInBuffer == 0)
            {
                return "";
            }
            int index = this.currentIndex;
            int byteCount = this.bytesInBuffer;
            this.currentIndex = -1;
            this.bytesInBuffer = 0;
            if (byteCount >= 2 && this.lineBuffer[byteCount - 2] == '\r')
            {
                byteCount -= 2;
            }
            else if (byteCount >= 1 && this.lineBuffer[byteCount - 1] == '\n')
            {
                byteCount -= 1;
            }
            return new String(Encoding.UTF8.GetChars(this.lineBuffer), index, byteCount);
        }

        #endregion


        #region Fields

        protected int currentIndex;
        protected int bytesInBuffer;
        protected byte[] lineBuffer;
        protected Hashtable tokens;
        protected char tokenDelimiter;

        #endregion


        #region Constants

        protected const int constLineBufferSize = 256;

        #endregion
    }
}
