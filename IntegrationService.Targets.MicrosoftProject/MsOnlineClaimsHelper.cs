using System;
using System.Linq;
using System.Text;
using System.Net;
using System.ServiceModel;
using ClientOmAuth;
using System.Xml;
using System.ServiceModel.Channels;
using Microsoft.IdentityModel.Protocols.WSTrust;
using System.Web;
using System.IO;
using System.Xml.Linq;
using Microsoft.SharePoint.Client;

namespace Wictor.Office365 
{
    public class MsOnlineClaimsHelper 
    {        
        #region Properties

        readonly string _username;
        readonly string _password;
        readonly bool _useRtfa;
        readonly Uri _host;

        CookieContainer _cachedCookieContainer = null;
        DateTime _expires = DateTime.MinValue;

        #endregion

        #region Constructors
        public MsOnlineClaimsHelper(string host, string username, string password): this(new Uri(host), username, password) {
            
        }
        public MsOnlineClaimsHelper(Uri host, string username, string password) {
            _host = host;
            _username = username;
            _password = password;
            _useRtfa = true;
        }
        public MsOnlineClaimsHelper(Uri host, string username, string password, bool useRtfa) {
            _host = host;
            _username = username;
            _password = password;
            _useRtfa = useRtfa;
        }
        #endregion

        #region Constants
        public const string office365STS = "https://login.microsoftonline.com/extSTS.srf";
        public const string office365Login = "https://login.microsoftonline.com/login.srf";
        public const string office365Metadata = "https://nexus.microsoftonline-p.com/federationmetadata/2007-06/federationmetadata.xml";
        public const string wsse = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd";
        public const string wsu = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd";
        private const string userAgent = "Mozilla/5.0 (compatible; MSIE 9.0; Windows NT 6.1; WOW64; Trident/5.0)";
        #endregion

        class MsoCookies {
            public string FedAuth { get; set; }
            public string rtFa { get; set; }
            public DateTime Expires { get; set; }
            public Uri Host{ get; set; }
        }

        // Method used to add cookies to CSOM
        public void clientContext_ExecutingWebRequest(object sender, WebRequestEventArgs e) {
            e.WebRequestExecutor.WebRequest.CookieContainer = getCookieContainer();
            //e.WebRequestExecutor.WebRequest.UserAgent = userAgent;
        }

        // Creates or loads cached cookie container
        CookieContainer getCookieContainer() {
            if (_cachedCookieContainer == null || DateTime.Now > _expires) {

                // Get the SAML tokens from SPO STS (via MSO STS) using fed auth passive approach
                MsoCookies cookies = getSamlToken();

                if (cookies != null && !string.IsNullOrEmpty(cookies.FedAuth)) {

                    // Create cookie collection with the SAML token                    
                    _expires = cookies.Expires;
                    CookieContainer cc = new CookieContainer();

                    // Set the FedAuth cookie
                    Cookie samlAuth = new Cookie("FedAuth", cookies.FedAuth) {
                        Expires = cookies.Expires,
                        Path = "/",
                        Secure = cookies.Host.Scheme == "https",
                        HttpOnly = true,
                        Domain = cookies.Host.Host
                    };
                    cc.Add(samlAuth);


                    if (_useRtfa) {
                        // Set the rtFA (sign-out) cookie, added march 2011
                        Cookie rtFa = new Cookie("rtFA", cookies.rtFa) {
                            Expires = cookies.Expires,
                            Path = "/",
                            Secure = cookies.Host.Scheme == "https",
                            HttpOnly = true,
                            Domain = cookies.Host.Host
                        };
                        cc.Add(rtFa);
                    }
                    _cachedCookieContainer = cc;
                    return cc;
                }
                return null;
            }
            return _cachedCookieContainer;
        }

        public CookieContainer CookieContainer {
            get {
                if (_cachedCookieContainer == null || DateTime.Now > _expires) {
                    return getCookieContainer();
                }
                return _cachedCookieContainer;
            }
        }

        private MsoCookies getSamlToken() {
            MsoCookies ret = new MsoCookies();

            try {
                var sharepointSite = new {
                    Wctx = office365Login,
                    Wreply = _host.GetLeftPart(UriPartial.Authority) + "/_forms/default.aspx?wa=wsignin1.0"
                };

                //get token from STS
                string stsResponse = getResponse(office365STS, sharepointSite.Wreply);

                // parse the token response
                XDocument doc = XDocument.Parse(stsResponse);

                // get the security token
                var crypt = from result in doc.Descendants()
                            where result.Name == XName.Get("BinarySecurityToken", wsse)
                            select result;

                // get the token expiration
                var expires = from result in doc.Descendants()
                              where result.Name == XName.Get("Expires", wsu)
                              select result;
                ret.Expires = Convert.ToDateTime(expires.First().Value);


                HttpWebRequest request = createRequest(sharepointSite.Wreply);
                byte[] data = Encoding.UTF8.GetBytes(crypt.FirstOrDefault().Value);
                using (Stream stream = request.GetRequestStream()) {
                    stream.Write(data, 0, data.Length);
                    stream.Close();

                    using (HttpWebResponse webResponse = request.GetResponse() as HttpWebResponse) {

                        // Handle redirect, added may 2011 for P-subscriptions
                        if (webResponse.StatusCode == HttpStatusCode.MovedPermanently) {
                            HttpWebRequest request2 = createRequest(webResponse.Headers["Location"]);
                            using (Stream stream2 = request2.GetRequestStream()) {
                                stream2.Write(data, 0, data.Length);
                                stream2.Close();

                                using (HttpWebResponse webResponse2 = request2.GetResponse() as HttpWebResponse) {
                                    ret.FedAuth = webResponse2.Cookies["FedAuth"].Value;
                                    ret.rtFa = webResponse2.Cookies["rtFa"].Value;
                                    ret.Host = request2.RequestUri;
                                }
                            }
                        }
                        else {
                            ret.FedAuth = webResponse.Cookies["FedAuth"].Value;
                            ret.rtFa = webResponse.Cookies["rtFa"].Value;
                            ret.Host = request.RequestUri;
                        }
                    }
                }
            }
            catch (Exception) {
                return null;
            }
            return ret;
        }

        static HttpWebRequest createRequest(string url) {
            HttpWebRequest request = HttpWebRequest.Create(url) as HttpWebRequest;
            request.Method = "POST";
            request.ContentType = "application/x-www-form-urlencoded";
            request.CookieContainer = new CookieContainer();
            request.AllowAutoRedirect = false; // Do NOT automatically redirect
            request.UserAgent = userAgent;
            return request;
        }

        private string getResponse(string stsUrl, string realm) {

            RequestSecurityToken rst = new RequestSecurityToken {
                RequestType = WSTrustFeb2005Constants.RequestTypes.Issue,
                AppliesTo = new EndpointAddress(realm),
                KeyType = WSTrustFeb2005Constants.KeyTypes.Bearer,
                TokenType = Microsoft.IdentityModel.Tokens.SecurityTokenTypes.Saml11TokenProfile11
            };

            WSTrustFeb2005RequestSerializer trustSerializer = new WSTrustFeb2005RequestSerializer();

            WSHttpBinding binding = new WSHttpBinding();

            binding.Security.Mode = SecurityMode.TransportWithMessageCredential;

            binding.Security.Message.ClientCredentialType = MessageCredentialType.UserName;
            binding.Security.Message.EstablishSecurityContext = false;
            binding.Security.Message.NegotiateServiceCredential = false;

            binding.Security.Transport.ClientCredentialType = HttpClientCredentialType.None;

            EndpointAddress address = new EndpointAddress(stsUrl);

            using (WSTrustFeb2005ContractClient trustClient = new WSTrustFeb2005ContractClient(binding, address)) {
                trustClient.ClientCredentials.UserName.UserName = _username;
                trustClient.ClientCredentials.UserName.Password = _password;
                Message response = trustClient.EndIssue(
                    trustClient.BeginIssue(
                        Message.CreateMessage(
                            MessageVersion.Default,
                            WSTrustFeb2005Constants.Actions.Issue,
                            new RequestBodyWriter(trustSerializer, rst)
                        ),
                        null,
                        null));
                trustClient.Close();
                using (XmlDictionaryReader reader = response.GetReaderAtBodyContents()) {
                    return reader.ReadOuterXml();
                }
            }
        }
    }
}
