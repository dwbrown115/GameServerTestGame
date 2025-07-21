using UnityEngine;

public static class DeviceUtils
{
    private const string DeviceIdKey = "DeviceId";

    public static string GetDeviceId()
    {
        if (!PlayerPrefs.HasKey(DeviceIdKey))
            PlayerPrefs.SetString(DeviceIdKey, SystemInfo.deviceUniqueIdentifier);

        return PlayerPrefs.GetString(DeviceIdKey);
    }
}
