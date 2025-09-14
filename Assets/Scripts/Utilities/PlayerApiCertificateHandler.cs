using System.Security.Cryptography.X509Certificates;
using UnityEngine;
using UnityEngine.Networking;

// This is a separate certificate handler specifically for the PlayerApiClient to avoid any potential state conflicts.
public class PlayerApiCertificateHandler : CertificateHandler
{
    private static PlayerApiCertificateHandler _instance;
    public static PlayerApiCertificateHandler Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = new PlayerApiCertificateHandler();
            }
            return _instance;
        }
    }

    private static X509Certificate2 _certificate;
    private static bool _isInitialized = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void Initialize()
    {
        if (_isInitialized)
            return;

        Debug.Log("PlayerApiCertificateHandler: Initializing...");
        var certAsset = Resources.Load<TextAsset>("server.crt");
        if (certAsset != null)
        {
            try
            {
                _certificate = new X509Certificate2(certAsset.bytes);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"PlayerApiCertificateHandler: Failed to create certificate: {e}");
                _certificate = null;
            }
        }
        else
        {
            Debug.LogError(
                "PlayerApiCertificateHandler: Certificate not found in Resources folder. Make sure it is named 'server.crt.txt' and is in a 'Resources' folder."
            );
        }
        _isInitialized = true;
    }

    protected override bool ValidateCertificate(byte[] certificateData)
    {
        if (!_isInitialized)
        {
            Initialize();
        }

        if (_certificate == null)
        {
            return false;
        }

        using (var serverCert = new X509Certificate2(certificateData))
        {
            return _certificate.GetPublicKeyString() == serverCert.GetPublicKeyString();
        }
    }
}
