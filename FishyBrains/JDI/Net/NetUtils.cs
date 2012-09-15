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
using System.IO;
using System.Text;
using System.Collections;


namespace JDI.NETMF.Net
{
    public static class NetUtils
    {
        #region Enums and Constants

        private const string hexValues = "0123456789ABCDEF";
        private const int constPostDataReadTimeout = 250;	// msec

        #endregion

        #region Public Methods

        /// <summary>
        /// Returns ContentType from file extension.
        /// </summary>
        public static string ContentType(string extension)
        {
            string contentType = "text/html";
            switch (extension.ToLower())
            {
                case "jpg":
                    contentType = "image/jpeg";
                    break;
                case "png":
                    contentType = "image/png";
                    break;
                case "gif":
                    contentType = "image/gif";
                    break;
                case "ico":
                    contentType = "image/x-icon";
                    break;
                case "pdf":
                    contentType = "application/pdf";
                    break;
                case "css":
                    contentType = "text/css";
                    break;
                case "txt":
                    contentType = "text/plain";
                    break;
                case "csv":
                    contentType = "text/csv";
                    break;
                case "xml":
                    contentType = "text/xml";
                    break;
            }
            return contentType;
        }

        /// <summary>
        /// Converts MAC address string into byte array.
        /// </summary>
        /// <param name="macAddress">MAC address in ##-##-##-##-##-## format.</param>
        /// <returns></returns>
        public static byte[] MACAddressToBytes(string macAddress)
        {
            string[] macValues = macAddress.Split('-');

            if (macAddress.Length != 17 || macValues.Length != 6)
                return null;

            byte[] macBytes = new byte[6];
            for (int i = 0; i < 6; i++)
            {
                macBytes[i] = HexToByte(macValues[i]);
            }
            return macBytes;
        }

        /// <summary>
        /// Converts IP address string to byte array
        /// </summary>
        /// <param name="ipAddress">IP address in ###.###.###.### format.</param>
        /// <returns></returns>
        public static byte[] IPAddressToBytes(string ipAddress)
        {
            string[] ipValues = ipAddress.Split('.');

            if (ipAddress.Length > 17 || ipAddress.Length < 7 || ipValues.Length != 4)
                return null;

            byte[] ipBytes = new byte[4];
            for (int i = 0; i < 4; i++)
            {
                ipBytes[i] = IntToByte(ipValues[i]);
            }
            return ipBytes;
        }

        /// <summary>
        /// Converts IP Address bytes to string in ###.###.###.### format.
        /// </summary>
        /// <param name="ipBytes"></param>
        /// <returns></returns>
        public static string IPBytesToString(byte[] ipBytes)
        {
            try
            {
                string ipAddr = "";
                for (int i = 0; i < 4; i++)
                {

                    ipAddr += (i > 0 ? "." : "") + ipBytes[i].ToString();
                }
                return ipAddr;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Gets baseUrl from rawUrl.
        /// </summary>
        /// <param name="rawUrl"></param>
        public static string GetBaseUrl(string rawUrl, bool decode = false, bool toLower = false)
        {
            int urlLen = rawUrl.IndexOf('?');
            if (urlLen < 0)
            {
                urlLen = rawUrl.Length;
            }

            string baseUrl = rawUrl.Substring(0, urlLen);

            if (decode)
            {
                baseUrl = UrlDecode(baseUrl);
            }
            if (toLower == true)
            {
                baseUrl = baseUrl.ToLower();
            }

            return baseUrl;
        }

        /// <summary>
        /// Gets queryString from rawUrl.
        /// </summary>
        /// <param name="rawUrl"></param>
        public static string GetQueryString(string rawUrl, bool decode = false, bool toLower = false)
        {
            int startIndex = rawUrl.IndexOf('?');
            if (startIndex < 0)
            {
                return "";
            }

            string queryString = rawUrl.Substring(startIndex + 1);

            if (decode)
            {
                queryString = UrlDecode(queryString);
            }
            if (toLower == true)
            {
                queryString = queryString.ToLower();
            }

            return queryString;
        }

        /// <summary>
        /// Parses querystring parameters from rawUrl.
        /// </summary>
        /// <param name="rawUrl"></param>
        /// <param name="decode"></param>
        /// <returns></returns>
        public static Hashtable ParseQueryString(string rawUrl, bool decode = false, bool toLower = false)
        {
            // get queryString
            string queryString = GetQueryString(rawUrl, decode, toLower);

            // get queryParams
            Hashtable queryParams = null;
            if (queryString.Length > 0)
            {
                string[] qsParams = queryString.Split('&');
                if (qsParams.Length > 0)
                {
                    queryParams = new Hashtable();
                    string[] kvPair = null;
                    foreach (string qsParam in qsParams)
                    {
                        kvPair = qsParam.Split('=');
                        if (kvPair.Length != 2) continue;
                        queryParams.Add(kvPair[0], kvPair[1]);
                    }
                }
            }
            return queryParams;
        }

        /// <summary>
        /// Gets post data parameters from inputStream.
        /// </summary>
        /// <param name="requestMethod"></param>
        /// <param name="contentType"></param>
        /// <param name="contentLength64"></param>
        /// <param name="maxPostDataLength"></param>
        /// <param name="inputStream"></param>
        /// <returns></returns>
        public static Hashtable ParsePostData(string requestMethod, string contentType, long contentLength64, int maxPostDataLength, Stream inputStream, bool decode = true, bool toLower = false)
        {
            // check for valid request
            if (requestMethod.ToLower() != "post")
            {
                return null;
            }
            if (contentType != null && contentType.ToLower() != "application/x-www-form-urlencoded")
            {
                throw new Exception("Content type (" + contentType + ") is not supported.");
            }
            if (contentLength64 == 0)
            {
                throw new Exception("Chunk encoded form data is not supported.");
            }
            if (contentLength64 > maxPostDataLength)
            {
                throw new Exception("Post data exceeds max size of " + maxPostDataLength.ToString() + " bytes.");
            }

            // get pdBytes
            inputStream.ReadTimeout = constPostDataReadTimeout;
            int contentLength = (int)contentLength64;
            byte[] pdBytes = new byte[contentLength];
            int bytesRead = 0;
            int totalBytesRead = 0;
            while (totalBytesRead < contentLength)
            {
                bytesRead = inputStream.Read(pdBytes, totalBytesRead, pdBytes.Length - totalBytesRead);
                if (bytesRead == 0)
                {
                    break;
                }
                totalBytesRead += bytesRead;
            }
            if (totalBytesRead < contentLength)
            {
                pdBytes = null;
                throw new Exception("Post data contains fewer bytes than expected.");
            }

            // get postParams
            Hashtable postParams = null;
            string pdString = new String(Encoding.UTF8.GetChars(pdBytes));
            if (decode == true)
            {
                pdString = UrlDecode(pdString);
            }
            if (toLower == true)
            {
                pdString = pdString.ToLower();
            }
            string[] pdParams = pdString.Split('&');
            if (pdParams.Length > 0)
            {
                postParams = new Hashtable();
                string[] kvPair = null;
                foreach (string pdParam in pdParams)
                {
                    kvPair = pdParam.Split('=');
                    if (kvPair.Length != 2) continue;
                    postParams.Add(kvPair[0], kvPair[1]);
                }
            }

            // cleanup
            pdBytes = null;
            pdString = null;

            return postParams;
        }

        /// <summary>
        /// Parses a baseUrl into the full filePath, fileName, and fileExt parts.
        /// </summary>
        /// <param name="baseUrl"></param>
        /// <param name="filePath"></param>
        /// <param name="fileName"></param>
        /// <param name="fileExt"></param>
        public static void ParseBaseUrl(string baseUrl, ref string filePath, ref string fileName, ref string fileExt, bool decode = false, bool toLower = false)
        {
            if (decode == true)
            {
                baseUrl = UrlDecode(baseUrl);
            }
            if (toLower == true)
            {
                baseUrl = baseUrl.ToLower();
            }
            filePath = "";
            fileName = "";
            fileExt = "";
            string[] segments = baseUrl.Split('/');
            for (int i = 0; i < segments.Length; i++)
            {
                filePath += segments[i] + (i < segments.Length - 1 ? "\\" : "");
            }

            fileName = Path.GetFileNameWithoutExtension(filePath);
            fileExt = Path.GetExtension(filePath);
            fileExt = (fileExt.IndexOf('.') == 0 ? fileExt.Substring(1) : fileExt);
        }

        /// <summary>
        /// Replaces escape characters with actual characters.
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        public static string UrlDecode(string url)
        {
            if (url == null || url.Length == 0)
                return "";

            // get char array
            char[] urlChars = url.ToCharArray();

            // replace '+' with space, and '%' encoded chars with their char equivalent
            int pos = 0;
            int endPos = urlChars.Length - 1;
            while (pos <= endPos)
            {
                if (urlChars[pos] == '+')
                {
                    // replace '+'
                    urlChars[pos] = ' ';
                }
                else if (urlChars[pos] == '%' && (endPos - pos >= 2))
                {
                    // replaces hex value
                    urlChars[pos] = HexToChar(new string(urlChars, pos + 1, 2));
                    Array.Copy(urlChars, pos + 3, urlChars, pos + 1, urlChars.Length - pos - 3);
                    endPos -= 2;
                }
                pos += 1;
            }

            return new string(urlChars, 0, endPos + 1);
        }

        /// <summary>
        /// Converts a 2-character hex string to a byte value.
        /// </summary>
        /// <param name="hexString"></param>
        /// <returns></returns>
        public static byte HexToByte(string hexString)
        {
            try
            {
                hexString = hexString.ToUpper();
                int hexValue = (hexValues.IndexOf(hexString[0]) << 4) | (hexValues.IndexOf(hexString[1]));
                return (byte)hexValue;
            }
            catch
            {
                throw new ArgumentException("Invalid hex number.");
            }
        }

        /// <summary>
        /// Converts a 2-character hex string to a char value.
        /// </summary>
        /// <param name="hexString"></param>
        /// <returns></returns>
        public static char HexToChar(string hexString)
        {
            try
            {
                hexString = hexString.ToUpper();
                int hexValue = (hexValues.IndexOf(hexString[0]) << 4) | (hexValues.IndexOf(hexString[1]));
                return (char)hexValue;
            }
            catch
            {
                throw new ArgumentException("Invalid hex number.");
            }
        }

        /// <summary>
        /// Converts a multi-digit decimal string to a byte value.
        /// </summary>
        /// <param name="intString"></param>
        /// <returns></returns>
        public static byte IntToByte(string intString)
        {
            int intValue = 0;
            try
            {
                intValue = int.Parse(intString);
            }
            catch
            {
                throw new ArgumentException("Invalid integer number.");
            }
            if (intValue > 255)
            {
                throw new ArgumentException("Invalid integer number.");
            }
            return (byte)intValue;
        }

        #endregion
    }
}
