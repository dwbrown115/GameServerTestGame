using UnityEngine;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

public static class GlobalCertificateManager
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void Initialize()
    {
        Debug.Log("GlobalCertificateManager: Initializing global certificate validation callback.");

        // Set the TLS protocol version. Tls12 is a common standard.
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

        // IMPORTANT: This globally overrides certificate validation for all HTTPS and WSS requests
        // made using .NET's HttpWebRequest or ClientWebSocket in this application domain.
        // This is necessary for self-signed certificates but is a security risk if you
        // connect to other, production services.
        ServicePointManager.ServerCertificateValidationCallback = (
            object sender,
            X509Certificate certificate,
            X509Chain chain,
            SslPolicyErrors sslPolicyErrors
        ) =>
        {
            Debug.Log("GlobalCertificateManager: Custom certificate validation is being called.");
            // In a production environment, you would perform real validation here.
            // For a self-signed cert, we are simply trusting it.
            return true;
        };
    }
}
