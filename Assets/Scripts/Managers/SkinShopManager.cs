using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Thin wrapper to preserve existing references while migrating to PlayerSkinManager.
public class SkinShopManager : MonoBehaviour
{
    public static SkinShopManager Instance { get; private set; }

    [Header("UI References")]
    [Tooltip("Displays the player's current points.")]
    public TextMeshProUGUI pointsText;

    [Tooltip("Displays errors related to the shop/skins APIs.")]
    public TextMeshProUGUI errorText;

    [Header("Loading UI (optional)")]
    [Tooltip("Modal panel object to show while waiting for server responses.")]
    public GameObject loadingModal;
    public LoadingTextAnimator loadingAnimator;

    private void Awake()
    {
        // Ensure a PlayerSkinManager exists and mirror singleton access.
        if (PlayerSkinManager.Instance == null)
        {
            var go = new GameObject("PlayerSkinManager");
            go.AddComponent<PlayerSkinManager>();
        }
        // Ensure a UserAssetsManager exists for points/owned skins
        if (UserAssetsManager.Instance == null)
        {
            var go2 = new GameObject("UserAssetsManager");
            go2.AddComponent<UserAssetsManager>();
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Best-effort initial UI refresh
        if (loadingModal != null)
            loadingModal.SetActive(false);
        UpdatePointsText();
    }

    private void OnEnable()
    {
        // Subscribe to updates/errors
        if (SkinsService.Instance != null)
            SkinsService.Instance.OnError += HandleError;
        if (UserAssetsManager.Instance != null)
        {
            UserAssetsManager.Instance.OnAssetsUpdated += UpdatePointsText;
            UserAssetsManager.Instance.OnError += HandleError;
        }
        // Optional: hook active-skin errors here if exposed

        // Always refresh user assets when this screen is shown to ensure points/owned are in sync with server
        if (UserAssetsManager.Instance != null)
        {
            UserAssetsManager.Instance.TryFetchUserAssets();
        }
    }

    private void OnDisable()
    {
        if (SkinsService.Instance != null)
            SkinsService.Instance.OnError -= HandleError;
        if (UserAssetsManager.Instance != null)
        {
            UserAssetsManager.Instance.OnAssetsUpdated -= UpdatePointsText;
            UserAssetsManager.Instance.OnError -= HandleError;
        }
        // Optional: unhook active-skin errors here if exposed
    }

    public void TryFetchActiveSkin()
    {
        PlayerSkinManager.Instance?.TryFetchActiveSkin();
    }

    public void ValidateActiveSkinAgainstSaved()
    {
        PlayerSkinManager.Instance?.ValidateActiveSkinAgainstSaved();
    }

    public void ShowLoading(string message)
    {
        if (loadingModal != null)
            loadingModal.SetActive(true);
        if (loadingAnimator != null)
            loadingAnimator.StartAnimation(string.IsNullOrEmpty(message) ? "Loading" : message);
    }

    public void HideLoading()
    {
        if (loadingAnimator != null)
            loadingAnimator.StopAnimation();
        if (loadingModal != null)
            loadingModal.SetActive(false);
    }

    private void UpdatePointsText()
    {
        if (pointsText == null)
            return;
        int points = PlayerManager.Instance != null ? PlayerManager.Instance.GetPoints() : 0;
        pointsText.text = points.ToString();
    }

    private void HandleError(string message)
    {
        if (errorText != null)
        {
            errorText.text = message;
        }
    }
}
