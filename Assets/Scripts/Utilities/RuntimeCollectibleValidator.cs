using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class RuntimeCollectibleValidator : MonoBehaviour
{
    [Tooltip("Interval in seconds to perform runtime validation checks.")]
    [SerializeField]
    private float validationInterval = 5f; // Check every 5 seconds

    void Start()
    {
        // Initial validation on scene load
        ValidateActiveCollectibles();
        // Start periodic validation
        StartCoroutine(PeriodicValidation());
    }

    IEnumerator PeriodicValidation()
    {
        while (true)
        {
            if (GameStateManager.IsGameOver)
            {
                yield break; // Exit the coroutine
            }
            yield return new WaitForSeconds(validationInterval);
            ValidateActiveCollectibles();
        }
    }

    void ValidateActiveCollectibles()
    {
        Collectible[] collectiblesInScene = FindObjectsByType<Collectible>(FindObjectsSortMode.None);
        HashSet<string> validatedObjectIds = ValidatedObjectsManager.GetValidatedObjectIds();
        HashSet<string> encounteredObjectIds = new HashSet<string>(); // To check for duplicates in scene

        foreach (Collectible collectible in collectiblesInScene)
        {
            // Only validate if the object is active in the hierarchy
            if (collectible.gameObject.activeInHierarchy)
            {
                string objectName = collectible.gameObject.name;

                // Check for duplicates in the scene first
                if (!encounteredObjectIds.Add(objectName))
                {
                    Debug.LogWarning($"RuntimeCollectibleValidator: Duplicate object '{objectName}' found in scene during runtime check. Destroying duplicate.");
                    Destroy(collectible.gameObject);
                    continue; // Move to the next collectible
                }

                // First, check if the name is a valid number format
                if (!IsNumberValid.isValidNumber(objectName))
                {
                    Debug.LogWarning($"RuntimeCollectibleValidator: Object '{objectName}' has an invalid number format during runtime check. Destroying object.");
                    Destroy(collectible.gameObject);
                    continue; // Move to the next collectible
                }

                // Second, check if the object ID exists in the validatedObjects.json
                if (!validatedObjectIds.Contains(objectName))
                {
                    Debug.LogWarning($"RuntimeCollectibleValidator: Object '{objectName}' is not found in validatedObjects.json during runtime check. Destroying object.");
                    Destroy(collectible.gameObject);
                    continue; // Move to the next collectible
                }
            }
        }
    }
}