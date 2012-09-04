using System;
using System.Net.Security;
using System.Net.Sockets;
using System.Net;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using QuickFix.SSL;

namespace QuickFix
{
    /// <summary>
    /// Handles a connection with an acceptor.
    /// </summary>
    public class SocketInitiatorThread : Responder
    {
        public Session Session { get { return session_; } }
        public Transport.SocketInitiator Initiator { get { return initiator_; } }

        public const int BUF_SIZE = 512;

        private Thread thread_ = null;
        private byte[] readBuffer_ = new byte[BUF_SIZE];
        private Parser parser_;
        private TcpClient client_;
        private Transport.SocketInitiator initiator_;
        private Session session_;
        private IPEndPoint socketEndPoint_;
        private bool isDisconnectRequested_ = false;
        private SslStream sslStream_;
        private SSLSettings sslSettings_;

        public SocketInitiatorThread(Transport.SocketInitiator initiator, Session session, IPEndPoint socketEndPoint, SocketSettings socketSettings, SSLSettings sslSettings)
        {
            isDisconnectRequested_ = false;
            initiator_ = initiator;
            session_ = session;
            socketEndPoint_ = socketEndPoint;
            parser_ = new Parser();
            client_ = new TcpClient(AddressFamily.InterNetwork);
            client_.NoDelay = socketSettings.SocketNodelay;
            session_ = session;
            sslSettings_ = sslSettings;
        }

        public void Start()
        {
            isDisconnectRequested_ = false;
            thread_ = new Thread(new ParameterizedThreadStart(Transport.SocketInitiator.SocketInitiatorThreadStart));
            thread_.Start(this);
        }

        public void Join()
        {
            if (null == thread_)
                return;
            Disconnect();
            thread_.Join(5000);
            thread_ = null;
        }

        public static bool ValidateServerCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            // Accept all certificates
            return true;
        }

        public void Connect(out Exception connectEx)
        {            
            //socket_.Connect(socketEndPoint_);
            connectEx = null;
            try
            {
                client_.Connect(socketEndPoint_);
                if (sslSettings_.UseSSL)
                {
                    sslStream_ = new SslStream(client_.GetStream(), false, new RemoteCertificateValidationCallback(ValidateServerCertificate));
                    sslStream_.AuthenticateAsClient(sslSettings_.ServerName, sslSettings_.SslSingleCertCollection, sslSettings_.SslProtocol, false);
                }
                session_.SetResponder(this);
            }
            catch (Exception ex)
            {
                // Exception must be manual pass to caller thread
                connectEx = new QuickFIXException("Error while connecting to server", ex);
            }
        }

        public bool Read()
        {
            try
            {
                if (client_.Client != null && client_.Client.Poll(1000000, SelectMode.SelectRead)) // one-second timeout
                {
                    int bytesRead = -1;
                    if (sslStream_ != null)
                        bytesRead = sslStream_.Read(readBuffer_, 0, readBuffer_.Length);
                    else
                        bytesRead = client_.Client.Receive(readBuffer_);

                    if (0 == bytesRead)
                        throw new SocketException(System.Convert.ToInt32(SocketError.ConnectionReset));
                    parser_.AddToStream(System.Text.Encoding.UTF8.GetString(readBuffer_, 0, bytesRead));
                }
                else if (null != session_)
                {
                    session_.Next();
                }
                else
                {
                    throw new QuickFIXException("Initiator timed out while reading socket");
                }
            
                ProcessStream();
                return true;
            }
            catch(System.ObjectDisposedException e)
            {
                // this exception means socket_ is already closed when poll() is called
                if(isDisconnectRequested_==false)
                {
                    // for lack of a better idea, do what the general exception does
                    if (null != session_)
                        session_.Disconnect(e.ToString());
                    else
                        Disconnect();
                }
                return false;                    
            }
            catch (System.Exception e)
            {
                if (null != session_)
                    session_.Disconnect(e.ToString());
                else
                    Disconnect();
                return false;
            }
        }

        private void ProcessStream()
        {
            string msg;
            while (parser_.ReadFixMessage(out msg))
                session_.Next(msg);
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
                bytesSent = client_.Client.Send(rawData);
            return bytesSent > 0;
        }

        public void Disconnect()
        {
            isDisconnectRequested_ = true;
            client_.Client.Close();
            client_.Close();
        }

        #endregion
    }
}
