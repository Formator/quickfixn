using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace QuickFix.SSL
{
    /// <summary>
    /// Settings for SSL
    /// </summary>
    public class SSLSettings
    {
        #region Private members
        private bool useSSL_;
        private string sslCertPath_ = string.Empty;
        private string sslCertPassword_ = string.Empty;
        private X509Certificate2 sslCert_;
        private X509Certificate2Collection sslSingleCertCollection_;
        private SslProtocols sslProtocol_ = SslProtocols.None;
        private string serverName_ = string.Empty;
        private SSLContainer sslContainer_;
        #endregion

        #region Public properties
        public bool UseSSL
        {
            get { return useSSL_; }
        }

        public string SslCertPath
        {
            get { return sslCertPath_; }
        }

        public string SSLCertPassword
        {
            get { return sslCertPassword_; }
        }
        
        public X509Certificate2 SslCert
        {
            get { return sslCert_; }
        }

        public X509Certificate2Collection SslSingleCertCollection
        {
            get
            {
                if (sslCert_ != null && sslSingleCertCollection_ == null) 
                    sslSingleCertCollection_ = new X509Certificate2Collection(sslCert_);
                return sslSingleCertCollection_;
            }
        }

        public SslProtocols SslProtocol
        {
            get { return sslProtocol_; }
        }

        public string ServerName
        {
            get { return serverName_; }
        }
        #endregion


        public SSLSettings (Dictionary settings, SSLContainer sslContainer)
        {
            sslContainer_ = sslContainer;
            if (settings.Has(SessionSettings.USE_SSL) && settings.GetBool(SessionSettings.USE_SSL))
            {
                useSSL_ = true;
                sslProtocol_ = SslProtocols.Default;
                if (settings.Has(SessionSettings.TARGETCOMPID))
                    serverName_ = settings.GetString(SessionSettings.TARGETCOMPID);

                if (settings.Has(SessionSettings.SSL_SERVER_NAME))
                    serverName_ = settings.GetString(SessionSettings.SSL_SERVER_NAME);

                if (settings.Has(SessionSettings.SSL_CERT))
                {
                    sslCertPath_ = settings.GetString(SessionSettings.SSL_CERT);                    
                    if (settings.Has(SessionSettings.SSL_CERT_PASSWORD))
                        sslCertPassword_ = settings.GetString(SessionSettings.SSL_CERT_PASSWORD); ;
                    sslCert_ = CreateCertificate(sslCertPath_, SSLCertPassword);
                }

                if (settings.Has(SessionSettings.SSL_PROTOCOL))
                {
                    string tmpsslProtocol = settings.GetString(SessionSettings.SSL_PROTOCOL);
                    switch (tmpsslProtocol.ToUpper())
                    {
                        case "TLS":
                            sslProtocol_ = SslProtocols.Tls;
                            break;
                        case "NONE":
                            sslProtocol_ = SslProtocols.None;
                            break;
                        case "SSL2":
                            sslProtocol_ = SslProtocols.Ssl2;
                            break;
                        case "SSL3":
                            sslProtocol_ = SslProtocols.Ssl3;
                            break;
                        default:
                            sslProtocol_ = SslProtocols.Default;
                            break;
                    }
                }
            }            
        }

        private X509Certificate2 CreateCertificate(string certPath, string certPassword)
        {
            X509Certificate2 cert = null;
            if (sslContainer_ != null && !sslContainer_.TryGetValue(certPath, out cert))
            {
                cert = new X509Certificate2(certPath, certPassword);
                sslContainer_[certPath] = cert;
            }

            return cert;
        }


    }
}
