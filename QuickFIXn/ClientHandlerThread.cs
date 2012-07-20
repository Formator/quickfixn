using System;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using QuickFix.SSL;

namespace QuickFix
{
    /// <summary>
    /// Created by a ThreadedSocketReactor to handle a client connection.
    /// Each ClientHandlerThread has a SocketReader which reads
    /// from the socket.
    /// </summary>
    public class ClientHandlerThread : Responder
    {
        private Thread thread_ = null;
        private volatile bool isShutdownRequested_ = false;
        private TcpClient tcpClient_;
        private SocketReader socketReader_;
        private long id_;
        private FileLog log_;
        private SslStream sslStream_;
        private SSLSettings sslSettings_;

        public ClientHandlerThread(TcpClient tcpClient, long clientId, SSLSettings sslSettings)
        {
            log_ = new FileLog("log", new SessionID("ClientHandlerThread", clientId.ToString(), "Debug")); /// FIXME
            tcpClient_ = tcpClient;
            id_ = clientId;
            sslSettings_ = sslSettings;

            if (sslSettings_.UseSSL)
            {
                sslStream_ = new SslStream(tcpClient_.GetStream(), false);
                socketReader_ = new SocketReader(tcpClient_, this, sslStream_);
            }
            else
                socketReader_ = new SocketReader(tcpClient_, this);
        }

        public void Start()
        {
            thread_ = new Thread(new ThreadStart(Run));
            thread_.Start();
        }

        public void Shutdown(string reason)
        {
            Log("shutdown requested: " + reason);
            isShutdownRequested_ = true;
        }

        public void Join()
        {
            if(null == thread_)
                return;
            if(thread_.IsAlive)
                thread_.Join(5000);
            thread_ = null;
        }

        public bool IsAlive()
        {
            if (null != thread_ && thread_.IsAlive)
                return true;
            return false;
        }

        public void Run()
        {
            if (sslSettings_.SslCert != null)
            {
                try
                {
                    sslStream_.AuthenticateAsServer(sslSettings_.SslCert, false, sslSettings_.SslProtocol, false);
                }
                catch (Exception ex)
                {
                    Disconnect(string.Format("Authentication error: {0} Client Adress: {1}", ex.Message, ((IPEndPoint)tcpClient_.Client.RemoteEndPoint).Address));
                }
            }

            while (!isShutdownRequested_)
            {
                try
                {
                    socketReader_.Read();
                }
                catch (System.Exception e)
                {
                    Disconnect(e.Message);
                }
            }

            this.Log("shutdown");
        }

        /// FIXME do real logging
        public void Log(string s)
        {
            log_.OnEvent(s);
        }

        #region Responder Members

        public bool Send(string data)
        {
            byte[] rawData = System.Text.Encoding.UTF8.GetBytes(data);
            int bytesSent = -1;
            if (sslStream_ != null)
            {
                sslStream_.Write(rawData);
                sslStream_.Flush();
                bytesSent = rawData.Length;
            }                
            else
                bytesSent = tcpClient_.Client.Send(rawData);
            return bytesSent > 0;
        }

        public void Disconnect()
        {
            Disconnect("Disconnected");
        }

        public void Disconnect(string reason)
        {
            if (string.IsNullOrEmpty(reason))
                reason = "Disconnected";
            Shutdown(reason);
            tcpClient_.Client.Close();
            if (sslStream_ != null) sslStream_.Close();
            tcpClient_.Close();
        }

        #endregion
    }
}
