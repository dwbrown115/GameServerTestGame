using UnityEngine;
using UnityEngine.SceneManagement;

public class AuthNavigation : MonoBehaviour
{
    public void NavigateToHome()
    {
        Debug.Log("Navigating to Home");
        SceneManager.LoadScene("Home", LoadSceneMode.Single);
    }

    public void NavigateToUser()
    {
        Debug.Log("Navigating to User");
        SceneManager.LoadScene("User", LoadSceneMode.Single);
    }

    public void NavigateToLogin()
    {
        Debug.Log("Navigating to Login");
        SceneManager.LoadScene("Login", LoadSceneMode.Single);
    }

    public void NavigateToRegister()
    {
        Debug.Log("Navigating to Register");
        SceneManager.LoadScene("Register", LoadSceneMode.Single);
    }
}
