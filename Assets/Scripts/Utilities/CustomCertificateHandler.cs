using UnityEngine;
using UnityEngine.Networking;

public class CustomCertificateHandler : CertificateHandler
{
    private static TextAsset _certificate;
    private static byte[] _certificateBytes;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void Initialize()
    {
        _certificate = Resources.Load<TextAsset>("server.crt");
        if (_certificate != null)
        {
            _certificateBytes = _certificate.bytes;
        }
        else
        {
            Debug.LogError(
                "Certificate not found in Resources folder. Make sure it is named 'server.crt' and is in a 'Resources' folder."
            );
        }
    }

    protected override bool ValidateCertificate(byte[] certificateData)
    {
        if (_certificateBytes == null)
        {
            Debug.LogWarning("Certificate is not loaded, validation will fail.");
            return false;
        }

        // Create a new certificate from the given data
        var cert = new System.Security.Cryptography.X509Certificates.X509Certificate2(
            _certificateBytes
        );

        // Compare the server's certificate with the one we have
        return cert.Equals(
            new System.Security.Cryptography.X509Certificates.X509Certificate2(certificateData)
        );
    }
}
