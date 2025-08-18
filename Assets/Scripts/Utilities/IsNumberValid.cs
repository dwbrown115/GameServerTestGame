using System.Linq;
using UnityEngine;

public static class IsNumberValid
{
    /// <summary>
    /// Validates a number based on specific criteria:
    /// - Must be at least 2 characters long.
    /// - Must consist only of digits.
    /// - The last digit must be a checksum digit, which is the sum of all previous digits modulo 10.
    /// </summary>
    /// <param name="number">The number to validate as a string.</param>
    /// <returns>True if the number is valid, otherwise false.</returns>
    public static bool isValidNumber(string number)
    {
        Debug.Log($"Validating number: '{number}'");
        if (string.IsNullOrEmpty(number) || number.Length <= 1 || !number.All(char.IsDigit))
        {
            Debug.Log(
                $"Basic validation failed: IsNullOrEmpty={string.IsNullOrEmpty(number)}, Length={number?.Length}, IsAllDigits={number?.All(char.IsDigit)}"
            );
            return false;
        }

        int sum = 0;
        for (int i = 0; i < number.Length - 1; i++)
        {
            sum += (int)char.GetNumericValue(number[i]);
        }
        Debug.Log($"Sum of digits: {sum}");

        int expectedChecksum = sum % 10;
        int actualChecksum = (int)char.GetNumericValue(number[number.Length - 1]);
        Debug.Log($"Expected checksum: {expectedChecksum}, Actual checksum: {actualChecksum}");

        bool result = expectedChecksum == actualChecksum;
        Debug.Log($"Validation result: {result}");
        return result;
    }
}
