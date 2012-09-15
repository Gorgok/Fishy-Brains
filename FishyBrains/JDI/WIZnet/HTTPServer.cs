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
using System.Threading;

using Microsoft.SPOT;

using GHIElectronics.NETMF.Net;
using GHIElectronics.NETMF.Net.NetworkInformation;

using JDI.NETMF.Net;
using JDI.NETMF.Web;
using JDI.NETMF.Storage;


namespace JDI.NETMF.WIZnet
{
    public class HTTPServer : IDisposable
    {
        #region Enums

        public enum HTTPStatus
        {
            Stopped = 0,
            Starting,
            ReStarting,
            Listening,
            RequestRcvd,
            NotFound,
            ResponseSent,
            Stopping,
            Error
        }

        #endregion

        #region Events

        // define status change handlers
        public delegate void HttpStatusChangedHandler(HTTPStatus httpStatus, string message);

        // define http request handler
        public delegate void HttpRequestHandler(string requestURL, HttpRequestEventArgs e);

        // define events
        public event HttpStatusChangedHandler HttpStatusChanged = delegate { };

        #endregion

        #region Constructors and Destructors

        public HTTPServer()
        {
            this.serverStatus = HTTPStatus.Stopped;
            this.httpRestarts = 0;
            this.httpRequests = 0;
            this.http404Hits = 0;
            this.httpListener = null;
            this.serverThread = null;
            this.restartServer = false;
            this.terminateServer = false;

            this.requestHandlers = new Hashtable();
        }

        public void Dispose()
        {
            // stop server
            if (this.serverThread != null)
            {
                this.terminateServer = true;
                this.httpListener.Close();
                this.serverThread.Join();
                this.serverThread = null;
                this.httpListener = null;
            }

            if (this.requestHandlers != null)
            {
                this.requestHandlers.Clear();
                this.requestHandlers = null;
            }
        }

        #endregion

        #region Properties

        public HTTPStatus Status
        {
            get { return this.serverStatus; }
        }

        public string StatusName
        {
            get
            {
                string name = "";
                switch (this.serverStatus)
                {
                    case HTTPStatus.Stopped:
                        name = "Stopped";
                        break;
                    case HTTPStatus.Starting:
                        name = "Starting";
                        break;
                    case HTTPStatus.ReStarting:
                        name = "ReStarting";
                        break;
                    case HTTPStatus.Listening:
                        name = "Listening";
                        break;
                    case HTTPStatus.RequestRcvd:
                        name = "RequestRcvd";
                        break;
                    case HTTPStatus.NotFound:
                        name = "NotFound";
                        break;
                    case HTTPStatus.ResponseSent:
                        name = "ResponseSent";
                        break;
                    case HTTPStatus.Stopping:
                        name = "Stopping";
                        break;
                    case HTTPStatus.Error:
                        name = "Error";
                        break;
                }
                return name;
            }
        }

        public int HttpRestarts
        {
            get { return this.httpRestarts; }
        }

        public int HttpRequests
        {
            get { return this.httpRequests; }
        }

        public int Http404Hits
        {
            get { return this.http404Hits; }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Return the URL of this http server.
        /// </summary>
        public string GetHttpURL()
        {
            return this.httpPrefix + "://" + NetUtils.IPBytesToString(NetworkInterface.IPAddress) + (this.httpPort == 80 ? "" : ":" + this.httpPort.ToString()) + "/";
            //	return this.httpPrefix + "://" + Dns.GetHostEntry(this.hostName).AddressList[0].ToString() + (this.httpPort == 80 ? "" : ":" + this.httpPort.ToString()) + "/";
            //	return this.httpPrefix + "://" + this.hostName + (this.httpPort == 80 ? "" : ":" + this.httpPort.ToString()) + "/";
        }

        public void InitializeServer(string hostName, string httpPrefix, int httpPort, int maxPostDataLength)
        {
            // initialize appSettings
            this.hostName = hostName;
            this.httpPrefix = httpPrefix;
            this.httpPort = httpPort;
            this.maxPostDataLength = maxPostDataLength;

            this.serverStatus = HTTPStatus.Stopped;
            this.httpRestarts = 0;
            this.httpRequests = 0;
            this.http404Hits = 0;
            this.httpListener = null;
            this.serverThread = null;
            this.restartServer = false;
            this.terminateServer = false;
        }

        /// <summary>
        /// Starts http server.
        /// </summary>
        public void StartServer()
        {
            // check if already running
            if (this.serverThread != null && this.httpListener != null && this.httpListener.IsListening)
            {
                return;
            }

            // create the listener
            this.httpListener = new HttpListener(this.httpPrefix, this.httpPort);

            // start server
            this.terminateServer = false;
            this.serverStatus = HTTPStatus.Starting;
            this.serverThread = new Thread(new ThreadStart(ServerThread));
            this.serverThread.Start();
            Debug.GC(true);
        }

        /// <summary>
        /// Stops http server
        /// </summary>
        public void StopServer()
        {
            // stop server
            this.serverStatus = HTTPStatus.Stopping;
            if (this.serverThread != null)
            {
                this.terminateServer = true;
                this.httpListener.Close();
                this.httpListener = null;
                this.serverThread.Join();
                this.serverThread = null;
            }
            this.serverStatus = HTTPStatus.Stopped;
            Debug.GC(true);
        }

        public void RegisterRequestHandler(string url, HttpRequestHandler handler)
        {
            lock (this)
            {
                url = url.ToLower();

                if (this.requestHandlers.Contains(url))
                    throw new ArgumentException("Request handler already in use.", "url");

                this.requestHandlers.Add(url, handler);
            }
        }

        public void RemoveRequestHandler(string url)
        {
            lock (this)
            {
                url = url.ToLower();

                if (this.requestHandlers.Contains(url))
                {
                    this.requestHandlers.Remove(url);
                }
            }
        }

        public void RemoveAllRequestHandlers()
        {
            lock (this)
                this.requestHandlers.Clear();
        }

        private void SetHttpStatus(HTTPStatus httpStatus)
        {
            SetHttpStatus(httpStatus, "");
        }

        private void SetHttpStatus(HTTPStatus httpStatus, string message)
        {
            if (this.serverStatus != httpStatus)
            {
                this.serverStatus = httpStatus;
                if (this.HttpStatusChanged != null)
                    this.HttpStatusChanged(httpStatus, message);
            }
        }

        #endregion

        #region Server Thread

        /// <summary>
        /// Handles page requests and dispatches them to registered handlers.
        /// </summary>
        private void ServerThread()
        {
            // initialize
            HttpListenerContext httpContext = null;

            // process requests
            while (this.terminateServer == false)
            {
                // start the listener
                this.httpListener.Start();

                // process requests
                while (this.terminateServer == false && this.restartServer == false)
                {
                    this.SetHttpStatus(HTTPStatus.Listening);

                    // wait for request
                    httpContext = this.httpListener.GetContext();

                    // process request
                    try
                    {
                        if (httpContext != null)
                        {
                            this.ProcessHttpRequest(httpContext);
                        }
                    }
                    catch (Exception e)
                    {
                        // display status message
                        this.SetHttpStatus(HTTPStatus.Error, e.Message);

                        // restart the server
                        this.restartServer = true;
                    }
                    finally
                    {
                        // cleanup
                        if (httpContext != null)
                        {
                            httpContext.Close();
                            httpContext = null;
                        }
                        Debug.GC(true);
                    }
                }

                // a problem was encountered, restarting the server
                this.SetHttpStatus(HTTPStatus.ReStarting);
                this.httpRestarts++;
                this.httpListener.Stop();
                this.restartServer = false;
            }

            // shutting down
            this.httpListener.Close();
            this.httpListener = null;
        }

        private void ProcessHttpRequest(HttpListenerContext httpContext)
        {
            // update status
            this.httpRequests++;
            this.SetHttpStatus(HTTPStatus.RequestRcvd, httpContext.Request.RawUrl);

            // get response object
            HttpListenerResponse httpResponse = httpContext.Response;

            // get baseUrl
            string baseUrl = NetUtils.GetBaseUrl(httpContext.Request.RawUrl, true, true);

            // get file info
            string filePath = null;
            string fileName = null;
            string fileExt = null;
            NetUtils.ParseBaseUrl(baseUrl, ref filePath, ref fileName, ref fileExt);
            filePath = SDCard.RootDirectory + filePath;

            // generate response
            byte[] byteBuffer = null;
            int bytesRead = 0;
            HttpRequestHandler handler = null;
            HttpRequestEventArgs eventArgs = null;

            try
            {
                if (this.requestHandlers.Contains(baseUrl))
                {
                    // get requestHandler
                    lock (this) handler = (HttpRequestHandler)this.requestHandlers[baseUrl];

                    // call requestHandler
                    eventArgs = new HttpRequestEventArgs(httpContext.Request, this.maxPostDataLength);
                    handler(baseUrl, eventArgs);

                    // process the response
                    if (eventArgs.ResponseRedirectTo != null)
                    {
                        // do a client-side redirection
                        httpResponse.Headers.Add("Location", eventArgs.ResponseRedirectTo);
                        httpResponse.StatusCode = (int)HttpStatusCode.Redirect;
                        httpResponse.StatusDescription = "Found";
                    }
                    else if (eventArgs.ResponseText != null)
                    {
                        // send text response
                        httpResponse.StatusCode = (int)HttpStatusCode.OK;
                        httpResponse.ContentType = eventArgs.ResponseContentType;

                        // send text to client
                        byteBuffer = Encoding.UTF8.GetBytes(eventArgs.ResponseText);
                        httpResponse.ContentLength64 = byteBuffer.Length;
                        httpResponse.OutputStream.Write(byteBuffer, 0, byteBuffer.Length);
                    }
                    else if (eventArgs.ResponseBytes != null)
                    {
                        // send byte response
                        httpResponse.StatusCode = (int)HttpStatusCode.OK;
                        httpResponse.ContentType = eventArgs.ResponseContentType;

                        // send bytes to client
                        httpResponse.ContentLength64 = eventArgs.ResponseBytes.Length;
                        httpResponse.OutputStream.Write(eventArgs.ResponseBytes, 0, eventArgs.ResponseBytes.Length);
                    }
                    else if (eventArgs.ResponseStream != null)
                    {
                        // send stream response
                        httpResponse.StatusCode = (int)HttpStatusCode.OK;
                        httpResponse.ContentType = eventArgs.ResponseContentType;

                        // send stream to client
                        using (Stream input = eventArgs.ResponseStream)
                        {
                            httpResponse.ContentLength64 = input.Length;
                            byteBuffer = new byte[512];
                            while (true)
                            {
                                bytesRead = input.Read(byteBuffer, 0, byteBuffer.Length);
                                if (bytesRead == 0) break;
                                httpResponse.OutputStream.Write(byteBuffer, 0, bytesRead);
                            }
                        }
                    }
                    else if (eventArgs.ResponseHtml != null)
                    {
                        // send html response
                        httpResponse.StatusCode = (int)HttpStatusCode.OK;
                        httpResponse.ContentType = eventArgs.ResponseContentType;

                        // send html to client
                        using (HtmlReader input = eventArgs.ResponseHtml)
                        {
                            byteBuffer = new byte[512];
                            while (true)
                            {
                                bytesRead = input.Read(byteBuffer, 0, byteBuffer.Length);
                                if (bytesRead == 0) break;
                                httpResponse.OutputStream.Write(byteBuffer, 0, bytesRead);
                            }
                        }
                    }

                    // check for terminate command
                    if (eventArgs.TerminateServer == true)
                    {
                        this.terminateServer = true;
                    }
                }
                else if (SDCard.IsMounted == false)
                {
                    // send error response
                    this.SetHttpStatus(HTTPStatus.Error, "");
                    httpResponse.StatusCode = (int)HttpStatusCode.InternalServerError;
                    httpResponse.ContentType = "text/html";
                    byteBuffer = Encoding.UTF8.GetBytes("<html><header><title>Error 500</title></header><body><h2>Error 500</h2><p>Cannot read the requested resource from the file system.</p></body></html>");
                    httpResponse.ContentLength64 = byteBuffer.Length;
                    httpResponse.OutputStream.Write(byteBuffer, 0, byteBuffer.Length);
                }
                else if (File.Exists(filePath))
                {
                    // send file
                    httpResponse.StatusCode = (int)HttpStatusCode.OK;
                    httpResponse.ContentType = NetUtils.ContentType(fileExt); ;

                    // send file to client
                    using (FileStream input = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                    {
                        httpResponse.ContentLength64 = input.Length;
                        byteBuffer = new byte[512];
                        while (true)
                        {
                            bytesRead = input.Read(byteBuffer, 0, byteBuffer.Length);
                            if (bytesRead == 0) break;
                            httpResponse.OutputStream.Write(byteBuffer, 0, bytesRead);
                        }
                    }
                }
                else
                {
                    // send NotFound response
                    this.SetHttpStatus(HTTPStatus.NotFound, "");
                    httpResponse.StatusCode = (int)HttpStatusCode.NotFound;
                    httpResponse.ContentType = "text/html";
                    this.http404Hits++;
                    byteBuffer = Encoding.UTF8.GetBytes("<html><header><title>Error 404</title></header><body><h2>Not Found</h2><p>The requested resource &quot;" + baseUrl + "&quot; was not found.</p></body></html>");
                    httpResponse.ContentLength64 = byteBuffer.Length;
                    httpResponse.OutputStream.Write(byteBuffer, 0, byteBuffer.Length);
                }

                // finish sending response
                if (httpResponse != null)
                {
                    httpResponse.OutputStream.Flush();
                    httpResponse.Close();
                    httpResponse = null;
                }

                this.SetHttpStatus(HTTPStatus.ResponseSent, "");
            }
            finally
            {
                // cleanup
                if (httpResponse != null)
                {
                    httpResponse.OutputStream.Flush();
                    httpResponse.Close();
                    httpResponse = null;
                }
                baseUrl = null;
                filePath = null;
                fileName = null;
                fileExt = null;
                byteBuffer = null;
                handler = null;
                if (eventArgs != null)
                {
                    eventArgs.Dispose();
                    eventArgs = null;
                }
                Debug.GC(true);
            }
        }

        #endregion

        #region HttpRequestEventArgs Class

        public class HttpRequestEventArgs : IDisposable
        {
            // Constructors and Destructors
            public HttpRequestEventArgs(HttpListenerRequest httpRequest, int maxPostDataLength)
            {
                this.HttpRequest = httpRequest;
                this.QueryParams = NetUtils.ParseQueryString(httpRequest.RawUrl);
                this.PostParams = NetUtils.ParsePostData(httpRequest.HttpMethod, httpRequest.ContentType, httpRequest.ContentLength64, maxPostDataLength, httpRequest.InputStream);

                this.TerminateServer = false;
                this.ResponseRedirectTo = null;
                this.ResponseText = null;
                this.ResponseBytes = null;
                this.ResponseStream = null;
                this.ResponseHtml = null;
                this.ResponseContentType = "text/html";
            }

            public void Dispose()
            {
                this.HttpRequest = null;
                if (this.QueryParams != null)
                {
                    this.QueryParams.Clear();
                    this.QueryParams = null;
                }
                if (this.PostParams != null)
                {
                    this.PostParams.Clear();
                    this.PostParams = null;
                }

                this.ResponseRedirectTo = null;
                this.ResponseText = null;
                this.ResponseBytes = null;
                if (this.ResponseStream != null)
                {
                    this.ResponseStream.Close();
                    this.ResponseStream.Dispose();
                    this.ResponseStream = null;
                }
                if (this.ResponseHtml != null)
                {
                    this.ResponseHtml.Close();
                    this.ResponseHtml.Dispose();
                    this.ResponseHtml = null;
                }
                this.ResponseContentType = null;
            }

            // Request Properties
            public HttpListenerRequest HttpRequest { get; private set; }
            public Hashtable QueryParams { get; private set; }
            public Hashtable PostParams { get; private set; }

            // Response Properties
            public bool TerminateServer;
            public string ResponseRedirectTo;
            public string ResponseText;
            public byte[] ResponseBytes;
            public Stream ResponseStream;
            public HtmlReader ResponseHtml;
            public string ResponseContentType;
        }

        #endregion

        #region Fields

        private string hostName;
        private string httpPrefix;
        private int httpPort;
        private int maxPostDataLength;

        private HTTPStatus serverStatus;
        private int httpRestarts;
        private int httpRequests;
        private int http404Hits;
        private HttpListener httpListener;
        private Thread serverThread;
        private bool restartServer;
        private bool terminateServer;

        private Hashtable requestHandlers;

        #endregion
    }
}
