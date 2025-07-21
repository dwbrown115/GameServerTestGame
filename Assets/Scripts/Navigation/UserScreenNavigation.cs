using UnityEngine;
using UnityEngine.SceneManagement;

public class UserScreenNavigation : MonoBehaviour
{
    public void NavigateToUserScreen()
    {
        Debug.Log("Navigating to User Screen");
        // Assuming you have a method to load the user screen
        SceneManager.LoadScene("User", LoadSceneMode.Single);
    }

    public void NavigateToChangeUserInfo()
    {
        Debug.Log("Navigating to Change Username or Password Screen");
        // Assuming you have a method to load the change username/password screen
        SceneManager.LoadScene("ChangeUserInfo", LoadSceneMode.Single);
    }
}
