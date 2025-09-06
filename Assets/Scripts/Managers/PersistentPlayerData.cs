using System;

[Serializable]
public class PersistentPlayerData
{
    public string userId;
    public string skinId; // last known equipped skin id
    public string hexValue; // last known color hex from server (e.g., #FF00AA)
}
