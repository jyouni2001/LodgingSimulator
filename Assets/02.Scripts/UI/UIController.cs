using UnityEngine;
using UnityEngine.UI;

public class UIController : MonoBehaviour
{
    [SerializeField] private LoadingSystem loadingSystem;

    public void Btn_Start()
    {
        loadingSystem.LoadScene("MainScene");
    }


    public void Btn_Exit()
    {
        Application.Quit();
    }
}
