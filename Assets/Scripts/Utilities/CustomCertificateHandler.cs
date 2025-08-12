using UnityEngine;
using UnityEngine.Networking;
using System.Security.Cryptography.X509Certificates;

public class CustomCertificateHandler : CertificateHandler
{
    private static CustomCertificateHandler _instance;
    public static CustomCertificateHandler Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = new CustomCertificateHandler();
            }
            return _instance;
        }
    }

    private static X509Certificate2 _certificate;
    private static bool _isInitialized = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void Initialize()
    {
        if (_isInitialized) return;

        Debug.Log("CustomCertificateHandler: Initializing...");
        var certAsset = Resources.Load<TextAsset>("server.crt");
        if (certAsset != null)
        {
            Debug.Log("CustomCertificateHandler: Found server.crt in Resources.");
            try
            {
                _certificate = new X509Certificate2(certAsset.bytes);
                Debug.Log("CustomCertificateHandler: Successfully created X509Certificate2 from asset.");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"CustomCertificateHandler: Failed to create certificate: {e}");
                _certificate = null;
            }
        }
        else
        {
            Debug.LogError(
                "CustomCertificateHandler: Certificate not found in Resources folder. Make sure it is named 'server.crt.txt' and is in a 'Resources' folder."
            );
        }
        _isInitialized = true;
    }

    protected override bool ValidateCertificate(byte[] certificateData)
    {
        if (!_isInitialized)
        {
            Debug.LogWarning("CustomCertificateHandler: Forcing initialization before validation.");
            Initialize();
        }

        Debug.Log("CustomCertificateHandler: ValidateCertificate called.");
        if (_certificate == null)
        {
            Debug.LogWarning("CustomCertificateHandler: Local certificate is not loaded, validation will fail.");
            return false;
        }

        X509Certificate2 serverCert = null;
        try
        {
            serverCert = new X509Certificate2(certificateData);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"CustomCertificateHandler: Failed to create certificate from server data: {e}");
            return false;
        }
        
        // Compare the public keys of the server's certificate and our trusted certificate.
        return _certificate.GetPublicKeyString() == serverCert.GetPublicKeyString();
    }
}